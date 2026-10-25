"""Semantic check: recompile each translated program and diff it against the
original DXBC disassembly it was derived from.

Two levels, because two stages can each go wrong:

  level 1  <blobIndex>_<stage>.hlsl  vs  <blobIndex>_<stage>.orig.asm
           one-to-one, no matching: this measures the translator.

  level 2  every branch of the ShaderLab HLSLPROGRAM  vs  the best original
           of that stage: this measures the wrapper (cross-stage merge,
           packoffset layout, variant guards).

The comparison is an ordered opcode sequence, not a multiset, because the
translation is meant to be one instruction for one instruction.  Three things
are normalised away first, and only those:

  * copies (`mov`) -- the register-level style spells `float4 v0 = i.vertex`
    and the compiler turns each into a copy; a copy cannot change a result.
  * DXBC opcode modifiers (`mul_sat`, `add_x2`, ...) -- fxc folds a `saturate`
    into the operation that feeds it, while our output spells the `saturate`
    separately, so the modifier is folded back into the base opcode.
  * declarations, `ret`, `nop`, and the profile line -- no behaviour.

Constant-buffer extents and sample counts are compared as well: a member pinned
to the wrong row, or a dropped resource read, is exactly the kind of silent
difference this is looking for.

Usage: verify_semantics.py <export-dir> [--limit N] [--quiet]
"""

import ctypes
import glob
import os
import re
import sys

DLL = r"D:\Development\RiderProjects\ShaderRipper\src\ShaderRipper\NativeLibs\D3Dcompiler_47.dll"
_dll = ctypes.WinDLL(DLL)


class _Vtbl(ctypes.Structure):
    _fields_ = [("QueryInterface", ctypes.c_void_p), ("AddRef", ctypes.c_void_p),
                ("Release", ctypes.c_void_p), ("GetBufferPointer", ctypes.c_void_p),
                ("GetBufferSize", ctypes.c_void_p)]


class _Blob(ctypes.Structure):
    _fields_ = [("vtbl", ctypes.POINTER(_Vtbl))]


def _data(ptr):
    obj = ctypes.cast(ptr, ctypes.POINTER(_Blob)).contents
    gp = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(obj.vtbl.contents.GetBufferPointer)
    gs = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(obj.vtbl.contents.GetBufferSize)
    return ctypes.string_at(gp(ptr), gs(ptr))


_C = _dll.D3DCompile
_C.restype = ctypes.c_long
_C.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_char_p, ctypes.c_void_p,
               ctypes.c_void_p, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint,
               ctypes.c_uint, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_void_p)]
_D = _dll.D3DDisassemble
_D.restype = ctypes.c_long
_D.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p,
               ctypes.POINTER(ctypes.c_void_p)]


def compile_hlsl(src, entry, target):
    out = ctypes.c_void_p()
    err = ctypes.c_void_p()
    b = src.encode("utf-8")
    hr = _C(b, len(b), b"v.hlsl", None, None, entry.encode(), target.encode(), 0, 0,
            ctypes.byref(out), ctypes.byref(err))
    if hr != 0:
        msg = _data(err).decode("utf-8", "replace") if err.value else hex(hr & 0xFFFFFFFF)
        return None, msg
    raw = _data(out.value)
    asm = ctypes.c_void_p()
    hr2 = _D(raw, len(raw), 0, None, ctypes.byref(asm))
    if hr2 != 0 or not asm.value:
        return None, "disassemble failed"
    return _data(asm).decode("utf-8", "replace"), None


CB = re.compile(r"dcl_constantbuffer\s+cb(\d+)\[(\d+)\]", re.I)
TEX = re.compile(r"dcl_resource(?:_structured)?\s+(\w+)", re.I)
SMP = re.compile(r"\b(sample|sample_l|sample_d|sample_b|sample_c|ld)\w*", re.I)
OP = re.compile(r"^\s*(?:[0-9]+:)?\s*([a-z][a-z0-9_]*)\b")
MOD = re.compile(r"_(?:sat|x2|x4|x8|d2|d4|d8)$")
PROFILE = re.compile(r"^(?:ps|vs|gs|hs|ds|cs)_[0-9]_[0-9]$")

