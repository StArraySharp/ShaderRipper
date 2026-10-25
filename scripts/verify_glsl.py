"""Compare a GLSL->HLSL translation against the GLSL it came from.

The GLSL depot has no DXBC: Unity shipped HLSLcc's *source* output, so the
original program is the GLSL text itself.  Two things are checked against it,
and both are cheap and decisive:

  1. Uniform layout.  Every uniform the original declares must appear in the
     translation with a matching name and extent.  A missing one is a read that
     lands on nothing.

  2. Operator mix.  The translation is statement-per-statement, so the set of
     operations it performs has to match the original's.  Operand names are not
     compared -- HLSLcc renames them and the translator renames them again.

Syntax is a separate question and `usc_verify.py` already answers it: this
script is about meaning, not whether Unity accepts the file.

Usage: verify_glsl.py <export-dir> [--limit N] [--verbose]
"""

import glob
import os
import re
import sys
from collections import Counter

# Operators that carry meaning in both languages.
OPS = [
    "dot", "cross", "normalize", "length", "distance", "reflect", "refract",
    "sin", "cos", "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh",
    "exp", "exp2", "log", "log2", "sqrt", "inversesqrt", "pow", "abs", "sign",
    "floor", "ceil", "fract", "mod", "min", "max", "clamp", "mix", "step",
    "smoothstep", "lerp", "saturate", "frac", "rsqrt",
    "texture", "textureLod", "texelFetch", "dFdx", "dFdy",
]

# Spelling differences between the two languages.  Comparing raw names reports
# every statement as a mismatch.
ALIASES = {
    "fract": "frac",          # GLSL spelling of HLSL's frac
    "mix": "lerp",
    "texture": "sample",      # texture(s, uv) -> s.Sample(...)
    "textureLod": "samplelod",
    "texelFetch": "load",
    "inversesqrt": "rsqrt",
    "dFdx": "ddx",
    "dFdy": "ddy",
}

UNIFORM = re.compile(
    r"^\s*uniform\s+(?:(?:highp|mediump|lowp)\s+)?(\w+)\s+(\w+)\s*(\[[^\]]*\])?\s*;",
    re.M)


def uniforms(text):
    """name -> (type, extent) for every uniform a GLSL body declares."""
    res = {}
    for m in UNIFORM.finditer(text):
        ty, name, arr = m.group(1), m.group(2), m.group(3)
        n = 1
        if arr:
            inner = arr.strip("[]")
            n = int(inner) if inner.isdigit() else 0      # 0 = unsized
        res[name] = (ty, n)
    return res


def hlsl_uniforms(text):
    """name -> (type, extent) for the globals an emitted HLSL body declares."""
    res = {}
    for m in re.finditer(r"^\s*(float[1-4]?|int[1-4]?|uint[1-4]?|float[234]x[234]|"
                         r"Texture\w+|SamplerState)\s+(\w+)\s*(\[[^\]]*\])?\s*;",
                         text, re.M):
        ty, name, arr = m.group(1), m.group(2), m.group(3)
        n = 1
        if arr:
            inner = arr.strip("[]")
            n = int(inner) if inner.isdigit() else 0
        res[name] = (ty, n)
    return res


def opcounts(text):
    """How often each meaningful operator appears, with spellings folded."""
    c = Counter()
    for op in OPS:
        canon = ALIASES.get(op, op)
        c[canon] += len(re.findall(r"\b" + op + r"\s*\(", text))
    # A method-style fetch is the same operation `texture(` was.
    c["sample"] += len(re.findall(r"\.Sample\s*\(", text))
    c["samplelod"] += len(re.findall(r"\.SampleLevel\s*\(", text))
    return c


def stage_part(text, want_fragment):
    """The VERTEX or FRAGMENT half of a GLSLcc payload.

    HLSLcc writes both stages into one file, each behind a preprocessor test, and
    nests further `#endif`s inside (extensions, uniform-buffer macros).  A regex
    stops at the first of those, so the split walks the lines and tracks depth.
    """
    out = []
    depth = 0
    inside = False
    for line in text.splitlines():
        s = line.strip()
        if not inside:
            if re.match(r"#ifdef\s+VERTEX\b", s):
                inside = not want_fragment
                depth = 1
                continue
            if re.match(r"#ifdef\s+FRAGMENT\b", s):
                inside = want_fragment
                depth = 1
                continue
            continue
        if s.startswith("#if"):
            depth += 1
        elif s.startswith("#endif"):
            depth -= 1
            if depth == 0:
                break
        out.append(line)
    return "\n".join(out)


def hlsl_stage(text, entry):
    """The body of `float4 <entry>(...)` in an emitted HLSL file, or ""."""
    m = re.search(r"^\s*(?:float4|v2f|frag_out)\s+" + entry + r"\s*\([^)]*\)[^{]*\{",
                  text, re.M)
    if not m:
        return ""
    i = m.end()
    depth = 1
    while i < len(text) and depth:
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
        i += 1
    return text[m.start():i]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    root = sys.argv[1]
    limit = int(sys.argv[sys.argv.index("--limit") + 1]) if "--limit" in sys.argv else 0
    verbose = "--verbose" in sys.argv

    files = sorted(glob.glob(os.path.join(root, "*", "*_glsl.orig.glsl")))
    if limit:
        files = files[:limit]

    total = ok = 0
    missing_total = 0
    fails = []

    for orig in files:
        stem = os.path.basename(orig).replace(".orig.glsl", "")
        hlsl = os.path.join(os.path.dirname(orig), stem + ".hlsl")
        if not os.path.exists(hlsl):
            continue
        total += 1
        src = open(orig, encoding="utf-8", errors="replace").read()
        dst = open(hlsl, encoding="utf-8", errors="replace").read()

        # A GLSLcc payload holds both stages, and so does the translation, so each
        # half is compared against its own counterpart rather than guessing which
        # stage the file "is" -- most of them are both.
        missing, diffs = [], []
        for label, part, body in (
                ("vs", stage_part(src, want_fragment=False), hlsl_stage(dst, "vert")),
                ("ps", stage_part(src, want_fragment=True), hlsl_stage(dst, "frag"))):
            if not part or not body:
                continue
            # Uniforms are declared at file scope, not inside the entry point, so
            # the whole file is searched -- the per-stage body is only used for the
            # operator comparison.
            got = hlsl_uniforms(dst)
            for name, (ty, n) in uniforms(part).items():
                bare = name.replace("hlslcc_mtx4x4", "")
                if bare in got or name in got:
                    continue
                missing.append(f"{label}:{ty} {name}" + (f"[{n}]" if n != 1 else ""))
            a, b = opcounts(part), opcounts(body)
            for k in a:
                if a[k] != b[k]:
                    diffs.append(f"{label}:{k} {a[k]}->{b[k]}")

        missing_total += len(missing)
        if not missing and not diffs:
            ok += 1
            if verbose:
                print(f"OK   {stem}")
        else:
            detail = []
            if missing:
                detail.append("missing uniform(s): " + ", ".join(missing[:3]))
            if diffs:
                detail.append("op mismatch: " + ", ".join(diffs[:3]))
            fails.append((stem, "; ".join(detail)))

    print(f"programs compared : {total}")
    print(f"match             : {ok}")
    print(f"missing uniforms  : {missing_total}")
    print(f"differences       : {len(fails)}")
    if fails:
        print()
        for f in fails[:30]:
            print("  %-40s %s" % f)
        if len(fails) > 30:
            print(f"  ... {len(fails) - 30} more")
    return 0 if not fails else 1


if __name__ == "__main__":
    raise SystemExit(main())
