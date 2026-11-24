"""Compile every emitted shader with Unity's own UnityShaderCompiler.

This is the end-to-end check: real CGIncludes, real keyword sets, real platform
rules -- the same compiler Unity runs when it imports the file.  It answers the
only question that matters for a re-import: does the shader compile at all?

`usc_compile.py` next door speaks USC's socket protocol; this drives it.  The unit
of work is the whole file: USC's `preprocess` expands the includes and emits the
pass programs. The default check compiles every declared vertex and fragment
entry point. Use capture replay to validate specific keyword combinations.

A stage the pass does not declare is skipped, because a vertex-only pass is legal
ShaderLab and asking USC for the missing kernel reports "kernel ''" rather than
the answer for the stages that do exist.

Usage:
    py -3 usc_check.py <file-or-dir...> [-j N] [-u <unity-dir>] [-p d3d11]
    py -3 usc_check.py <project-or-export-root> -u <unity-dir> --replay <capture.json>
        --report <results.json> [--all-captured]

On Windows, -u may be omitted: installed editors are read from the registry
when neither -u nor UNITY_EDITOR is set, and the newest usable one is picked.
"""

import json
import os
import queue
import re
import socket
import subprocess
import sys
import threading
import time
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import usc_compile as usc   # noqa: E402
from usc_compile import Client   # noqa: E402

PLATFORM_IDS = {"d3d11": 4, "gles3": 9, "gles20": 5, "openglcore": 15, "glcore": 15}

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))


def find_unity():
    """Newest installed Unity editor with a usable shader compiler."""
    if sys.platform != "win32":
        return None
    import winreg

    roots = (
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Unity Technologies\Installer"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Unity Technologies\Installer"),
    )
    found = []
    for hive, path in roots:
        try:
            root = winreg.OpenKey(hive, path)
        except OSError:
            continue
        with root:
            i = 0
            while True:
                try:
                    name = winreg.EnumKey(root, i)
                except OSError:
                    break
                i += 1
                try:
                    with winreg.OpenKey(root, name) as sub:
                        loc, _ = winreg.QueryValueEx(sub, "Location x64")
                        ver, _ = winreg.QueryValueEx(sub, "Version")
                except OSError:
                    continue
                usc = os.path.join(loc, "Editor", "Data", "Tools", "UnityShaderCompiler.exe")
                if os.path.isfile(usc):
                    found.append((tuple(int(n) for n in re.findall(r"\d+", ver)[:4]), loc))
    return max(found, key=lambda c: c[0])[1] if found else None


class Server:
    """One UnityShaderCompiler.exe, many client connections.

    USC takes a base path and a log file and listens on the port it is told; each
    worker thread gets its own connection so N shaders compile in parallel without
    spawning a process per variant.
    """

    def __init__(self, unity_dir, log_path=None):
        data_dir = unity_dir
        if os.path.isdir(os.path.join(data_dir, "Editor", "Data")):
            data_dir = os.path.join(data_dir, "Editor", "Data")
        usc_exe = os.path.join(data_dir, "Tools", "UnityShaderCompiler.exe")
        cg = os.path.join(data_dir, "Resources", "CGIncludes")
        if not os.path.isfile(usc_exe):
            raise SystemExit(f"UnityShaderCompiler.exe not found: {usc_exe}")
        self.exe, self.cg = usc_exe, cg
        self.log = str(log_path) if log_path else os.path.join(SCRIPT_DIR, "usc_check.log")
        self.srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.srv.bind(("127.0.0.1", 0))
        self.port = self.srv.getsockname()[1]
        self.srv.listen(64)
        self.srv.settimeout(60)
        self.proc = subprocess.Popen(
            [usc_exe, data_dir, self.log, str(self.port)], cwd=SCRIPT_DIR,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))

    def connect(self):
        conn, _ = self.srv.accept()
        return Client(conn, verbose=False)

    def close(self):
        try:
            self.srv.close()
        except OSError:
            pass
        try:
            self.proc.terminate()
            self.proc.wait(timeout=10)
        except Exception:                        # noqa: BLE001
            try:
                self.proc.kill()
            except Exception:                    # noqa: BLE001
                pass


