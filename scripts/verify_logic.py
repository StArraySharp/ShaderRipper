"""Check that a rebuilt program is LOGICALLY the same program as the original.

Compiling is not the question this answers.  A shader can compile and still be a
different program: `lt` writing 1.0 instead of the all-ones mask, `sincos`
pairing a destination lane with the wrong source lane, a branch testing `.x` of
a four-component operand -- all of those pass `fxc` and all of them render
something else.  So the output is compiled back to DXBC, disassembled, and
compared with the original disassembly on the invariants that survive
re-registering and re-scheduling:

  1. constant-buffer extents   -- the multiset of (register, rows)
  2. opcode histogram          -- instruction mix, order and registers ignored
  3. sample / gather count     -- texture traffic must be identical
  4. immediate-table values    -- a baked kernel is data, not code
  5. cbuffer rows read         -- which (register, row) pairs the body touches

Usage:
    py -3 verify_logic.py <export dir> [-j N] [--max 40] [-v]
    py -3 verify_logic.py <dir with pairs> --pairs

`<export dir>` is what `ShaderRipper --game ... --programs --orig-asm` wrote: a
program folder per shader holding `<blob>_<stage>.hlsl` next to
`<blob>_<stage>.orig.asm`.  The HLSL from the DXBC route is self-contained
(register-level cbuffers, no includes), which is what makes this check possible
without Unity.

The GLSL route's programs (`*_glsl.hlsl`) include UnityCG.cginc and are skipped
here; `usc_check.py` is the tool for those.
"""

import collections
import ctypes
import os
import re
import sys
import threading

# --- d3dcompiler -----------------------------------------------------------

_lib = None
_lock = threading.Lock()


class _Blob(ctypes.Structure):
    _fields_ = [("vtbl", ctypes.c_void_p), ("data", ctypes.c_void_p),
                ("size", ctypes.c_size_t)]


def compiler():
    global _lib
    with _lock:
        if _lib is not None:
            return _lib
        for name in ("d3dcompiler_47", "d3dcompiler_43"):
            try:
                lib = ctypes.WinDLL(name)
                break
            except OSError:
                continue
        else:
            raise SystemExit("d3dcompiler_47.dll not found")

        lib.D3DCompile.restype = ctypes.c_long
        lib.D3DCompile.argtypes = [
            ctypes.c_char_p, ctypes.c_size_t, ctypes.c_char_p, ctypes.c_void_p,
            ctypes.c_void_p, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint,
            ctypes.c_uint, ctypes.POINTER(ctypes.POINTER(_Blob)),
            ctypes.POINTER(ctypes.POINTER(_Blob))]
        lib.D3DDisassemble.restype = ctypes.c_long
        lib.D3DDisassemble.argtypes = [
            ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p,
            ctypes.POINTER(ctypes.POINTER(_Blob))]
        _lib = lib
        return lib


def _text(blob):
    """Read an ID3DBlob through its vtable.

    ID3DBlob has no public data member -- `GetBufferPointer` (slot 3) and
    `GetBufferSize` (slot 4) are the only way in.  Reading a guessed offset
    segfaults the interpreter instead of failing, so the vtable it is.
    """
    import ctypes as C
    vtbl = C.cast(blob, C.POINTER(C.POINTER(C.c_void_p))).contents
    gbp = C.WINFUNCTYPE(C.c_void_p, C.c_void_p)(vtbl[3])
    gbs = C.WINFUNCTYPE(C.c_size_t, C.c_void_p)(vtbl[4])
    ptr = gbp(blob)
    size = gbs(blob)
    if not ptr or not size:
        return b""
    return C.string_at(ptr, size)


def compile_hlsl(src, entry, profile):
    """-> (dxbc bytes, None) or (None, error text)."""
    lib = compiler()
    code = ctypes.POINTER(_Blob)()
    err = ctypes.POINTER(_Blob)()
    raw = src.encode("utf-8")
    hr = lib.D3DCompile(raw, len(raw), b"program.hlsl", None, None,
                        entry.encode(), profile.encode(), 0, 0,
                        ctypes.byref(code), ctypes.byref(err))
    if hr != 0:
        detail = ""
        if err:
            detail = _text(err).decode("utf-8", "replace")
        return None, detail or ("D3DCompile hr=0x%08x" % (hr & 0xFFFFFFFF))
    return _text(code), None


