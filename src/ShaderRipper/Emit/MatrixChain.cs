// ShaderRipper -- collapse the four-statement expansion of a matrix-vector product.
//
// GLSLcc, which produces the payloads Unity ships in a GLES program, never emits a
// `mul(M, v)`: it unrolls the product into one assignment per row,
//
//     u_xlat0 = in_POSITION0.yyyy * unity_ObjectToWorld[1];
//     u_xlat0 = unity_ObjectToWorld[0] * in_POSITION0.xxxx + u_xlat0;
//     u_xlat0 = unity_ObjectToWorld[2] * in_POSITION0.zzzz + u_xlat0;
//     u_xlat0 = u_xlat0 + unity_ObjectToWorld[3];
//
// and a vertex shader then does it a second time with `unity_MatrixVP`, because
// Unity's own `UnityObjectToClipPos` was inlined the same way before the GLSL was
// generated.  Nothing in the pipeline puts it back, so the deliverable spelled every
// vertex transform out as eight statements over eight matrix rows.
//
// This pass is a peephole over the TRANSLATED STATEMENT LIST -- the body the GLSL
// translator has already rendered.  It reads statements the same way the DXBC route
// reads instructions (lhs, scalar operand, matrix row), rebuilds the product the
// four assignments stand for, and re-spells it as one `mul`.  A product that feeds a
// second one becomes `UnityObjectToClipPos`, which is the macro the original shader
// was written with.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShaderRipper.Emit;

internal static class MatrixChain
{
    /// <summary>`u_xlat0 = in_POSITION0.yyyy * unity_ObjectToWorld[1];`</summary>
    private static readonly Regex ScalarTimesRow = new(
        @"^(?<lhs>[A-Za-z_]\w*) = (?<s>[A-Za-z_]\w*(?:\.[xyzw]+)?) \* " +
        @"(?<m>[A-Za-z_]\w*)\[(?<r>\d)\];$", RegexOptions.Compiled);

    /// <summary>`u_xlat0 = unity_ObjectToWorld[0] * in_POSITION0.xxxx + u_xlat0;`</summary>
    private static readonly Regex RowTimesScalarPlus = new(
        @"^(?<lhs>[A-Za-z_]\w*) = (?<m>[A-Za-z_]\w*)\[(?<r>\d)\] \* " +
        @"(?<s>[A-Za-z_]\w*(?:\.[xyzw]+)?) \+ (?<acc>[A-Za-z_]\w*);$", RegexOptions.Compiled);

    /// <summary>`u_xlat0 = u_xlat0 + unity_ObjectToWorld[3];`</summary>
    private static readonly Regex AccPlusRow = new(
        @"^(?<lhs>[A-Za-z_]\w*) = (?<acc>[A-Za-z_]\w*) \+ (?<m>[A-Za-z_]\w*)\[(?<r>\d)\];$",
        RegexOptions.Compiled);

    /// <summary>One rebuilt product: the four assignments it replaces.</summary>
    private sealed class Product
    {
        public int Start;
        public int Count;
        /// <summary>The register the row terms accumulate into.</summary>
        public string Dst;
        /// <summary>The register the closing row writes; often, not always, `Dst`.</summary>
        public string Final;
        public string Matrix;
        /// <summary>The scalar weight of each row, as HLSL text.</summary>
        public readonly string[] Comp = new string[4];
        /// <summary>The register each weight is a lane of, when it is one.</summary>
        public readonly string[] Base = new string[4];
        public readonly char[] Lane = new char[4];
        public string Text;
        /// <summary>The vector argument of the `mul`, when all four weights are one vector.</summary>
        public string Vector;
        /// <summary>The closing weight is the homogeneous 1.0 (so `Vector` is a float3).</summary>
        public bool Homogeneous;
        /// <summary>The product was absorbed by the macro; every one of its lines goes.</summary>
        public bool Drop;
    }

    /// <summary>`float4 unity_ObjectToWorld[4];` -- a matrix this file declares itself.</summary>
    private static readonly Regex ArrayDecl = new(
        @"^\s*(?:static\s+)?float4\s+(\w+)\s*\[", RegexOptions.Compiled);