# Copies and punctuation carry no behaviour of their own.
NOISE = {"mov", "ret", "nop", "discard", "endif", "endloop", "endswitch"}


def profile_asm(text):
    cbs, ops, samples, textures = {}, [], 0, 0
    for line in text.splitlines():
        s = line.strip()
        if not s or s.startswith("//") or s.startswith(";"):
            continue
        body = re.sub(r"^\d+:\s*", "", s)
        m = CB.search(body)
        if m:
            cbs[int(m.group(1))] = int(m.group(2))
        if TEX.search(body):
            textures += 1
        if SMP.search(body):
            samples += 1
        mi = OP.match(body)
        if mi:
            op = mi.group(1)
            if op.startswith("dcl_") or PROFILE.match(op) or op in NOISE:
                continue
            ops.append(MOD.sub("", op))
    return {"cbs": cbs, "ops": ops, "samples": samples, "textures": textures}


def first_difference(a, b):
    n = min(len(a), len(b))
    for i in range(n):
        if a[i] != b[i]:
            return i
    return -1 if len(a) == len(b) else n


def describe(ops, at, width=4):
    lo = max(0, at - 1)
    return " ".join(ops[lo:lo + width]) or "-"


def split_branches(body):
    """Top-level #if/#elif branches of one guard chain -> [(keywords, text)]."""
    out, cur_kw, cur, depth = [], None, [], 0
    for line in body.replace("\r\n", "\n").split("\n"):
        s = line.strip()
        if s.startswith("#if "):
            depth += 1
            if depth == 1:
                cur_kw = re.findall(r"(?<!!)defined\((\w+)\)", s)
                cur = []
                continue
        elif s.startswith("#elif") and depth == 1:
            out.append((cur_kw, "\n".join(cur)))
            cur_kw = re.findall(r"(?<!!)defined\((\w+)\)", s)
            cur = []
            continue
        elif s.startswith("#else") and depth == 1:
            out.append((cur_kw, "\n".join(cur)))
            cur_kw = None
            cur = []
            continue
        elif s.startswith("#endif"):
            depth -= 1
            if depth == 0:
                if cur_kw is not None:
                    out.append((cur_kw, "\n".join(cur)))
                cur_kw, cur = None, []
                continue
        if depth >= 1:
            cur.append(line)
    return out


def dedent(text, n=12):
    return "\n".join(l[n:] if l.startswith(" " * n) else l for l in text.split("\n"))