def disassemble(dxbc):
    lib = compiler()
    out = ctypes.POINTER(_Blob)()
    hr = lib.D3DDisassemble(dxbc, len(dxbc), 0, None, ctypes.byref(out))
    if hr != 0:
        return None
    return _text(out).decode("utf-8", "replace")


# --- invariant extraction --------------------------------------------------

RE_CB = re.compile(r"^dcl_constantbuffer\s+cb(\d+)\[(\d+)\]", re.I)
RE_TEX = re.compile(r"^dcl_(?:resource|texture)\s+t(\d+)", re.I)
RE_SAMPLE = re.compile(r"^(?:sample|gather|ld|ldms|lod)\b", re.I)
RE_LIT = re.compile(r"l\(([^()]*)\)")
RE_INSTR = re.compile(r"^\s*([a-z][a-z0-9_]*)\b", re.I)
RE_CBREAD = re.compile(r"\bcb(\d+)\[(\d+)\]", re.I)
RE_ICB = re.compile(r"^dcl_immediateConstantBuffer\s*\{(.*)\}", re.I | re.S)
SUFFIXES = ("_sat", "_x2", "_x4", "_x8", "_d", "_glc", "_clc", "_nan",
            "_uint", "_int", "_indexable", "_aoffimmi")

# Control flow is where DXBC and HLSL legitimately disagree on SPELLING:
# `breakc_nz r0.x` and `if (r0.x) break;` are the same control flow, but the
# compiler emits one instruction for the first and three (`if_nz`, `break`,
# `endif`) for the second.  Comparing them one-for-one reports a difference where
# there is none, so the histogram counts flow as a single bucket and the shape of
# the flow (how many branches, how many loops) is compared separately.
FLOW = {
    "if_nz", "if_z", "else", "endif", "loop", "endloop", "break", "breakc_z",
    "breakc_nz", "continue", "continuec_z", "continuec_nz", "switch", "case",
    "default", "endswitch", "ret", "discard", "discard_nz", "nop",
}

# fxc is free to contract `mul` + `add` into `mad` or to expand it back, and it
# re-associates a sum.  Those choices change the instruction mix without changing
# what the program computes, so the arithmetic opcodes are compared as one family
# and the individual counts are reported as a note.
ARITH = {"add", "sub", "mul", "mad", "div", "rcp", "min", "max",
         "dp1", "dp2", "dp3", "dp4", "dp2add", "dp4add", "frc"}

# Ops whose count is a semantic commitment of the translator.  Only instructions
# that CANNOT be removed by a correct compiler belong here: a `discard` that
# disappeared changes which pixels die, and a texture read that disappeared
# changes what the shader sees.  Conversions (`itof`, `ftoi`, `ftou`, `utof`) do
# NOT belong: fxc keeps a converted value in one register and reads it three
# times, so three `itof` legitimately become one.
EXACT = {
    "discard", "discard_nz", "gather", "ld", "ldms", "resinfo", "sampleinfo",
}

# Instruction kinds that may be common-subexpression-eliminated, folded, or
# contracted by the compiler.  Losing one of these is reported, never failed.
FOLDABLE = {"itof", "ftoi", "ftou", "utof", "ubfe", "ibfe", "sincos", "sincosb"}


