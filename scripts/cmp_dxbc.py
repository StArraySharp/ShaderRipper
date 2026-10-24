"""Compare a reconstructed shader's DXBC against the original it came from.

The translation is only as good as the constant-buffer layout it restores: if a
member is declared in the wrong order the offsets shift, and every `cbN[row]` read
lands on a different value.  Recompiling the emitted ShaderLab through Unity's own
compiler is the way to see that -- the output DXBC then has to agree with the
original on the things that matter:

    * the constant buffers it declares and how many rows each one binds
    * the instructions it runs, as a multiset of opcodes (register allocation and
      scheduling are the compiler's business and will differ)
    * the resources it samples

Usage:
    cmp_dxbc.py <original.asm> <rebuilt.asm> [--verbose]

Both files are D3DDisassemble output.  Exit code 0 when the program is "close
enough", 1 otherwise -- close enough being: same constant-buffer sizes, opcode
multiset overlap at or above OVERLAP_MIN, and the same number of samples.
"""

import re
import sys

OVERLAP_MIN = 0.80

CB_DECL = re.compile(r"dcl_constantbuffer\s+cb(\d+)\[(\d+)\]", re.I)
TEX_DECL = re.compile(r"dcl_resource(?:_structured)?\s+(\w+)", re.I)
SAMPLE = re.compile(r"\b(sample|sample_l|sample_d|sample_b|sample_c)\w*", re.I)
INSTR = re.compile(r"^\s*(?:[0-9]+:)?\s*([a-z][a-z0-9_]*)\b")


def read_asm(path):
    """Constant buffers, opcode multiset and sample count of a disassembly."""
    cbs, ops, samples, textures = {}, [], 0, 0
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            s = line.strip()
            if not s or s.startswith("//") or s.startswith(";"):
                continue
            # instruction lines start with an offset or a bare opcode
            body = re.sub(r"^\d+:\s*", "", s)
            m = CB_DECL.search(body)
            if m:
                cbs[int(m.group(1))] = int(m.group(2))
            if TEX_DECL.search(body):
                textures += 1
            if SAMPLE.search(body):
                samples += 1
            mi = INSTR.match(body)
            if mi:
                op = mi.group(1)
                if op.startswith("dcl_") or op in ("ret", "nop"):
                    # declarations carry no behaviour; `ret`/`nop` no arithmetic
                    if op.startswith("dcl_"):
                        continue
                ops.append(op)
    return {"cbs": cbs, "ops": ops, "samples": samples, "textures": textures}


def overlap(a, b):
    """Multiset overlap: how much of the smaller program the larger accounts for."""
    from collections import Counter
    ca, cb = Counter(a), Counter(b)
    if not ca or not cb:
        return 0.0 if ca != cb else 1.0
    common = sum(min(ca[k], cb[k]) for k in ca.keys() & cb.keys())
    return common / max(sum(ca.values()), sum(cb.values()))


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    a, b = sys.argv[1], sys.argv[2]
    verb = "--verbose" in sys.argv

    o = read_asm(a)
    r = read_asm(b)

    cb_same = sorted(o["cbs"].values()) == sorted(r["cbs"].values())
    ov = overlap(o["ops"], r["ops"])
    sample_same = o["samples"] == r["samples"]

    ok = cb_same and ov >= OVERLAP_MIN and sample_same

    if verb or not ok:
        print(f"  original : {len(o['ops']):5d} instr, cb={o['cbs']}, "
              f"samples={o['samples']}, textures={o['textures']}")
        print(f"  rebuilt  : {len(r['ops']):5d} instr, cb={r['cbs']}, "
              f"samples={r['samples']}, textures={r['textures']}")
        print(f"  cb sizes : {'same multiset' if cb_same else 'DIFFERENT'}, "
              f"opcode overlap {ov:.1%}, samples {'same' if sample_same else 'DIFFERENT'}")
        if not cb_same:
            print("    (slots are per-stage in DXBC; extents are compared as a multiset)")
            print(f"    original extents {sorted(o['cbs'].values())} -> rebuilt {sorted(r['cbs'].values())}")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
