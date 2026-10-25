"""Compile every exported shader with Unity's own compiler and compare the
result against the original DXBC.

This is the authoritative check: `usc_compile.py` drives the real
UnityShaderCompiler.exe, with Unity's own CGIncludes and platform, so a shader
that compiles here is one Unity accepts.  Each compiled program is then diffed
against the original disassembly the translator started from -- buffer extents,
resources, samples and opcode sequence -- the same invariants
`verify_semantics.py` checks, but through Unity's compiler rather than
d3dcompiler.

Usage: usc_verify.py <export-dir> [--unity DIR] [--platform d3d11]
                     [--limit N] [--only NAME]
"""

import glob
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.join(HERE, "..", "scripts")):
    p = os.path.abspath(p)
    if p not in sys.path:
        sys.path.insert(0, p)
import usc_compile as U          # noqa: E402
import verify_semantics as V     # noqa: E402


def read_dxbc_asm(path):
    return open(path, encoding="utf-8", errors="replace").read()


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    root = sys.argv[1]
    unity = (sys.argv[sys.argv.index("--unity") + 1] if "--unity" in sys.argv
             else os.environ.get("UNITY_EDITOR", r"D:/Softwares/Unity/6000.3.10f1"))
    platform = sys.argv[sys.argv.index("--platform") + 1] if "--platform" in sys.argv else "d3d11"
    limit = int(sys.argv[sys.argv.index("--limit") + 1]) if "--limit" in sys.argv else 0
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None

    data_dir = unity
    if os.path.isdir(os.path.join(data_dir, "Editor", "Data")):
        data_dir = os.path.join(data_dir, "Editor", "Data")
    usc_exe = os.path.join(data_dir, "Tools", "UnityShaderCompiler.exe")
    cg = os.path.join(data_dir, "Resources", "CGIncludes")
    if not os.path.isfile(usc_exe):
        print(f"UnityShaderCompiler.exe not found: {usc_exe}")
        return 2

    PLATFORM_IDS = {"d3d11": 4, "gles20": 5, "gles3": 9, "openglcore": 15, "glcore": 15}
    plat_id = PLATFORM_IDS.get(platform, 4)

    shaders = sorted(glob.glob(os.path.join(root, "*.shader")))
    if only:
        shaders = [s for s in shaders if only.lower() in os.path.basename(s).lower()]
    if limit:
        shaders = shaders[:limit]

    import socket
    import subprocess
    import time

    logfile = os.path.join(root, "_usc.log")
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", 0))
    port = srv.getsockname()[1]
    srv.listen(4)
    srv.settimeout(60)

    proc = subprocess.Popen([usc_exe, data_dir, logfile, str(port)], cwd=HERE,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    conn, _ = srv.accept()
    c = U.Client(conn)

    disasm = U._load_d3d_disassembler()

    ok_shaders = fail_shaders = 0
    ok_programs = fail_programs = 0
    semantic = 0
    soft = 0
    fails = []
    warns = 0

    try:
        c.r.read("hello")
        for sh in shaders:
            base = os.path.splitext(os.path.basename(sh))[0]
            d = os.path.join(root, base)
            src = open(sh, encoding="utf-8", errors="replace").read()
            try:
                snips, errs, ok, _ = c.preprocess(src, cg, plat_id)
            except Exception as e:
                fails.append((base, "-", "PREPROCESS", f"{type(e).__name__}: {e}"))
                fail_shaders += 1
                continue

            fatal = [e for e in errs if e["lvl"] >= 2] + [e for e in errs if e["lvl"] == 1]
            warns += len([e for e in errs if e["lvl"] == 1])
            if not ok or not snips:
                fails.append((base, "-", "PREPROCESS FAIL", f"{len(errs)} error(s)"))
                fail_shaders += 1
                continue

            shader_ok = True
            for s in snips:
                # A pass may define only one of the two entry points -- a
                # fragment-only effect pass is common -- and asking USC for a
                # kernel the snippet does not declare fails with "Did not find
                # shader kernel '' to compile".  Compile only what is there.
                has_vs = re.search(r"^\s*#pragma\s+vertex\s+\w+", s["text"], re.M) is not None
                has_ps = re.search(r"^\s*#pragma\s+fragment\s+\w+", s["text"], re.M) is not None
                for fn, fname, present in ((0, "vs", has_vs), (1, "ps", has_ps)):
                    if not present:
                        continue
                    try:
                        cok, blob, cerrs, binds = c.compile_snippet(
                            s["text"], cg, fn, [], plat_id)
                    except Exception as e:
                        fails.append((base, fname, "USC ERROR", f"{type(e).__name__}: {e}"))
                        shader_ok = False
                        fail_programs += 1
                        continue

                    if not cok:
                        shader_ok = False
                        fail_programs += 1
                        first = cerrs[0] if cerrs else "compile failed"
                        fails.append((base, fname, "USC COMPILE", first[:150]))
                        continue

                    ok_programs += 1

                    # Compare against the originals of this stage.
                    dxbc = U.extract_dxbc(blob)
                    if not dxbc or not disasm or not os.path.isdir(d):
                        continue
                    new_asm = disasm(dxbc)
                    if not new_asm:
                        continue
                    got = V.profile_asm(new_asm)
                    best_why, best_ok, best_kind = "", False, "hard"
                    for p in sorted(glob.glob(os.path.join(d, f"*_{fname}.orig.asm"))):
                        want = V.profile_asm(read_dxbc_asm(p))
                        good, kind, why = V.compare(want, got)
                        if good:
                            best_ok = True
                            if kind == "soft" and not best_why:
                                best_why, best_kind = why, "soft"
                            break
                        if not best_why:
                            best_why, best_kind = why, kind
                    if best_ok:
                        if best_kind == "soft":
                            soft += 1
                    else:
                        semantic += 1
                        fails.append((base, fname, "SEMANTIC " + best_kind.upper(), best_why))

            if shader_ok:
                ok_shaders += 1
            else:
                fail_shaders += 1

        c.send(U.pack_cmd("shutdown"))
        time.sleep(0.2)
    finally:
        try:
            conn.close()
        except OSError:
            pass
        srv.close()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()

    print()
    print(f"shaders   : {ok_shaders}/{ok_shaders + fail_shaders} compile with UnityShaderCompiler")
    print(f"programs  : {ok_programs} compiled, {fail_programs} failed")
    print(f"semantics : {semantic} differ from the original, {soft} optimizer-only")
    print(f"warnings  : {warns}")

    with open(os.path.join(root, "_usc_failures.txt"), "w", encoding="utf-8") as out:
        for f in fails:
            out.write("%s\t%s\t%s\t%s\n" % f)

    if fails:
        print()
        for f in fails[:40]:
            print("  %-44s %-4s %-18s %s" % f)
        if len(fails) > 40:
            print(f"  ... {len(fails) - 40} more (all in _usc_failures.txt)")
    return 0 if not fails else 1


if __name__ == "__main__":
    raise SystemExit(main())