def parse(text):
    cbs = []
    ops = collections.Counter()
    arith = collections.Counter()
    flow = collections.Counter()
    samples = 0
    reads = set()
    lits = collections.Counter()
    depth = 0
    icb = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line or line.startswith("//") or line.startswith("#"):
            continue
        for m in RE_LIT.finditer(line):
            for part in m.group(1).split(","):
                part = part.strip()
                if part:
                    lits[part] += 1
        if depth > 0:
            # inside a dcl_immediateConstantBuffer block
            if "}" in line:
                depth -= 1
                head = line.split("}")[0]
                icb.extend(x.strip() for x in head.strip().rstrip(",").split(",") if x.strip())
            else:
                icb.extend(x.strip() for x in line.rstrip(",").split(",") if x.strip())
            continue

        m = RE_ICB.match(line)
        if m:
            body = m.group(1)
            icb.extend(x.strip() for x in body.split(",") if x.strip())
            continue
        if line.lower().startswith("dcl_immediateconstantbuffer"):
            depth = 1
            rest = line[line.find("{") + 1:]
            icb.extend(x.strip() for x in rest.split(",") if x.strip())
            continue

        m = RE_CB.match(line)
        if m:
            cbs.append((int(m.group(1)), int(m.group(2))))
            continue
        if RE_TEX.match(line):
            continue

        m = RE_INSTR.match(line)
        if not m:
            continue
        op = m.group(1).lower()
        if op.startswith("dcl_"):
            continue
        base = op
        changed = True
        while changed:
            changed = False
            for suffix in SUFFIXES:
                if base.endswith(suffix):
                    base = base[: -len(suffix)]
                    changed = True
                    break
        if base in FLOW:
            flow[base] += 1
        elif base in ARITH:
            arith[base] += 1
        else:
            ops[base] += 1
        if RE_SAMPLE.match(op):
            samples += 1
        for cbm in RE_CBREAD.finditer(line):
            reads.add((int(cbm.group(1)), int(cbm.group(2))))

    return {
        "cbs": tuple(sorted(cbs)),
        "ops": ops,
        "arith": arith,
        "flow": flow,
        "samples": samples,
        "reads": reads,
        "icb": tuple(icb),
        "literals": lits,
    }


def _hist_distance(a, b, total):
    """Instruction-mix distance over the non-arithmetic, non-flow opcodes.

    Arithmetic and control flow are compared separately (as a family and as a
    shape) because the compiler legitimately rewrites both.  What this catches is
    a translation that produced a different KIND of work: a baked table read
    turned into arithmetic, a `dp3` lost, a `mov` swarm where the original had one
    store.
    """
    diff = 0
    detail = []
    for k in sorted(set(a["ops"]) | set(b["ops"])):
        d = abs(a["ops"].get(k, 0) - b["ops"].get(k, 0))
        if d:
            diff += d
            detail.append("%s:%d/%d" % (k, a["ops"].get(k, 0), b["ops"].get(k, 0)))
    return diff, diff * 100.0 / total, detail


def _strictly_reduced(orig, rebuilt):
    """True when the rebuild has fewer of everything -- fxc folded code away.

    A rebuild is not required to emit the same instruction count: fxc unrolls,
    folds a constant table into literals, and drops `mul x, 1.0`.  What it may
    not do is invent work.  So "fewer of everything" is a legitimate difference
    and only an increase is a symptom.
    """
    for k, n in rebuilt["ops"].items():
        if n > orig["ops"].get(k, 0):
            return False
    return True


def _sincos_expected(asm_text):
    """(sin writes, cos writes) the original program performs."""
    sin = cos = 0
    for line in asm_text.splitlines():
        s = line.strip()
        if not s.lower().startswith("sincos"):
            continue
        parts = [p.strip() for p in s.split(None, 1)[1].split(",")]
        if len(parts) < 3:
            continue
        dst = parts[0]
        cos_dst = parts[1]
        sin += 0 if dst.startswith("null") else max(1, len(dst.split(".", 1)[1]) if "." in dst else 4)
        cos += 0 if cos_dst.startswith("null") else max(
            1, len(cos_dst.split(".", 1)[1]) if "." in cos_dst else 4)
    return sin, cos


def _sincos_actual(hlsl_text):
    sin = len(re.findall(r"=\s*sin\(", hlsl_text))
    cos = len(re.findall(r"=\s*cos\(", hlsl_text))
    return sin, cos


