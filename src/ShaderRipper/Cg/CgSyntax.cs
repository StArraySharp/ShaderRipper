using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Shared spelling rules for the two translation dialects.
    ///
    /// Translation is a straight-line transliteration, not a re-optimisation:
    /// instructions are rewritten one for one and nothing is reordered, folded or
    /// regenerated.
    ///
    /// Both producers read a source that describes samplers the Cg way and write
    /// straight into the requested dialect: the DXBC path goes assembly ->
    /// <see cref="CgTranslator"/> -> text, the GLSL path goes source ->
    /// <see cref="GlslToCg"/> -> text.  Each decides the sampler spelling as it
    /// writes, because it already knows the dimension -- <see cref="CgTranslator"/>
    /// reads it to fix the coordinate width, <see cref="GlslToCg"/> has the uniform
    /// declarations in hand.  The helpers here hold that decision in one place so
    /// both paths spell a declaration and a sampling call identically.
    ///
    /// <c>--syntax cg</c> selects the combined-sampler dialect
    /// (<c>sampler2D t; tex2D(t, uv)</c>), which is what the historic output looked
    /// like.  <c>--syntax hlsl</c>, the default, selects the separated form
    /// <c>HLSLPROGRAM</c> wants (<c>Texture2D t; SamplerState sampler_t;
    /// t.Sample(sampler_t, uv)</c>).  Neither is a pass over a finished body: a
    /// name's type never has to be rediscovered from text, and no variant body is
    /// re-read after it is written.
    ///
    /// Emitting directly costs three local deviations, all documented where they
    /// happen and none of them introduced here: an over-wide coordinate swizzle is
    /// truncated to the components the texture type consumes (<c>ClampCoord</c> --
    /// what the hardware does anyway, and what <c>tex2D</c> refuses to accept); a
    /// constant LOD collapses to <c>0</c> (<c>Lod</c>, and the note in
    /// <see cref="CgTranslator"/> -- both producers in this corpus only ever pack
    /// zero); and a raw <c>SamplerState sN;</c> register that nothing samples
    /// through is never declared.
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

        /// <summary>
        /// One texture declaration in the selected dialect.
        ///
        /// <c>hlsl</c> gives the two lines Unity actually binds -- a typed texture
        /// plus its <see cref="SamplerField"/> state, the shape
        /// <c>UNITY_DECLARE_TEX2D</c> expands to.  <c>cg</c> gives the historic
        /// single combined sampler.  Both are emitted with the caller's indent so
        /// a variant body can be assembled without a later rewrite pass.
        /// </summary>
        public static IEnumerable<string> DeclareTexture(string cgType, string name, bool hlsl,
                                                          string indent = "")
        {
            if (!hlsl)
            {
                yield return $"{indent}{cgType} {name};";
                yield break;
            }

            string texType = TexType.TryGetValue(cgType, out var t) ? t : "Texture2D";
            yield return $"{indent}{texType} {name};";
            yield return $"{indent}SamplerState {SamplerField(name)};";
        }

        /// <summary>The HLSL texture type a Cg sampler type maps to.</summary>
        public static string TexTypeOf(string cgType) =>
            TexType.TryGetValue(cgType, out var t) ? t : "Texture2D";

        /// <summary>A plain sample (no explicit LOD) in the selected dialect.</summary>
        public static string Sample(string tex, string coord, bool hlsl) =>
            hlsl ? $"{tex}.Sample({SamplerField(tex)}, {coord})" : $"tex2D({tex}, {coord})";

        /// <summary>An explicit-LOD sample in the selected dialect.</summary>
        public static string SampleLevel(string tex, string coord, string lod, bool hlsl) =>
            hlsl
                ? $"{tex}.SampleLevel({SamplerField(tex)}, {coord}, {lod})"
                : $"tex2Dlod({tex}, float4({coord}, 0, {lod}))";

        /// <summary>A bias sample (DXBC <c>sample_b</c>) in the selected dialect.</summary>
        public static string SampleBias(string tex, string coord, string bias, bool hlsl) =>
            hlsl
                ? $"{tex}.SampleBias({SamplerField(tex)}, {coord}, {bias})"
                : $"tex2Dbias({tex}, float4({coord}, 0, {bias}))";

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
        ///
        /// Both producers call this on each line as they write it, passing the
        /// texture table they already know, so no pass has to re-read a finished
        /// body to rediscover which names are what.
        /// </summary>
        public static string RewriteLine(string line, IReadOnlyDictionary<string, string> textures)
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

        private static string Sample(List<string> a, IReadOnlyDictionary<string, string> textures)
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
        private static string Lod(List<string> a, IReadOnlyDictionary<string, string> textures)
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

    }
}