def collect(paths):
    out = []
    for p in paths:
        if os.path.isdir(p):
            for root, _dirs, files in os.walk(p):
                for f in files:
                    if f.endswith(".shader"):
                        out.append(os.path.join(root, f))
        elif p.endswith(".shader"):
            out.append(p)
    return sorted(out)


def check_one(client, path, cg, platform):
    """-> (name, ok, detail).  ok None means 'nothing to compile'."""
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        text = fh.read()
    name = os.path.splitext(os.path.basename(path))[0]

    if "HLSLPROGRAM" not in text and "CGPROGRAM" not in text:
        return name, None, "no program block"

    try:
        snips, errs, ok, _ = client.preprocess(text, cg, platform)
    except Exception as e:                       # noqa: BLE001
        return name, False, f"preprocess raised {type(e).__name__}: {e}"

    hard = [e for e in errs if e["lvl"] >= 2]
    if hard:
        return name, False, "preprocess: " + "; ".join(e["msg"] for e in hard[:2])[:220]
    if not snips or not ok:
        return name, False, "preprocess produced no snippet"

    checked = 0
    problems = []
    for index, snippet in enumerate(snips, 1):
        body = snippet["text"]
        for stage, fn in STAGES.items():
            if not re.search(rf"(?m)^\s*#\s*pragma\s+{stage.lower()}\b", body):
                continue
            checked += 1
            label = f"program {index} {stage.lower()}"
            try:
                cok, blob, cerrs, _ = client.compile_snippet(body, cg, fn, [], platform)
            except Exception as e:               # noqa: BLE001
                problems.append(f"{label}: {type(e).__name__}: {e}")
                continue
            if not cok:
                problems.append(f"{label}: {'; '.join(cerrs)[:220] if cerrs else 'ok=0'}")
            elif not blob:
                problems.append(f"{label}: no bytecode")

    if problems:
        return name, False, " | ".join(problems[:2])
    if not checked:
        return name, None, "no entry point declared"
    return name, True, ""


PROGRAM = re.compile(r"^[ \t]*((?:HLSL|CG)PROGRAM)\b", re.MULTILINE)
END_PROGRAM = re.compile(r"^\s*END(?:HLSL|CG)\b", re.MULTILINE)
DECLARED_SHADER = re.compile(r'^\s*Shader\s+"([^"\r\n]+)"', re.MULTILINE)
STAGES = {"Vertex": 0, "Fragment": 1}


def program_blocks(source):
    """Key each generated program by pass name, LightMode and occurrence."""
    blocks = {}
    occurrences = Counter()
    previous_end = 0
    for program in PROGRAM.finditer(source):
        token = program.start(1)
        start = source.count("\n", 0, token) + 1
        prefix = source[previous_end:token]
        names = re.findall(r'(?im)^\s*Name\s+"([^"]+)"', prefix)
        modes = re.findall(r'(?i)"LightMode"\s*=\s*"([^"]+)"', prefix)
        pair = (names[-1] if names else "", modes[-1] if modes else "")
        blocks[start] = (*pair, occurrences[pair])
        occurrences[pair] += 1
        end = END_PROGRAM.search(source, program.end())
        previous_end = end.end() if end else program.end()
    return blocks


