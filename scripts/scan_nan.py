"""Report where a NaN could reach the framebuffer.

DXBC has no bool type: a comparison writes the 0xFFFFFFFF bit pattern, which is a
NaN in IEEE 754.  The translator normalises most of those to `1.0f` (see
`AnalyseBoolLanes`), but a mask that is ALSO read as a value keeps the bit pattern
-- and if one of those reaches `o0`/`SV_Target` without passing a clamp, the pixel
turns black or white.

This walks the emitted HLSL per program and answers three questions:

  1. **Store** -- does a register holding -1 get multiplied/added into an output?
  2. **Escape** -- does a mask reach the output with no `saturate`/`min`/`max` on
     the way (the dangerous case)?
  3. **Benign** -- is the mask only ever a branch or a select condition (the case
     the normalisation pass already handles)?

Usage:
    py -3 scan_nan.py <export dir> [--max N] [-v]
"""

import collections
import os
import re
import sys

# `r0.x = (a < b) ? asfloat(0xFFFFFFFFu) : 0.0;`  ->  the mask constant
MASK = re.compile(r"\b(r\d+)\.([xyzw]+)\s*=\s*.*asfloat\(0xFFFFFFFFu\)")
# `r0.x = ... ? 1.0f : 0.0;` written by the normalisation pass
SAFE_ONE = re.compile(r"\b(r\d+)\.([xyzw]+)\s*=\s*.*\?\s*1\.0f\s*:\s*0\.0")
# any write to a register lane
WRITE = re.compile(r"(?m)^\s*(r\d+)\.([xyzw]+)\s*(?:=|\+=|-=|\*=|/=)[^=]")
# a read of a register lane
READ = re.compile(r"\b(r\d+)(?:\.([xyzw]+))?(?![\w.])")
# outputs
OUT = re.compile(r"\b(o\d+|result\.target\d+)\s*=")
# a clamp on the way
CLAMP = re.compile(r"\b(saturate|clamp|min|max)\s*\(")


def lanes_of(mask):
    return set(mask) if mask else set("xyzw")


def _is_condition(stmt, reg):
    """Is `reg` the CONDITION of the `?:` on this line, rather than one of its arms?

    `o0.xyz = r0.w ? r1.xyz : r0.xyz;` reads a mask as a boolean -- entirely safe.
    `o0.xyz = r0.w * r1.xyz;` reads it as a value -- that is the NaN risk.  The
    difference is only the position, so it has to be measured against the `?` that
    belongs to this statement.
    """
    q = stmt.find("?")
    if q < 0:
        return False
    at = stmt.find(reg)
    return 0 <= at < q


def analyse(text):
    """-> (escaping, contained) lists of (reg, lane, line)."""
    lines = text.split("\n")

    # Track the CURRENT meaning of each lane: True when its most recent write is
    # the -1 mask pattern, False when it is anything else.  Without the "anything
    # else" reset, a lane that held a mask once and holds a position later is
    # reported for the rest of the file -- which is what produced 1,692 false
    # positives on the first run.
    masks = {}
    for line in lines:
        for m in WRITE.finditer(line):
            for lane in lanes_of(m.group(2)):
                masks[(m.group(1), lane)] = False
        for m in MASK.finditer(line):
            for lane in lanes_of(m.group(2)):
                masks[(m.group(1), lane)] = True
    if not any(masks.values()):
        return [], []

    escaping, contained = [], []
    live = {}
    for line in lines:
        stmt = line.strip()
        if not stmt or stmt.startswith("//"):
            continue

        # Reads see the state BEFORE this line's writes.
        hits = [(m.group(1), lane) for m in READ.finditer(stmt)
                for lane in lanes_of(m.group(2))
                if live.get((m.group(1), lane))]
        if hits:
            condition_only = (
                re.match(r"^(if|while)\s*\(", stmt)
                or (re.search(r"\?[^:]*:", stmt) is not None
                    and _is_condition(stmt, hits[0][0]))
            )
            if condition_only:
                contained.append((hits[0][0], hits[0][1], stmt))
            elif OUT.search(stmt) or re.search(r"\bo\d+\.", stmt):
                clamped = CLAMP.search(stmt) is not None
                (contained if clamped else escaping).append((hits[0][0], hits[0][1], stmt))
            else:
                contained.append((hits[0][0], hits[0][1], stmt))

        # Now apply this line's writes.
        for m in WRITE.finditer(line):
            for lane in lanes_of(m.group(2)):
                live[(m.group(1), lane)] = False
        for m in MASK.finditer(line):
            for lane in lanes_of(m.group(2)):
                live[(m.group(1), lane)] = True

    return escaping, contained


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    verbose = "-v" in sys.argv
    limit = 40
    for i, a in enumerate(sys.argv):
        if a == "--max" and i + 1 < len(sys.argv):
            limit = int(sys.argv[i + 1])

    if not args:
        print(__doc__)
        return 2
    root = args[0]

    files = 0
    total_escape = 0
    total_contained = 0
    per_file = []
    for dp, _, fs in os.walk(root):
        for f in fs:
            if not f.endswith(".shader"):
                continue
            files += 1
            text = open(os.path.join(dp, f), encoding="utf-8-sig", errors="replace").read()
            esc, con = analyse(text)
            if esc or con:
                per_file.append((f, esc, con))
                total_escape += len(esc)
                total_contained += len(con)

    print("files=%d  nan-escaping=%d  nan-contained=%d"
          % (files, total_escape, total_contained))
    if not per_file:
        return 0

    per_file.sort(key=lambda x: -len(x[1]))
    shown = 0
    for name, esc, con in per_file:
        if not esc and not verbose:
            continue
        if shown >= limit:
            break
        shown += 1
        print()
        print("%s   escaping=%d contained=%d" % (name, len(esc), len(con)))
        for reg, lane, stmt in esc[:8]:
            print("   !! %s.%s reaches an output: %s" % (reg, lane, stmt[:110]))
        if verbose:
            for reg, lane, stmt in con[:4]:
                print("    ok %s.%s: %s" % (reg, lane, stmt[:110]))
    return 0 if total_escape == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