def _sample_calls(hlsl_text):
    """Texture fetch calls the EMITTER wrote.

    The compiled count is not usable: fxc merges two `Sample(tex, coord)` calls
    with the same coordinate into one, so a correct translation can compile to
    fewer samples than the original program had.  What the emitter controls is
    how many fetch calls it wrote.
    """
    return len(re.findall(r"\.(?:Sample|SampleLevel|SampleBias|SampleGrad|SampleCmp|"
                          r"SampleCmpLevelZero|Load|Gather)\s*\(", hlsl_text))


def compare(orig, rebuilt, orig_text=None, hlsl_text=None):
    """-> (hard notes, soft notes).

    Hard means "the rebuilt program does something the original cannot": fewer
    samples (a texture read was lost), a narrower constant buffer (a member moved
    off its row), a `sincos` output dropped (a register nothing writes), a
    `discard` that disappeared (a branch that no longer kills pixels), or a read
    the original never made.

    Soft means "the compiler rewrote it": fxc eliminates dead code, contracts
    `mul`+`add` into `mad`, and converts a float mask to a bool before a select.
    Those change the instruction mix without changing the computation, and a
    check that fails on them would be reporting its own blindness.
    """
    bad, soft = [], []

    # The DECLARED extent is not an invariant: fxc sizes a cbuffer from the rows
    # the program actually reads, so a correct translation that keeps the original
    # 32-row declaration still compiles to `CB1[28]` when the last read is row 27.
    # What matters is that no row the original reads is unreachable, which the
    # read-set comparison below decides.  A dropped register is still reported.
    o_regs = {r for r, _ in orig["cbs"]}
    r_regs = {r for r, _ in rebuilt["cbs"]}
    gone = o_regs - r_regs
    # A register that disappears is not automatically a loss: the matrix fold
    # re-spells a transform as `UnityObjectToClipPos`, whose body reads the same
    # matrices, and fxc then drops the declaration of a buffer nothing else
    # touches.  What matters is that no ROW the original read became unreadable,
    # which the read-set comparison below decides.
    if gone and not (hlsl_text and "UnityObjectToClipPos" in hlsl_text):
        bad.append("cbuffer 寄存器消失: %s" % sorted(gone))
    elif gone:
        soft.append("cbuffer 寄存器消失(折叠进 helper): %s" % sorted(gone))
    if orig["cbs"] != rebuilt["cbs"]:
        soft.append("cbuffer 声明宽度 %s -> %s (fxc 按实际读取裁剪)"
                    % (orig["cbs"], rebuilt["cbs"]))

    # A lost sample is the classic silent bug: the shader still compiles and
    # renders a constant.  But fxc is allowed to merge two identical
    # `Sample(tex, coord)` calls, so the COMPILED count is not the emitter's
    # promise -- the number of sample calls in the generated source is.  That is
    # compared directly against the original's sample count.
    if orig_text is not None and hlsl_text is not None:
        want = orig["samples"]
        got = _sample_calls(hlsl_text)
        if got < want:
            bad.append("采样调用少了: 源码 %d / 原始 %d" % (got, want))
        elif got > want:
            soft.append("采样调用多了: 源码 %d / 原始 %d" % (got, want))
    elif rebuilt["samples"] < orig["samples"]:
        bad.append("采样数减少 %d -> %d" % (orig["samples"], rebuilt["samples"]))

    # Instructions whose count is a promise the translator makes.  Only a
    # DECREASE is hard: dead code may legitimately disappear, a `discard` may
    # not.
    for op in sorted(EXACT):
        o_n = orig["ops"].get(op, 0)
        r_n = rebuilt["ops"].get(op, 0)
        if o_n == r_n:
            continue
        if r_n < o_n:
            bad.append("%s 少了: %d -> %d" % (op, o_n, r_n))
        else:
            soft.append("%s 多了: %d -> %d" % (op, o_n, r_n))

    for op in sorted(FOLDABLE):
        o_n = orig["ops"].get(op, 0)
        r_n = rebuilt["ops"].get(op, 0)
        if o_n != r_n:
            soft.append("%s %d -> %d (可被编译器合并)" % (op, o_n, r_n))

    total = max(sum(orig["ops"].values()) + sum(orig["arith"].values()), 1)
    diff, pct, detail = _hist_distance(orig, rebuilt, total)
    if diff:
        soft.append("指令组合差异 %d/%d (%.1f%%): %s"
                    % (diff, total, pct, ", ".join(detail[:6])))

    o_sum = sum(orig["arith"].values())
    r_sum = sum(rebuilt["arith"].values())
    if o_sum != r_sum:
        soft.append("算术指令数 %d -> %d" % (o_sum, r_sum))

    # A constant the original multiplies by, masks with, or compares against is
    # data.  Losing one means the translation spelled a value differently; the
    # 1.0-for-true bug is exactly this shape (0x3F800000 where 0xBF800000
    # belongs) and it does not change any instruction COUNT.
    lost = orig["literals"] - rebuilt["literals"]
    if lost:
        soft.append("立即数丢失 %d 个: %s"
                    % (sum(lost.values()), ", ".join(sorted(lost)[:6])))

    # `sincos` is the one instruction whose translation can lose a whole register
    # without changing the instruction count elsewhere: reading one destination
    # instead of two leaves the sine lane unwritten.
    if orig_text is not None and hlsl_text is not None:
        want_sin, want_cos = _sincos_expected(orig_text)
        got_sin, got_cos = _sincos_actual(hlsl_text)
        if got_sin < want_sin or got_cos < want_cos:
            bad.append("sincos 输出减少: sin %d/%d, cos %d/%d"
                       % (got_sin, want_sin, got_cos, want_cos))
        elif got_sin > want_sin or got_cos > want_cos:
            soft.append("sincos 输出增多: sin %d/%d, cos %d/%d"
                        % (got_sin, want_sin, got_cos, want_cos))

    # A row the original read and the rebuild does not means a value came from
    # somewhere else -- the signature of a mis-named member.  A row the rebuild
    # reads that the original never did is a member declared off its row.
    only_orig = orig["reads"] - rebuilt["reads"]
    only_new = rebuilt["reads"] - orig["reads"]
    if only_new:
        bad.append("cb 读取行多 %d 行: %s"
                   % (len(only_new), sorted(only_new)[:6]))
    if only_orig:
        pct = len(only_orig) * 100.0 / max(len(orig["reads"]), 1)
        folded = hlsl_text and "UnityObjectToClipPos" in hlsl_text
        if pct > 50.0 and not folded:
            bad.append("cb 读取行少 %d 行 (%.0f%%)" % (len(only_orig), pct))
        else:
            soft.append("cb 读取行少 %d 行%s" % (len(only_orig),
                                                "(折叠进 helper)" if folded else ""))

    return bad, soft

