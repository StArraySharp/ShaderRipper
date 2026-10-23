using System;
using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using ShaderRipper.Cg;

namespace ShaderRipper.Antlr
{
    /// <summary>
    /// ANTLR-based rewriter for sampling intrinsics in emitted Cg/HLSL text.
    ///
    /// This is the structured counterpart to <see cref="CgSyntax.RewriteLine"/>.
    /// That one walks one line of text at a time, matching `tex2D(` with a regex
    /// and then counting parentheses by hand, which leaves three things it cannot
    /// see: a call whose arguments continue onto the next line (it gives up and
    /// the call survives untranslated, silently), a `tex2D(` that lives inside a
    /// `//` or `/* */` comment, and a `tex2D(` that lives inside a string literal.
    /// Here the lexer owns the notion of "comment" and "string", so all three are
    /// impossible by construction.
    ///
    /// Scope is deliberately the expression layer only: there is no statement or
    /// declaration grammar.  The rewriter lexes the whole body, finds tokens that
    /// name a sampling intrinsic, and asks the parser for the argument structure
    /// of that one call.  Anything the grammar does not accept is left untouched
    /// rather than guessed at.
    ///
    /// Nothing in the existing translation path calls this yet -- it is additive,
    /// and <see cref="CgSyntax.RewriteLine"/> remains authoritative until the two
    /// are shown to agree on real corpora (see <c>SelfCheck</c>).
    /// </summary>
    public static class SamplingRewriter
    {
        /// <summary>The intrinsics this rewriter knows, mirroring CgSyntax.RewriteLine.</summary>
        private static readonly HashSet<string> LodeNames = new(StringComparer.Ordinal)
        {
            "tex2Dlod", "texCUBElod", "tex3Dlod",
        };

        private static readonly HashSet<string> PlainNames = new(StringComparer.Ordinal)
        {
            "tex2D", "tex3D", "texCUBE",
        };

        /// <summary>
        /// Rewrites every sampling call in <paramref name="body"/>, returning the
        /// new text.  Offsets outside rewritten calls are preserved byte for byte:
        /// replacements are spliced into the original string, never re-emitted.
        /// </summary>
        /// <param name="body">Cg/HLSL source text; may span lines.</param>
        /// <param name="textures">Texture name to HLSL texture type, used to clamp
        /// an over-wide coordinate swizzle.  May be null.</param>
        /// <param name="hlsl">True to emit the separated `.Sample()` dialect, false
        /// for the combined `tex2D()` one.</param>
        public static string Rewrite(string body, IReadOnlyDictionary<string, string> textures = null,
                                     bool hlsl = true)
        {
            if (string.IsNullOrEmpty(body)) return body;

            var edits = FindEdits(body, textures, hlsl);
            if (edits.Count == 0) return body;

            // Splice right to left so earlier offsets stay valid.
            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            var sb = new StringBuilder(body);
            foreach (var (start, stop, text) in edits)
                sb.Remove(start, stop - start + 1).Insert(start, text);
            return sb.ToString();
        }

