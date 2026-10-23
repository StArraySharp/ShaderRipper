using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using ShaderRipper.Cg;

namespace ShaderRipper.Antlr
{
    /// <summary>
    /// The GLSL reader driven by <c>Glsl.g4</c> — the language conversion done
    /// on a parse tree instead of on lines.
    ///
    /// What changes compared with the line scanner in <see cref="GlslToCg"/> is
    /// where the knowledge lives.  The scanner recognises a rewrite by matching
    /// text, then re-balances parentheses by hand to find a call's arguments;
    /// here a call *is* a node with arguments, a constructor *is* a node with a
    /// type and arguments, and an index *is* a base plus a subscript.  So the
    /// rules below say "a call named texture, two arguments" instead of "the
    /// characters tex2D followed by a parenthesis that may contain one nested
    /// pair", and a comment, a string or a call split across lines cannot make
    /// them misfire.
    ///
    /// Everything that is not rewritten keeps its original bytes: the printer
    /// copies the source span and substitutes only the nodes that changed, so
    /// the output stays comparable with the scanner's on the same corpus.
    ///
    /// Thread safety: ANTLR's generated parsers cache DFA/ATN state in
    /// process-wide statics that are documented as unsafe to mutate from several
    /// threads at once.  The exporter parses shaders in a Parallel.ForEach, so
    /// this route serialises its parses behind <see cref="Gate"/>.
    /// </summary>
    internal static class GlslToCgTree
    {
        private static readonly object Gate = new();

        /// <summary>Bodies whose GLSL did not parse; those fall back to the scanner.</summary>
        public static int ParseFailures;

        /// <summary>Bodies that were read from the tree.</summary>
        public static int TreeBodies;

        public static GlslToCg.Result Translate(string glsl, bool hlsl)
        {
            string vsrc = GlslToCg.ExtractBranch(glsl, "VERTEX");
            string fsrc = GlslToCg.ExtractBranch(glsl, "FRAGMENT");
            var pv = vsrc == null ? null : GlslToCg.ParseBranch(vsrc, true);
            var pf = fsrc == null ? null : GlslToCg.ParseBranch(fsrc, false);
            if (pv == null && pf == null) return GlslEmitter.Emit(null, null, hlsl);

            // The declarations decide how a sampler is spelled in the body, so
            // they are read before any statement is rendered.
            GlslToCg.TexInfo(pv, pf, out var texTypes, out var s3d, out var cube);

            lock (Gate)
            {
                if (pv != null) ReadBody(vsrc, pv, true, hlsl, texTypes, s3d, cube);
                if (pf != null) ReadBody(fsrc, pf, false, hlsl, texTypes, s3d, cube);
            }

            return GlslEmitter.Emit(pv, pf, hlsl);
        }

        private static void ReadBody(string src, GlslBranch branch, bool vertex, bool hlsl,
                                     Dictionary<string, string> texTypes,
                                     HashSet<string> s3d, HashSet<string> cube)
        {
            // `BodyText` stays the scanner's reading of the same statements: the
            // emitter consults it for stage facts (gl_Position, gl_VertexID)
            // rather than for the text it emits.
            var reader = new Reader(src, vertex, s3d, cube);
            var body = reader.Translate(hlsl, texTypes);
            if (body == null)
            {
                ParseFailures++;
                branch.Body = GlslToCg.TranslateBody(branch.BodyText, vertex, hlsl, texTypes, s3d, cube);
                return;
            }
            TreeBodies++;
            branch.Body = body;
        }

        /// <summary>Raised when a rewrite would land outside the span it belongs to.</summary>
        private sealed class Bad : Exception
        {
            public Bad(string m) : base(m) { }
        }

        private readonly struct Edit
        {
            public readonly int Start;
            public readonly int Stop;
            public readonly string Text;
            public Edit(int s, int e, string t) { Start = s; Stop = e; Text = t; }
        }

        private sealed class Reader
        {
            private readonly string _src;
            private readonly bool _vertex;
            private readonly HashSet<string> _s3d;
            private readonly HashSet<string> _cube;
            private readonly GlslParser.TranslationUnitContext _unit;
            private readonly string _error;

            public Reader(string src, bool vertex, HashSet<string> s3d, HashSet<string> cube)
            {
                _src = src;
                _vertex = vertex;
                _s3d = s3d;
                _cube = cube;
                var lexer = new GlslLexer(new AntlrInputStream(src));
                lexer.RemoveErrorListeners();
                var parser = new GlslParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                var listener = new Errors();
                parser.AddErrorListener(listener);
                _unit = parser.translationUnit();
                _error = listener.First;
            }

