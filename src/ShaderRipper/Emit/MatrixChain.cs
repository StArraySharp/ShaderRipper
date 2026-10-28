// ShaderRipper -- collapse the four-statement expansion of a matrix-vector product.
//
// GLSLcc never emits `mul(M, v)`: it unrolls the product into one assignment per
// row, and a vertex shader then does it a second time with `unity_MatrixVP` because
// `UnityObjectToClipPos` was inlined the same way before the GLSL was generated.
//
// This pass is a peephole over the TRANSLATED STATEMENT LIST: it reads statements
// the way the DXBC route reads instructions (lhs, scalar operand, matrix row),
// rebuilds the product the assignments stand for, and re-spells it as one `mul`.  A
// product that feeds a second one becomes `UnityObjectToClipPos`, which is the macro
// the original shader was written with.
//
// Reading the statements through <see cref="Parse"/> rather than a regex per shape
// is what lets the same recogniser cover the column-projections a payload writes
// (`M[k].xyz` on a three-lane destination) and the renamed accumulators HLSLcc
// invents -- both are the same product, spelled around a mask.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShaderRipper.Emit;

internal static class MatrixChain
{
    /// <summary>`u_xlat0.x = dot(in_NORMAL0.xyz, unity_WorldToObject[0].xyz);`</summary>
    private static readonly Regex DotRow = new(
        @"^(?<lhs>[A-Za-z_]\w*)\.(?<lane>[xyzw]) = dot\((?<v>[A-Za-z_]\w*(?:\.[xyzw]+)?), " +
        @"(?<m>[A-Za-z_]\w*)\[(?<r>\d)\]\.xyz\);$", RegexOptions.Compiled);

    /// <summary>
    /// One statement's left-hand side and its additive terms:
    /// <c>dst.mask = a * b + c</c> becomes <c>("dst", ".mask", ["a * b", "c"])</c>.
    /// </summary>
    private sealed class Stmt
    {
        public string Lhs = "";
        /// <summary>The destination's swizzle, with its leading dot; empty when none.</summary>
        public string LhsMask = "";
        public readonly List<string> Terms = new();
        public string Text = "";
    }

    /// <summary>A matrix row read as one factor: <c>M[2].xyz</c>.</summary>
    private sealed class RowRef
    {
        public string Matrix = "";
        public int Row;
        /// <summary>The row's swizzle; this is the mask of the product it builds.</summary>
        public string Mask = "";
    }

    /// <summary>`float4 unity_ObjectToWorld[4];` -- a matrix this file declares itself.</summary>
    private static readonly Regex ArrayDecl = new(
        @"^\s*(?:static\s+)?float4\s+(\w+)\s*\[", RegexOptions.Compiled);

    /// <summary>
    /// May this name be handed to <c>mul</c> as a matrix?
    ///
    /// A name Unity's includes provide is a matrix only when the name table says so;
    /// <c>unity_LightColor</c> is a real <c>float4[8]</c> and `mul` has no overload
    /// for it.  A name nothing declares is the payload's own temporary, and those are
    /// locals the body built as a matrix.
    /// </summary>
    private static bool IsMatrixName(string name) =>
        UnityIncludes.IsMatrixMember(name) || !UnityIncludes.IsEngineMember(name);

    /// <summary>One rebuilt product: the statements it replaces.</summary>
    private sealed class Product
    {
        public int Start;
        public int Count;
        /// <summary>The register the row terms accumulate into.</summary>
        public string Dst;
        /// <summary>The register the closing row writes; often, not always, `Dst`.</summary>
        public string Final;
        public string Matrix;
        /// <summary>The product's mask (the row swizzle), with its dot; maybe empty.</summary>
        public string RowMask;
        /// <summary>The destination's swizzle, with its dot; maybe empty.</summary>
        public string DstMask;
        /// <summary>The scalar weight of each row, as HLSL text.</summary>
        public readonly string[] Comp = new string[4];
        /// <summary>The register each weight is a lane of, when it is one.</summary>
        public readonly string[] Base = new string[4];
        public readonly char[] Lane = new char[4];
        /// <summary>Statements that have to be written where the product starts.</summary>
        public readonly List<string> Extras = new();
        /// <summary>
        /// Statements of a second product over the same sum that are folded with it
        /// -- the three-lane completion a sprite shader asks for in addition to the
        /// whole four-row one.  These survive a merge into a macro: the macro
        /// recomputes the product, this one is a different product.
        /// </summary>
        public readonly List<string> Keeps = new();
        /// <summary>Statements absorbed besides the chain itself.</summary>
        public readonly List<int> Skip = new();
        /// <summary>The product itself; null when it was absorbed by a macro.</summary>
        public string Text;
        /// <summary>The vector argument of the `mul`, when all four weights are one vector.</summary>
        public string Vector;
        /// <summary>The closing weight is the homogeneous 1.0 (so `Vector` is a float3).</summary>
        public bool Homogeneous;
        /// <summary>Rows 0..2 only: nothing closes the product, so row 3 weighs zero.</summary>
        public bool PartialOnly;
    }

    public static List<string> Fold(List<string> lines, bool macros = true)
    {
        // A matrix the includes provide is a `float4x4` and `mul` takes it.  A
        // name the payload declared itself may still be a row array, and then `mul`
        // has no overload for it (X3013), so those products keep the expansion.
        var arrays = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            var decl = ArrayDecl.Match(line);
            if (decl.Success) arrays.Add(decl.Groups[1].Value);
        }

        // A normal transform is written as three dots, one per row, and has nothing
        // to do with the four-row products below -- fold it first, on the same list.
        lines = FoldNormalTransforms(lines, arrays, macros);
        // A single output column of a product is written as four scalar terms.
        lines = FoldDotColumn(lines, arrays);
        // A product of two matrices: sixteen rows, combined four at a time.
        lines = FoldMatrixProducts(lines, arrays);

        var products = new List<Product>();
        for (int i = 0; i < lines.Count; i++)
        {
            var p = TryProduct(lines, i, arrays);
            if (p == null) continue;
            products.Add(p);
            i += p.Count - 1;
        }
        if (products.Count == 0) return FoldRowSums(lines, arrays);

        MergeClipPos(lines, products, arrays);