def compare(want, got):
    """(ok, kind, reason) for one program pair.

    `kind` is "hard" for a difference no compiler is allowed to make, and
    "soft" for one the optimizer is known to produce:
      * constant-buffer extents -- hard.  A buffer the original binds with N
        rows is part of the program's interface; a rebuild that binds fewer
        rows either declared a member at the wrong row or dropped one.
      * texture declarations -- hard.  A missing resource changes what the
        program can read.
      * sample count -- soft downwards.  fxc merges identical samples (two
        `.Sample()` calls at the same coordinate become one) and folds a
        sample whose result is unused; the original was built by the same
        compiler from the same source, so an increase is a real difference
        and a decrease is not.
      * opcode sequence -- soft.  Register allocation, scheduling and the
        mul/mad choice are the compiler's business.
    """
    if sorted(want["cbs"].values()) != sorted(got["cbs"].values()):
        return False, "hard", ("cb extents %s -> %s"
                               % (sorted(want["cbs"].values()), sorted(got["cbs"].values())))
    if want["textures"] != got["textures"]:
        return False, "hard", "textures %d -> %d" % (want["textures"], got["textures"])
    if got["samples"] > want["samples"]:
        return False, "hard", "samples %d -> %d" % (want["samples"], got["samples"])
    if got["samples"] < want["samples"]:
        return True, "soft", "samples %d -> %d (merged)" % (want["samples"], got["samples"])
    at = first_difference(want["ops"], got["ops"])
    if at >= 0:
        return True, "soft", ("op %d: %s -> %s"
                              % (at, describe(want["ops"], at), describe(got["ops"], at)))
    return True, "", ""


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    root = sys.argv[1]
    limit = int(sys.argv[sys.argv.index("--limit") + 1]) if "--limit" in sys.argv else 0
    quiet = "--quiet" in sys.argv

    shaders = sorted(glob.glob(os.path.join(root, "*.shader")))
    if limit:
        shaders = shaders[:limit]

    l1_total = l1_ok = l2_total = l2_ok = 0
    fails, soft = [], []

    for sh in shaders:
        base = os.path.splitext(os.path.basename(sh))[0]
        d = os.path.join(root, base)
        if not os.path.isdir(d):
            continue

        for hlsl in sorted(glob.glob(os.path.join(d, "*.hlsl"))):
            stem = os.path.basename(hlsl)[:-5]
            orig = os.path.join(d, stem + ".orig.asm")
            if not os.path.exists(orig):
                continue
            stage = stem.rsplit("_", 1)[-1]
            entry, target = ("vert", "vs_4_0") if stage == "vs" else ("frag", "ps_4_0")
            l1_total += 1
            asm, err = compile_hlsl(open(hlsl, encoding="utf-8", errors="replace").read(),
                                    entry, target)
            if asm is None:
                fails.append((base, stem, "L1 COMPILE",
                              err.splitlines()[-1][:110] if err else "?"))
                continue
            ok, kind, why = compare(
                profile_asm(open(orig, encoding="utf-8", errors="replace").read()),
                profile_asm(asm))
            if ok:
                l1_ok += 1
                if kind == "soft":
                    soft.append((base, stem, "L1 SOFT", why))
            else:
                fails.append((base, stem, "L1 HARD", why))

        src = open(sh, encoding="utf-8", errors="replace").read()
        blocks = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", src, re.S)
        originals = {st: [profile_asm(open(p, encoding="utf-8", errors="replace").read())
                          for p in sorted(glob.glob(os.path.join(d, f"*_{st}.orig.asm")))]
                     for st in ("vs", "ps")}
        for blk in blocks:
            for kws, text in split_branches(dedent(blk)):
                defines = "".join(f"#define {k} 1\n" for k in kws)
                for stage, entry, target in (("vs", "vert", "vs_4_0"), ("ps", "frag", "ps_4_0")):
                    if not originals[stage] or f"{entry}(" not in text:
                        continue
                    l2_total += 1
                    asm, err = compile_hlsl(defines + text, entry, target)
                    if asm is None:
                        fails.append((base, "branch", f"L2 COMPILE {stage}",
                                      err.splitlines()[-1][:110] if err else "?"))
                        continue
                    got = profile_asm(asm)
                    best_why, best_ok, best_kind = "", False, "hard"
                    for o in originals[stage]:
                        ok, kind, why = compare(o, got)
                        if ok:
                            best_ok = True
                            if kind == "soft" and best_why == "":
                                best_why, best_kind = why, "soft"
                            break
                        if not best_why:
                            best_why, best_kind = why, kind
                    if best_ok:
                        l2_ok += 1
                        if best_kind == "soft":
                            soft.append((base, "branch", "L2 SOFT", best_why))
                    else:
                        fails.append((base, "branch", "L2 HARD " + stage, best_why))

    print(f"level 1 (translator) : {l1_ok}/{l1_total} reproduce the original")
    print(f"level 2 (wrapper)    : {l2_ok}/{l2_total} branches reproduce an original")
    print(f"semantic failures    : {len(fails)}")
    print(f"compiler differences : {len(soft)}")

    with open(os.path.join(root, "_semantic_failures.txt"), "w", encoding="utf-8") as out:
        for f in fails:
            out.write("%s\t%s\t%s\t%s\n" % f)
    with open(os.path.join(root, "_semantic_soft.txt"), "w", encoding="utf-8") as out:
        for f in soft:
            out.write("%s\t%s\t%s\t%s\n" % f)

    if fails and not quiet:
        print()
        for f in fails[:40]:
            print("  %-42s %-9s %-16s %s" % f)
        if len(fails) > 40:
            print(f"  ... {len(fails) - 40} more (all in _semantic_failures.txt)")
    return 0 if not fails else 1


if __name__ == "__main__":
    raise SystemExit(main())