            private sealed class Errors : BaseErrorListener
            {
                public string First;
                public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                                 IToken offendingSymbol, int line, int charPositionInLine,
                                                 string msg, RecognitionException e)
                {
                    First ??= $"line {line}:{charPositionInLine}: {msg}";
                }
            }

            /// <summary>One Cg/HLSL line per statement of <c>main</c>, or null if the body did not parse.</summary>
            public List<string> Translate(bool hlsl, Dictionary<string, string> texTypes)
            {
                if (_error != null)
                {
                    if (Environment.GetEnvironmentVariable("SHADERRIP_GLSLDBG") == "1")
                        Console.Error.WriteLine($"[tree] parse failed: {_error}");
                    return null;
                }

                var res = new List<string>();
                bool found = false;
                foreach (var item in _unit.externalItem())
                {
                    var fn = item.functionDefinition();
                    if (fn == null || fn.IDENT()?.GetText() != "main") continue;
                    var body = fn.compoundStatement();
                    if (body == null) continue;
                    found = true;
                    foreach (var st in body.statement())
                    {
                        string text;
                        try { text = Render(st); }
                        catch (Bad) { return null; }
                        if (string.IsNullOrEmpty(text)) continue;

                        // One line per statement, the way the scanner emits them.
                        // A control-flow statement renders as its own block (the
                        // braces stay, so the result still compiles); splitting
                        // and trimming per line is what keeps the two routes
                        // comparable on text as well as on meaning.
                        string orig = Span(st).Trim();
                        if (GlslToCg.AnnotateSource && !string.Equals(orig, text.Trim(), StringComparison.Ordinal))
                            res.Add("// " + orig);

                        foreach (var raw in text.Split('\n'))
                        {
                            string line = raw.Trim();
                            if (line.Length == 0) continue;
                            if (hlsl) line = CgSyntax.RewriteLine(line, texTypes ?? None);
                            res.Add(line);
                        }
                    }
                }
                return found ? res : null;
            }

            private static readonly IReadOnlyDictionary<string, string> None =
                new Dictionary<string, string>(StringComparer.Ordinal);

            private string Span(ParserRuleContext ctx) =>
                _src.Substring(ctx.Start.StartIndex, ctx.Stop.StopIndex - ctx.Start.StartIndex + 1);

            // ---------------------------------------------------------------
            // Printing: original bytes, with the rewritten nodes substituted.
            // ---------------------------------------------------------------

            private string Render(ParserRuleContext ctx)
            {
                if (ctx == null) return "";
                string custom = Custom(ctx);
                if (custom != null) return custom;

                var edits = new List<Edit>();
                for (int i = 0; i < ctx.ChildCount; i++)
                {
                    var child = ctx.GetChild(i);
                    if (child is ParserRuleContext rc)
                    {
                        if (rc.Start == null || rc.Stop == null) continue;
                        string t = Render(rc);
                        string orig = Span(rc);
                        if (!string.Equals(t, orig, StringComparison.Ordinal))
                            edits.Add(new Edit(rc.Start.StartIndex, rc.Stop.StopIndex, t));
                    }
                    else if (child is ITerminalNode tn)
                    {
                        string t = TokenText(tn.Symbol);
                        if (t != null) edits.Add(new Edit(tn.Symbol.StartIndex, tn.Symbol.StopIndex, t));
                    }
                }
                return Apply(ctx.Start.StartIndex, ctx.Stop.StopIndex, edits);
            }

            private string Apply(int start, int stop, List<Edit> edits)
            {
                string text = _src.Substring(start, stop - start + 1);
                if (edits.Count == 0) return text;
                edits.Sort((a, b) => b.Start.CompareTo(a.Start));
                foreach (var e in edits)
                {
                    int s = e.Start - start, len = e.Stop - e.Start + 1;
                    if (s < 0 || s + len > text.Length) throw new Bad("edit outside span");
                    text = text.Substring(0, s) + e.Text + text.Substring(s + len);
                }
                return text;
            }

            /// <summary>Nodes that are restructured rather than re-spelled.</summary>
            private string Custom(ParserRuleContext ctx)
            {
                switch (ctx)
                {
                    case GlslParser.NamedCallContext nc: return NamedCall(nc);
                    case GlslParser.ConstructorCallContext cc: return ConstructorCall(cc);
                    case GlslParser.IndexExprContext ix: return MatrixIndex(ix);
                    case GlslParser.ReturnStmtContext r when r.expression() == null:
                        return _vertex ? "return o;" : "return fragOut;";
                    default: return null;
                }
            }

            private List<string> Args(GlslParser.FunctionArgumentListContext a)
            {
                var res = new List<string>();
                var list = a?.assignmentExpression();
                if (list == null) return res;
                foreach (var e in list) res.Add(Render(e));
                return res;
            }

