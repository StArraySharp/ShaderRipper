"""Compare a translated GLSL-route program with the GLSL it came from.

`verify_logic.py` covers the DXBC route: it compiles back to DXBC and compares
invariants.  That route has a reference disassembly; the GLSL route does not --
the payload is GLSL text that Unity's HLSLcc emitted, and there is no second
compiler to ask.

So the reference is the GLSL itself.  Translation is a re-spelling, not a
re-derivation, so the comparison is over the ENTRY-POINT BODY of each stage
(declarations, preamble and `#version` lines are hoisted or dropped by design and
would only add noise):

  1. texture fetches        -- `texture(s, uv)` in, `.Sample(...)` out
  2. statement count        -- a dropped statement is dropped logic
  3. assignment targets     -- which variables the body writes
  4. intrinsics used        -- mapped through the translator's own rename table
  5. operator vocabulary    -- the arithmetic the body performs
  6. reads                  -- every uniform / varying the body names, after the
                               HLSLcc prefix and the stage renames are mapped

The renames matter: HLSLcc writes `hlslcc_mtx4x4unity_ObjectToWorld` where the
translation says `unity_ObjectToWorld`, `SV_Target0` where it says `o0`, and
`mix` where it says `lerp`.  A comparison that did not map those would report a
correct translation as completely wrong.

Usage:
    py -3 verify_glsl.py <export dir> [-j N] [-v]

`<export dir>` is what `ShaderRipper --game ... --programs --orig-asm` wrote: each
program folder holds `<blob>_glsl.hlsl` next to `<blob>_glsl.orig.glsl`.
"""

import collections
import os
import re
import sys
import threading

# --- the GLSL side ---------------------------------------------------------

GLSL_FETCH = re.compile(r"\b(texture|textureLod|textureProj|textureGrad|textureOffset|"
                        r"texelFetch|texelFetchOffset|textureSize)\s*\(")
GLSL_CALL = re.compile(r"\b([A-Za-z_]\w*)\s*\(")
GLSL_KEYWORDS = {
    "if", "else", "for", "while", "do", "return", "break", "continue", "discard",
    "switch", "case", "default", "in", "out", "inout", "const", "uniform",
    "attribute", "varying", "layout", "struct", "void", "true", "false",
    "highp", "mediump", "lowp", "precision", "flat", "smooth", "noperspective",
    "centroid", "invariant", "uint", "int", "float", "bool", "double",
    "vec2", "vec3", "vec4", "ivec2", "ivec3", "ivec4", "uvec2", "uvec3", "uvec4",
    "bvec2", "bvec3", "bvec4", "mat2", "mat3", "mat4", "sampler2D", "sampler3D",
    "samplerCube", "sampler2DArray", "sampler2DShadow", "main",
}
GLSL_OPS = ("&&", "||", "^^", "+=", "-=", "*=", "/=", "<<", ">>", "<=", ">=",
            "==", "!=", "++", "--", "+", "-", "*", "/", "%", "<", ">", "&",
            "|", "^", "~", "!")

# --- the emitted HLSL side -------------------------------------------------

HLSL_FETCH = re.compile(r"\.(Sample|SampleLevel|SampleBias|SampleGrad|SampleCmp|"
                        r"SampleCmpLevelZero|Load|Gather|GetDimensions)\s*\(")
HLSL_CALL = re.compile(r"\b([A-Za-z_]\w*)\s*\(")
HLSL_KEYWORDS = {
    "if", "else", "for", "while", "do", "return", "break", "continue", "discard",
    "switch", "case", "default", "cbuffer", "struct", "register", "packoffset",
    "static", "const", "uniform", "extern", "in", "out", "inout", "true", "false",
    "void", "float", "float2", "float3", "float4", "float2x2", "float3x3",
    "float4x4", "int", "int2", "int3", "int4", "uint", "uint2", "uint3", "uint4",
    "bool", "bool2", "bool3", "bool4", "half", "fixed", "double",
    "Texture1D", "Texture2D", "Texture3D", "TextureCube", "Texture2DArray",
    "Texture2DMS", "Texture2DMSArray", "SamplerState", "SamplerComparisonState",
    "ByteAddressBuffer", "Buffer", "main", "vert", "frag",
}
HLSL_OPS = ("&&", "||", "+=", "-=", "*=", "/=", "<<", ">>", "<=", ">=",
            "==", "!=", "++", "--", "+", "-", "*", "/", "%", "<", ">", "&",
            "|", "^", "~", "!")