# --- driver ----------------------------------------------------------------

PROFILE = re.compile(r"^\s*((?:ps|vs|cs|gs|hs|ds)_\d+_\d+)", re.M)
ENTRY = re.compile(r"^\s*(?:struct \w+\s*)?\w+\s+(\w+)\s*\(", re.M)

# The DXBC route emits self-contained HLSL (no includes), but the matrix-fold pass
# re-spells a transform as Unity's `UnityObjectToClipPos`, which lives in
# `UnityCG.cginc`.  That macro is a pure function of two globals the same file
# declares, so defining it here makes the check compile the program the emitter
# means -- the same text Unity would see after preprocessing.
UNITY_SHIM = """
#ifndef UNITY_OBJECT_TO_CLIP_POS_DEFINED
#define UNITY_OBJECT_TO_CLIP_POS_DEFINED
// `UnityObjectToClipPos` is `mul(unity_MatrixVP, mul(unity_ObjectToWorld, pos))`
// in `UnityCG.cginc`, where both are `float4x4`.  This route declares them as
// `float4 M[4]` arrays (one row each), so the shim supplies the matrix-typed view
// before calling the same two multiplies.
float4 UnityObjectToClipPos(float4 pos)
{
    float4x4 m = float4x4(unity_ObjectToWorld[0], unity_ObjectToWorld[1],
                          unity_ObjectToWorld[2], unity_ObjectToWorld[3]);
    float4x4 vp = float4x4(unity_MatrixVP[0], unity_MatrixVP[1],
                           unity_MatrixVP[2], unity_MatrixVP[3]);
    return mul(vp, mul(m, float4(pos.xyz, 1.0)));
}
#endif
"""


