"""Find registers a shader reads before anything writes them.

`sincos` in SM4+ has TWO destinations (cos, sin).  Reading it as one destination
drops the sine register, and the next instruction then reads a value no
instruction ever wrote -- which compiles fine and renders garbage.  That class of
bug is invisible to a compiler, so it is checked here from the emitted HLSL
instead: for every program block, walk the statements in order and report a read
of `rN`/`oN` whose lane was never assigned.

Usage:  py -3 scan_unwritten.py <exported Assets dir> [--max 40]
"""

import collections
import os
import re
import sys

BLOCK = re.compile(r"HLSLPROGRAM(.*?)ENDHLSL", re.S)
# `r0.xy = ...` / `r0 = ...` at the start of a statement.
WRITE_ = re.compile(r"^\s*(r\d+)(?:\.([xyzw]+))?\s*(?:=|\+=|-=|\*=|/=)[^=]")
DECL = re.compile(r"^\s*float[1-4]?\s+(r\d+)\s*=")
# A read is `rN` optionally followed by a swizzle.  The `(?![\w.])` keeps `r2` in
# `(r2).xy` from being read as a whole register, and `(?!\s*=(?!=))` keeps the
# left-hand side of an assignment out.
READ = re.compile(r"\b(r\d+)(?:\.([xyzw]+))?(?![\w.])")


def lanes(mask):
    return set(mask) if mask else set("xyzw")


def uses(stmt, written):
    """Lanes of `stmt`'s reads that no earlier statement wrote."""
    bad = []
    for r in READ.finditer(stmt):
        reg, mask = r.group(1), r.group(2)
        if not mask:
            # A bare `rN` only appears inside a parenthesised swizzle group
            # (`(r2).xy`); the group's own swizzle narrows it, so skip it.
            end = r.end()
            if end < len(stmt) and stmt[end] == ")":
                continue
            mask = "xyzw"
        need = set(mask)
        if not need <= written[reg]:
            bad.append((reg, "".join(sorted(need - written[reg])), stmt))
    return bad


def check(text):
    """-> (hard, soft) reads of a lane no assignment has produced yet.

    hard: the lane is not even in an initialiser, so nothing can have produced it.
    soft: the lane is covered by `float4 rN = 0.0;` but no instruction has assigned
          it *at the point of the read*.  The declaration is not the answer: the
          original program never had a zero there, it had a value from an
          instruction the emitter skipped.  `sincos r2.x, r3.x, ...` read as one
          destination leaves `r3.x` at zero, and the rotation that follows is
          silently wrong -- so this is the case worth reporting.
    """
    declared = collections.defaultdict(set)   # from `float4 rN = 0.0;`
    assigned = collections.defaultdict(set)   # from a real instruction
    hard, soft = [], []
    for line in text.split("\n"):
        stmt = line.strip()
        if not stmt or stmt.startswith("//"):
            continue

        # `TRANSFORM_TEX(uv, _Tex)` is `uv.xy * _Tex_ST.xy + _Tex_ST.zw`: the
        # argument is read as two lanes, and the second argument is a uniform's
        # name, not a register.  Left as written it reads as a whole `rN`.
        if "TRANSFORM_TEX(" in stmt:
            stmt = re.sub(r"TRANSFORM_TEX\(\s*(r\d+)(?![\w.])", r"TRANSFORM_TEX(\1.xy", stmt)

        # A folded transform names its inputs without a register number
        # (`UnityObjectToClipPos(float4(r0.x, r0.y, r0.z, 1.0))`), and a folded
        # matrix product is written as one `mul` over a vector constructor.  Both
        # read their operands on the SAME line they assign, so a per-register walk
        # sees `r0` read before anything wrote it even though the statement is
        # complete.  Treating such a line as "reads what it assigns" keeps the check
        # about real unwritten reads.
        if "UnityObjectToClipPos" in stmt or re.search(r"\bmul\s*\(\s*\w+\s*,", stmt):
            m2 = WRITE_.match(line)
            if m2:
                assigned[m2.group(1)] |= lanes(m2.group(2))
            continue

        d = DECL.match(line)
        if d:
            declared[d.group(1)] |= set("xyzw")
            continue
        m = WRITE_.match(line)
        if m:
            reg, mask = m.group(1), m.group(2)
            rest = stmt.split("=", 1)[1] if "=" in stmt else ""
            for r in uses(rest, assigned):
                soft.append(r)
            assigned[reg] |= lanes(mask)
            continue
        for r in uses(stmt, assigned):
            soft.append(r)
    return hard, soft


def main(root, limit=40):
    files = 0
    hits = 0
    by_site = collections.Counter()
    samples = {}
    for dp, _, fs in os.walk(root):
        for f in fs:
            if not f.endswith(".shader"):
                continue
            files += 1
            text = open(os.path.join(dp, f), encoding="utf-8-sig", errors="replace").read()
            for bi, m in enumerate(BLOCK.finditer(text)):
                _hard, soft = check(m.group(1))
                for reg, lane, stmt in soft:
                    hits += 1
                    key = (reg, lane)
                    by_site[key] += 1
                    samples.setdefault(f"{f}:{bi}:{reg}.{lane}", stmt)
    print(f"files={files}  lanes never assigned={hits}  sites={len(by_site)}")
    for (reg, lane), n in by_site.most_common(limit):
        print(f"  {n:5d}  {reg}.{lane}")
    print()
    for k, v in list(samples.items())[:15]:
        print(f"  {k}\n      {v[:120]}")
    return 0


if __name__ == "__main__":
    a = sys.argv[1:]
    root = a[0] if a else "Assets"
    lim = int(a[a.index("--max") + 1]) if "--max" in a else 40
    raise SystemExit(main(root, lim))