# Intrinsics and identifiers the translator re-spells.  Comparing verbatim
# reports every one of these as "lost" or "invented"; mapping them is what makes
# the comparison about LOGIC rather than about spelling.
RENAMED = {
    # GLSL intrinsic -> HLSL intrinsic
    "mix": "lerp", "fract": "frac", "mod": "fmod", "dFdx": "ddx", "dFdy": "ddy",
    "roundEven": "round", "inversesqrt": "rsqrt",
    "intBitsToFloat": "asfloat", "floatBitsToInt": "asint",
    "floatBitsToUint": "asuint", "uintBitsToFloat": "asfloat",
    "texture": "Sample", "textureLod": "SampleLevel",
    "texelFetch": "Load", "textureSize": "GetDimensions",
    # stage built-ins -> the emitted output register
    "gl_Position": "o0", "gl_FragCoord": "sv_position",
    "SV_Target0": "o0", "SV_Target1": "o1", "SV_Target2": "o2", "SV_Target3": "o3",
}

# HLSLcc prefixes its flattened globals; the translator strips them.
PREFIXES = ("hlslcc_mtx4x4", "hlslcc_mtx3x3", "hlslcc_mtx2x2")

COMPARE_OPS = ("<", ">", "<=", ">=", "==", "!=", "&&", "||")


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    return re.sub(r"//[^\n]*", " ", text)


def normalise_name(name):
    """One canonical spelling per identifier, so renames compare equal.

    `vs_COLOR0`, `in_POSITION0` and `color0` are the same variable; HLSLcc's
    `hlslcc_mtx4x4unity_ObjectToWorld` is the translator's
    `unity_ObjectToWorld`; `mix` is `lerp`.  Without this mapping a correct
    translation reports every identifier as both lost and invented.
    """
    for p in PREFIXES:
        if name.startswith(p):
            return name[len(p):].lower()
    if name in RENAMED:
        return RENAMED[name].lower()
    if name.startswith("vs_"):
        return name[3:].lower()
    return name.lower()


def statements(text):
    return [s.strip() for s in re.split(r"[;{}]", text) if s.strip()]


def identifiers(text, keywords):
    out = []
    for m in re.finditer(r"\b([A-Za-z_]\w*)\b", text):
        w = m.group(1)
        if w in keywords or w.isdigit():
            continue
        out.append(normalise_name(w))
    return out


def ops(text, table):
    """Operator histogram; longest match first so `<=` does not count as `<`."""
    counts = collections.Counter()
    i = 0
    order = sorted(table, key=len, reverse=True)
    while i < len(text):
        for op in order:
            if text.startswith(op, i):
                counts[op] += 1
                i += len(op)
                break
        else:
            i += 1
    return counts


ASSIGN = re.compile(r"(?m)(?:^|[;{}])\s*([A-Za-z_]\w*)\s*(?:\.[xyzwrgba]{1,4})?\s*"
                    r"(?:[-+*/]?=)(?!=)")


def assignments(text):
    """`lhs = ...` targets, including the `lhs.swizzle` form.

    `(?m)` is required: without it `^` only matches at the start of the whole
    string, so only the body's FIRST line contributed an assignment -- which made
    a correct translation look like it had dropped almost every one.
    """
    out = collections.Counter()
    for m in ASSIGN.finditer(text):
        out[normalise_name(m.group(1))] += 1
    return out


# --- matrix-chain folds ---------------------------------------------------
#
# The translation collapses the four-statement expansion of a matrix-vector
# product into one `mul(M, v)`, and a chained pair into `UnityObjectToClipPos`.
# Those statements are exactly the ones whose right-hand side reads a matrix ROW,
# so counting them on the payload side and then demanding the same count of the
# output reports the fold itself as a lost write.  They are excluded from the
# per-target count instead; everything that is not matrix arithmetic is still
# compared one for one, which is the part a fold cannot affect.

MATRIX_ROW = re.compile(
    r"\b(?:hlslcc_mtx\w*|\w*(?:unity_|glstate_)\w*)\s*\[\s*\d+\s*\]")
STATEMENT = re.compile(r"[^;{}\n]+;")


SPLIT_ASSIGN = re.compile(r"^(.*?[^=<>!])=([^=].*)$", re.S)


def row_assignments(text):
    """Per-target count of assignments the matrix fold is allowed to replace.

    That is every statement whose right-hand side reads a matrix row.  A product of two
    matrices adds one more kind: its row terms read TWO matrix rows each (a row of one
    matrix against a row of the other), and every group of four is closed by the
    statement that scales and accumulates it -- which reads no row itself.
    """
    sts = []
    for st in STATEMENT.finditer(text):
        body = st.group(0)
        m = SPLIT_ASSIGN.match(body)
        tgt = re.match(r"\s*([A-Za-z_]\w*)", m.group(1)) if m else None
        sts.append((normalise_name(tgt.group(1)) if tgt else None,
                    len(MATRIX_ROW.findall(body))))

    out = collections.Counter()
    for target, rows in sts:
        if target is not None and rows:
            out[target] += 1
    for k in range(len(sts) - 4):
        if all(sts[k + j][1] >= 2 for j in range(4)):
            target, rows = sts[k + 4]
            if target is not None and rows == 0:
                out[target] += 1
    return out