            private static readonly Dictionary<string, string> Relations =
                new(StringComparer.Ordinal)
                {
                    ["lessThan"] = "<",
                    ["lessThanEqual"] = "<=",
                    ["greaterThan"] = ">",
                    ["greaterThanEqual"] = ">=",
                    ["notEqual"] = "!=",
                    ["equal"] = "==",
                };

            private string NamedCall(GlslParser.NamedCallContext nc) =>
                CallByName(nc.IDENT().GetText(), Args(nc.functionArgumentList()));

            /// <summary>
            /// Calls whose arguments are restructured.  Both syntax routes land
            /// here: the grammar has two alternatives for a call, and because a
            /// type name is an identifier in GLSL, <c>texture(s, uv)</c> parses
            /// as the constructor alternative with the type "texture".  A name
            /// that is no built-in type is a call, whatever alternative ANTLR
            /// picked.
            /// </summary>
            private string CallByName(string name, List<string> args)
            {
                if (Relations.TryGetValue(name, out var op) && args.Count == 2)
                    return $"(({args[0]}) {op} ({args[1]}))";

                if (name == "texture" && args.Count == 2)
                    return Sample(args[0], args[1]);

                if (name == "textureLod" && args.Count == 3)
                    return $"tex2Dlod({args[0]}, float4(({args[1]}).xy, ({args[2]}), 0))";

                if (name == "textureLod" && args.Count == 2)
                    return $"tex2Dlod({args[0]}, {args[1]})";

                // Everything else keeps its text; single-word renames (mix ->
                // lerp, mod -> fmod, ...) happen per token in TokenText, which
                // is also where the scanner's word-boundary rules land.
                return null;
            }

            /// <summary>
            /// <c>texture(s, uv)</c> — the sampler declaration decides whether
            /// this is a 2D, 3D or cube fetch, and only the 2D form narrows the
            /// coordinate to <c>.xy</c>.
            /// </summary>
            private string Sample(string sampler, string coord)
            {
                string fn = _s3d.Contains(sampler) ? "tex3D"
                          : _cube.Contains(sampler) ? "texCUBE"
                          : "tex2D";
                if (fn != "tex2D") return $"{fn}({sampler}, {coord})";

                // A vector coordinate carries four components in GLSL and two in
                // HLSL; anything else (an already-narrow expression, an
                // arithmetic result) is left as written — the same rule the
                // scanner applies, now decided on the argument node instead of
                // on a character pattern.
                var m = Swizzle.Match(coord);
                if (m.Success && m.Groups[2].Value.Length >= 3 && OnlyXyzw(m.Groups[2].Value))
                    return $"tex2D({sampler}, ({m.Groups[1].Value}).xy)";
                return $"tex2D({sampler}, {coord})";
            }

            private static bool OnlyXyzw(string swz)
            {
                if (swz.Length == 0) return false;
                foreach (char c in swz)
                    if (c != 'x' && c != 'y' && c != 'z' && c != 'w') return false;
                return true;
            }

            private static readonly System.Text.RegularExpressions.Regex Swizzle =
                new(@"^(\w[\w.\[\]]*)\.([xyzw]+)$",
                    System.Text.RegularExpressions.RegexOptions.Compiled);

            private static readonly HashSet<string> VectorCtors = new(StringComparer.Ordinal)
            {
                "float2", "float3", "float4", "int2", "int3", "int4",
                "uint2", "uint3", "uint4", "bool2", "bool3", "bool4",
            };

            private string ConstructorCall(GlslParser.ConstructorCallContext cc)
            {
                string type = Render(cc.typeSpecifierNonArray());
                var args = Args(cc.functionArgumentList());

                // `texture`, `lessThan`, `mix` and friends parse here too: the
                // grammar reads a type name and an identifier the same way.  Only
                // a real type can be constructed.
                if (!BuiltinTypes.Contains(type)) return CallByName(type, args);

                if (args.Count != 1) return null;

                // int(0x1f) — a literal reinterpreted through a constructor is
                // just the literal; the scanner drops the wrapper, so do we.
                // The test is on the argument's own text, not on what it renders
                // to: `int(int(0x1f))` keeps its outer wrapper, because the
                // scanner's rule only ever sees the whole line once.
                var argNode = cc.functionArgumentList()?.assignmentExpression(0);
                string rawArg = argNode == null ? args[0] : Span(argNode).Trim();
                if (type == "int" && Hex.Match(rawArg).Success) return args[0];

                // A one-argument vector constructor is legal GLSL and rejected by
                // the Cg front end; spell the cast instead.
                if (VectorCtors.Contains(type)) return $"({type})({args[0]})";
                return null;
            }

