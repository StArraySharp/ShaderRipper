using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Second stage of the two-stage translation to HLSL.
    ///
    /// Both input paths first produce <em>Cg text</em>, and only then is that text
    /// turned into HLSL: the DXBC path goes assembly -> <see cref="CgTranslator"/> ->
    /// Cg, the GLSL path goes source -> <see cref="GlslToCg"/> -> Cg
    /// (<c>GlslToCg.Translate(...).Cg</c>).  That Cg text uses the combined-sampler
    /// dialect (<c>sampler2D t; tex2D(t, uv)</c>), while <c>HLSLPROGRAM</c> wants the
    /// separated form (<c>Texture2D t; SamplerState sampler_t; t.Sample(sampler_t, uv)</c>),
    /// so this class rewrites the finished body once at the writer rather than
    /// teaching each translator to emit HLSL directly.  <c>--syntax cg</c> stops
    /// after the first stage and emits the intermediate Cg verbatim; the default
    /// (<c>hlsl</c>) runs this pass on top of it.
    ///
    /// That the second stage is a pass over text, not a mode of the translators,
    /// has three consequences worth knowing:
    ///   * it runs per finished variant body -- once per GLSL slot
    ///     (<c>ShaderLabWriter</c>:157), once per DXBC pass (:203) -- so a texture's
    ///     dimension is resolved only against declarations inside the preprocessor
    ///     branch that makes them (see <c>ScopeBoundary</c>);
    ///   * it is line-oriented: the body is split on newlines, and a call is matched
    ///     and paren-balanced inside a single one of them.  That is sound only
    ///     because both producers put one statement per line -- <see cref="CgTranslator"/>
    ///     appends statement by statement, and <see cref="GlslToCg"/> maps one input
    ///     line to one output line (<c>TranslateBody</c>); its only multi-line edit,
    ///     <c>CollapseMvp</c>, merges eight whole lines into one and so cannot split
    ///     a statement.  A call whose arguments straddle a newline is not rewritten
    ///     and nothing reports it: the rewrite backs off as soon as the parentheses
    ///     do not close on the line it is looking at (<c>RewriteCode</c>).  hlslcc
    ///     output is in practice strictly one statement per line, so this is an
    ///     assumption about the input rather than a property the pass enforces;
    ///   * it adds no instruction and reorders nothing, though three local
    ///     deviations are deliberate and are documented where they happen: an
    ///     over-wide coordinate swizzle is truncated to the components the texture
    ///     type consumes (<c>ClampCoord</c> -- what the hardware does anyway, and
    ///     what <c>tex2D</c> refuses to accept); the lod packed into a lod-sampling
    ///     call is re-derived from whichever trailing component is not a literal
    ///     zero, because the two producers pack it at different indices
    ///     (<c>Lod</c>); and <c>SamplerState sN;</c> declarations the rewrite left
    ///     unused are dropped (<c>DropUnusedSamplerStates</c>).
    ///
    /// Verified against UnityShaderCompiler 6000.3.10f1 by compiling probes on
    /// d3d11, gles3 and openglcore:
    ///   * HLSLPROGRAM / ENDHLSL is accepted; target 3.0 is enough.
    ///   * HLSLPROGRAM does NOT pre-include HLSLSupport.cginc -- without it
    ///     `fixed`/`half` are "unrecognized identifier" (X3004), so the writer
    ///     emits that include explicitly.
    ///   * <c>SamplerState sampler##tex</c> is Unity's own binding convention, taken
    ///     from HLSLSupport.cginc:461
    ///     (<c>UNITY_DECLARE_TEX2D(tex) Texture2D tex; SamplerState sampler##tex</c>),
    ///     which is what makes the material texture's importer sampler state apply
    ///     at runtime.
    /// </summary>
    internal static class CgSyntax
    {
        public const string Hlsl = "hlsl";
        public const string CgDialect = "cg";

        /// <summary>Anything but an explicit "cg" selects the HLSL form.</summary>
        public static bool IsHlsl(string syntax) =>
            !string.Equals(syntax, CgDialect, StringComparison.OrdinalIgnoreCase);

        /// <summary>Normalises a user-supplied --syntax value.</summary>
        public static string Normalize(string syntax) => IsHlsl(syntax) ? Hlsl : CgDialect;

        private static readonly Dictionary<string, string> TexType = new(StringComparer.Ordinal)
        {
            ["sampler1D"] = "Texture1D",
            ["sampler2D"] = "Texture2D",
            ["sampler3D"] = "Texture3D",
            ["samplerCUBE"] = "TextureCube",
            ["sampler2DArray"] = "Texture2DArray",
        };

        /// <summary>Coordinate components each texture type consumes.</summary>
        private static readonly Dictionary<string, int> TexCoords = new(StringComparer.Ordinal)
        {
            ["Texture1D"] = 1,
            ["Texture2D"] = 2,
            ["Texture3D"] = 3,
            ["TextureCube"] = 3,
            ["Texture2DArray"] = 3,
        };

        /// <summary>
        /// Truncates a coordinate swizzle to what the texture type consumes.
        ///
        /// HLSLcc emits <c>tex2D(_LutTex, u_xlat0.xyz)</c> -- a three-component
        /// coordinate into a 2D sampler.  <c>tex2D</c> rejects that with
        /// "no matching 2 parameter intrinsic function", while <c>.Sample()</c>
        /// takes it silently and drops the third component.  Relying on that
        /// would make the shader compile for the wrong reason: the truncation has
        /// to be written out, which is also exactly what the hardware does.
        /// Only a plain <c>.xyzw</c> suffix is rewritten -- a constructed
        /// coordinate is left alone.
        /// </summary>
        private static string ClampCoord(string coord, string texType)
        {
            if (!TexCoords.TryGetValue(texType, out int need)) return coord;

            var m = Regex.Match(coord, @"^(.*)\.([xyzw]+)$", RegexOptions.Singleline);
            if (!m.Success) return coord;
            string sw = m.Groups[2].Value;
            if (sw.Length <= need) return coord;
            return m.Groups[1].Value + "." + sw[..need];
        }

        /// <summary>
        /// The sampler field Unity binds to a texture: <c>sampler##tex</c> from
        /// HLSLSupport.cginc.  A property name already starts with '_', so
        /// <c>_MainTex</c> yields <c>sampler_MainTex</c> -- exactly what the
        /// macro produces -- while an unresolved <c>t0</c> yields
        /// <c>sampler_t0</c>.
        /// </summary>
        public static string SamplerField(string tex) =>
            tex.StartsWith("_", StringComparison.Ordinal) ? "sampler" + tex : "sampler_" + tex;

        private static readonly Regex DeclRe = new(
            @"^([ \t]*)(sampler\w+)[ \t]+([A-Za-z_]\w*(?:[ \t]*,[ \t]*[A-Za-z_]\w*)*)[ \t]*;([ \t]*//.*)?$");

        private static readonly Regex SamplerStateDecl = new(
            @"^[ \t]*SamplerState[ \t]+(s\d+)[ \t]*;[ \t]*$");

        /// <summary>
        /// Scope boundaries: a declaration's type only holds inside the variant
        /// (or preprocessor branch) that declares it.  The same name can be a
        /// different texture dimension per variant -- a light cookie is a
        /// <c>Texture2D</c> in the forward pass and a <c>TextureCube</c> in the
        /// reflection pass, both spelled <c>_LightTexture0</c> -- so a single
        /// whole-body type table would let the last declaration win and clamp the
        /// first variant's coordinate to the wrong width.
        /// </summary>
        private static readonly Regex ScopeBoundary = new(
            @"^[ \t]*(?://\s*----\s*variant\b.*|#if\b.*|#elif\b.*|#else\b.*)$");

        /// <summary>
        /// Converts one finished CGPROGRAM body into HLSLPROGRAM form.
        ///
        /// Single streaming pass: declarations are recorded as they are met and
        /// each later line is rewritten against the texture types in force at
        /// that point, so a use can never be resolved against a sibling variant's
        /// declaration.
        /// </summary>
        public static string ToHlsl(string body)
        {
            if (string.IsNullOrEmpty(body)) return body;

            var lines = body.Replace("\r\n", "\n").Split('\n');
            var result = new List<string>(lines.Length + 8);
            var textures = new Dictionary<string, string>(StringComparer.Ordinal);
            var samplerDecls = new List<string>();

            foreach (var line in lines)
            {
                if (ScopeBoundary.IsMatch(line)) textures.Clear();

                var m = DeclRe.Match(line);
                if (m.Success && TexType.TryGetValue(m.Groups[2].Value, out var texType))
                {
                    string indent = m.Groups[1].Value;
                    string note = m.Groups[4].Success ? m.Groups[4].Value : "";
                    foreach (var nm in m.Groups[3].Value.Split(',').Select(x => x.Trim())
                                                       .Where(x => x.Length > 0))
                    {
                        textures[nm] = texType;
                        result.Add($"{indent}{texType} {nm};{note}");
                        result.Add($"{indent}SamplerState {SamplerField(nm)};");
                    }
                    continue;
                }
                if (SamplerStateDecl.IsMatch(line)) samplerDecls.Add(SamplerStateDecl.Match(line).Groups[1].Value);
                result.Add(RewriteLine(line, textures));
            }

            string text = string.Join("\n", result);
            return DropUnusedSamplerStates(text, samplerDecls);
        }

        /// <summary>
        /// Rewrites every sampling call on one line.  Balancing parentheses needs
        /// the whole argument list, so a call whose arguments continue onto the
        /// next line cannot be seen from here: parens are counted to the end of
        /// this line, and if they have not closed by then <see cref="RewriteCode"/>
        /// gives up and the call survives untranslated, silently.  That does not
        /// happen with either producer's output -- both keep one statement per
        /// line -- but it is a property of the input, not something enforced here.
        /// A trailing <c>//</c> comment is split off first so text inside it is
        /// never rewritten.
        /// </summary>
        private static string RewriteLine(string line, Dictionary<string, string> textures)
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            string code = c < 0 ? line : line[..c];
            string tail = c < 0 ? "" : line[c..];

            code = RewriteCode(code, "tex2Dlod", (a) => Lod(a, textures));
            code = RewriteCode(code, "texCUBElod", (a) => Lod(a, textures));
            code = RewriteCode(code, "tex3Dlod", (a) => Lod(a, textures));
            code = RewriteCode(code, "tex2D", a => Sample(a, textures));
            code = RewriteCode(code, "tex3D", a => Sample(a, textures));
            code = RewriteCode(code, "texCUBE", a => Sample(a, textures));

            return code + tail;
        }

        private static string Sample(List<string> a, Dictionary<string, string> textures)
        {
            if (a.Count < 2) return null;
            string tex = a[0].Trim();
            string coord = a[1].Trim();
            if (textures.TryGetValue(tex, out var ty)) coord = ClampCoord(coord, ty);
            return $"{tex}.Sample({SamplerField(tex)}, {coord})";
        }

        /// <summary>
        /// <c>tex2Dlod(t, float4(coord, 0, lod))</c> -> <c>t.SampleLevel(sampler_t, coord, lod)</c>.
        ///
        /// The two producers pack the fourth component differently -- the DXBC
        /// path writes <c>float4(coord, 0, lod)</c>, the GLSL path writes
        /// <c>float4(coord, lod, 0)</c> -- so the LOD is whichever trailing
        /// component is not a literal zero, not a fixed index.
        /// </summary>
        private static string Lod(List<string> a, Dictionary<string, string> textures)
        {
            if (a.Count < 2) return null;
            string tex = a[0].Trim();
            string packed = a[1].Trim();

            var inner = new Regex(@"^float4\s*\((.*)\)$", RegexOptions.Singleline).Match(packed);
            var parts = inner.Success ? SplitArgs(inner.Groups[1].Value) : new List<string> { packed };
            if (parts.Count == 0) return null;

            string coord = parts[0].Trim();
            string lod = "0";
            for (int i = parts.Count - 1; i >= 1; i--)
            {
                if (IsLiteral(parts[i])) continue;
                lod = parts[i].Trim();
                break;
            }
            if (textures.TryGetValue(tex, out var ty)) coord = ClampCoord(coord, ty);
            return $"{tex}.SampleLevel({SamplerField(tex)}, {coord}, {lod})";
        }

        private static readonly Regex LiteralRe = new(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?[fFhH]?$");

        private static bool IsLiteral(string s) => LiteralRe.IsMatch(s.Trim());

        /// <summary>Rewrites every <paramref name="fn"/>(...) call in one line.</summary>
        private static string RewriteCode(string code, string fn, Func<List<string>, string> build)
        {
            var re = new Regex(@"\b" + fn + @"\s*\(");
            int from = 0;
            while (true)
            {
                var m = re.Match(code, from);
                if (!m.Success) return code;

                int open = m.Index + m.Length - 1;
                int depth = 0, close = -1;
                for (int i = open; i < code.Length; i++)
                {
                    if (code[i] == '(') depth++;
                    else if (code[i] == ')' && --depth == 0) { close = i; break; }
                }
                if (close < 0) return code;

                string replacement = build(SplitArgs(code.Substring(open + 1, close - open - 1)));
                if (replacement == null) { from = close + 1; continue; }

                code = code[..m.Index] + replacement + code[(close + 1)..];
                from = m.Index + replacement.Length;
            }
        }

        /// <summary>Splits on top-level commas, balancing parentheses.</summary>
        private static List<string> SplitArgs(string args)
        {
            var res = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < args.Length; i++)
            {
                char ch = args[i];
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (ch == ',' && depth == 0)
                {
                    res.Add(args[start..i]);
                    start = i + 1;
                }
            }
            res.Add(args[start..]);
            return res;
        }

        /// <summary>
        /// Removes <c>SamplerState sN;</c> declarations the rewrite left unused.
        /// A raw sampler register has no material binding, so keeping one that
        /// nothing samples through is pure noise.
        /// </summary>
        private static string DropUnusedSamplerStates(string text, List<string> names)
        {
            foreach (var name in names)
            {
                if (Regex.Matches(text, @"\b" + name + @"\b").Count != 1) continue;
                text = Regex.Replace(text, @"(?m)^[ \t]*SamplerState[ \t]+" + name + @"[ \t]*;[ \t]*\r?\n?", "");
            }
            return text;
        }
    }
}