def replay_capture(capture_path, root, unity, jobs, report_path, all_captured=False):
    """Replay captured compileSnippet requests for the same shader and pass."""
    capture = json.loads(Path(capture_path).read_text(encoding="utf-8-sig"))
    original = Path(capture["project"]).resolve()
    root = Path(root).resolve()
    report = Path(report_path).resolve()
    report.parent.mkdir(parents=True, exist_ok=True)

    preprocess = {}
    pending = defaultdict(list)
    for index, cmd in enumerate(capture["commands"]):
        if cmd["command"] == "preprocess":
            preprocess[cmd["file"]] = cmd
        elif cmd["command"] == "compileSnippet" and (all_captured or cmd.get("ok") == 0):
            pending[cmd["file"]].append((index, cmd))
    if not pending:
        print("No compileSnippet requests matched the replay scope.")
        return 2

    by_name = defaultdict(list)
    if root != original:
        for path in (root / "Assets").rglob("*.shader"):
            declaration = DECLARED_SHADER.search(path.read_text(encoding="utf-8-sig", errors="replace"))
            if declaration:
                by_name[declaration[1]].append(path)

    def find_shader(file, name):
        same_path = root / file
        if same_path.is_file():
            return same_path
        matches = by_name[name]
        if len(matches) == 1:
            return matches[0]
        same_basename = [path for path in matches if path.name.casefold() == Path(file).name.casefold()]
        if len(same_basename) == 1:
            return same_basename[0]
        source = original / file
        if not matches or not source.is_file():
            return None

        def shader_code(path):
            lines = (line.strip() for line in path.read_text(encoding="utf-8-sig", errors="replace").splitlines())
            return [re.sub(r'^(#\s*include\s+"[^"]+")\s*//.*$', r"\1", line)
                    for line in lines if line and not line.startswith("//")]

        code = shader_code(source)
        equivalent = [path for path in matches if shader_code(path) == code]
        return equivalent[0] if len(equivalent) == 1 else None

    work = queue.Queue()
    for file, commands in pending.items():
        work.put((file, commands))
    results = queue.Queue()

    def record(index, cmd, status, *, path=None, detail=None, start=None, size=None):
        results.put({"capture_index": index, "status": status, "source": cmd["file"],
                     "shader": cmd["name"], "pass": cmd["pass"], "stage": cmd["type"],
                     "source_ok": cmd.get("ok"),
                     "source_start": cmd["start"], "start": start, "path": path,
                     "outsize": size, "expected": cmd.get("error"), "detail": detail})

    def replay_file(client, cg, file, commands):
        path = find_shader(file, commands[0][1]["name"])
        if path is None:
            for index, cmd in commands:
                record(index, cmd, "unmatched-shader", detail="No unique exported shader matched.")
            return
        relative = path.relative_to(root).as_posix()
        source = path.read_text(encoding="utf-8-sig", errors="replace")
        old_blocks = program_blocks((original / file).read_text(encoding="utf-8-sig", errors="replace"))
        new_starts = {block: line for line, block in program_blocks(source).items()}
        pp = dict(preprocess.get(file, {}), file=relative)
        snips, errs, ok, _ = client.preprocess(source, cg, int(commands[0][1]["buildPlatform"]), capture=pp)
        hard = [err["msg"] for err in errs if err["lvl"] >= 2]
        if hard or not ok:
            for index, cmd in commands:
                record(index, cmd, "preprocess-error", path=relative, detail=hard or ["Preprocess failed."])
            return
        by_start = {}
        for snip in snips:
            location = re.search(r"(?m)^#line (\d+)\b", snip["text"])
            if location:
                # USC's source line directive precedes the logged start by two lines.
                by_start[int(location[1]) + 2] = snip["text"]

        for index, cmd in commands:
            old_start = int(cmd["start"])
            start = old_start if root == original else new_starts.get(old_blocks.get(old_start))
            snippet = by_start.get(start)
            stage = STAGES.get(cmd["type"])
            platform = PLATFORM_IDS.get(cmd["platform"])
            if snippet is None or stage is None or platform is None:
                record(index, cmd, "unmatched-program", path=relative, start=start)
                continue
            options = dict(cmd, file=relative, start=start)
            passed, blob, errors, _ = client.compile_snippet(snippet, cg, stage, [], platform, capture=options)
            status = "passed" if passed and blob else "compile-error" if not passed else "empty-bytecode"
            record(index, cmd, status, path=relative, detail=errors, start=start, size=len(blob))

    def worker(worker_id):
        server = None
        client = None
        try:
            while True:
                try:
                    file, commands = work.get_nowait()
                except queue.Empty:
                    break
                try:
                    if server is None:
                        server = Server(unity, report.with_suffix(f".{worker_id}.usc.log"))
                        client = server.connect()
                    replay_file(client, server.cg, file, commands)
                except (OSError, ValueError, EOFError) as exc:
                    if server is not None:
                        server.close()
                        server = None
                    for index, cmd in commands:
                        record(index, cmd, "replay-error", detail=f"{type(exc).__name__}: {exc}")
        finally:
            if server is not None:
                server.close()

    threads = [threading.Thread(target=worker, args=(i,)) for i in range(min(jobs, len(pending)))]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()
    collected = {}
    while not results.empty():
        row = results.get_nowait()
        collected.setdefault(row["capture_index"], row)
    expected = sum(len(group) for group in pending.values())
    summary = Counter(row["status"] for row in collected.values())
    report.write_text(json.dumps({"root": str(root), "expected": expected, "summary": summary,
                                 "results": [collected[i] for i in sorted(collected)]},
                                ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"expected": expected, "results": len(collected), **summary}), flush=True)
    return 0 if summary == {"passed": expected} else 1