        /// <summary>
        /// Every sampling call found in <paramref name="body"/> as
        /// (start offset, stop offset, replacement).  Exposed so a caller can
        /// compare against the regex path without splicing.
        /// </summary>
        public static List<(int Start, int Stop, string Text)> FindEdits(
            string body, IReadOnlyDictionary<string, string> textures = null, bool hlsl = true)
        {
            var edits = new List<(int, int, string)>();
            if (string.IsNullOrEmpty(body)) return edits;

            var lexer = new CgExprLexer(new AntlrInputStream(body));
            lexer.RemoveErrorListeners();
            var tokens = new CommonTokenStream(lexer);
            tokens.Fill();

            var parser = new CgExprParser(tokens);
            parser.RemoveErrorListeners();
            parser.ErrorHandler = new BailErrorStrategy();

            int i = 0;
            while (i < tokens.Size)
            {
                IToken t = tokens.Get(i);
                if (t.Channel != TokenConstants.DefaultChannel) { i++; continue; }

                string name = t.Text;
                bool lode = LodeNames.Contains(name);
                if (!lode && !PlainNames.Contains(name)) { i++; continue; }

                // Only a call: the next default-channel token must open the list.
                int next = NextDefault(tokens, i + 1);
                if (next < 0 || tokens.Get(next).Type != CgExprLexer.LPAREN) { i++; continue; }

                CgExprParser.CallSuffixContext call = ParseCallAt(parser, tokens, i);
                if (call == null) { i++; continue; }

                string replacement = Build(call, tokens, body, name, lode, textures, hlsl);
                if (replacement == null) { i++; continue; }

                int start = call.Start.StartIndex;
                int stop = call.Stop.StopIndex;
                edits.Add((start, stop, replacement));

                // Resume after the call we just consumed.
                i = call.Stop.TokenIndex + 1;
            }
            return edits;
        }

        /// <summary>
        /// Parses an expression starting at token <paramref name="tokenIndex"/> and
        /// returns the call subtree that begins there, or null.
        ///
        /// The parser is asked for a whole expression because `tex2D(a,b) + c` is
        /// one; the call is then the left-most call-suffix of that expression.
        /// That keeps the rewrite bounded to the call itself and leaves the
        /// surrounding operator to the original text.
        /// </summary>
        private static CgExprParser.CallSuffixContext ParseCallAt(
            CgExprParser parser, CommonTokenStream tokens, int tokenIndex)
        {
            tokens.Seek(tokenIndex);
            CgExprParser.ExpressionContext expr;
            try
            {
                expr = parser.expression();
            }
            catch (Exception)
            {
                // Not an expression the grammar accepts: leave it alone.  This is
                // the safe direction -- an unparsed call stays exactly as emitted.
                return null;
            }
            return LeftmostCallAt(expr, tokenIndex);
        }

        private static CgExprParser.CallSuffixContext LeftmostCallAt(
            CgExprParser.ExpressionContext expr, int tokenIndex)
        {
            if (expr == null) return null;
            if (expr.Start == null || expr.Start.TokenIndex != tokenIndex) return null;

            if (expr is CgExprParser.CallSuffixContext self) return self;

            CgExprParser.CallSuffixContext best = null;
            foreach (var child in expr.children ?? new List<IParseTree>())
            {
                if (child is not CgExprParser.ExpressionContext sub) continue;
                var found = LeftmostCallAt(sub, tokenIndex);
                if (found == null) continue;
                if (best == null || found.Stop.TokenIndex < best.Stop.TokenIndex) best = found;
            }
            return best;
        }

        private static int NextDefault(CommonTokenStream tokens, int from)
        {
            for (int i = from; i < tokens.Size; i++)
                if (tokens.Get(i).Channel == TokenConstants.DefaultChannel) return i;
            return -1;
        }

        /// <summary>Builds the replacement text for one call, or null to skip it.</summary>
        private static string Build(CgExprParser.CallSuffixContext call, CommonTokenStream tokens,
                                    string body, string name, bool lode,
                                    IReadOnlyDictionary<string, string> textures, bool hlsl)
        {
            var args = Args(call, body);
            if (args.Count < 2) return null;   // not the intrinsic's shape; leave it

            string tex = args[0].Trim();
            if (tex.Length == 0) return null;

            if (!lode)
            {
                string coord = Clamp(args[1], tex, textures);
                return CgSyntax.Sample(tex, coord, hlsl);
            }

            // tex2Dlod family: the coordinate and LOD are packed into one float4.
            // Both producers do that, but they pack the LOD at different indices
            // (DXBC writes float4(coord, 0, lod), the GLSL path writes
            // float4(coord, lod, 0)), so the LOD is whichever trailing argument is
            // not a literal -- decided structurally here, from the parse tree,
            // rather than by matching the text against a number regex.
            var packed = args[1];
            var parts = PackedArgs(call, body);
            string packedCoord = packed;
            string lod = "0";
            if (parts != null && parts.Count >= 2)
            {
                packedCoord = parts[0].Trim();
                for (int i = parts.Count - 1; i >= 1; i--)
                {
                    if (IsLiteralArg(call, i)) continue;
                    lod = parts[i].Trim();
                    break;
                }
            }
            return CgSyntax.SampleLevel(tex, Clamp(packedCoord, tex, textures), lod, hlsl);
        }