def summarise(text, keywords, fetch_re, call_re, op_table):
    body = strip_comments(text)
    calls = collections.Counter()
    for m in call_re.finditer(body):
        name = m.group(1)
        if name in keywords:
            continue
        calls[normalise_name(name)] += 1
    return {
        "statements": len(statements(body)),
        "fetches": len(fetch_re.findall(body)),
        "calls": calls,
        "assigns": assignments(body),
        "rows": row_assignments(body),
        "idents": collections.Counter(identifiers(body, keywords)),
        "ops": ops(body, op_table),
    }


# --- extracting the comparable body ---------------------------------------

GLSL_MAIN = re.compile(r"\bvoid\s+main\s*\(\s*(?:void)?\s*\)\s*\{", re.S)


def glsl_stages(text):
    """Split a payload into its VERTEX / FRAGMENT translation units.

    HLSLcc emits both stages into one file, each behind `#ifdef VERTEX` /
    `#ifdef FRAGMENT`.  Comparing the merged text against one emitted stage
    reports every statement of the other stage as lost.
    """
    out = {}
    cur = None
    buf = []
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("#ifdef VERTEX"):
            if cur:
                out[cur] = "\n".join(buf)
            cur, buf = "VERTEX", []
            continue
        if s.startswith("#ifdef FRAGMENT"):
            if cur:
                out[cur] = "\n".join(buf)
            cur, buf = "FRAGMENT", []
            continue
        if cur and not s.startswith("#"):
            buf.append(line)
    if cur:
        out[cur] = "\n".join(buf)
    return out


def _body_after(text, at):
    """Text between the brace at `at` and its match."""
    depth = 1
    i = at
    while i < len(text) and depth > 0:
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
        i += 1
    return text[at:max(at, i - 1)]


def glsl_body(unit):
    """The text of `main()`: declarations are hoisted by design, so only the body
    is comparable."""
    m = GLSL_MAIN.search(unit)
    return _body_after(unit, m.end()) if m else unit


HLSL_FUNC = re.compile(r"(?:^|\n)[^\n{;]*?\b(vert|frag|main)\s*\([^)]*\)\s*"
                       r"(?::\s*[\w<>]+)?\s*\{", re.S)


def hlsl_function(text, want):
    """The body of the entry point named `want` (`vert` / `frag`).

    Two things make this regex-shaped rather than a split: the emitted file puts
    both entry points AFTER the stage markers (structs first, then `vert`, then
    `frag`), and the return type can be any struct the signature table produced
    (`vs_out`), not just `float4`.  Matching the NAME is what pairs a body with
    its payload.
    """
    alt = ("vert", "main") if want == "VERTEX" else ("frag", "main")
    for m in HLSL_FUNC.finditer(text):
        if m.group(1) not in alt:
            continue
        return _body_after(text, m.end() - 1)
    return None


def summary_glsl(unit):
    return summarise(glsl_body(unit), GLSL_KEYWORDS, GLSL_FETCH, GLSL_CALL, GLSL_OPS)


def summary_hlsl(body):
    return summarise(body, HLSL_KEYWORDS, HLSL_FETCH, HLSL_CALL, HLSL_OPS)


def spread(a, b):
    """Difference as a fraction of the larger count (0 = identical)."""
    hi = max(a, b)
    return 0.0 if hi == 0 else abs(a - b) / hi


