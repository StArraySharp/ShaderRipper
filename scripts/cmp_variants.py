"""Compile every keyword variant of a ripped .shader locally and compare it
against the original variant pool.

UnityShaderCompiler only builds the base variant of a multi_compile shader, so
the guarded branches never get exercised.  This script does it with
d3dcompiler_47 instead:

    1. split the HLSLPROGRAM body on its top-level #if / #elif / #else branches
       (each branch is one keyword combination's full program);
    2. read the keywords off each guard (`defined(X)`);
    3. compile the branch with those keywords defined, vert and frag;
    4. compare the result against every original disassembly of that stage
       (cbuffer-extent multiset + opcode overlap + samples) and take the best.

Usage:  cmp_variants.py <shader-file> <orig-asm-root> [--quiet]
Exit 0 when every branch that compiles also matches an original.
"""

import ctypes
import glob
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
import cmp_dxbc as C

DLL = r"D:\Development\RiderProjects\ShaderRipper\src\ShaderRipper\NativeLibs\D3Dcompiler_47.dll"
_dll = ctypes.WinDLL(DLL)


class _Vtbl(ctypes.Structure):
    _fields_ = [("QueryInterface", ctypes.c_void_p), ("AddRef", ctypes.c_void_p),
                ("Release", ctypes.c_void_p), ("GetBufferPointer", ctypes.c_void_p),
                ("GetBufferSize", ctypes.c_void_p)]


class _Blob(ctypes.Structure):
    _fields_ = [("vtbl", ctypes.POINTER(_Vtbl))]


def _blob_data(ptr):
    obj = ctypes.cast(ptr, ctypes.POINTER(_Blob)).contents
    gp = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(obj.vtbl.contents.GetBufferPointer)
    gs = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(obj.vtbl.contents.GetBufferSize)
    return ctypes.string_at(gp(ptr), gs(ptr))


_D3DCompile = _dll.D3DCompile
_D3DCompile.restype = ctypes.c_long
_D3DCompile.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_char_p, ctypes.c_void_p,
                        ctypes.c_void_p, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint,
                        ctypes.c_uint, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_void_p)]
_D3DDisassemble = _dll.D3DDisassemble
_D3DDisassemble.restype = ctypes.c_long
_D3DDisassemble.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p,
                            ctypes.POINTER(ctypes.c_void_p)]


def compile_hlsl(src, entry, target):
    out = ctypes.c_void_p()
    err = ctypes.c_void_p()
    b = src.encode("utf-8")
    hr = _D3DCompile(b, len(b), b"branch.hlsl", None, None, entry.encode(), target.encode(), 0, 0,
                     ctypes.byref(out), ctypes.byref(err))
    if hr != 0:
        msg = _blob_data(err).decode("utf-8", "replace") if err.value else hex(hr)
        return None, msg
    asm = ctypes.c_void_p()
    raw = _blob_data(out.value)          # bytecode bytes, not the blob pointer
    hr2 = _D3DDisassemble(raw, len(raw), 0, None, ctypes.byref(asm))
    if hr2 != 0 or asm.value == 0:
        return None, f"disassemble failed hr=0x{hr2 & 0xffffffff:08x}"
    return _blob_data(asm).decode("utf-8", "replace"), None


def _BlobSize(ptr):
    obj = ctypes.cast(ptr, ctypes.POINTER(_Blob)).contents
    gs = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(obj.vtbl.contents.GetBufferSize)
    return gs(ptr)


