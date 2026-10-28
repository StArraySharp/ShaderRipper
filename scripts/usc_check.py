"""Compile every emitted shader with Unity's own UnityShaderCompiler.

This is the end-to-end check: real CGIncludes, real keyword sets, real platform
rules -- the same compiler Unity runs when it imports the file.  It answers the
only question that matters for a re-import: does the shader compile at all?

`usc_compile.py` next door speaks USC's socket protocol; this drives it.  The unit
of work is the whole file: USC's `preprocess` expands the includes and picks a
snippet, then `compile_snippet` compiles the vertex and the fragment entry point
of it.  A shader passes when both entry points compile.

A stage the pass does not declare is skipped, because a vertex-only pass is legal
ShaderLab and asking USC for the missing kernel reports "kernel ''" rather than
the answer for the stages that do exist.

Usage:
    py -3 usc_check.py <file-or-dir...> [-j N] [-u <unity-dir>] [-p d3d11]
"""

import os
import queue
import socket
import subprocess
import sys
import threading
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import usc_compile as usc   # noqa: E402
from usc_compile import Client   # noqa: E402

PLATFORM_IDS = {"d3d11": 4, "gles3": 9, "gles20": 5, "openglcore": 15, "glcore": 15}

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))


class Server:
    """One UnityShaderCompiler.exe, many client connections.

    USC takes a base path and a log file and listens on the port it is told; each
    worker thread gets its own connection so N shaders compile in parallel without
    spawning a process per variant.
    """

    def __init__(self, unity_dir):
        data_dir = unity_dir
        if os.path.isdir(os.path.join(data_dir, "Editor", "Data")):
            data_dir = os.path.join(data_dir, "Editor", "Data")
        usc_exe = os.path.join(data_dir, "Tools", "UnityShaderCompiler.exe")
        cg = os.path.join(data_dir, "Resources", "CGIncludes")
        if not os.path.isfile(usc_exe):
            raise SystemExit(f"UnityShaderCompiler.exe not found: {usc_exe}")
        self.exe, self.cg = usc_exe, cg
        self.log = os.path.join(SCRIPT_DIR, "usc_check.log")
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

    body = snips[0]["text"]
    stages = []
    if "pragma vertex" in body:
        stages.append((0, "vert"))
    if "pragma fragment" in body:
        stages.append((1, "frag"))
    if not stages:
        return name, None, "no entry point declared"

    problems = []
    for fn, stage in stages:
        try:
            cok, _blob, cerrs, _ = client.compile_snippet(body, cg, fn, [], platform)
        except Exception as e:                   # noqa: BLE001
            problems.append(f"{stage}: {type(e).__name__}: {e}")
            continue
        if not cok:
            problems.append(f"{stage}: {'; '.join(cerrs)[:220] if cerrs else 'ok=0'}")

    if problems:
        return name, False, " | ".join(problems[:2])
    return name, True, ""


def main():
    args = sys.argv[1:]
    jobs, unity = 8, os.environ.get("UNITY_EDITOR")
    platform_name = "d3d11"
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
        rest.append(a); i += 1

    if not unity or not rest:
        print(__doc__)
        return 2

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