def compare(g, h):
    """-> (hard, soft) notes."""
    bad, soft = [], []

    # A fetch the GLSL made and the translation does not is a texture read that
    # disappeared -- the shader still compiles and renders the wrong colour.
    if h["fetches"] < g["fetches"]:
        bad.append("纹理取值少了: GLSL %d / 生成 %d" % (g["fetches"], h["fetches"]))
    elif h["fetches"] > g["fetches"]:
        soft.append("纹理取值多了: GLSL %d / 生成 %d" % (g["fetches"], h["fetches"]))

    # Statement count is reported, never failed.  The emitted body declares its
    # locals, seeds the inputs, hoists the output register and adds the return --
    # four to eight statements the GLSL has at file scope or does not need.  A
    # translation that LOST logic shows up as a missing assignment target or a
    # missing fetch below, which are the checks that can actually distinguish
    # "re-spelled" from "dropped".
    if g["statements"] != h["statements"]:
        soft.append("语句数 %d -> %d (声明/返回由翻译补出)" % (g["statements"], h["statements"]))

    # A target may vanish entirely when EVERY write it had was a matrix-row term:
    # a register that only existed to hold an intermediate product goes away with
    # the fold (`u_xlat1` in an `UnityObjectToClipPos` chain).  A target that had
    # at least one ordinary write still has to be there.
    lost = sorted(k for k in g["assigns"]
                  if k not in h["assigns"] and g["assigns"][k] > g["rows"].get(k, 0))
    if lost:
        bad.append("赋值目标消失: %s" % lost[:6])

    # Existing is not the same as surviving: deleting ONE of four writes to the
    # same register leaves the target present and the program wrong.  The
    # translation is statement-for-statement, so a target that lost ANY write is
    # reported -- except for the matrix-row statements, which the fold is supposed
    # to replace (see row_assignments).
    thin = []
    for k, n in g["assigns"].items():
        got = h["assigns"].get(k, 0)
        folded = g["rows"].get(k, 0)
        if n and got < n - folded:
            thin.append("%s %d->%d" % (k, n - folded, got))
    if thin:
        bad.append("赋值次数减少: %s" % thin[:6])

    # An intrinsic the GLSL used has to exist in the output under some spelling.
    missing = sorted(k for k in g["calls"] if k not in h["calls"] and len(k) > 1)
    if missing:
        soft.append("调用不见(检查是否重命名): %s" % missing[:6])

    # A lost comparison is a lost branch condition.
    gone_ops = sorted(k for k in COMPARE_OPS
                      if g["ops"].get(k, 0) and not h["ops"].get(k, 0))
    if gone_ops:
        bad.append("比较/逻辑运算符消失: %s" % gone_ops)

    if spread(sum(g["ops"].values()), sum(h["ops"].values())) > 0.35:
        soft.append("运算符总数 %d -> %d"
                    % (sum(g["ops"].values()), sum(h["ops"].values())))

    return bad, soft


# --- driver ----------------------------------------------------------------

def pairs(root):
    for dirpath, _dirs, files in os.walk(root):
        for f in files:
            if not f.endswith("_glsl.hlsl"):
                continue
            stem = f[: -len(".hlsl")]
            orig = os.path.join(dirpath, stem + ".orig.glsl")
            if os.path.exists(orig):
                yield os.path.join(dirpath, f), orig


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    verbose = "-v" in sys.argv
    jobs = 1
    limit = 0
    for i, a in enumerate(sys.argv):
        if a in ("-j", "--jobs") and i + 1 < len(sys.argv):
            jobs = int(sys.argv[i + 1])
        if a == "--max" and i + 1 < len(sys.argv):
            limit = int(sys.argv[i + 1])

    if not args:
        print(__doc__)
        return 2
    root = args[0]
    work = list(pairs(root))
    if limit:
        work = work[:limit]
    if not work:
        print("no *_glsl.hlsl + *_glsl.orig.glsl pairs under %s" % root)
        return 2

    print("%d program pair(s), %d thread(s)" % (len(work), jobs))
    results = []
    q = collections.deque(work)
    lock = threading.Lock()

    def worker():
        while True:
            with lock:
                if not q:
                    return
                hlsl_path, glsl_path = q.popleft()
            hlsl = open(hlsl_path, encoding="utf-8", errors="replace").read()
            glsl = open(glsl_path, encoding="utf-8", errors="replace").read()

            units = glsl_stages(glsl)
            bad, soft = [], []
            for name, unit in units.items():
                if not unit.strip():
                    continue
                g = summary_glsl(unit)
                if not g["statements"]:
                    continue
                body = hlsl_function(hlsl, name)
                if body is None:
                    bad.append("%s: 生成文件中没有对应入口" % name.lower())
                    continue
                h = summary_hlsl(body)
                b, s = compare(g, h)
                bad += ["%s: %s" % (name.lower(), note) for note in b]
                soft += ["%s: %s" % (name.lower(), note) for note in s]
            results.append((hlsl_path, bad, soft))

    threads = [threading.Thread(target=worker, daemon=True) for _ in range(max(1, jobs))]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    ok = soft_only = 0
    failed = []
    for path, bad, soft in results:
        rel = os.path.relpath(path, root)
        if not bad:
            ok += 1
            if soft:
                soft_only += 1
            if verbose:
                print("PASS %s%s" % (rel, ("  (soft: " + "; ".join(soft) + ")") if soft else ""))
        else:
            failed.append((rel, bad))

    print()
    print("programs=%d passed=%d failed=%d (soft %d) rate=%.1f%%"
          % (len(results), ok, len(failed), soft_only,
             100.0 * ok / max(len(results), 1)))
    for rel, bad in failed[:40]:
        print("FAIL %s" % rel)
        for note in bad:
            print("     " + note)
    if len(failed) > 40:
        print("... %d more" % (len(failed) - 40))
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
