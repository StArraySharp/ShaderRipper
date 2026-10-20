using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Translates Unity's GLES3/GLSL payloads (HLSLcc output) back into HLSL/Cg.
    ///
    /// The reconstructed ShaderLab is meant to be a CGPROGRAM like every other
    /// artifact this tool produces, and HLSLcc's output is itself a register-style
    /// transliteration (u_xlatN temporaries, one statement per op), so a line-based
    /// rewrite is faithful by construction.
    ///
    /// Input shape (verified against real assets and USC output):
    ///   #ifdef VERTEX            -- one complete program, GLSL ES 3.0
    ///     uniforms / in / out / globals
    ///     void main() { ... }
    ///   #endif
    ///   #ifdef FRAGMENT          -- same shape, "in" = varyings
    ///   #endif
    ///
    /// Naming conventions carry the semantics: attributes are in_POSITION0,
    /// in_TEXCOORD0 ...; varyings are vs_COLOR0, vs_TEXCOORD0 ...; the fragment
    /// output is declared "layout(location = 0) out vec4 SV_Target0".  Those names
    /// are how everything maps onto appdata/v2f semantics here.
    /// </summary>
    internal static class GlslToCg
    {
        public sealed class Result
        {
            public string Cg = "";
            public List<string> Notes = new();
        }

        private sealed class InOut
        {
            public string Name;
            public string CgType = "float4";
            public string Semantic;
        }

        private sealed class Branch
        {
            public List<(string decl, string name)> Uniforms = new();
            public List<InOut> Ins = new();
            public List<InOut> Varyings = new();
            public List<string> Globals = new();
            public List<string> Locals = new();
            public List<string> Body = new();
            public string BodyText = "";
            public string OutputType;
            public string OutputSemantic;
            public bool UsesFragCoord;
        }

        /// <summary>
        /// Names Unity's CGPROGRAM front-end injects via UnityShaderVariables.cginc
        /// (included through UnityCG.cginc): declaring our own copy is a
        /// redefinition error, and -- worse -- renaming them (the old xlat_ trick)
        /// leaves them permanently unbound: a zero matrix collapsed every vertex to
        /// the origin and the whole mesh simply vanished in-game.  The right move is
        /// to reference the canonical name and let the cginc supply the declaration.
        /// </summary>
        private static readonly HashSet<string> BuiltinUniforms = new(StringComparer.Ordinal)
        {
            "glstate_matrix_projection", "glstate_matrix_modelview0", "glstate_matrix_mvp",
            "glstate_matrix_invtrans_modelview0", "glstate_matrix_transpose_modelview0",
            "unity_ObjectToWorld", "unity_WorldToObject", "unity_MatrixVP", "unity_MatrixV",
            "unity_MatrixP",
            "_Time", "_SinTime", "_CosTime", "unity_DeltaTime", "_WorldSpaceCameraPos",
            "_ScreenParams", "_ProjectionParams", "_ZBufferParams", "unity_OrthoParams",
            "unity_SHAr", "unity_SHAg", "unity_SHAb", "unity_SHBr", "unity_SHBg", "unity_SHBb",
            "unity_SHC", "_WorldSpaceLightPos0", "unity_AmbientSky", "unity_AmbientEquator",
            "unity_AmbientGround", "unity_IndirectSpecColor", "unity_OcclusionMaskSelector",
            "unity_WorldTransformParams", "unity_LightShadowBias",
        };

        private static readonly HashSet<string> SkipLines = new(StringComparer.Ordinal)
        {
            "#version", "precision highp float;", "precision highp int;", "precision mediump float;",
            "#define HLSLCC_ENABLE_UNIFORM_BUFFERS 1", "#if HLSLCC_ENABLE_UNIFORM_BUFFERS",
            "#define UNITY_UNIFORM", "#else", "#endif",
            "#define UNITY_SUPPORTS_UNIFORM_LOCATION 1", "#if UNITY_SUPPORTS_UNIFORM_LOCATION",
            "#define UNITY_LOCATION(x) layout(location = x)", "#define UNITY_BINDING(x) layout(binding = x, std140)",
            "#define UNITY_LOCATION(x)", "#define UNITY_BINDING(x) layout(std140)",
        };

        private static readonly (Regex re, string rep)[] Rules =
        {
            (new Regex(@"\bintBitsToFloat\s*\(\s*int\s*\(([^()]*)\)\s*\)"), "asfloat($1)"),
            (new Regex(@"\bintBitsToFloat\b"), "asfloat"),
            (new Regex(@"\bfloatBitsToInt\b"), "asint"),
            (new Regex(@"\bfloatBitsToUint\b"), "asuint"),
            (new Regex(@"\buintBitsToFloat\b"), "asfloat"),
            (new Regex(@"\binversesqrt\b"), "rsqrt"),
            (new Regex(@"\bfract\b"), "frac"),
            (new Regex(@"\bmix\b"), "lerp"),
            (new Regex(@"\bmod\s*\("), "fmod("),
            (new Regex(@"\btextureLod\s*\("), "tex2Dlod("),
            (new Regex(@"\btexture\s*\("), "tex2D("),
            (new Regex(@"\bvec2\b"), "float2"),
            (new Regex(@"\bvec3\b"), "float3"),
            (new Regex(@"\bvec4\b"), "float4"),
            (new Regex(@"\bivec2\b"), "int2"),
            (new Regex(@"\bivec3\b"), "int3"),
            (new Regex(@"\bivec4\b"), "int4"),
            (new Regex(@"\bbvec2\b"), "bool2"),
            (new Regex(@"\bbvec3\b"), "bool3"),
            (new Regex(@"\bbvec4\b"), "bool4"),
            (new Regex(@"\buvec2\b"), "uint2"),
            (new Regex(@"\buvec3\b"), "uint3"),
            (new Regex(@"\buvec4\b"), "uint4"),
            (new Regex(@"\bmat3\b"), "float3x3"),
            (new Regex(@"\bmat4\b"), "float4x4"),
            (new Regex(@"\bsamplerCube\b"), "samplerCUBE"),
        };

        /// <summary>--asm：在每条翻译出的 Cg 行上方插入对应的原始 GLSL 行注释。
        /// GLSL 本身就是 Unity 存下的"原始汇编"，这就是它的逐行反汇编注释。</summary>
        public static bool AnnotateSource;

        public static Result Translate(string glsl)
        {
            var notes = new List<string>();
            var pv = ParseBranch(ExtractBranch(glsl, "VERTEX"), true);
            var pf = ParseBranch(ExtractBranch(glsl, "FRAGMENT"), false);
            if (pv == null && pf == null)
            {
                notes.Add("no VERTEX/FRAGMENT branches found");
                return new Result { Notes = notes };
            }

            var uniforms = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (decl, name) in (pv?.Uniforms ?? new List<(string, string)>())
                     .Concat(pf?.Uniforms ?? new List<(string, string)>()))
            {
                if (!seen.Add(name)) continue;
                if (BuiltinUniforms.Contains(name)) continue;
                string d = decl;
                if (name.StartsWith("_") && d.StartsWith("int "))
                    d = "float " + name + ";";
                uniforms.Add(d);
            }

            var globals = new List<string>();
            var gseen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in (pv?.Globals ?? new List<string>())
                     .Concat(pf?.Globals ?? new List<string>()))
                if (gseen.Add(g)) globals.Add(g);
            foreach (var g in globals)
            {
                if (pv != null && pv.Globals.Contains(g)) pv.Locals.Insert(0, g);
                if (pf != null && pf.Globals.Contains(g)) pf.Locals.Insert(0, g);
            }

            _s3d.Clear(); _cube.Clear();
            foreach (var u in (pv?.Uniforms ?? new List<(string, string)>())
                     .Concat(pf?.Uniforms ?? new List<(string, string)>()))
            {
                var m = Regex.Match(u.decl, @"^(sampler3D|samplerCUBE|samplerCube)\s+(\w+)");
                if (!m.Success) continue;
                if (m.Groups[1].Value == "sampler3D") _s3d.Add(m.Groups[2].Value);
                else _cube.Add(m.Groups[2].Value);
            }

            if (pv != null && pv.BodyText.Length > 0)
                pv.Body = TranslateBody(pv.BodyText, true);
            if (pf != null && pf.BodyText.Length > 0)
                pf.Body = TranslateBody(pf.BodyText, false);

            var v2f = new Dictionary<string, InOut>(StringComparer.Ordinal);
            foreach (var v in pv?.Varyings ?? Enumerable.Empty<InOut>())
                v2f[v.Name] = v;
            foreach (var v in pf?.Varyings ?? Enumerable.Empty<InOut>())
                if (!v2f.ContainsKey(v.Name)) v2f[v.Name] = v;
            if (pf != null && pf.UsesFragCoord && !v2f.ContainsKey("fragcoord"))
                v2f["fragcoord"] = new InOut { Name = "fragcoord", CgType = "float4", Semantic = "SV_Position" };
            if (pv != null && pv.BodyText.Contains("gl_Position") && !v2f.ContainsKey("pos"))
                v2f["pos"] = new InOut { Name = "pos", CgType = "float4", Semantic = "SV_Position" };

            var sb = new StringBuilder();

            sb.AppendLine("struct appdata");
            sb.AppendLine("{");
            foreach (var a in pv?.Ins ?? new List<InOut>())
                sb.AppendLine($"\t{a.CgType} {a.Name} : {a.Semantic};");
            sb.AppendLine("};");
            sb.AppendLine();

            sb.AppendLine("struct v2f");
            sb.AppendLine("{");
            foreach (var v in v2f.Values)
                sb.AppendLine($"\t{v.CgType} {v.Name} : {v.Semantic};");
            sb.AppendLine("};");
            sb.AppendLine();

            foreach (var u in uniforms) sb.AppendLine(u);

            if (pv != null && pv.Body.Count > 0)
            {
                sb.AppendLine("v2f vert (appdata v)");
                sb.AppendLine("{");
                foreach (var g in pv.Locals) sb.AppendLine("\t" + g + ";");
                sb.AppendLine("\tv2f o;");
                foreach (var l in pv.Body) sb.AppendLine("\t" + l);
                if (!pv.Body[^1].Trim().StartsWith("return", StringComparison.Ordinal))
                    sb.AppendLine("\treturn o;");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            if (pf != null && pf.Body.Count > 0)
            {
                string ret = pf.OutputType ?? "float4";
                string sem = pf.OutputSemantic ?? "SV_Target";
                sb.AppendLine($"{ret} frag (v2f i) : {sem}");
                sb.AppendLine("{");
                foreach (var g in pf.Locals) sb.AppendLine("\t" + g + ";");
                sb.AppendLine($"\t{ret} fragOut = ({ret})0;");
                foreach (var l in pf.Body) sb.AppendLine("\t" + l);
                if (!pf.Body[^1].Trim().StartsWith("return", StringComparison.Ordinal))
                    sb.AppendLine("\treturn fragOut;");
                sb.AppendLine("}");
            }

            return new Result { Cg = sb.ToString(), Notes = notes };
        }

        private static string ExtractBranch(string glsl, string stage)
        {
            if (glsl == null) return null;
            int at = glsl.IndexOf("#ifdef " + stage, StringComparison.Ordinal);
            if (at < 0) return null;
            at += ("#ifdef " + stage).Length;
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

        private static Branch ParseBranch(string src, bool isVertex)
        {
            if (src == null) return null;
            var res = new Branch();
            var varyings = new Dictionary<string, InOut>(StringComparer.Ordinal);
            var lines = src.Replace("\r\n", "\n").Split('\n');

            int mainAt = Array.FindIndex(lines, l => l.TrimStart().StartsWith("void main()"));
            string bodyText = "";
            if (mainAt >= 0)
            {
                int open = Array.FindIndex(lines, mainAt, l => l.TrimStart().StartsWith("{"));
                int depth = 0, close = -1;
                for (int i = open; i < lines.Length; i++)
                {
                    string t = lines[i];
                    depth += t.Count(c => c == '{') - t.Count(c => c == '}');
                    if (depth == 0 && i > open) { close = i; break; }
                }
                var bl = new List<string>();
                for (int i = open + 1; i < (close < 0 ? lines.Length : close); i++)
                    if (lines[i].Trim().Length > 0) bl.Add(lines[i].Trim());
                if (Environment.GetEnvironmentVariable("SHADERRIP_GLSLDBG") == "1")
                    Console.Error.WriteLine($"[glsl] mainAt={mainAt} open={open} close={close} bl={bl.Count}");
                bodyText = string.Join("\n", bl);
                lines = lines[..mainAt];
            }

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || SkipLines.Contains(line)) continue;
                if (line.StartsWith("#") || line.StartsWith("}") || line.StartsWith("{")) continue;

                string l = Regex.Replace(StripQual(line), @"^layout\s*\([^)]*\)\s*", "");

                var ms = Regex.Match(l, @"^(?:\w+\([^)]*\)\s*)?uniform\s+(sampler\w+)\s+(\w+)\s*;");
                if (ms.Success)
                {
                    string st = ms.Groups[1].Value == "samplerCube" ? "samplerCUBE" : ms.Groups[1].Value;
                    res.Uniforms.Add(($"{st} {ms.Groups[2].Value};", ms.Groups[2].Value));
                    continue;
                }
                ms = Regex.Match(l, @"^uniform\s+(.+?);\s*$");
                if (ms.Success)
                {
                    string d = MapDecl(ms.Groups[1].Value);
                    var nm = Regex.Match(d, @"(\w+)(\[.*\])?\s*$");
                    res.Uniforms.Add((d + ";", nm.Success ? nm.Groups[1].Value : d));
                    continue;
                }
                ms = Regex.Match(l, @"^in\s+(.+?)\s+(in_\w+)\s*;");
                if (ms.Success && isVertex)
                {
                    string full = ms.Groups[2].Value;
                    string sem = full[3..];
                    sem = Regex.Replace(sem, @"^(POSITION|NORMAL|TANGENT)\d+$", "$1",
                                        RegexOptions.IgnoreCase);
                    res.Ins.Add(new InOut
                    {
                        Name = full,
                        CgType = MapType(ms.Groups[1].Value),
                        Semantic = sem,
                    });
                    continue;
                }
                ms = Regex.Match(l, @"^(in|out)\s+(.+?)\s+(vs_\w+)\s*;");
                if (ms.Success)
                {
                    string member = ms.Groups[3].Value[3..].ToLowerInvariant();
                    if (!varyings.TryGetValue(member, out var io))
                    {
                        io = new InOut
                        {
                            Name = member,
                            CgType = MapType(ms.Groups[2].Value),
                            Semantic = ms.Groups[3].Value[3..],
                        };
                        varyings[member] = io;
                    }
                    if (isVertex && ms.Groups[1].Value == "out") io.Semantic ??= ms.Groups[3].Value[3..];
                    continue;
                }
                ms = Regex.Match(l, @"^(?:const\s+)?(vec[234]|ivec[234]|bvec[234]|uvec[234]|float|half|int|uint|bool)\s+(\w+)\s*(\[[^\]]*\])\s*(=.*)?;$");
                if (ms.Success)
                {
                    res.Globals.Add(MapDecl($"{ms.Groups[1].Value} {ms.Groups[2].Value}{ms.Groups[3].Value}"));
                    continue;
                }
                ms = Regex.Match(l, @"^out\s+(.+?)\s+(SV_Target\d*)\s*;");
                if (ms.Success)
                {
                    res.OutputType = MapType(ms.Groups[1].Value);
                    res.OutputSemantic = ms.Groups[2].Value;
                    continue;
                }
                ms = Regex.Match(l, @"^(.+?)\s+(\w+)\s*;\s*$");
                if (ms.Success)
                {
                    string ty = ms.Groups[1].Value;
                    foreach (var (re, rep) in Rules) ty = re.Replace(ty, rep);
                    string name = ms.Groups[2].Value;
                    if (name.StartsWith("vs_"))
                    {
                        string member = name[3..].ToLowerInvariant();
                        if (!varyings.ContainsKey(member))
                            varyings[member] = new InOut
                            {
                                Name = member,
                                CgType = ty,
                                Semantic = name[3..],
                            };
                    }
                    else
                        res.Locals.Add($"{ty} {name}");
                    continue;
                }
            }

            if (Environment.GetEnvironmentVariable("SHADERRIP_GLSLDBG") == "1")
                Console.Error.WriteLine($"[glsl] vertex={isVertex} lines={lines.Length} "
                    + $"uniforms={res.Uniforms.Count} ins={res.Ins.Count} varyings={varyings.Count} "
                    + $"locals={res.Locals.Count} body={res.Body.Count} out={res.OutputSemantic}");

            res.Varyings.AddRange(varyings.Values);
            if (!isVertex) res.Ins.Clear();

            if (bodyText.Length > 0)
            {
                if (bodyText.Contains("gl_FragCoord")) res.UsesFragCoord = true;
                res.BodyText = bodyText;
            }
            return res;
        }

        private static string StripQual(string line) =>
            Regex.Replace(line, @"\b(highp|mediump|lowp)\b\s*", "").Trim();

        private static string MapType(string gl) => gl switch
        {
            "vec2" => "float2", "vec3" => "float3", "vec4" => "float4",
            "ivec2" => "int2", "ivec3" => "int3", "ivec4" => "int4",
            "bvec2" => "bool2", "bvec3" => "bool3", "bvec4" => "bool4",
            "uvec2" => "uint2", "uvec3" => "uint3", "uvec4" => "uint4",
            "mat3" => "float3x3", "mat4" => "float4x4",
            _ => gl,
        };

        /// <summary>Map the inside of a declaration ("vec4 _ClipRect" / "vec4 hlslcc_mtx4x4X[4]").</summary>
        private static string MapDecl(string decl)
        {
            decl = decl.Trim();
            var m = Regex.Match(decl, @"^(.*?\s)?(vec[234]|ivec[234]|bvec[234]|uvec[234]|mat[34])\s+(\w+)(\[.*\])?\s*$");
            if (!m.Success) return decl;
            string quals = m.Groups[1].Value.Trim();
            string ty = MapType(m.Groups[2].Value);
            string name = m.Groups[3].Value;
            string arr = m.Groups[4].Value;
            if (name.StartsWith("hlslcc_mtx4x4"))
                return $"{(quals.Length > 0 ? quals + " " : "")}float4 {name["hlslcc_mtx4x4".Length..]}{arr}";
            return $"{(quals.Length > 0 ? quals + " " : "")}{ty} {name}{arr}";
        }

        private static List<string> TranslateBody(string body, bool isVertex)
        {
            var res = new List<string>();
            foreach (var raw in body.Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length == 0) continue;

                foreach (var (re, rep) in Rules) s = re.Replace(s, rep);
                s = Regex.Replace(s, @"hlslcc_mtx4x4(\w+)", "$1");
                s = Regex.Replace(s,
                    @"\b(unity_ObjectToWorld|unity_WorldToObject|unity_MatrixV|unity_MatrixP|unity_MatrixVP|glstate_matrix_\w+)\[(\d+)\]",
                    "$1._m0$2_m1$2_m2$2_m3$2");
                s = Regex.Replace(s, @"\bint\s*\(\s*(0x[0-9a-fA-F]+u?)\s*\)", "$1");
                s = RewriteVecCtors(s);

                s = RewriteRelational(s);
                s = RewriteTexLod(s);
                foreach (var n in _s3d)
                    s = Regex.Replace(s, @"tex2D\(" + Regex.Escape(n) + @"\b", "tex3D(" + n);
                foreach (var n in _cube)
                    s = Regex.Replace(s, @"tex2D\(" + Regex.Escape(n) + @"\b", "texCUBE(" + n);
                string excl = string.Concat(_s3d.Concat(_cube)
                    .Select(n => Regex.Escape(n) + "|"));
                s = Regex.Replace(s,
                    @"tex2D\((?!" + excl + @")([^,()]*(?:\([^()]*\))?),\s*(\w[\w.\[\]]*)\.xyzw*\)",
                    "tex2D($1, ($2).xy)");

                if (isVertex)
                {
                    s = Regex.Replace(s, @"\bin_(\w+)", "v.in_$1");
                    s = Regex.Replace(s, @"\bvs_(\w+)", m => "o." + m.Groups[1].Value.ToLowerInvariant());
                    s = s.Replace("gl_Position", "o.pos");
                    if (s == "return;") s = "return o;";
                }
                else
                {
                    s = Regex.Replace(s, @"\bvs_(\w+)", m => "i." + m.Groups[1].Value.ToLowerInvariant());
                    s = s.Replace("gl_FragCoord", "i.fragcoord");
                    s = Regex.Replace(s, @"\bSV_Target\d*\b", "fragOut");
                    if (s == "return;") s = "return fragOut;";
                }
                res.Add(s);
                if (AnnotateSource && s != raw?.Trim())
                    res.Insert(res.Count - 1, "// " + (raw?.Trim() ?? ""));
            }
            return res;
        }

        private static readonly Regex RelCall = new Regex(
            @"\b(lessThanEqual|greaterThanEqual|notEqual|lessThan|greaterThan|equal)\s*\(");

        private static readonly Regex VecCtorCall = new Regex(
            @"\b(float2|float3|float4|int2|int3|int4|uint2|uint3|uint4|bool2|bool3|bool4)\s*\(");

        /// <summary>
        /// 整数/布尔向量构造只有单参时（GLSL 允许，Cg 前端拒绝），改写成显式 cast：
        /// int2(uint2(x)) → (int2)(uint2(x))。多参构造保持原样。
        /// </summary>
        private static string RewriteVecCtors(string s)
        {
            int pos = 0;
            while (true)
            {
                var m = VecCtorCall.Match(s, pos);
                if (!m.Success) return s;
                int open = m.Index + m.Length - 1, depth = 0, close = -1;
                for (int i = open; i < s.Length; i++)
                {
                    if (s[i] == '(') depth++;
                    else if (s[i] == ')' && --depth == 0) { close = i; break; }
                }
                if (close < 0) return s;
                var parts = SplitArgs(s.Substring(open + 1, close - open - 1));
                if (parts.Count != 1) { pos = close + 1; continue; }
                s = s.Substring(0, m.Index)
                   + $"({m.Groups[1].Value})({parts[0].Trim()})"
                   + s.Substring(close + 1);
                pos = m.Index + 1;
            }
        }

        /// <summary>本 shader 的 3D / Cube 采样器名单（每次 Translate 刷新）。</summary>
        private static readonly HashSet<string> _s3d = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _cube = new(StringComparer.Ordinal);

        /// <summary>
        /// tex2Dlod(tex, uv, lod) 三参形态（textureLod 直译而来）→
        /// tex2Dlod(tex, float4((uv).xy, (lod), 0))。已是双参的保持原样。
        /// </summary>
        private static string RewriteTexLod(string s)
        {
            while (true)
            {
                var m = Regex.Match(s, @"\btex2Dlod\s*\(");
                if (!m.Success) return s;
                int open = m.Index + m.Length - 1, depth = 0, close = -1;
                for (int i = open; i < s.Length; i++)
                {
                    if (s[i] == '(') depth++;
                    else if (s[i] == ')' && --depth == 0) { close = i; break; }
                }
                if (close < 0) return s;
                string args = s.Substring(open + 1, close - open - 1);
                var parts = SplitArgs(args);
                if (parts.Count != 3) return s;
                string call = $"tex2Dlod({parts[0].Trim()}, float4(({parts[1].Trim()}).xy, ({parts[2].Trim()}), 0))";
                s = s.Substring(0, m.Index) + call + s.Substring(close + 1);
            }
        }

        /// <summary>顶层逗号拆参（括号配平）。</summary>
        private static List<string> SplitArgs(string args)
        {
            var res = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) { res.Add(args[start..i]); start = i + 1; }
            }
            res.Add(args[start..]);
            return res;
        }

        /// <summary>
        /// lessThan(A,B) 一族的函数式改写。参数可能带嵌套括号
        /// （abs(...)、float4(...)），先配平括号取全参，再按顶层逗号拆两半，
        /// 包上括号交给关系运算符；调用后的 .xy 之类 swizzle 天然可用。
        /// </summary>
        private static string RewriteRelational(string s)
        {
            while (true)
            {
                var m = RelCall.Match(s);
                if (!m.Success) return s;
                int open = m.Index + m.Length - 1, depth = 0, close = -1;
                for (int i = open; i < s.Length; i++)
                {
                    if (s[i] == '(') depth++;
                    else if (s[i] == ')' && --depth == 0) { close = i; break; }
                }
                if (close < 0) return s;
                string args = s.Substring(open + 1, close - open - 1);
                int d2 = 0, split = -1;
                for (int k = 0; k < args.Length; k++)
                {
                    char c = args[k];
                    if (c == '(') d2++;
                    else if (c == ')') d2--;
                    else if (c == ',' && d2 == 0) { split = k; break; }
                }
                if (split < 0) return s;
                string a = args.Substring(0, split).Trim(), b = args.Substring(split + 1).Trim();
                string op = m.Groups[1].Value switch
                {
                    "lessThan" => "<", "lessThanEqual" => "<=",
                    "greaterThan" => ">", "greaterThanEqual" => ">=",
                    "notEqual" => "!=", _ => "==",
                };
                s = s.Substring(0, m.Index) + $"(({a}) {op} ({b}))" + s.Substring(close + 1);
            }
        }
    }
}