def main():
    args = sys.argv[1:]
    jobs, unity = 8, os.environ.get("UNITY_EDITOR")
    platform_name = "d3d11"
    capture = report = None
    all_captured = False
    rest = []
    i = 0
    while i < len(args):
        a = args[i]
        if a in ("-j", "--jobs"):
            jobs = int(args[i + 1]); i += 2; continue
        if a in ("-u", "--unity"):
            unity = args[i + 1]; i += 2; continue
        if a in ("-p", "--platform"):
            platform_name = args[i + 1]; i += 2; continue
        if a == "--replay":
            capture = args[i + 1]; i += 2; continue
        if a == "--report":
            report = args[i + 1]; i += 2; continue
        if a == "--all-captured":
            all_captured = True; i += 1; continue
        rest.append(a); i += 1

    if not unity and rest:
        unity = find_unity()
        if unity:
            print(f"Unity editor (registry): {unity}")

    if not unity or not rest:
        print(__doc__)
        return 2

    if capture:
        if len(rest) != 1 or not report or jobs < 1:
            print("Usage: usc_check.py <project-or-export-root> -u <unity> --replay <capture.json> --report <results.json>")
            return 2
        return replay_capture(capture, rest[0], unity, jobs, report, all_captured)

    platform = PLATFORM_IDS.get(platform_name, 4)
    files = collect(rest)
    if not files:
        print("no .shader files found")
        return 2

    jobs = max(1, min(jobs, len(files)))
    print(f"{len(files)} shader file(s), {jobs} USC process(es), platform={platform_name}")

    servers = [Server(unity) for _ in range(jobs)]
    q = queue.Queue()
    for f in files:
        q.put(f)

    lock = threading.Lock()
    results = []
    t0 = time.time()

    def worker(srv):
        client = None
        while True:
            try:
                path = q.get_nowait()
            except queue.Empty:
                return
            try:
                if client is None:
                    client = srv.connect()
                name, ok, detail = check_one(client, path, srv.cg, platform)
            except Exception as e:               # noqa: BLE001
                try:
                    if client is not None:
                        client.conn.close()
                except OSError:
                    pass
                client = None
                try:
                    client = srv.connect()
                    name, ok, detail = check_one(client, path, srv.cg, platform)
                except Exception as e2:          # noqa: BLE001
                    name, ok, detail = os.path.basename(path), False, f"{type(e2).__name__}: {e2}"
            with lock:
                results.append((name, ok, detail))
                if ok is False:
                    print(f"  [FAIL] {name}: {detail}", flush=True)

    threads = [threading.Thread(target=worker, args=(s,), daemon=True) for s in servers]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    for s in servers:
        s.close()

    good = sum(1 for _, ok, _ in results if ok is True)
    bad = [(n, d) for n, ok, d in results if ok is False]
    skipped = sum(1 for _, ok, _ in results if ok is None)

    print()
    print(f"passed  : {good}")
    print(f"failed  : {len(bad)}")
    print(f"skipped : {skipped}  (no program block)")
    print(f"elapsed : {time.time() - t0:.1f}s")
    if bad:
        print()
        print("failures:")
        for n, d in bad[:40]:
            print(f"  {n}: {d}")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
