"""Validate every keyword variant of every reconstructed shader with Unity's own
UnityShaderCompiler — real CGIncludes, real keyword sets, real platform rules.

Faster and truer than the fxc+stub harness: one USC process serves N worker
threads (each with its own socket), and nothing is spawned per variant.

Usage:
    python usc_validate.py <file-or-dir...> [-j N] [--unity <dir>] [-p d3d11]
    python usc_validate.py .temp/final_check/Assets            # whole tree

A shader passes when preprocess() yields a snippet and every variant listed in
its "// ---- variant N" markers compiles for both vertex and fragment.
"""

import os
import re
import socket
import subprocess
import sys
import threading
import time
import queue

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import usc_compile as usc  # noqa: E402  (MAGIC, pack_*, Client)

usc.log = lambda m: None  # preprocess() chatter; we only want the verdict

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

VARIANT_RX = re.compile(r"^\s*// ---- variant (\d+)(?:\s*\(base\)|:\s*(.+?))?\s*----\s*$", re.M)
PLATFORM_IDS = {"d3d11": 4, "gles20": 5, "gles3": 9, "openglcore": 15, "glcore": 15}


def parse_variants(text):
    """[(label, [keyword, ...])] from the '// ---- variant N' markers."""
    out = []
    for m in VARIANT_RX.finditer(text):
        kw = [k.strip() for k in m.group(2).split(" + ")] if m.group(2) else []
        out.append((m.group(1), kw))
    return out


def collect(paths):
    files = []
    for p in paths:
        if os.path.isdir(p):
            for root, _, names in os.walk(p):
                files += [os.path.join(root, n) for n in names if n.endswith(".shader")]
        elif os.path.isfile(p):
            files.append(p)
    return sorted(files)


class Server:
    """One UnityShaderCompiler.exe, many client connections."""

    def __init__(self, unity_dir):
        data_dir = unity_dir
        if os.path.isdir(os.path.join(data_dir, "Editor", "Data")):
            data_dir = os.path.join(data_dir, "Editor", "Data")
        usc_exe = os.path.join(data_dir, "Tools", "UnityShaderCompiler.exe")
        cg = os.path.join(data_dir, "Resources", "CGIncludes")
        if not os.path.isfile(usc_exe):
            raise SystemExit(f"UnityShaderCompiler.exe 不存在: {usc_exe}")
        self.exe, self.cg = usc_exe, cg
        self.log = os.path.join(SCRIPT_DIR, "usc_validate.log")
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
        c = usc.Client(conn)
        c.r.read("hello")
        return c

    def close(self):
        try:
            self.srv.close()
        except OSError:
            pass
        try:
            self.proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.proc.kill()


def check_file(client, path, cg, platform):
    """-> (shader name, [(variant, stage, ok, first error)])."""
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        text = f.read()
    name = os.path.splitext(os.path.basename(path))[0]
    # Shaders whose blob carries no translatable program (Unity's own inspector
    # stubs, GrabPass-only passes) are written as an empty SubShader: nothing to
    # compile, so they are skipped rather than failed.
    if "CGPROGRAM" not in text:
        return name, None
    variants = parse_variants(text)
    if not variants:
        return name, [("-", "-", False, "no '// ---- variant N' marker found")]
    try:
        snips, errs, ok, _ = client.preprocess(text, cg, platform)
    except Exception as e:
        return name, [("-", "-", False, f"preprocess raised {type(e).__name__}: {e}")]
    if not snips or not ok:
        msg = "; ".join(e["msg"] for e in errs)[:200] if errs else "preprocess returned no snippet"
        return name, [("-", "-", False, msg)]

    res = []
    for label, kws in variants:
        for fn, stage in ((0, "vert"), (1, "frag")):
            try:
                cok, blob, cerrs, _ = client.compile_snippet(snips[0]["text"], cg, fn, kws, platform)
            except Exception as e:
                res.append((label, stage, False, f"compile raised {type(e).__name__}: {e}"))
                continue
            if cok:
                res.append((label, stage, True, ""))
            else:
                msg = "; ".join(cerrs)[:200] if cerrs else "compileSnippet returned ok=0"
                res.append((label, stage, False, msg))
    return name, res


def main():
    args = [a for a in sys.argv[1:]]
    jobs, unity, platform = 8, os.environ.get("UNITY_EDITOR"), 4
    rest = []
    i = 0
    while i < len(args):
        a = args[i]
        if a in ("-j", "--jobs"):
            jobs = int(args[i + 1]); i += 2; continue
        if a in ("-u", "--unity"):
            unity = args[i + 1]; i += 2; continue
        if a in ("-p", "--platform"):
            key = args[i + 1].lower()
            platform = PLATFORM_IDS.get(key, int(key)); i += 2; continue
        rest.append(a); i += 1

    if not unity or not rest:
        print(__doc__)
        return 2

    files = collect(rest)
    if not files:
        print("no .shader files found")
        return 2
    # USC dials back exactly once per process, so parallelism means processes,
    # not connections: one UnityShaderCompiler.exe per worker.
    jobs = max(1, min(jobs, len(files)))
    print(f"{len(files)} shader file(s), {jobs} USC process(es), platform={platform}")

    servers = [Server(unity) for _ in range(jobs)]
    t0 = time.time()
    q = queue.Queue()
    for f in files:
        q.put(f)

    lock = threading.Lock()
    fails, done, skipped = [], [0], [0]

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
                name, res = check_file(client, path, srv.cg, platform)
            except Exception as e:
                # A dead connection or a crashed compiler: drop it and retry the
                # file once on a fresh connection before recording a failure.
                try:
                    if client is not None:
                        client.conn.close()
                except OSError:
                    pass
                client = None
                try:
                    client = srv.connect()
                    name, res = check_file(client, path, srv.cg, platform)
                except Exception as e2:
                    name, res = os.path.basename(path), [("-", "-", False, f"{type(e2).__name__}: {e2}")]
            bad = [r for r in res if not r[2]] if res else []
            with lock:
                done[0] += 1
                if res is None:
                    skipped[0] += 1
                elif bad:
                    fails.append((name, bad))
                    for label, stage, _, msg in bad[:2]:
                        print(f"  [FAIL] {name} v{label}/{stage}: {msg[:150]}", flush=True)
            q.task_done()

    threads = [threading.Thread(target=worker, args=(s,), daemon=True) for s in servers]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    for s in servers:
        s.close()

    dt = time.time() - t0
    print(f"\n{done[0]} shader(s) checked in {dt:.1f}s ({skipped[0]} without code, skipped); "
          f"{len(fails)} with failing variants")
    for name, bad in fails:
        print(f"  {name}: {len(bad)} failing variant/stage(s)")
    return 1 if fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