    public static List<string> Fold(List<string> lines)
    {
        // A matrix the includes provide is a `float4x4` and `mul` takes it.  When the
        // payload declared the rows itself -- the GLSL names them
        // `hlslcc_mtx4x4unity_ObjectToWorld`, which is a `float4 name[4]` -- `mul` has
        // no overload for it (X3013), so those products keep the expansion they came
        // with.
        var arrays = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            var decl = ArrayDecl.Match(line);
            if (decl.Success) arrays.Add(decl.Groups[1].Value);
        }

        var products = new List<Product>();
        for (int i = 0; i < lines.Count; i++)
        {
            var p = TryProduct(lines, i, arrays);
            if (p == null) continue;
            products.Add(p);
            i += p.Count - 1;
        }
        if (products.Count == 0) return lines;

        MergeClipPos(lines, products, arrays);

        var outLines = new List<string>(lines.Count);
        var at = new Dictionary<int, Product>();
        foreach (var p in products) at[p.Start] = p;
        var consumed = new HashSet<int>();
        foreach (var p in products)
            for (int k = 1; k < p.Count; k++) consumed.Add(p.Start + k);

        for (int i = 0; i < lines.Count; i++)
        {
            if (at.TryGetValue(i, out var p))
            {
                if (!p.Drop) outLines.Add(Indent(lines[i]) + p.Text);
                continue;
            }
            if (consumed.Contains(i)) continue;
            outLines.Add(lines[i]);
        }
        return outLines;
    }

    private static string Indent(string line)
    {
        int n = 0;
        while (n < line.Length && line[n] == ' ') n++;
        return line[..n];
    }

    /// <summary>
    /// Read the product whose expansion starts at <paramref name="i"/>, or null.
    ///
    /// The three row terms have to name one destination, one matrix and the three
    /// distinct rows 0..2; the fourth is either the homogeneous `acc + M[3]` or a
    /// real `M[3] * v.w + acc`.  Anything else is left exactly as it was.
    /// </summary>
    private static Product TryProduct(List<string> lines, int i, HashSet<string> arrays)
    {
        string head = Strip(lines[i]);
        var first = ScalarTimesRow.Match(head);
        if (!first.Success) return null;
        if (arrays.Contains(first.Groups["m"].Value)) return null;

        var p = new Product { Start = i, Dst = first.Groups["lhs"].Value };
        string matrix = first.Groups["m"].Value;
        var rows = new bool[4];
        int n = 0;

        // Term 0, then as many `M[r] * s + acc` terms as follow with the same
        // destination.  fxc's row order is 1, 0, 2 in practice; any order works.
        Take(p, rows, matrix, first.Groups["r"].Value, first.Groups["s"].Value);
        n = 1;
        int k = i + 1;
        for (; k < lines.Count && n < 3; k++, n++)
        {
            var m = RowTimesScalarPlus.Match(Strip(lines[k]));
            if (!m.Success) return null;
            if (m.Groups["lhs"].Value != p.Dst || m.Groups["acc"].Value != p.Dst) return null;
            if (m.Groups["m"].Value != matrix) return null;
            if (!Take(p, rows, matrix, m.Groups["r"].Value, m.Groups["s"].Value)) return null;
        }
        if (n != 3 || !rows[0] || !rows[1] || !rows[2] || rows[3]) return null;
        if (k >= lines.Count) return null;

        // The closing row.  It may write a DIFFERENT register than the one the sum
        // lives in -- fxc reaches for the register it needs next rather than copying:
        //
        //     u_xlat1 = u_xlat0.yyyy * VP[1];  ...  u_xlat1 = VP[2] * u_xlat0.zzzz + u_xlat1;
        //     u_xlat0 = VP[3] * u_xlat0.wwww + u_xlat1;
        //
        var add = AccPlusRow.Match(Strip(lines[k]));
        string final;
        if (add.Success && add.Groups["acc"].Value == p.Dst &&
            add.Groups["m"].Value == matrix && add.Groups["r"].Value == "3")
        {
            p.Comp[3] = "1.0";
            p.Base[3] = null;
            p.Homogeneous = true;
            final = add.Groups["lhs"].Value;
        }
        else
        {
            var mad = RowTimesScalarPlus.Match(Strip(lines[k]));
            if (!mad.Success || mad.Groups["acc"].Value != p.Dst ||
                mad.Groups["m"].Value != matrix || mad.Groups["r"].Value != "3")
                return null;
            p.Comp[3] = mad.Groups["s"].Value;
            if (Split(p.Comp[3], out string b3, out char l3))
            {
                p.Base[3] = b3;
                p.Lane[3] = l3;
            }
            final = mad.Groups["lhs"].Value;
        }
        if (final.Length == 0) return null;
        p.Final = final;
        p.Count = k - i + 1;

        // The vector argument: the weights have to be the x/y/z(/w) lanes of one
        // register.  Three unrelated scalars are still correct, just longer.
        p.Vector = VectorOf(p);
        string arg = p.Vector != null
            ? (p.Homogeneous ? $"float4({p.Vector}.xyz, 1.0)" : p.Vector)
            : $"float4({Narrow(p.Comp[0])}, {Narrow(p.Comp[1])}, " +
              $"{Narrow(p.Comp[2])}, {Narrow(p.Comp[3])})";
        p.Text = $"{final} = mul({matrix}, {arg});";
        p.Matrix = matrix;
        return p;
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
        int dot = text.IndexOf('.');
        if (dot <= 0) return text;
        string tail = text[(dot + 1)..];
        return tail.Length > 1 && tail.All(c => c == tail[0]) ? text[..(dot + 2)] : text;
    }

    private static bool Take(Product p, bool[] rows, string matrix, string row, string scalar)
    {
        if (!int.TryParse(row, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r))
            return false;
        if (r < 0 || r > 3 || rows[r]) return false;
        if (Split(scalar, out string bas, out char lane))
        {
            p.Base[r] = bas;
            p.Lane[r] = lane;
        }
        p.Comp[r] = scalar;
        rows[r] = true;
        return true;
    }

    /// <summary>`u_xlat0.xxxx` -&gt; ("u_xlat0", 'x'); `1.0` -&gt; (null, '\0').</summary>
    private static bool Split(string text, out string bas, out char lane)
    {
        bas = null;
        lane = '\0';
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
        for (int a = 0; a < products.Count; a++)
        {
            var p1 = products[a];
            if (p1.Matrix != "unity_ObjectToWorld" || !p1.Homogeneous) continue;
            if (arrays.Contains("unity_ObjectToWorld")) continue;
            foreach (var p2 in products)
            {
                if (p2.Start != p1.Start + p1.Count) continue;
                if (p2.Matrix != "unity_MatrixVP") continue;
                if (arrays.Contains("unity_MatrixVP")) continue;
                if (p2.Vector != p1.Dst || p2.Homogeneous) continue;
                // The world position may be read on afterwards; that is only safe when
                // the clip-position statement overwrites the very same register.
                if (p2.Final != p1.Dst && LiveAfter(lines, p1.Dst, p2.Start + p2.Count)) continue;

                p2.Text = $"{p2.Final} = UnityObjectToClipPos({(p1.Vector != null ? p1.Vector + ".xyz" : $"float3({Narrow(p1.Comp[0])}, {Narrow(p1.Comp[1])}, {Narrow(p1.Comp[2])})")});";
                // The first product is recomputed inside the macro; dropping it is
                // only safe because nothing else reads its register.
                p1.Drop = true;
                break;
            }
        }
    }

    /// <summary>Is this register read after line <paramref name="from"/>?</summary>
    private static bool LiveAfter(List<string> lines, string reg, int from)
    {
        var read = new Regex($@"\b{Regex.Escape(reg)}\b(?![A-Za-z0-9_])", RegexOptions.Compiled);
        for (int i = from; i < lines.Count; i++)
            if (read.IsMatch(lines[i])) return true;
        return false;
    }

    private static string Strip(string line) => line.Trim();
}