            private static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
            {
                "void", "float", "double", "half", "int", "uint", "bool",
                "float2", "float3", "float4", "int2", "int3", "int4",
                "uint2", "uint3", "uint4", "bool2", "bool3", "bool4",
                "float2x2", "float2x3", "float2x4", "float3x2", "float3x3", "float3x4",
                "float4x2", "float4x3", "float4x4",
                "sampler1D", "sampler2D", "sampler3D", "samplerCUBE", "sampler2DArray",
            };

            private static readonly System.Text.RegularExpressions.Regex Hex =
                new(@"^0x[0-9a-fA-F]+u?$", System.Text.RegularExpressions.RegexOptions.Compiled);

            /// <summary>
            /// <c>unity_MatrixVP[1]</c> — a matrix subscript in GLSL is a column
            /// access in HLSL, spelled by name.  Only integer-literal subscripts
            /// on the built-in matrices are rewritten; a local array keeps its
            /// brackets.
            /// </summary>
            private string MatrixIndex(GlslParser.IndexExprContext ix)
            {
                string literal = ix.expression()?.GetText();
                if (literal == null || !int.TryParse(literal, out int col)) return null;
                if (col < 0 || col > 3) return null;

                string baseText = Render(ix.postfixExpression());
                // `glstate_matrix_*` is Unity's whole family of built-in
                // matrices; the scanner matches that prefix, so do we.
                if (!Matrices.Contains(baseText)
                    && !baseText.StartsWith("glstate_matrix_", StringComparison.Ordinal))
                    return null;
                return $"{baseText}._m0{col}_m1{col}_m2{col}_m3{col}";
            }

            private static readonly HashSet<string> Matrices = new(StringComparer.Ordinal)
            {
                "unity_ObjectToWorld", "unity_WorldToObject", "unity_MatrixV",
                "unity_MatrixP", "unity_MatrixVP",
            };

            private string TokenText(IToken t)
            {
                switch (t.Type)
                {
                    case GlslLexer.VEC2: return "float2";
                    case GlslLexer.VEC3: return "float3";
                    case GlslLexer.VEC4: return "float4";
                    case GlslLexer.IVEC2: return "int2";
                    case GlslLexer.IVEC3: return "int3";
                    case GlslLexer.IVEC4: return "int4";
                    case GlslLexer.BVEC2: return "bool2";
                    case GlslLexer.BVEC3: return "bool3";
                    case GlslLexer.BVEC4: return "bool4";
                    case GlslLexer.UVEC2: return "uint2";
                    case GlslLexer.UVEC3: return "uint3";
                    case GlslLexer.UVEC4: return "uint4";
                    case GlslLexer.MAT3: return "float3x3";
                    case GlslLexer.MAT4: return "float4x4";
                    case GlslLexer.SAMPLERCUBE: return "samplerCUBE";
                    case GlslLexer.IDENT: return Identifier(t.Text);
                    default: return null;
                }
            }

            /// <summary>
            /// Word-level renames, the same ones the scanner applies to a whole
            /// line: GLSL spellings HLSL does not have, plus the stage naming
            /// (in_/vs_/gl_ built-ins, the matrix prefix, SV_Target).
            /// </summary>
            private string Identifier(string s)
            {
                if (_vertex)
                {
                    if (s == "gl_Position") return "o.pos";
                    if (s == "gl_VertexID") return "v.sv_vertexid";
                    if (s == "gl_InstanceID") return "v.sv_instanceid";
                    if (s.StartsWith("in_", StringComparison.Ordinal)) return "v." + s;
                    if (s.StartsWith("vs_", StringComparison.Ordinal)) return "o." + s[3..].ToLowerInvariant();
                }
                else
                {
                    if (s == "gl_FragCoord") return "i.fragcoord";
                    if (s.StartsWith("vs_", StringComparison.Ordinal)) return "i." + s[3..].ToLowerInvariant();
                    if (s.StartsWith("SV_Target", StringComparison.Ordinal)) return "fragOut";
                }
                if (s.StartsWith("hlslcc_mtx4x4", StringComparison.Ordinal))
                    return s["hlslcc_mtx4x4".Length..];
                return Renamed.TryGetValue(s, out var t) ? t : null;
            }

            private static readonly Dictionary<string, string> Renamed =
                new(StringComparer.Ordinal)
                {
                    ["mix"] = "lerp",
                    ["fract"] = "frac",
                    ["mod"] = "fmod",
                    ["dFdx"] = "ddx",
                    ["dFdy"] = "ddy",
                    ["roundEven"] = "round",
                    ["inversesqrt"] = "rsqrt",
                    ["intBitsToFloat"] = "asfloat",
                    ["floatBitsToInt"] = "asint",
                    ["floatBitsToUint"] = "asuint",
                    ["uintBitsToFloat"] = "asfloat",
                };
        }
    }
}
