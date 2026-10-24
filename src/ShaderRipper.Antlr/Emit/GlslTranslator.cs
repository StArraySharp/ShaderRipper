using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// GLSL -> HLSL, read with <c>Glsl.g4</c>.
    ///
    /// Unity stores the HLSLcc output for its GL platforms, so the input is a
    /// register-style transliteration already: one statement per line,
    /// <c>u_xlatN</c> temporaries, stage built-ins named <c>in_*</c>,
    /// <c>vs_*</c>, <c>gl_*</c>.  That shape is what makes a straight
    /// transliteration faithful — the work is renaming and choosing the right
    /// sampling form, not recovering intent.
    ///
    /// The output keeps the same register-level, compilable style as the DXBC
    /// side: <c>float4 u_xlat0</c>, <c>Texture2D</c>/<c>SamplerState</c>,
    /// <c>.Sample()</c>.
    /// </summary>
    internal sealed class GlslTranslator
    {
        public sealed class Result
        {
            public string Hlsl = "";
            public int ParseErrors;
            public int Todo;
            public List<string> Missing = new();
        }

        private readonly string _src;
        private readonly bool _vertex;
        private readonly StringBuilder _sb = new();
        private readonly Result _r = new();
        private readonly List<(string type, string name)> _uniforms = new();
        private readonly HashSet<string> _samplers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _samplerKind = new(StringComparer.Ordinal);
        private readonly List<(string type, string name)> _ins = new();
        private readonly List<(string type, string name)> _outs = new();
        private readonly List<string> _locals = new();
        private GlslParser.TranslationUnitContext _unit;

        private GlslTranslator(string src, bool vertex) { _src = src; _vertex = vertex; }

        /// <summary>
        /// Translate a whole payload: both stages into one HLSL block, which is
        /// what a ShaderLab Pass holds.
        ///
        /// The two stages share a scope in the input (they are separate
        /// translation units guarded by `#ifdef`), so the merge has to deal with
        /// two things: duplicated globals (`u_xlat0` is declared once per stage)
        /// and two entry points.  Globals are emitted once by first occurrence,
        /// and the entry points become `vert` / `frag` — the names the ShaderLab
        /// wrapper's pragmas refer to.
        /// </summary>
        public static Result Translate(string glsl)
        {
            var r = new Result();
            var globals = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var functions = new List<string>();

            foreach (var (stage, text) in Branches(glsl))
            {
                if (text == null) continue;
                bool vertex = stage == "VERTEX";
                var t = new GlslTranslator(text, vertex);
                t.Read();
                r.ParseErrors += t._r.ParseErrors;
                if (t._r.ParseErrors > 0) continue;
                r.Todo += t._r.Todo;
                r.Missing.AddRange(t._r.Missing);

                var (g, fn) = t.Split();
                foreach (var line in g)
                {
                    string key = line.Trim();
                    if (key.Length == 0 || !seen.Add(key)) continue;
                    globals.Add(line);
                }
                functions.Add(fn);
            }

            var sb = new StringBuilder();
            sb.Append("// translated from GLSL by ShaderRipper.Antlr\n");
            sb.Append("// grammar -> statements -> HLSL, register level\n\n");
            foreach (var line in globals) sb.Append(line).Append('\n');
            sb.Append('\n');
            foreach (var fn in functions) sb.Append(fn).Append('\n');
            r.Hlsl = sb.ToString();
            return r;
        }

        /// <summary>Separate the global declarations from the entry point.</summary>
        private (List<string> globals, string function) Split()
        {
            string text = Emit();
            var lines = text.Split('\n').ToList();
            int at = lines.FindIndex(l => l.StartsWith("float4 main()", StringComparison.Ordinal));
            if (at < 0) return (lines, "");

            var globals = lines.Take(at).ToList();
            var fn = lines.Skip(at).ToList();
            fn[0] = fn[0].Replace("main()", _vertex ? "vert()" : "frag()");
            return (globals, string.Join('\n', fn));
        }

        /// <summary>
        /// Split `#ifdef VERTEX` / `#ifdef FRAGMENT`.  The preprocessor is
        /// deliberately not modelled by the grammar (a directive is one opaque
        /// line), so the branch boundaries are the caller's business — the same
        /// split the stored payload itself uses.
        /// </summary>
        private static IEnumerable<(string stage, string text)> Branches(string glsl)
        {
            foreach (var stage in new[] { "VERTEX", "FRAGMENT" })
                yield return (stage, Branch(glsl, stage));
        }

        private static string Branch(string glsl, string stage)
        {
            if (glsl == null) return null;
            string head = "#ifdef " + stage;
            int at = glsl.IndexOf(head, StringComparison.Ordinal);
            if (at < 0) return null;
            at += head.Length;
            int depth = 0;
            for (int i = at; i < glsl.Length;)
            {
                int eol = glsl.IndexOf('\n', i);
                if (eol < 0) eol = glsl.Length;
                string line = glsl[i..eol].Trim();
                if (line.StartsWith("#if")) depth++;
                else if (line.StartsWith("#endif"))
                {
                    if (depth == 0) return glsl[at..i];
                    depth--;
                }
                i = eol + 1;
            }
            return glsl[at..];
        }

        // ------------------------------------------------------------------ read

        private void Read()
        {
            var lexer = new GlslLexer(new AntlrInputStream(_src));
            lexer.RemoveErrorListeners();
            var parser = new GlslParser(new CommonTokenStream(lexer));
            var errors = new Counter();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errors);
            _unit = parser.translationUnit();
            _r.ParseErrors = errors.Count;

            foreach (var item in _unit.externalItem())
            {
                var decl = item.declaration();
                if (decl is GlslParser.VariableDeclContext v) { ReadVariables(v); continue; }
                var fn = item.functionDefinition();
                if (fn?.IDENT()?.GetText() == "main") _main = fn;
            }
        }

        private GlslParser.FunctionDefinitionContext _main;

        private void ReadVariables(GlslParser.VariableDeclContext v)
        {
            var type = v.fullySpecifiedType();
            string storage = StorageOf(type);
            string typeName = TypeText(type);

            foreach (var d in v.declaratorList().declarator())
            {
                string declared = d.IDENT().GetText();
                string name = Identifier(declared) ?? declared;

                // HLSLcc packs a matrix as `vec4 hlslcc_mtx4x4<name>[4]`; the
                // prefix is a naming convention, and the array is the matrix.
                bool matrix = declared.StartsWith("hlslcc_mtx4x4", StringComparison.Ordinal);
                string text = matrix ? "float4" : MapType(typeName);

                // The array suffix belongs to the declarator (`name[4]`).
                string arr = "";
                string raw = d.GetText();
                int br = raw.IndexOf('[');
                if (br >= 0)
                {
                    int close = raw.IndexOf(']', br);
                    if (close > br) arr = raw[br..(close + 1)];
                }

                if (storage == "uniform") _uniforms.Add((text, name + arr));
                else if (storage == "in" && _vertex) _ins.Add((text, name + arr));
                else if (storage == "out" || (storage == "in" && !_vertex)) _outs.Add((text, name + arr));
                else _locals.Add(text + " " + name + arr);
            }
        }

        private static string StorageOf(GlslParser.FullySpecifiedTypeContext t)
        {
            foreach (var q in t.typeQualifier())
            {
                var s = q.storageQualifier()?.GetText();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return "";
        }

        private static string TypeText(GlslParser.FullySpecifiedTypeContext t) =>
            t.typeSpecifier()?.GetText() ?? "float";

        private sealed class Counter : BaseErrorListener
        {
            public int Count;
            public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                             IToken offendingSymbol, int line, int charPositionInLine,
                                             string msg, RecognitionException e) => Count++;
        }

        // ------------------------------------------------------------------ emit

        private string Emit()
        {
            _sb.Append("// ---- ").Append(_vertex ? "VERTEX" : "FRAGMENT").Append(" ----\n");

            var samplers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (type, name) in _uniforms)
            {
                if (!type.StartsWith("sampler", StringComparison.Ordinal)) continue;
                samplers.Add(name);
                _samplers.Add(name);
                _samplerKind[name] = type;
                _samplerKind[name] = type;
                _samplers.Add(name);
                string tex = type switch
                {
                    "sampler3D" => "Texture3D",
                    "samplerCube" => "TextureCube",
                    "sampler2DArray" => "Texture2DArray",
                    _ => "Texture2D",
                };
                _sb.Append(tex).Append(' ').Append(name).Append(";\n");
                _sb.Append("SamplerState ").Append(name).Append("_sampler;\n");
            }
            foreach (var (type, name) in _uniforms)
            {
                if (type.StartsWith("sampler", StringComparison.Ordinal)) continue;
                _sb.Append(type).Append(' ').Append(name).Append(";\n");
            }
            foreach (var (type, name) in _ins)
                _sb.Append("static ").Append(type).Append(' ').Append(name).Append(";\n");
            _sb.Append('\n');

            _sb.Append(_vertex ? "float4 main() : SV_Position\n{\n" : "float4 main() : SV_Target\n{\n");

            // Temporaries belong to the function, not the file: a global is
            // implicitly const in HLSL and every assignment to it is X3025.
            foreach (var l in _locals)
                _sb.Append("    ").Append(l).Append(l.Contains('[') ? ";\n" : " = 0.0;\n");
            foreach (var (type, name) in _outs.Distinct())
                _sb.Append("    ").Append(type).Append(' ').Append(name).Append(" = 0.0;\n");
            if (_vertex && !_outs.Any(o => o.name == "o0"))
                _sb.Append("    float4 o0 = 0.0;\n");

            if (_main?.compoundStatement() != null)
                foreach (var st in _main.compoundStatement().statement())
                {
                    string text = Render(st)?.Trim();
                    if (string.IsNullOrEmpty(text)) continue;
                    foreach (var line in text.Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.Length == 0) continue;
                        if (l == "return;") l = _vertex ? "return o0;" : "return " + (_outs.Count > 0 ? _outs[0].name : "o0") + ";";
                        _sb.Append("    ").Append(l).Append('\n');
                    }
                }

            _sb.Append("}\n\n");
            return _sb.ToString();
        }

        private static string MapType(string gl) => gl switch
        {
            "vec2" => "float2", "vec3" => "float3", "vec4" => "float4",
            "ivec2" => "int2", "ivec3" => "int3", "ivec4" => "int4",
            "uvec2" => "uint2", "uvec3" => "uint3", "uvec4" => "uint4",
            "bvec2" => "bool2", "bvec3" => "bool3", "bvec4" => "bool4",
            "mat3" => "float3x3", "mat4" => "float4x4",
            "mat2" => "float2x2",
            _ => gl,
        };

        // ------------------------------------------------------------- rewriting

        private struct Edit
        {
            public int Start, Stop;
            public string Text;
            public Edit(int s, int e, string t) { Start = s; Stop = e; Text = t; }
        }

        /// <summary>
        /// Print a node: the original bytes, with only the rewritten children
        /// substituted.  That is what keeps the transliteration honest — an
        /// expression the rewriter does not understand comes out exactly as the
        /// compiler wrote it.
        /// </summary>
        private string Render(ParserRuleContext ctx)
        {
            if (ctx == null) return "";
            string custom = Custom(ctx);
            if (custom != null) return custom;

            var edits = new List<Edit>();
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                var child = ctx.GetChild(i);
                if (child is ParserRuleContext rc && rc.Start != null && rc.Stop != null)
                {
                    string t = Render(rc);
                    string orig = _src.Substring(rc.Start.StartIndex, rc.Stop.StopIndex - rc.Start.StartIndex + 1);
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
            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            foreach (var e in edits)
            {
                int s = e.Start - start, len = e.Stop - e.Start + 1;
                text = text[..s] + e.Text + text[(s + len)..];
            }
            return text;
        }

        private string Custom(ParserRuleContext ctx)
        {
            switch (ctx)
            {
                case GlslParser.NamedCallContext nc: return Call(nc.IDENT().GetText(), Args(nc.functionArgumentList()));
                case GlslParser.ConstructorCallContext cc:
                    return Ctor(Render(cc.typeSpecifierNonArray()), Args(cc.functionArgumentList()));
                // GLSL `return;` hands back whatever was written to the outputs;
                // HLSL has to name it, or the compiler rejects the path (X3507).
                case GlslParser.ReturnStmtContext r when r.expression() == null:
                    return "return o0;";
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

        private string Ctor(string type, List<string> args)
        {
            string mapped = MapType(type);
            if (!BuiltinTypes.Contains(mapped)) return Call(type, args);
            if (args.Count == 1 && (mapped.StartsWith("float") || mapped.StartsWith("int")
                                    || mapped.StartsWith("uint") || mapped.StartsWith("bool")))
                return $"({mapped})({args[0]})";
            return null;
        }

        /// <summary>
        /// Calls: sampling and the GLSL spellings HLSL does not have.  Everything
        /// else keeps its name and arguments.
        /// </summary>
        private string Call(string name, List<string> args)
        {
            if (name is "texture" && args.Count == 2 && _samplers.Contains(args[0]))
            {
                string sampler = args[0];
                string coord = args[1];
                // GLSL passes a vec4 coordinate; .Sample takes a float2.
                // A 2D fetch narrows the vec4 coordinate to .xy; a cube or 3D
                // fetch takes three components and must keep them.
                string kind = _samplerKind.TryGetValue(sampler, out var k) ? k : "sampler2D";
                if (kind is "sampler2D" or "sampler2DArray")
                {
                    var m = Swizzle.Match(coord);
                    coord = m.Success ? $"({m.Groups[1].Value}).xy" : $"({coord}).xy";
                }
                return $"{sampler}.Sample({sampler}_sampler, {coord})";
            }
            if (Relations.TryGetValue(name, out var op) && args.Count == 2)
                return $"(({args[0]}) {op} ({args[1]}))";

            if (name == "textureLod" && args.Count == 3)
                return $"{args[0]}.SampleLevel({args[0]}_sampler, {args[1]}, {args[2]})";

            return null;
        }

        private static readonly System.Text.RegularExpressions.Regex Swizzle =
            new(@"^(\w[\w.\[\]]*)\.([xyzw]+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

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
                case GlslLexer.UVEC2: return "uint2";
                case GlslLexer.UVEC3: return "uint3";
                case GlslLexer.UVEC4: return "uint4";
                case GlslLexer.BVEC2: return "bool2";
                case GlslLexer.BVEC3: return "bool3";
                case GlslLexer.BVEC4: return "bool4";
                case GlslLexer.MAT2: return "float2x2";
                case GlslLexer.MAT3: return "float3x3";
                case GlslLexer.MAT4: return "float4x4";
                case GlslLexer.IDENT: return Identifier(t.Text);
                default: return null;
            }
        }

        private string Identifier(string s)
        {
            if (_vertex)
            {
                if (s == "gl_Position") return "o0";
                if (s.StartsWith("in_", StringComparison.Ordinal)) return s;
                if (s.StartsWith("vs_", StringComparison.Ordinal)) return s[3..].ToLowerInvariant();
            }
            else
            {
                if (s == "gl_FragCoord") return "sv_position";
                if (s.StartsWith("vs_", StringComparison.Ordinal)) return s[3..].ToLowerInvariant();
                if (s.StartsWith("SV_Target", StringComparison.Ordinal)) return "o0";
            }
            if (s.StartsWith("hlslcc_mtx4x4", StringComparison.Ordinal)) return s["hlslcc_mtx4x4".Length..];
            return Renamed.TryGetValue(s, out var t) ? t : null;
        }

        private static readonly Dictionary<string, string> Relations = new(StringComparer.Ordinal)
        {
            ["lessThan"] = "<", ["lessThanEqual"] = "<=",
            ["greaterThan"] = ">", ["greaterThanEqual"] = ">=",
            ["equal"] = "==", ["notEqual"] = "!=",
        };

        private static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
        {
            "void", "float", "double", "half", "int", "uint", "bool",
            "float2", "float3", "float4", "int2", "int3", "int4",
            "uint2", "uint3", "uint4", "bool2", "bool3", "bool4",
            "float2x2", "float3x3", "float4x4",
            "sampler1D", "sampler2D", "sampler3D", "samplerCube", "sampler2DArray",
        };

        private static readonly Dictionary<string, string> Renamed = new(StringComparer.Ordinal)
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