        /// <summary>
        /// The argument source text of a call, each trimmed of nothing -- the
        /// slices are exact, so `float4(uv, 0, 1)` keeps its spacing.
        /// </summary>
        private static List<string> Args(CgExprParser.CallSuffixContext call, string body)
        {
            var res = new List<string>();
            var list = call.argumentList();
            if (list == null) return res;
            foreach (var arg in list.expression())
                res.Add(Slice(arg, body));
            return res;
        }

        /// <summary>
        /// If the second argument of a lod call is a vector constructor such as
        /// <c>float4(...)</c>, its arguments; otherwise null.
        /// </summary>
        private static List<string> PackedArgs(CgExprParser.CallSuffixContext call, string body)
        {
            var list = call.argumentList();
            if (list == null || list.expression().Length < 2) return null;

            var packed = list.expression()[1];
            var inner = LeftmostCall(packed);
            if (inner == null) return null;

            string callee = CalleeName(inner, body);
            if (callee == null || !callee.StartsWith("float", StringComparison.Ordinal)) return null;

            var res = new List<string>();
            var innerList = inner.argumentList();
            if (innerList == null) return res;
            foreach (var arg in innerList.expression())
                res.Add(Slice(arg, body));
            return res;
        }

        /// <summary>True when the i-th packed argument is a numeric literal.</summary>
        private static bool IsLiteralArg(CgExprParser.CallSuffixContext call, int index)
        {
            var list = call.argumentList();
            if (list == null || list.expression().Length < 2) return false;
            var packed = LeftmostCall(list.expression()[1]);
            var innerList = packed?.argumentList();
            if (innerList == null || index >= innerList.expression().Length) return false;

            var arg = innerList.expression()[index];
            // A literal is a bare literal expression, possibly signed: `-1.0`.
            if (arg is CgExprParser.LiteralExprContext) return true;
            if (arg is CgExprParser.UnaryExprContext u)
                return u.expression() is CgExprParser.LiteralExprContext;
            return false;
        }

        private static CgExprParser.CallSuffixContext LeftmostCall(CgExprParser.ExpressionContext expr)
        {
            if (expr == null) return null;
            if (expr is CgExprParser.CallSuffixContext self) return self;
            foreach (var child in expr.children ?? new List<IParseTree>())
                if (child is CgExprParser.ExpressionContext sub)
                {
                    var found = LeftmostCall(sub);
                    if (found != null) return found;
                }
            return null;
        }

        private static string CalleeName(CgExprParser.CallSuffixContext call, string body)
        {
            var callee = call.expression();
            return callee == null ? null : Slice(callee, body);
        }

        private static string Slice(ParserRuleContext ctx, string body)
        {
            if (ctx?.Start == null || ctx.Stop == null) return "";
            int start = ctx.Start.StartIndex;
            int stop = ctx.Stop.StopIndex;
            if (start < 0 || stop < start || stop >= body.Length) return "";
            return body.Substring(start, stop - start + 1);
        }

        /// <summary>
        /// Clamps a coordinate swizzle to the components the texture type consumes.
        /// Delegates to <see cref="CgSyntax"/> so both paths clamp identically
        /// rather than keeping two copies of the rule.
        /// </summary>
        private static string Clamp(string coord, string tex, IReadOnlyDictionary<string, string> textures)
        {
            if (textures == null || !textures.TryGetValue(tex.Trim(), out var ty)) return coord;
            return CgSyntax.ClampCoordinate(coord, ty);
        }
    }
}