def split_branches(body):
    """Top-level #if/#elif/#else branches of one guard chain -> [(keywords, text)].

    Nested #if inside a branch cannot happen: variant bodies are flat emitter
    output.  The chain's #else fallback repeats branch 0 and is skipped.
    """
    lines = body.replace("\r\n", "\n").split("\n")
    branches = []
    cur_kw, cur = None, []
    depth = 0
    for line in lines:
        s = line.strip()
        if s.startswith("#if "):
            depth += 1
            if depth == 1:
                cur_kw, cur = [k for k in re.findall(r"defined\((\w+)\)", s)
                               if not s.strip().startswith("#if !") or True], []
                # `defined(X)` inside `!defined(X)` is an OFF keyword; keep only ON ones
                cur_kw = re.findall(r"(?<!!)defined\((\w+)\)", s)
                cur = []
                continue
        elif s.startswith("#elif") and depth == 1:
            branches.append((cur_kw, "\n".join(cur)))
            cur_kw = re.findall(r"(?<!!)defined\((\w+)\)", s)
            cur = []
            continue
        elif s.startswith("#else") and depth == 1:
            branches.append((cur_kw, "\n".join(cur)))
            cur_kw = None      # fallback repeats branch 0
            cur = []
            continue
        elif s.startswith("#endif"):
            depth -= 1
            if depth == 0:
                if cur_kw is not None:
                    branches.append((cur_kw, "\n".join(cur)))
                cur_kw, cur = None, []
                continue
        if depth >= 1:
            cur.append(line)
    return branches


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    shader, root = sys.argv[1], sys.argv[2]
    quiet = "--quiet" in sys.argv
    name = os.path.splitext(os.path.basename(shader))[0]

    src = open(shader, encoding="utf-8", errors="replace").read()
    m = re.search(r"HLSLPROGRAM\n(.*?)ENDHLSL", src, re.S)
    if not m:
        print("no HLSLPROGRAM")
        return 2
    body = "\n".join(l[12:] if l.startswith(" " * 12) else l
                     for l in m.group(1).split("\n"))

    branches = split_branches(body)
    origs = {st: sorted(glob.glob(os.path.join(root, name, f"*_{st}.orig.asm")))
             for st in ("ps", "vs")}

    passed = failed = errcount = 0
    for bi, (kws, text) in enumerate(branches):
        if kws is None:
            continue     # #else fallback = branch 0 again
        defines = "".join(f"#define {k} 1\n" for k in kws)
        row = [f"variant {bi} [{' + '.join(kws) if kws else 'base'}]"]
        ok_all = True
        for stage, entry, target in (("vs", "vert", "vs_4_0"), ("ps", "frag", "ps_4_0")):
            r = C.read_asm if False else None
        for stage, entry, target in (("vs", "vert", "vs_4_0"), ("ps", "frag", "ps_4_0")):
            # the branch text carries both stages; unused entry just fails to
            # find its struct sometimes -- tolerate per-stage failure
            try:
                asm, msg = compile_hlsl(defines + text, entry, target)
            except Exception as e:
                asm, msg = None, f"{type(e).__name__}: {e}"
            if asm is None:
                errcount += 1
                ok_all = False
                row.append(f"{stage}: COMPILE FAIL ({msg.splitlines()[-1][:70] if msg else '?'})")
                continue
            r = C.read_asm if False else None
            got = C.read_asm(io := __import__("io")) if False else None
            # write to temp for read_asm
            tmp = os.path.join(os.path.dirname(os.path.abspath(shader)), "_branch.asm")
            open(tmp, "w", encoding="utf-8").write(asm)
            r = C.read_asm(tmp)
            best = None
            for o in origs[stage]:
                d = C.read_asm(o)
                cb_same = sorted(d["cbs"].values()) == sorted(r["cbs"].values())
                ov = C.overlap(d["ops"], r["ops"])
                sc = (1 if cb_same else 0, ov, -(abs(d["samples"] - r["samples"])))
                if best is None or sc > best[0]:
                    best = (sc, o, d)
            sc, o, d = best
            ok = sc[0] == 1 and sc[1] >= C.OVERLAP_MIN and d["samples"] == r["samples"]
            if not ok:
                ok_all = False
            row.append(f"{stage}: {'PASS' if ok else 'FAIL'} best={sc[1]:.0%} "
                       f"cb={'Y' if sc[0] else 'N'} smpl={d['samples']}/{r['samples']}")
        if ok_all:
            passed += 1
            if not quiet:
                print("PASS", row[0], " | ".join(row[1:]))
        else:
            failed += 1
            print("FAIL", row[0], " | ".join(row[1:]))
    print(f"\n{passed} variant(s) pass, {failed} fail, {errcount} compile error(s), "
          f"of {len(branches)} branch(es)")
    return 0 if failed == 0 and errcount == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