        var outLines = new List<string>(lines.Count);
        var at = new Dictionary<int, Product>();
        foreach (var p in products) at[p.Start] = p;
        var consumed = new HashSet<int>();
        foreach (var p in products)
        {
            for (int k = 1; k < p.Count; k++) consumed.Add(p.Start + k);
            foreach (int k in p.Skip) consumed.Add(k);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (at.TryGetValue(i, out var p))
            {
                string indent = Indent(lines[i]);
                foreach (string extra in p.Extras) outLines.Add(indent + extra);
                if (p.Text != null) outLines.Add(indent + p.Text);
                foreach (string keep in p.Keeps) outLines.Add(indent + keep);
                continue;
            }
            if (consumed.Contains(i)) continue;
            outLines.Add(lines[i]);
        }
        // Whatever is left spelled as matrix rows -- a product written on one line,
        // or the single row that completes one produced elsewhere -- still folds.
        return FoldRowSums(outLines, arrays);
    }

    private static string Indent(string line)
    {
        int n = 0;
        while (n < line.Length && line[n] == ' ') n++;
        return line[..n];
    }

    /// <summary>Split one statement into its destination and additive terms, or null.</summary>
    private static Stmt Parse(string line)
    {
        string text = line.Trim();
        if (text.Length == 0 || !text.EndsWith(';')) return null;
        text = text[..^1];

        int eq = text.IndexOf('=');
        if (eq <= 0) return null;
        // `+=` / `==` are not the shape this pass rebuilds.
        if (text[eq + 1] == '=' || "+-*/".Contains(text[eq - 1])) return null;

        var s = new Stmt { Text = text };
        (s.Lhs, s.LhsMask) = SplitMask(text[..eq].Trim());
        if (s.Lhs.Length == 0) return null;

        foreach (string term in SplitTerms(text[(eq + 1)..].Trim()))
        {
            if (term.Length == 0) return null;
            s.Terms.Add(term);
        }
        // Up to a whole product on one line (`a*x + b*y + c*z + d`); the four-row
        // chains this pass rebuilds use one or two, and each recogniser checks that.
        return s.Terms.Count is >= 1 and <= 5 ? s : null;
    }

    /// <summary>`u_xlat0.xyz` -&gt; ("u_xlat0", ".xyz"); `u_xlat0` -&gt; ("u_xlat0", "").</summary>
    private static (string name, string mask) SplitMask(string text)
    {
        int dot = text.IndexOf('.');
        if (dot <= 0) return (text, "");
        string tail = text[(dot + 1)..];
        if (tail.Length is < 1 or > 4 || tail.Any(c => !"xyzw".Contains(c))) return (text, "");
        return (text[..dot], text[dot..]);
    }

    /// <summary>
    /// Split an additive chain on `+` (<paramref name="by"/> = '+') or one term on
    /// `*` (<paramref name="by"/> = '*'), keeping the pieces in the order written.
    /// Parentheses do not appear in what HLSLcc emits for a product, so a flat split
    /// is exact here.
    /// </summary>
    private static List<string> SplitOn(string text, char by)
    {
        var parts = new List<string>();
        int start = 0, depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && c == by)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(text[start..].Trim());
        return parts;
    }

    private static List<string> SplitTerms(string text) => SplitOn(text, '+');

    private static List<string> SplitFactors(string text) => SplitOn(text, '*');

    /// <summary>`unity_ObjectToWorld[2].xyz` -&gt; a row reference, or null.</summary>
    private static RowRef RowOf(string text)
    {
        int br = text.IndexOf('[');
        if (br <= 0) return null;
        int close = text.IndexOf(']', br);
        if (close < 0) return null;
        string index = text[(br + 1)..close];
        if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int row))
            return null;
        if (row is < 0 or > 3) return null;

        string name = text[..br];
        if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_')) return null;
        if (name.Any(c => !char.IsLetterOrDigit(c) && c != '_')) return null;

        var r = new RowRef { Matrix = name, Row = row };
        string rest = text[(close + 1)..];
        if (rest.Length > 0)
        {
            if (rest[0] != '.') return null;
            string tail = rest[1..];
            if (tail.Length is < 1 or > 4 || tail.Any(c => !"xyzw".Contains(c))) return null;
            r.Mask = rest;
        }
        return r;
    }

    /// <summary>
    /// Three `dot(v, M[row].xyz)` statements into one 3x3 multiply.
    ///
    /// A normal is transformed by the INVERSE transpose, which the payload spells as
    /// three dots against the columns of <c>unity_WorldToObject</c>:
    ///
    ///     u_xlat0.x = dot(in_NORMAL0.xyz, unity_WorldToObject[0].xyz);
    ///     u_xlat0.y = dot(in_NORMAL0.xyz, unity_WorldToObject[1].xyz);
    ///     u_xlat0.z = dot(in_NORMAL0.xyz, unity_WorldToObject[2].xyz);
    ///
    /// That is <c>mul(n, (float3x3)unity_WorldToObject)</c> -- the transpose form,
    /// which is exactly what <c>UnityObjectToWorldNormal</c> is.  Rows may arrive in
    /// any order and land in any lanes: the lane letter the statement writes says
    /// where the result goes.
    /// </summary>
    private static List<string> FoldNormalTransforms(List<string> lines, HashSet<string> arrays,
                                                    bool macros)
    {
        List<string> outp = null;
        for (int i = 0; i < lines.Count; i++)
        {
            if (i + 2 >= lines.Count) { outp?.Add(lines[i]); continue; }

            var a = DotRow.Match(Strip(lines[i]));
            var b = DotRow.Match(Strip(lines[i + 1]));
            var c = DotRow.Match(Strip(lines[i + 2]));
            if (!a.Success || !b.Success || !c.Success) { outp?.Add(lines[i]); continue; }

            string dst = a.Groups["lhs"].Value;
            string vec = a.Groups["v"].Value;
            string matrix = a.Groups["m"].Value;
            if (b.Groups["lhs"].Value != dst || c.Groups["lhs"].Value != dst) { outp?.Add(lines[i]); continue; }
            if (b.Groups["v"].Value != vec || c.Groups["v"].Value != vec) { outp?.Add(lines[i]); continue; }
            if (b.Groups["m"].Value != matrix || c.Groups["m"].Value != matrix) { outp?.Add(lines[i]); continue; }
            if (arrays.Contains(matrix) || !IsMatrixName(matrix)) { outp?.Add(lines[i]); continue; }

            var rows = new bool[3];
            var lanes = new char[3];
            bool ok = true;
            foreach (var m in new[] { a, b, c })
            {
                int r = int.Parse(m.Groups["r"].Value, NumberStyles.Integer,
                                  CultureInfo.InvariantCulture);
                if (r > 2 || rows[r]) { ok = false; break; }
                rows[r] = true;
                lanes[r] = m.Groups["lane"].Value[0];
            }
            // The three dots read the whole vector before any lane is written; a
            // destination that the vector is a projection of would read its own output.
            if (!ok || vec == dst || vec.StartsWith(dst + ".", StringComparison.Ordinal))
            {
                outp?.Add(lines[i]);
                continue;
            }

            outp ??= Copy(lines, i);
            outp.Add(macros && matrix == "unity_WorldToObject"
                ? $"{dst}.{new string(lanes)} = UnityObjectToWorldNormal({vec});"
                : $"{dst}.{new string(lanes)} = mul({vec}, (float3x3){matrix});");
            i += 2;
        }
        return outp ?? lines;
    }

    /// <summary>
    /// Four scalar terms that read ONE column of a matrix into one output lane.
    ///
    /// A product whose result is used as a scalar -- the homogeneous `w` that scales
    /// the projection, or the view-space `z` a depth shader wants -- is written as
    ///
    ///     u_xlat15 = u_xlat2.y * unity_MatrixVP[1].w;
    ///     u_xlat15 = unity_MatrixVP[0].w * u_xlat2.x + u_xlat15;
    ///     u_xlat15 = unity_MatrixVP[2].w * u_xlat2.z + u_xlat15;
    ///     u_xlat15 = unity_MatrixVP[3].w * u_xlat2.w + u_xlat15;
    ///
    /// because the other three columns are never needed.  That is `mul(M, v).w`, and
    /// spelling it that way keeps the raw row index out of the body: `M[i].c` only
    /// means what it should while the declaration it indexes is the one the engine
    /// binds, and a product is the spelling that does not have to care.
    /// </summary>
    private static List<string> FoldDotColumn(List<string> lines, HashSet<string> arrays)
    {
        List<string> outp = null;
        for (int i = 0; i < lines.Count; i++)
        {
            if (i + 3 >= lines.Count) { outp?.Add(lines[i]); continue; }

            var a = LaneOfColumn(Strip(lines[i]));
            var b = LaneOfColumn(Strip(lines[i + 1]));
            var c = LaneOfColumn(Strip(lines[i + 2]));
            var d = LaneOfColumn(Strip(lines[i + 3]));
            if (a == null || b == null || c == null || d == null) { outp?.Add(lines[i]); continue; }

            // One matrix, one column, one vector -- and the accumulator has to run
            // through all four terms in order.  The first term is whichever row fxc
            // scheduled first (row 1 in practice), so it is not required to be row 0;
            // what identifies the head is that it carries no accumulator yet, and that
            // it writes the whole register (the rest write one lane of the result).
            string matrix = a.Value.matrix, column = a.Value.column, vec = a.Value.vec;
            string dst = b.Value.lhs, dstMask = b.Value.lhsMask;
            bool ok = a.Value.acc == null && a.Value.lhsMask.Length == 0;
            ok &= IsWritten(b.Value.acc, a.Value.lhs, "");
            foreach (var t in new[] { c, d })
            {
                if (t.Value.lhs != dst || t.Value.lhsMask != dstMask ||
                    !IsWritten(t.Value.acc, dst, dstMask)) { ok = false; break; }
            }
            if (!ok) { outp?.Add(lines[i]); continue; }

            // One matrix, one column, one vector everywhere.
            ok = true;
            foreach (var t in new[] { b, c, d })
            {
                if (t.Value.matrix != matrix || t.Value.column != column || t.Value.vec != vec)
                { ok = false; break; }
            }
            if (!ok) { outp?.Add(lines[i]); continue; }

            // Row k must be paired with component k: `mul(M, v)` sums M[k] * v[k].
            var rows = new bool[4];
            foreach (var t in new[] { a, b, c, d })
            {
                int r = t.Value.index;
                if (r > 3 || rows[r] || "xyzw"[r] != t.Value.lane) { ok = false; break; }
                rows[r] = true;
            }
            if (!ok || arrays.Contains(matrix) || !IsMatrixName(matrix)) { outp?.Add(lines[i]); continue; }

            // The head's register held the running sum; if anything reads it after the
            // fold the sum is gone.  It is the same register as the result only when the
            // chain never moved it, which the check above allows.
            if (a.Value.lhs != dst && ReadBeforeWrite(lines, a.Value.lhs, i + 4)) { outp?.Add(lines[i]); continue; }

            outp ??= Copy(lines, i);
            outp.Add($"{dst}{dstMask} = mul({matrix}, {vec}).{column};");
            i += 3;
        }
        return outp ?? lines;
    }

    /// <summary>Is this term the given register, read through the given mask?</summary>
    private static bool IsWritten(string term, string name, string mask)
    {
        var (n, m) = SplitMask(term ?? "");
        return n == name && m == mask;
    }

    /// <summary>One term of a single-column product, or null.</summary>
    private static (string lhs, string lhsMask, string matrix, string column, string vec,
                    char lane, int index, string acc)? LaneOfColumn(string line)
    {
        var s = Parse(line);
        // The destination names one lane at most: the whole register for the term that
        // starts the sum, one component for the ones that add into it.
        if (s == null || s.Terms.Count > 2) return null;
        if (s.LhsMask.Length > 0 && s.LhsMask.Length != 2) return null;

        string acc = null;
        var term = s.Terms[0];
        if (s.Terms.Count == 2) acc = s.Terms[1];

        var factors = SplitFactors(term);
        if (factors.Count != 2) return null;

        // One factor is a matrix column, the other is a lane of the vector.
        for (int k = 0; k < 2; k++)
        {
            var r = RowOf(factors[k]);
            if (r == null || r.Mask.Length != 2) continue;
            var (vname, vmask) = SplitMask(factors[1 - k]);
            string vlanes = vmask.TrimStart('.');
            if (vlanes.Length != 1) continue;
            char lane = vlanes[0];
            if ("xyzw"[r.Row] != lane) continue;
            return (s.Lhs, s.LhsMask, r.Matrix, r.Mask[1..], vname, lane, r.Row, acc);
        }
        return null;
    }

    /// <summary>
    /// Fold a product that was written on ONE line, or the single row that completes
    /// one written elsewhere.
    ///
    ///     r4.xyz = M[1].xyz * v.y + M[0].xyz * v.x + M[2].xyz * v.z;
    ///     r2.xzw = M[3].xyz * v.www + r1.xyz;
    ///
    /// The first is a three-row product with a zero fourth weight, the second is one
    /// column of the matrix scaled by one component.  Both are `mul`: the rows that
    /// were left out simply weigh nothing.
    ///
    /// This only runs on statements whose matrix is a `float4x4` -- a name declared
    /// here as a row array is read correctly as it stands, and `mul` has no overload
    /// for it anyway.
    /// </summary>
    private static List<string> FoldRowSums(List<string> lines, HashSet<string> arrays)
    {
        List<string> outp = null;
        for (int i = 0; i < lines.Count; i++)
        {
            string text = RowSum(lines[i], arrays);
            if (text == null) { outp?.Add(lines[i]); continue; }
            outp ??= Copy(lines, i);
            outp.Add(text);
        }
        return outp ?? lines;
    }

    /// <summary>The folded spelling of one statement, or null when it is not a row sum.</summary>
    private static string RowSum(string line, HashSet<string> arrays)
    {
        var s = Parse(line);
        if (s == null) return null;

        var terms = s.Terms;
        string acc = null;
        if (terms.Count >= 2)
        {
            var (an, am) = SplitMask(terms[^1]);
            // Lanes map positionally (`dst.xzw = … + acc.xyz`), so only the width has
            // to agree -- the destination is free to be a permutation of the sum.
            if (an.Length > 0 && am.Length == s.LhsMask.Length && RowOf(terms[^1]) == null)
            {
                acc = terms[^1];
                terms = terms.GetRange(0, terms.Count - 1);
            }
        }
        // One lonely row term is only worth folding when something accumulates it.
        if (terms.Count == 0 || (terms.Count == 1 && acc == null)) return null;

        string matrix = null, mask = null;
        var weight = new string[4];
        var seen = new bool[4];
        foreach (string term in terms)
        {
            var f = SplitFactors(term);
            if (f.Count != 2) return null;
            RowRef row = RowOf(f[0]) ?? RowOf(f[1]);
            if (row == null) return null;
            string scalar = RowOf(f[0]) != null ? f[1] : f[0];
            // The weight has to be ONE component: a vector would make `float4(...)`
            // five arguments wide.
            string plain = Unwrap(scalar);
            if (!Split(plain, out _, out _) && !IsNumeric(plain)) return null;
            if (row.Mask.Length != s.LhsMask.Length) return null;
            if (seen[row.Row]) return null;
            if (matrix == null) { matrix = row.Matrix; mask = row.Mask; }
            else if (matrix != row.Matrix || mask != row.Mask) return null;
            seen[row.Row] = true;
            weight[row.Row] = scalar;
        }
        if (matrix == null) return null;
        if (arrays.Contains(matrix) || !IsMatrixName(matrix)) return null;

        var comps = new string[4];
        for (int r = 0; r < 4; r++) comps[r] = seen[r] ? Narrow(weight[r]) : "0.0";
        return $"{s.Lhs}{s.LhsMask} = mul({matrix}, " +
               $"float4({comps[0]}, {comps[1]}, {comps[2]}, {comps[3]})){mask}" +
               (acc != null ? $" + {acc}" : "") + ";";
    }

    /// <summary>A bare number: it already is one component.</summary>
    private static bool IsNumeric(string text)
    {
        string t = text.TrimStart('-');
        return t.Length > 0 && (char.IsDigit(t[0]) || t[0] == '.');
    }

    /// <summary>
    /// Fold matrix products in an already-emitted body.
    ///
    /// The DXBC route builds its statements from instructions, where the same product
    /// arrives as `mul r, cb0[k].yyy, cb1[j].xyz` -- a matrix element weighing a matrix
    /// row -- or as the three-lane completion of a sum scheduled far from it.  Its own
    /// recogniser works on registers and scalars, so it sees nothing to fold there; the
    /// shape is identical at statement level, and this pass is what the GLSL route
    /// already runs on its own output.
    /// </summary>
    public static string FoldText(string text, bool macros)
    {
        var lines = text.Split('\n').ToList();
        var folded = Fold(lines, macros);
        return ReferenceEquals(folded, lines) ? text : string.Join('\n', folded);
    }

    /// <summary>
    /// A product of TWO matrices and a vector: sixteen row terms that the payload
    /// writes as four four-row blocks, each scaled by one component of the vector and
    /// summed.
    ///
    ///     u_xlat0.xyz = OW[1].yyy * MV[1].xyz;
    ///     u_xlat0.xyz = MV[0].xyz * OW[1].xxx + u_xlat0.xyz;
    ///     u_xlat0.xyz = MV[2].xyz * OW[1].zzz + u_xlat0.xyz;
    ///     u_xlat0.xyz = MV[3].xyz * OW[1].www + u_xlat0.xyz;
    ///     u_xlat0.xyz = u_xlat0.xyz * p.yyy;               <- block for row 1
    ///     u_xlat1.xyz = OW[0].yyy * MV[1].xyz;
    ///     ...                                              <- block for row 0
    ///     u_xlat0.xyz = u_xlat1.xyz * p.xxx + u_xlat0.xyz;
    ///     ...                                              <- rows 2, 3 the same
    ///     u_xlat0.xyz = u_xlat0.xyz + u_xlat1.xyz;         <- row 3 weighs 1.0
    ///
    /// A block is one row of the first matrix taken whole against all four rows of
    /// the second, and the four blocks combine with the components of one vector --
    /// which together is <c>mul(mul(OW, p), MV)</c>, the object-to-view transform.
    /// Folding it keeps all sixteen row references out of the body, and the sum of
    /// four scaled blocks is exactly what `mul` is.
    /// </summary>
    private static List<string> FoldMatrixProducts(List<string> lines, HashSet<string> arrays)
    {
        List<string> outp = null;
        for (int i = 0; i < lines.Count; i++)
        {
            if (!TryViewRun(lines, i, arrays, out int end, out string text))
            {
                outp?.Add(lines[i]);
                continue;
            }
            outp ??= Copy(lines, i);
            outp.Add(text);
            i = end;
        }
        return outp ?? lines;
    }

    /// <summary>The four blocks-and-combines that make one matrix-matrix product.</summary>
    private static bool TryViewRun(List<string> lines, int i, HashSet<string> arrays,
                                   out int end, out string text)
    {
        end = i;
        text = null;
        const int Group = 5;                      // four row terms, then the combine
        if (i + 4 * Group > lines.Count) return false;

        string outer = null;                      // matrix the blocks take rows of
        string inner = null;                      // matrix whose one row weights them
        string suffix = null;                     // destination swizzle, shared
        string sum = null;                        // register the blocks add into
        string vec = null;                        // register the weights are lanes of
        var used = new bool[4];
        bool homogeneous = false;

        for (int g = 0; g < 4; g++)
        {
            int at = i + g * Group;
            string dst = null;
            int row = -1;                         // inner row this block takes whole
            var rows = new bool[4];

            for (int k = 0; k < 4; k++)
            {
                var s = Parse(lines[at + k]);
                if (s == null || s.LhsMask.Length == 0) return false;
                if (s.Terms.Count != (k == 0 ? 1 : 2)) return false;
                if (k == 0) { dst = s.Lhs; suffix ??= s.LhsMask; }
                else if (s.Lhs != dst) return false;
                if (s.LhsMask != suffix) return false;
                if (k > 0 && !IsAccumulator(s.Terms[1], dst, suffix)) return false;

                var f = SplitFactors(s.Terms[0]);
                if (f.Count != 2) return false;
                // Both factors are matrix elements: one names a whole row (the
                // product's columns), the other a single component (the weight).
                RowRef fa = RowOf(f[0]);
                RowRef fb = RowOf(f[1]);
                if (fa == null || fb == null) return false;
                bool aIsWeight = Broadcast(fa.Mask);
                bool bIsWeight = Broadcast(fb.Mask);
                if (aIsWeight == bIsWeight) return false;
                RowRef o = aIsWeight ? fb : fa;
                RowRef w = aIsWeight ? fa : fb;
                if (o.Mask != suffix) return false;

                // Row k of the outer matrix is weighed by component k of the inner
                // row: `MV[1] * OW[r].y`.
                string lane = w.Mask.TrimStart('.');
                if (lane.Length == 0 || lane[0] != "xyzw"[o.Row]) return false;
                if (o.Row is < 0 or > 3 || rows[o.Row]) return false;
                rows[o.Row] = true;

                if (inner == null) inner = w.Matrix;
                else if (inner != w.Matrix) return false;
                if (outer == null) outer = o.Matrix;
                else if (outer != o.Matrix) return false;

                if (row < 0) row = w.Row;
                else if (row != w.Row) return false;
            }

            if (row < 0 || used[row]) return false;
            used[row] = true;

            // The combine.  The first block is scaled where it stands; the rest add
            // into one register, the last one either with a weight or with 1.0.
            var c = Parse(lines[at + 4]);
            if (c == null || c.LhsMask != suffix) return false;
            string w2;
            if (g == 0)
            {
                if (c.Terms.Count != 1 || c.Lhs != dst) return false;
                var f = SplitFactors(c.Terms[0]);
                if (f.Count != 2) return false;
                if (!IsAccumulator(f[0], dst, suffix) && !IsAccumulator(f[1], dst, suffix))
                    return false;
                w2 = IsAccumulator(f[0], dst, suffix) ? f[1] : f[0];
                sum = dst;
            }
            else
            {
                if (c.Terms.Count != 2 || c.Lhs != sum) return false;
                string product, accTerm;
                if (IsAccumulator(c.Terms[1], sum, suffix)) { product = c.Terms[0]; accTerm = c.Terms[1]; }
                else if (IsAccumulator(c.Terms[0], sum, suffix)) { product = c.Terms[1]; accTerm = c.Terms[0]; }
                else return false;
                var f = SplitFactors(product);
                if (f.Count == 1)
                {
                    if (!IsAccumulator(f[0], dst, suffix)) return false;
                    if (g != 3) return false;
                    homogeneous = true;
                    continue;
                }
                if (f.Count != 2) return false;
                if (!IsAccumulator(f[0], dst, suffix) && !IsAccumulator(f[1], dst, suffix))
                    return false;
                w2 = IsAccumulator(f[0], dst, suffix) ? f[1] : f[0];
            }

            var (wn, wm) = SplitMask(w2);
            string wl = wm.TrimStart('.');
            if (wl.Length == 0 || wl[0] != "xyzw"[row]) return false;
            if (vec == null) vec = wn;
            else if (vec != wn) return false;
        }

        if (vec == null || !used[0] || !used[1] || !used[2] || !used[3]) return false;
        if (arrays.Contains(outer) || !IsMatrixName(outer)) return false;
        if (arrays.Contains(inner) || !IsMatrixName(inner)) return false;

        string arg = homogeneous ? $"float4({vec}.xyz, 1.0)" : vec;
        text = $"{sum}{suffix} = mul(mul({inner}, {arg}), {outer}){suffix};";
        end = i + 4 * Group - 1;
        return true;
    }

    private static List<string> Copy(List<string> lines, int count)
    {
        var copy = new List<string>(lines.Count);
        for (int i = 0; i < count; i++) copy.Add(lines[i]);
        return copy;
    }

    /// <summary>
    /// Read the product whose expansion starts at <paramref name="i"/>, or null.
    ///
    /// The three row terms have to name one matrix and the three distinct rows
    /// 0..2; the fourth is either the homogeneous `acc + M[3]` or a real
    /// `M[3] * v.w + acc`.  Anything else is left exactly as it was.
    ///
    /// The terms do NOT have to land in one register.  HLSLcc picks the register it
    /// needs next, so a product routinely runs
    ///
    ///     u_xlat1 = u_xlat0.yyyy * M[1];
    ///     u_xlat1 = M[0] * u_xlat0.xxxx + u_xlat1;
    ///     u_xlat0 = M[2] * u_xlat0.zzzz + u_xlat1;     &lt;- renamed mid-chain
    ///     u_xlat0 = u_xlat0 + M[3];
    ///
    /// and reading that as "one destination" lost every one of them.  What the four
    /// lines actually say is "accumulate rows 0..2 of M into a running sum, then add
    /// row 3", so the accumulator is tracked by name and each term may hand it on to
    /// a fresh register.
    ///
    /// A product written with a mask -- `u_xlat5.xyz = M[k].xyz * s` -- is the same
    /// product with three of its four columns asked for, and a chain that stops after
    /// row 2 is the product with a zero fourth weight.  Both are folded, because both
    /// are written with the same `mul`.
    /// </summary>
    private static Product TryProduct(List<string> lines, int i, HashSet<string> arrays)
    {
        var head = Parse(lines[i]);
        if (head == null || head.Terms.Count != 1) return null;

        var factors = SplitFactors(head.Terms[0]);
        if (factors.Count != 2) return null;
        RowRef first = RowOf(factors[0]) ?? RowOf(factors[1]);
        if (first == null) return null;
        if (arrays.Contains(first.Matrix) || !IsMatrixName(first.Matrix)) return null;
        string headScalar = RowOf(factors[0]) != null ? factors[1] : factors[0];
        if (RowOf(headScalar) != null) return null;

        var p = new Product
        {
            Start = i,
            Dst = head.Lhs,
            Matrix = first.Matrix,
            RowMask = first.Mask,
            DstMask = head.LhsMask,
        };
        // The projection may permute the columns (`dst.xyz` from `M[k].yzx`) -- one
        // `mul` spells that by putting the same swizzle on its result.  Only the
        // WIDTH has to agree: a four-lane product cannot be narrowed by the row
        // alone.
        if (p.RowMask.Length != p.DstMask.Length) return null;

        var rows = new bool[4];
        // Each renamed accumulator, with the weights it holds.  A register only
        // becomes a partial sum worth keeping when it is handed on to a fresh one.
        var partial = new List<(string reg, string[] comps)>();
        var comps = new string[4];

        if (!Take(p, rows, first, headScalar)) return null;
        comps[first.Row] = headScalar;
        string acc = p.Dst;
        int n = 1;

        // Rows beside the first, each `M[r] * s + acc` in any factor order.
        int k = i + 1;
        for (; k < lines.Count && n < 3; k++, n++)
        {
            var s = Parse(lines[k]);
            // The destination is whatever register fxc reaches for next; only the
            // ACCUMULATOR has to be the one the previous term left its sum in.
            if (s == null || s.LhsMask != p.DstMask) break;
            if (!SplitSum(s, acc, p.DstMask, out string product)) break;
            var sum = SplitFactors(product);
            if (sum.Count != 2) break;
            RowRef row = RowOf(sum[0]) ?? RowOf(sum[1]);
            if (row == null || row.Matrix != p.Matrix || row.Mask != p.RowMask) break;
            string other = RowOf(sum[0]) != null ? sum[1] : sum[0];
            if (RowOf(other) != null) break;
            if (!Take(p, rows, row, other)) break;
            comps[row.Row] = other;

            string next = s.Lhs;
            if (next != acc) partial.Add((acc, (string[])comps.Clone()));
            acc = next;
        }

        // The rows are in; what follows may be a NARROWER completion first.
        //
        // A sprite shader wants the world position as well as the clip position, and
        // the payload writes that one first because it needs only three columns:
        //
        //     u_xlat2.xzw = M[3].xyz * v.www + u_xlat1.xyz;   <- three lanes
        //     u_xlat1     = M[3] * v.wwww + u_xlat1;          <- the whole product
        //
        // Both are products over the same three-row sum, so both fold: the narrow one
        // with the row's swizzle, the wide one whole.  Sliding over them is what lets
        // the closing row still be recognised.
        int k2 = i + n;
        bool rowsReady = n == 3 && rows[0] && rows[1] && rows[2];
        for (; rowsReady && k2 < lines.Count && p.Keeps.Count < 4; k2++)
        {
            if (!TryNarrow(lines[k2], p, acc, out string narrow)) break;
            p.Skip.Add(k2);
            p.Keeps.Add(narrow);
        }

        // The closing row, or none at all.
        bool homogeneous = false;
        bool closed = n == 3 && k2 < lines.Count &&
                      TryClose(lines[k2], p, rows, acc, comps, out homogeneous);
        if (closed)
        {
            p.Homogeneous = homogeneous;
            p.Final = Parse(lines[k2]).Lhs;
            p.Count = k2 - i + 1;
        }
        else
        {
            // No fourth row: the first three ARE the product, with a zero weight
            // where row 3 would be.  Only rows 0, 1 and 2 of one array qualify --
            // any other three rows would need the missing one spelled out.
            if (n != 3 || !rows[0] || !rows[1] || !rows[2]) return null;
            if (rows[3]) return null;
            // The sum may have moved to a fresh register on the way -- and then the
            // one it started in is only a half-product nobody reads again.
            if (p.Dst != acc && ReadBeforeWrite(lines, p.Dst, k2)) return null;
            p.PartialOnly = true;
            p.Comp[3] = "0.0";
            p.Final = acc;
            p.Count = k2 - i;
        }
        if (!rows[0] || !rows[1] || !rows[2]) return null;
        if (p.Final != acc && !closed) return null;
        if (p.Final != acc) partial.Add((acc, (string[])comps.Clone()));

        int after = p.Start + p.Count;

        // The narrow completion may also come AFTER the closing row, reading the sum
        // the closing row left behind in its own register rather than overwriting it.
        // Same product, written a statement later.
        if (rowsReady && p.Final != acc)
        {
            // Readers of the sum can sit far below -- fxc schedules the whole
            // clip-position product in between -- so the scan is over the rest of the
            // function, stopping at the first statement that writes the sum or any
            // register the weights are read from.  Past that point the recomputed
            // product would no longer mean the same thing.
            var guard = new HashSet<string>(StringComparer.Ordinal) { acc };
            for (int r = 0; r < 4; r++)
                if (p.Base[r] != null) guard.Add(p.Base[r]);
            for (int j = after; j < lines.Count && j < after + 400; j++)
            {
                // A completion that lands back in the sum's own register is
                // legitimate -- it writes what it just computed instead of adding.
                // The sum is gone after that, so nothing further may be absorbed.
                if (TryNarrow(lines[j], p, acc, out string later))
                {
                    p.Skip.Add(j);
                    p.Keeps.Add(later);
                    if (p.Keeps.Count >= 8) break;
                    if (Regex.IsMatch(lines[j], $@"^\s*{Regex.Escape(acc)}\s*=")) break;
                    continue;
                }
                if (WritesAny(lines[j], guard)) break;
            }
        }

        // A partial sum something still reads cannot simply disappear: the fold
        // writes the whole product instead of the halves.  That is not a reason to
        // give up -- the sum is a product too (rows 0..2, zero fourth weight), so it
        // is written as one, and the body keeps the register it names.
        foreach (var (reg, held) in partial)
        {
            if (reg == p.Final) continue;
            if (!ReadBeforeWrite(lines, reg, after, p.Skip)) continue;
            string arg = $"float4({Narrow(held[0])}, {Narrow(held[1])}, " +
                         $"{Narrow(held[2])}, 0.0)";
            p.Extras.Add($"{reg}{p.DstMask} = mul({p.Matrix}, {arg}){p.RowMask};");
        }

        // The vector argument: the weights have to be the x/y/z(/w) lanes of one
        // register.  Four unrelated scalars are still correct, just longer.
        p.Vector = VectorOf(p);
        string vec = p.Vector != null
            ? (p.Homogeneous ? $"float4({p.Vector}.xyz, 1.0)" : p.Vector)
            : $"float4({Narrow(p.Comp[0])}, {Narrow(p.Comp[1])}, " +
              $"{Narrow(p.Comp[2])}, {Narrow(p.Comp[3])})";
        p.Text = $"{p.Final}{p.DstMask} = mul({p.Matrix}, {vec}){p.RowMask};";
        return p;
    }

    /// <summary>
    /// Read the closing row: <c>acc + M[3]</c> (weight 1.0, reported through
    /// <paramref name="homogeneous"/>) or <c>M[3] * s + acc</c>, whose weight is
    /// recorded like any other.
    /// </summary>
    private static bool TryClose(string line, Product p, bool[] rows, string acc,
                                 string[] comps, out bool homogeneous)
    {
        homogeneous = false;
        var s = Parse(line);
        if (s == null || s.LhsMask != p.DstMask) return false;
        if (!SplitSum(s, acc, p.DstMask, out string product)) return false;

        var f = SplitFactors(product);
        RowRef r3 = f.Count == 1 ? RowOf(f[0]) : (RowOf(f[0]) ?? RowOf(f[1]));
        if (r3 == null || r3.Row != 3 || r3.Matrix != p.Matrix || r3.Mask != p.RowMask)
            return false;

        if (f.Count == 1)
        {
            // `acc + M[3]`: the fourth weight is the homogeneous 1.0.
            p.Comp[3] = "1.0";
            homogeneous = true;
            return true;
        }
        if (f.Count != 2) return false;
        string weight = RowOf(f[0]) != null ? f[1] : f[0];
        if (RowOf(weight) != null) return false;
        if (!Take(p, rows, r3, weight)) return false;
        comps[3] = weight;
        return true;
    }

    /// <summary>
    /// The three-lane completion of a sum this chain just built:
    /// <c>dst.xyz = M[3].xyz * v.w + acc.xyz</c>.
    ///
    /// The accumulator has to be the register the chain's rows 0..2 landed in, and
    /// its lanes have to be the product's first ones in order -- a permuted read
    /// would mean the caller wants different columns, which one `mul` spells only
    /// when the row's swizzle and the destination agree.
    /// </summary>
    private static bool TryNarrow(string line, Product p, string acc, out string text)
    {
        text = null;
        var s = Parse(line);
        if (s == null || s.Terms.Count != 2) return false;

        string product;
        string accTerm;
        if (SplitMask(s.Terms[1]).name == acc) { product = s.Terms[0]; accTerm = s.Terms[1]; }
        else if (SplitMask(s.Terms[0]).name == acc) { product = s.Terms[1]; accTerm = s.Terms[0]; }
        else return false;

        var f = SplitFactors(product);
        RowRef r3 = f.Count switch
        {
            1 => RowOf(f[0]),
            2 => RowOf(f[0]) ?? RowOf(f[1]),
            _ => null,
        };
        if (r3 == null || r3.Row != 3 || r3.Matrix != p.Matrix) return false;
        string rm = r3.Mask.TrimStart('.');
        if (rm.Length == 0) return false;

        string weight = "1.0";
        if (f.Count == 2)
        {
            weight = RowOf(f[0]) != null ? f[1] : f[0];
            if (RowOf(weight) != null) return false;
        }

        // The accumulator's lanes are the product's first ones, in order.
        string am = SplitMask(accTerm).mask.TrimStart('.');
        if (am.Length == 0 || am != "xyzw"[..am.Length]) return false;
        if (rm != am || s.LhsMask.TrimStart('.').Length != rm.Length) return false;

        // A completion NARROWER than the product, or it is the product's own closing
        // row written in the same width.  `u_xlat.xyz = M[3].xyz * v.www + u_xlat.xyz`
        // closes a three-lane product; folding it here would leave the chain without
        // its fourth row and the wide statement behind.
        int width = p.RowMask.Length == 0 ? 4 : p.RowMask.Length - 1;
        if (rm.Length >= width) return false;

        text = $"{s.Lhs}{s.LhsMask} = mul({p.Matrix}, float4({Narrow(p.Comp[0])}, " +
               $"{Narrow(p.Comp[1])}, {Narrow(p.Comp[2])}, {Narrow(weight)})){r3.Mask};";
        return true;
    }

    /// <summary>
    /// Split <c>a + b</c> into the accumulator term and the product term, whichever
    /// order they were written in.
    /// </summary>
    private static bool SplitSum(Stmt s, string acc, string accMask, out string product)
    {
        product = null;
        if (s.Terms.Count != 2) return false;
        if (IsAccumulator(s.Terms[1], acc, accMask)) product = s.Terms[0];
        else if (IsAccumulator(s.Terms[0], acc, accMask)) product = s.Terms[1];
        return product != null;
    }

    /// <summary>Does this statement write any of these registers?</summary>
    private static bool WritesAny(string line, HashSet<string> names)
    {
        var s = Parse(line);
        if (s != null) return names.Contains(s.Lhs);
        foreach (string n in names)
            if (Regex.IsMatch(line, $@"^\s*{Regex.Escape(n)}(?:\.[xyzw]+)?\s*=(?!=)")) return true;
        return false;
    }

    /// <summary>`yyyy` -&gt; true: one lane repeated, which is how the payload broadcasts.</summary>
    private static bool Broadcast(string mask)
    {
        string m = mask.TrimStart('.');
        return m.Length > 0 && m.All(c => c == m[0]);
    }

    private static bool IsAccumulator(string text, string acc, string mask)
    {
        var (name, m) = SplitMask(text);
        return name == acc && m == mask;
    }

    /// <summary>
    /// One component of a swizzled operand: `u_xlat0.yyyy` -&gt; `u_xlat0.y`.
    ///
    /// The payload broadcasts with a full four-letter swizzle, and a broadcast is
    /// a float4; handing it to `float4(...)` as one component would be thirteen
    /// arguments (X3014) where one is meant.
    /// </summary>
    private static string Narrow(string text)
    {
        text = Unwrap(text);
        int dot = text.IndexOf('.');
        if (dot <= 0) return text;
        string tail = text[(dot + 1)..];
        return tail.Length > 1 && tail.All(c => c == tail[0]) ? text[..(dot + 2)] : text;
    }

    /// <summary>
    /// Strip a scalar-to-vector cast: `(float4)(x)` and `float4(x)` are both how the
    /// payload spells a broadcast, and inside `float4(...)` the wrapper would count as
    /// four more components.
    /// </summary>
    private static string Unwrap(string text)
    {
        string t = text.Trim();
        while (true)
        {
            int open;
            if (t.StartsWith("(float4)(", StringComparison.Ordinal)) open = 8;
            else if (t.StartsWith("float4(", StringComparison.Ordinal)) open = 6;
            else if (t.StartsWith("vec4(", StringComparison.Ordinal)) open = 4;
            else return t;
            if (!t.EndsWith(')')) return t;
            string inner = t[(open + 1)..^1];
            // Only a one-argument constructor is a broadcast; `float4(a, b, c, d)` is
            // four components and stays as it is.
            if (inner.Contains(',')) return t;
            t = inner.Trim();
            if (t.Length == 0) return text;
        }
    }

    private static bool Take(Product p, bool[] rows, RowRef row, string scalar)
    {
        if (row.Row is < 0 or > 3 || rows[row.Row]) return false;
        if (Split(scalar, out string bas, out char lane))
        {
            p.Base[row.Row] = bas;
            p.Lane[row.Row] = lane;
        }
        p.Comp[row.Row] = scalar;
        rows[row.Row] = true;
        return true;
    }

    /// <summary>`u_xlat0.xxxx` -&gt; ("u_xlat0", 'x'); `1.0` -&gt; (null, '\0').</summary>
    private static bool Split(string text, out string bas, out char lane)
    {
        bas = null;
        lane = '\0';
        text = Unwrap(text);
        int dot = text.IndexOf('.');
        if (dot <= 0) return false;
        string tail = text[(dot + 1)..];
        if (tail.Length == 0 || tail.Any(c => c != tail[0])) return false;
        if (!"xyzw".Contains(tail[0])) return false;
        bas = text[..dot];
        lane = tail[0];
        return true;
    }

    /// <summary>The shared register whose x/y/z(/w) lanes are the weights, or null.</summary>
    private static string VectorOf(Product p)
    {
        if (p.Base[0] == null || p.Base[1] == null || p.Base[2] == null) return null;
        if (p.Base[0] != p.Base[1] || p.Base[0] != p.Base[2]) return null;
        if (p.Lane[0] != 'x' || p.Lane[1] != 'y' || p.Lane[2] != 'z') return null;
        if (p.Homogeneous) return p.Base[0];
        // A real fourth component has to be the same register's .w, which is what
        // makes the whole argument the register itself.
        if (p.Base[3] != p.Base[0] || p.Lane[3] != 'w') return null;
        return p.Base[0];
    }

    /// <summary>
    /// `mul(VP, mul(OW, v))` -&gt; `UnityObjectToClipPos(v)`.
    ///
    /// A vertex transform is written twice, once per matrix, and the intermediate
    /// register usually dies right after -- which is exactly the shape of the macro.
    /// </summary>
    private static void MergeClipPos(List<string> lines, List<Product> products,
                                     HashSet<string> arrays)
    {
        foreach (var p1 in products)
        {
            if (p1.Matrix != "unity_ObjectToWorld" || !p1.Homogeneous) continue;
            if (p1.PartialOnly || p1.RowMask.Length > 0 || p1.DstMask.Length > 0) continue;
            if (arrays.Contains("unity_ObjectToWorld")) continue;
            foreach (var p2 in products)
            {
                if (p2.Start != p1.Start + p1.Count) continue;
                if (p2.Matrix != "unity_MatrixVP") continue;
                if (p2.PartialOnly || p2.RowMask.Length > 0 || p2.DstMask.Length > 0) continue;
                if (arrays.Contains("unity_MatrixVP")) continue;
                if (p2.Vector != p1.Final || p2.Homogeneous) continue;
                // The world position may be read on afterwards; that is only safe when
                // the clip-position statement overwrites the very same register.  The
                // test has to be "still live", not "mentioned again": HLSLcc reuses
                // the register for the next value, so `u_xlat0.x = dot(...)` right
                // after the transform is a write, and reading it as a use dropped the
                // merge on most shaders.
                if (p2.Final != p1.Final && ReadBeforeWrite(lines, p1.Final, p2.Start + p2.Count))
                    continue;
                // The world position is recomputed inside the macro; dropping the
                // separate statement is only safe because nothing reads it.  A live
                // partial sum keeps its own statement (in `Extras`), which the fold
                // emits either way.
                p2.Text = $"{p2.Final} = UnityObjectToClipPos(" +
                          (p1.Vector != null
                              ? p1.Vector + ".xyz"
                              : $"float3({Narrow(p1.Comp[0])}, {Narrow(p1.Comp[1])}, {Narrow(p1.Comp[2])})") +
                          ");";
                p1.Text = null;
                break;
            }
        }
    }

    /// <summary>
    /// Is a lane of this register still holding its old value when it is read back?
    ///
    /// The payload reuses one small set of temporaries (`u_xlat0`..`u_xlat5`) for
    /// the whole function, so "is it read later at all" is always yes and the
    /// question that decides whether a partial sum may disappear is "is it still
    /// live there".  A statement that writes the whole register ends it; a statement
    /// that writes one lane only ends that lane, so the scan tracks lanes.
    ///
    /// A write that reads the register on its right-hand side -- `u_xlat1.xyz =
    /// M[3].xyz * v.w + u_xlat1.xyz` -- keeps the old value alive for that read, so
    /// the right-hand side is scanned before the write counts as ending it.  Reading
    /// the statement as "the destination is written, therefore the value is dead"
    /// dropped the three rows of a world position whose sum was only ever completed
    /// through such a self-referencing add.
    /// </summary>
    private static bool ReadBeforeWrite(List<string> lines, string reg, int from,
                                       ICollection<int> skip = null)
    {
        string name = Regex.Escape(reg);
        var write = new Regex($@"^\s*{name}(?<sw>\.[xyzw]+)?\s*=(?!=)");
        var read = new Regex($@"\b{name}(?<sw>\.[xyzw]+)?(?![A-Za-z0-9_])");
        var gone = new HashSet<char>();
        for (int i = from; i < lines.Count; i++)
        {
            if (skip != null && skip.Contains(i)) continue;
            string line = lines[i];
            var w = write.Match(line);
            int rhs = w.Success ? w.Index + w.Length : -1;
            foreach (Match r in read.Matches(line))
            {
                if (rhs >= 0 && r.Index < rhs) continue;      // the destination itself
                string sw = r.Groups["sw"].Value.TrimStart('.');
                foreach (char c in sw.Length == 0 ? "xyzw" : sw)
                    if (!gone.Contains(c)) return true;
            }
            if (!w.Success) continue;
            string wsw = w.Groups["sw"].Value.TrimStart('.');
            if (wsw.Length == 0) return false;
            foreach (char c in wsw) gone.Add(c);
            if (gone.Count >= 4) return false;
        }
        return false;
    }

    private static string Strip(string line) => line.Trim();
}