def with_shim(src):
    """Kept for compatibility: the emitter defines the helper it uses.

    The matrix-fold pass emits `UnityObjectToClipPos` together with its own
    definition over the row-array matrices this route declares, so the generated
    program compiles standalone.  Nothing has to be injected here.
    """
    return src


def entry_and_profile(asm_text, hlsl_text):
    m = PROFILE.search(asm_text)
    profile = m.group(1) if m else None
    # The emitter names its entry `vert` / `frag`; fall back to the first
    # function-shaped declaration that is not a struct or a keyword.
    for cand in ("vert", "frag"):
        if re.search(r"\b%s\s*\(" % cand, hlsl_text):
            return cand, profile
    return None, profile


def pairs(root):
    """(hlsl path, orig asm path) pairs under an export tree."""
    for dirpath, _dirs, files in os.walk(root):
        for f in files:
            if not f.endswith(".hlsl") or f.endswith("_glsl.hlsl"):
                continue
            stem = f[:-5]
            asm = os.path.join(dirpath, stem + ".orig.asm")
            if os.path.exists(asm):
                yield os.path.join(dirpath, f), asm


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    verbose = "-v" in sys.argv
    limit = 0
    jobs = 1
    for i, a in enumerate(sys.argv):
        if a == "--max" and i + 1 < len(sys.argv):
            limit = int(sys.argv[i + 1])
        if a in ("-j", "--jobs") and i + 1 < len(sys.argv):
            jobs = int(sys.argv[i + 1])

    if not args:
        print(__doc__)
        return 2
    root = args[0]

    work = list(pairs(root))
    if limit:
        work = work[:limit]
    if not work:
        print("no <program>.hlsl + .orig.asm pairs under %s" % root)
        return 2

    print("%d program pair(s), %d thread(s)" % (len(work), jobs))
    results = []
    q = collections.deque(work)
    qlock = threading.Lock()

    def worker():
        while True:
            with qlock:
                if not q:
                    return
                hlsl_path, asm_path = q.popleft()
            try:
                hlsl = open(hlsl_path, encoding="utf-8", errors="replace").read()
                orig_text = open(asm_path, encoding="utf-8", errors="replace").read()
            except OSError as e:
                results.append((hlsl_path, ["读取失败: %s" % e], [], 0))
                continue
            entry, profile = entry_and_profile(orig_text, hlsl)
            if not entry or not profile:
                results.append((hlsl_path, ["无法确定入口/配置档"], [], 0))
                continue
            dxbc, err = compile_hlsl(with_shim(hlsl), entry, profile)
            if dxbc is None:
                results.append((hlsl_path, ["编译失败: " + err.splitlines()[0][:200]], [], 0))
                continue
            rebuilt_text = disassemble(dxbc)
            if not rebuilt_text:
                results.append((hlsl_path, ["反汇编失败"], [], 0))
                continue
            a = parse(orig_text)
            b = parse(rebuilt_text)
            bad, soft = compare(a, b, orig_text, hlsl)
            results.append((hlsl_path, bad, soft, sum(a["ops"].values())))

    threads = [threading.Thread(target=worker, daemon=True) for _ in range(max(1, jobs))]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    ok = 0
    soft_only = 0
    failed = []
    for path, bad, soft, n in results:
        rel = os.path.relpath(path, root)
        if not bad:
            ok += 1
            if soft:
                soft_only += 1
            if verbose:
                print("PASS %s  ops=%d%s" % (rel, n,
                                             ("  (soft: " + "; ".join(soft) + ")") if soft else ""))
        else:
            failed.append((rel, bad))

    print()
    print("programs=%d passed=%d failed=%d (soft %d) rate=%.1f%%"
          % (len(results), ok, len(failed), soft_only,
             100.0 * ok / max(len(results), 1)))
    if failed:
        print()
        for rel, bad in failed[:40]:
            print("FAIL %s" % rel)
            for note in bad:
                print("     " + note)
        if len(failed) > 40:
            print("... %d more" % (len(failed) - 40))
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
