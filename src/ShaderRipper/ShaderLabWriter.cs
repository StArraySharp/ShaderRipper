using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ShaderRipper
{
    /// <summary>Emits Unity ShaderLab from a parsed Shader (<see cref="Shader"/>).</summary>
    internal static class ShaderLabWriter
    {
        private static readonly Dictionary<int, string> PropType = new()
        {
            [0] = "Color", [1] = "Vector", [2] = "float", [3] = "Range",
            [4] = "2D", [5] = "3D", [6] = "Cube", [7] = "2DArray", [8] = "CubeArray",
        };
        private static readonly Dictionary<int, string> PropFlag = new()
        {
            [0x1] = "[HideInInspector]", [0x2] = "[PerRendererData]", [0x4] = "[NoScaleOffset]",
            [0x8] = "[Normal]", [0x10] = "[HDR]", [0x20] = "[Gamma]",
        };
        private static readonly Dictionary<int, string> ZWriteMap = new() { [0] = "Off", [1] = "On" };
        private static readonly Dictionary<int, string> ZTestMap = new()
        { [0] = "Always", [1] = "Never", [2] = "Less", [3] = "Equal", [4] = "LEqual",
          [5] = "Greater", [6] = "NotEqual", [7] = "GEqual", [8] = "Always" };
        private static readonly Dictionary<int, string> CullMap = new() { [0] = "Off", [1] = "Front", [2] = "Back" };
        private static readonly Dictionary<int, string> BlendMap = new()
        { [0] = "Zero", [1] = "One", [2] = "DstColor", [3] = "SrcColor", [4] = "OneMinusDstColor",
          [5] = "SrcAlpha", [6] = "OneMinusSrcColor", [7] = "DstAlpha", [8] = "OneMinusDstAlpha",
          [9] = "SrcAlphaSaturate", [10] = "OneMinusSrcAlpha" };
        private static readonly Dictionary<int, string> BlendOpMap = new()
        { [0] = "Add", [1] = "Sub", [2] = "RevSub", [3] = "Min", [4] = "Max" };
        private static readonly Dictionary<int, string> CompareMap = new()
        { [0] = "Disabled", [1] = "Never", [2] = "Less", [3] = "Equal", [4] = "LEqual",
          [5] = "Greater", [6] = "NotEqual", [7] = "GEqual", [8] = "Always" };
        private static readonly Dictionary<int, string> StencilOpMap = new()
        { [0] = "Keep", [1] = "Zero", [2] = "Replace", [3] = "IncrSat", [4] = "DecrSat",
          [5] = "Invert", [6] = "IncrWrap", [7] = "DecrWrap" };
        private static readonly Dictionary<int, string> FogMap = new()
        { [0] = "Off", [1] = "Linear", [2] = "Exp", [3] = "Exp2" };
        private static readonly Dictionary<int, string> EmptyMap = new();

        private static readonly Dictionary<int, string> ColorMaskMap = new()
        { [0] = "0", [1] = "R", [2] = "G", [3] = "RG", [4] = "B", [5] = "RB", [6] = "GB", [7] = "RGB",
          [8] = "A", [9] = "RA", [10] = "GA", [11] = "RGA", [12] = "BA", [13] = "RBA",
          [14] = "GBA", [15] = "RGBA" };

        private static string N(float f) => f.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        public static string Write(Shader sh, string name,
                                   Dictionary<int, Cg.AsmParser.Program> progs,
                                   Dictionary<int, List<BlobTool.CbInfo>> paramBlobs,
                                   string shaderKey = null,
                                   string mainFormat = "dxbc", int mainPlatform = 4,
                                   Dictionary<int, ExtractedProgram> sources = null,
                                   string syntax = Cg.CgSyntax.Hlsl)
        {
            bool hlsl = Cg.CgSyntax.IsHlsl(syntax);
            string begin = hlsl ? "HLSLPROGRAM" : "CGPROGRAM";
            string end = hlsl ? "ENDHLSL" : "ENDCG";
            var sb = new StringBuilder();
            sb.AppendLine("// Reconstructed ShaderLab by ShaderRipper");
            sb.AppendLine($"// pathID={sh.PathId}  platforms=[{string.Join(",", sh.Platforms)}]");
            if (mainFormat != "dxbc")
                sb.AppendLine($"// main program source: {Platforms.Describe(mainPlatform)} "
                              + "(not DXBC — see Disassembly/ for the original source)");
            sb.AppendLine($"Shader \"{Esc(name)}\"");
            sb.AppendLine("{");

            sb.AppendLine("\tProperties");
            sb.AppendLine("\t{");
            foreach (var p in sh.ParsedForm.Props) sb.AppendLine("\t\t" + PropLine(p));
            sb.AppendLine("\t}");

            // Unity 6 strips the whole pass structure for SRP-managed shaders
            // (Shader Graph, custom passes, ray-tracing) at build time: the
            // serialized object only carries Properties + SubShader tags, and
            // the compressed program blob is an empty placeholder.  Say so
            // instead of shipping a silently empty shell.
            bool noPasses = sh.ParsedForm.SubShaders.All(s => s.Passes.Count == 0);
            if (noPasses && sh.ParsedForm.SubShaders.Count > 0)
                sb.AppendLine("\t// NOTE: this build contains no passes for this shader — Unity 6 "
                    + "strips pass/program data for SRP-managed and ray-tracing shaders at build time.");

            // Which $Globals members are matrices is a property of the shader, not
            // of one pass.  Only the base-light pass of a surface shader records
            // _EnvMatrix as float4x4; the additive pass compiles to a different
            // $Globals layout whose reflection lists two members and cannot say so.
            // Collecting here lets every pass know the span.
            var shaderMatrixRows = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var ssm in sh.ParsedForm.SubShaders)
                foreach (var pm in ssm.Passes)
                    foreach (var spm in pm.SubPrograms)
                        foreach (var cbm in spm.ConstantBuffers)
                        {
                            if (!cbm.IsGlobals) continue;
                            foreach (var mm in cbm.Members)
                            {
                                if (string.IsNullOrEmpty(mm.Name) || !mm.IsMatrix) continue;
                                if (mm.RowCount is < 2 or > 4) continue;
                                shaderMatrixRows.TryGetValue(mm.Name, out int had);
                                if (mm.RowCount > had) shaderMatrixRows[mm.Name] = mm.RowCount;
                            }
                        }

            foreach (var ss in sh.ParsedForm.SubShaders)
            {
                sb.AppendLine();
                sb.AppendLine("\tSubShader");
                sb.AppendLine("\t{");
                WriteSubShaderTags(sb, ss);
                sb.AppendLine($"\t\tLOD {(ss.LOD > 0 ? ss.LOD : 100)}");

                foreach (var pass in ss.Passes)
                {
                    sb.AppendLine();
                    sb.AppendLine($"\t\tPass");
                    sb.AppendLine("\t\t{");
                    if (!string.IsNullOrEmpty(pass.StateName))
                        sb.AppendLine($"\t\t\tName \"{Esc(pass.StateName)}\"");
                    WritePassTags(sb, pass);
                    WriteState(sb, pass);

                    var byStage = new Dictionary<string, List<SubProgram>>();
                    foreach (var sp in pass.SubPrograms)
                    {
                        if (!byStage.TryGetValue(sp.Stage, out var l)) byStage[sp.Stage] = l = new List<SubProgram>();
                        l.Add(sp);
                    }

                    bool any = byStage.Keys.Any(k => k is "Vertex" or "Fragment");
                    bool glslDone = false;

                    if (mainFormat is "d3d9" or "dxil")
                    {
                        sb.AppendLine("\t\t\t// " + (mainFormat == "d3d9"
                            ? Strings.Get("D3d9Note")
                            : Strings.Get("DxilNote")));
                        sb.AppendLine("\t\t\t// " + Strings.F(mainFormat == "d3d9" ? "D3d9NoteDir" : "DxilNoteDir", shaderKey));
                    }
                    else if (mainFormat == "glsl" && sources != null &&
                        byStage.Keys.Any(k => k is "Vertex" or "Fragment"))
                    {
                        var kwIndex = new Dictionary<int, string>();
                        for (int ki = 0; ki < sh.ParsedForm.KeywordNames.Count; ki++)
                            if (!string.IsNullOrEmpty(sh.ParsedForm.KeywordNames[ki]))
                                kwIndex[ki] = sh.ParsedForm.KeywordNames[ki];
                        var subs = pass.SubPrograms
                            .Where(sp => sources.ContainsKey(sp.BlobIndex) &&
                                         sources[sp.BlobIndex].Text.Length > 0)
                            .OrderBy(sp => sp.BlobIndex)
                            .ToList();
                        if (subs.Count > 0)
                        {
                            var groups = Cg.VariantPlan.Groups(
                                subs.Select(sp => Cg.VariantPlan.KeywordsOf(sp, kwIndex)));
                            var keys = subs.Select(sp => Cg.VariantPlan.Key(
                                Cg.VariantPlan.KeywordsOf(sp, kwIndex))).Distinct().ToList();
                            var srcByKey = new Dictionary<string, string>();
                            foreach (var sp in subs)
                            {
                                var k = Cg.VariantPlan.Key(Cg.VariantPlan.KeywordsOf(sp, kwIndex));
                                if (!srcByKey.ContainsKey(k)) srcByKey[k] = sources[sp.BlobIndex].Text;
                            }

                            sb.AppendLine();
                            sb.AppendLine("\t\t\t" + begin);
                            sb.AppendLine("\t\t\t#pragma vertex vert");
                            sb.AppendLine("\t\t\t#pragma fragment frag");
                            sb.AppendLine("\t\t\t#pragma target 3.0");
                            // HLSLPROGRAM does not pre-include HLSLSupport.cginc, and
                            // UnityCG.cginc needs the fixed/half types it defines.
                            if (hlsl) sb.AppendLine("\t\t\t#include \"HLSLSupport.cginc\"");
                            // Builtin uniforms are stripped from the emitted declarations
                            // (GlslToCg) — the cginc must supply them.
                            sb.AppendLine("\t\t\t#include \"UnityCG.cginc\"");
                            foreach (var g in groups)
                                sb.AppendLine("\t\t\t" + Cg.VariantPlan.Pragma(g));
                            sb.AppendLine($"\t\t\t// {keys.Count} keyword variant(s), {groups.Count} multi_compile line(s)");
                            var vanalysis = Cg.VariantPlan.Analyze(groups,
                                keys.Select(k => new HashSet<string>(k.Split(',').Where(x => x.Length > 0), StringComparer.Ordinal)).ToList());
                            foreach (var note in vanalysis.Notes)
                                sb.AppendLine($"\t\t\t// variants: {note}");

                            string Body(string src)
                            {
                                // 两个产码器都按目标方言直接产出，不再事后遍历文本改写。
                                return hlsl
                                    ? Cg.GlslToCg.Translate(src, true).Cg
                                    : Cg.GlslToCg.Translate(src).Cg;
                            }

                            for (int vi = 0; vi < keys.Count; vi++)
                            {
                                var kw = keys[vi].Split(',').Where(x => x.Length > 0).ToList();
                                string guard = Cg.VariantPlan.Guard(groups, kw);
                                sb.AppendLine();
                                sb.AppendLine($"\t\t\t// ---- variant {vi}"
                                              + (kw.Count == 0 ? " (base)" : ": " + string.Join(" + ", kw)) + " ----");
                                sb.AppendLine("\t\t\t" + (vi == 0
                                    ? (guard.Length > 0 ? "#if " + guard : "#if 1")
                                    : "#elif " + guard));
                                foreach (var line in Body(srcByKey[keys[vi]])
                                             .Replace("\r\n", "\n").Split('\n'))
                                    sb.AppendLine(line.TrimEnd().Length == 0 ? "" : "\t\t\t" + line.TrimEnd());
                            }
                            sb.AppendLine();
                            sb.AppendLine("\t\t\t// ---- fallback: combinations Unity never compiled ----");
                            sb.AppendLine("\t\t\t#else");
                            foreach (var line in Body(srcByKey[keys[0]])
                                         .Replace("\r\n", "\n").Split('\n'))
                                sb.AppendLine(line.TrimEnd().Length == 0 ? "" : "\t\t\t" + line.TrimEnd());
                            sb.AppendLine("\t\t\t#endif");
                            sb.AppendLine("\t\t\t" + end);
                            glslDone = true;
                        }
                    }

                    else if (any)
                    {
                        foreach (var stage in byStage.Keys
                                 .Where(k => k is "Geometry" or "Hull" or "Domain"))
                            sb.AppendLine($"\t\t\t// ({stage} stage present but not translated)");

                        sb.AppendLine();
                        sb.AppendLine("\t\t\t" + begin);
                        if (byStage.ContainsKey("Vertex")) sb.AppendLine("\t\t\t#pragma vertex vert");
                        if (byStage.ContainsKey("Fragment")) sb.AppendLine("\t\t\t#pragma fragment frag");
                        // HLSLPROGRAM does not pre-include HLSLSupport.cginc, and
                        // UnityCG.cginc needs the fixed/half types it defines.
                        if (hlsl) sb.AppendLine("\t\t\t#include \"HLSLSupport.cginc\"");
                        sb.AppendLine("\t\t\t#include \"UnityCG.cginc\"");

                        string body = Cg.CgProgramWriter.Build(pass, progs, pass.Tags, sh.ParsedForm.Props,
                                                               sh.ParsedForm.KeywordNames, paramBlobs, shaderKey, hlsl,
                                                               shaderMatrixRows);

                        bool usesLight = body.Contains("_LightColor0") ||
                                         body.Contains("_SpecColor");
                        bool declaresLight =
                            System.Text.RegularExpressions.Regex.IsMatch(body,
                                @"^\s*(fixed4|half4|float4)\s+(_LightColor0|_SpecColor)\b",
                                System.Text.RegularExpressions.RegexOptions.Multiline);
                        if (usesLight && !declaresLight)
                        {
                            sb.AppendLine("\t\t\t#include \"UnityLightingCommon.cginc\"");
                        }
                        else if (usesLight)
                        {
                            sb.AppendLine("\t\t\t#include \"UnityLightingCommon.cginc\"");
                            body = System.Text.RegularExpressions.Regex.Replace(
                                body,
                                @"^[ \t]*(?:float4|half4|fixed4)[ \t]+_LightColor0[ \t]*;[ \t]*\r?\n",
                                "",
                                System.Text.RegularExpressions.RegexOptions.Multiline);
                            body = System.Text.RegularExpressions.Regex.Replace(
                                body,
                                @"^[ \t]*(?:float4|half4|fixed4)[ \t]+_SpecColor[ \t]*;[ \t]*\r?\n",
                                "",
                                System.Text.RegularExpressions.RegexOptions.Multiline);
                        }

                        foreach (var line in body.Split('\n'))
                            sb.AppendLine(line.TrimEnd('\r').Trim().Length == 0 ? "" : "\t\t\t" + line.TrimEnd('\r'));

                        sb.AppendLine("\t\t\t" + end);
                    }
                    else
                        sb.AppendLine("\t\t\t// (no compiled program in this pass)");

                    sb.AppendLine("\t\t}");
                }
                sb.AppendLine("\t}");
            }

            if (!string.IsNullOrEmpty(sh.ParsedForm.CustomEditorName))
                sb.AppendLine($"\tCustomEditor \"{sh.ParsedForm.CustomEditorName}\"");
            foreach (var dep in sh.ParsedForm.Dependencies)
                if (!string.IsNullOrEmpty(dep))
                    sb.AppendLine($"\tDependency \"{dep}\"");
            if (!string.IsNullOrEmpty(sh.FallbackName))
                sb.AppendLine($"\tFallback \"{sh.FallbackName}\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string PropLine(Property p)
        {
            int t = p.Type;
            string flags = "";
            foreach (var kv in PropFlag)
                if ((p.Flags & kv.Key) != 0) flags += kv.Value + " ";

            string nm = p.Name, desc = Esc(p.Description);
            float[] d = p.DefValue;
            string v4 = $"({N(d[0])},{N(d[1])},{N(d[2])},{N(d[3])})";

            if (t == 0) return $"{flags}{nm} (\"{desc}\", Color) = {v4}";
            if (t == 1) return $"{flags}{nm} (\"{desc}\", Vector) = {v4}";
            if (t == 2) return $"{flags}{nm} (\"{desc}\", Float) = {N(d[0])}";
            if (t == 3) return $"{flags}{nm} (\"{desc}\", Range({N(d[1])}, {N(d[2])})) = {N(d[0])}";
            string dim = p.DefTextureDim switch
            {
                1 => "Any", 3 => "3D", 4 => "Cube", 5 => "2DArray", 6 => "CubeArray",
                2 => "2D",
                _ => t switch { 5 => "3D", 6 => "Cube", 7 => "2DArray", 8 => "CubeArray", _ => "2D" },
            };
            string def = string.IsNullOrEmpty(p.DefTextureName) ? "white" : p.DefTextureName;
            return $"{flags}{nm} (\"{desc}\", {dim}) = \"{def}\" {{}}";
        }

        /// <summary>Escape a string for embedding inside a ShaderLab double-quoted literal.</summary>
        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? s : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>
        /// Unity normalises tag keys to upper case inside the serialized file
        /// ("QUEUE", "RENDERTYPE"), but ShaderLab source always spells them in
        /// canonical mixed case.  Map back so the output matches what a human
        /// would write.
        /// </summary>
        private static readonly Dictionary<string, string> TagKeyCase = new(StringComparer.OrdinalIgnoreCase)
        {
            ["RenderType"] = "RenderType",
            ["Queue"] = "Queue",
            ["IgnoreProjector"] = "IgnoreProjector",
            ["PreviewType"] = "PreviewType",
            ["CanUseSpriteAtlas"] = "CanUseSpriteAtlas",
            ["ForceNoShadowCasting"] = "ForceNoShadowCasting",
            ["DisableBatching"] = "DisableBatching",
            ["LightMode"] = "LightMode",
            ["RequireOptions"] = "RequireOptions",
        };

        private static string CanonTag(string key, string val)
        {
            string k = TagKeyCase.TryGetValue(key, out var c) ? c : key;
            if (val.Equals("true", StringComparison.OrdinalIgnoreCase)) val = "True";
            else if (val.Equals("false", StringComparison.OrdinalIgnoreCase)) val = "False";
            return $"\"{k}\"=\"{val}\"";
        }

        /// <summary>
        /// Tag defaults follow ShaderLab, not a guess from the render state: an
        /// absent Queue tag means Geometry and an absent RenderType means Opaque.
        /// Deriving "Transparent" from blending was wrong for every surface shader
        /// that drives ZWrite from a material property -- Standard's asset carries
        /// no Queue tag at all, so Unity itself puts it in Geometry.
        /// </summary>
        private static void WriteSubShaderTags(StringBuilder sb, SubShader ss)
        {
            var tags = new List<(string k, string v)>(ss.Tags);
            bool Has(string k) => tags.Any(t => t.k.Equals(k, StringComparison.OrdinalIgnoreCase));

            if (!Has("RenderType")) tags.Add(("RenderType", "Opaque"));
            if (!Has("Queue")) tags.Add(("Queue", "Geometry"));

            sb.AppendLine("\t\tTags { " + string.Join(" ",
                tags.Select(t => CanonTag(t.k, t.v))) + " }");
        }

        private static void WritePassTags(StringBuilder sb, Pass p)
        {
            var tags = new List<(string k, string v)>(p.Tags);
            if (!tags.Any(t => t.k.Equals("LightMode", StringComparison.OrdinalIgnoreCase)))
                tags.Add(("LightMode", "Always"));
            sb.AppendLine("\t\t\tTags { " + string.Join(" ",
                tags.Select(t => CanonTag(t.k, t.v))) + " }");
        }

        private static void WriteState(StringBuilder sb, Pass p)
        {
            void W(string k, string v) => sb.AppendLine($"\t\t\t{k} {v}");

            string C(float v, string name, Dictionary<int, string> map) =>
                !string.IsNullOrEmpty(name) ? $"[{name}]"
                : map.GetValueOrDefault((int)v, ((int)v).ToString());

            if (!string.IsNullOrEmpty(p.AlphaToMaskName)) W("AlphaToMask", $"[{p.AlphaToMaskName}]");
            else if ((int)p.AlphaToMask > 0) W("AlphaToMask", "On");

            if (!string.IsNullOrEmpty(p.ZClipName)) W("ZClip", $"[{p.ZClipName}]");
            else if ((int)p.ZClip != 1) W("ZClip", ((int)p.ZClip) == 0 ? "Off" : "On");

            if (!string.IsNullOrEmpty(p.ZTestName) || (int)p.ZTest != 4)
                W("ZTest", C(p.ZTest, p.ZTestName, ZTestMap));
            if (!string.IsNullOrEmpty(p.ZWriteName) || (int)p.ZWrite != 1)
                W("ZWrite", C(p.ZWrite, p.ZWriteName, ZWriteMap));
            if (!string.IsNullOrEmpty(p.CullingName) || (int)p.Culling != 2)
                W("Cull", C(p.Culling, p.CullingName, CullMap));

            if (!string.IsNullOrEmpty(p.OffsetFactorName) || !string.IsNullOrEmpty(p.OffsetUnitsName) ||
                p.OffsetFactor != 0 || p.OffsetUnits != 0)
            {
                string fx = string.IsNullOrEmpty(p.OffsetFactorName) ? N(p.OffsetFactor) : $"[{p.OffsetFactorName}]";
                string ux = string.IsNullOrEmpty(p.OffsetUnitsName) ? N(p.OffsetUnits) : $"[{p.OffsetUnitsName}]";
                W("Offset", $"{fx}, {ux}");
            }

            bool IsInert(StencilOp o) => (int)o.Comp == 8 &&
                (int)o.PassOp == 0 && (int)o.Fail == 0 && (int)o.ZFail == 0 && !o.AnyName;
            bool Emits(StencilOp o) => (int)o.Comp != 0 || (int)o.PassOp != 0 ||
                (int)o.Fail != 0 || (int)o.ZFail != 0 || o.AnyName;
            bool NonDefault(StencilOp o) => !IsInert(o);

            bool Silent(StencilOp o) => (int)o.Comp == 0 &&
                (int)o.PassOp == 0 && (int)o.Fail == 0 && (int)o.ZFail == 0;

            bool MasksAreNoop() => (int)p.StencilRef == 0 &&
                (int)p.StencilReadMask == 0 && (int)p.StencilWriteMask == 0;

            bool NoopStencil() =>
                p.StencilOps.Where(Emits).All(o => IsInert(o) || Silent(o)) &&
                (MasksAreNoop() || ((int)p.StencilRef == 0 && (int)p.StencilReadMask == 0));

            bool same = p.StencilOps.Where(Emits)
                                      .Select(o => (o.Comp, o.PassOp, o.Fail, o.ZFail,
                                                    o.CompName, o.PassName, o.FailName, o.ZFailName))
                                      .Distinct().Count() <= 1;

            bool stencilNamed = !string.IsNullOrEmpty(p.StencilRefName) ||
                                !string.IsNullOrEmpty(p.StencilReadMaskName) ||
                                !string.IsNullOrEmpty(p.StencilWriteMaskName) ||
                                p.StencilOps.Any(o => o.AnyName);

            bool stencilUsed = !NoopStencil() || stencilNamed;
            string Sl(float v, string nm, Dictionary<int, string> map) =>
                !string.IsNullOrEmpty(nm) ? $"[{nm}]"
                : map.GetValueOrDefault((int)v, ((int)v).ToString());
            if (stencilUsed)
            {
                sb.AppendLine("\t\t\tStencil");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\tRef {Sl(p.StencilRef, p.StencilRefName, EmptyMap)}");
                sb.AppendLine($"\t\t\t\tReadMask {Sl(p.StencilReadMask, p.StencilReadMaskName, EmptyMap)}");
                sb.AppendLine($"\t\t\t\tWriteMask {Sl(p.StencilWriteMask, p.StencilWriteMaskName, EmptyMap)}");
                string[] opNames = { "", "Front", "Back" };
                for (int i = 0; i < 3 && i < p.StencilOps.Count; i++)
                {
                    if (same && i > 0) continue;
                    var so = p.StencilOps[i];
                    if (i > 0 && IsInert(so)) continue;
                    string pre = same ? "" : (opNames[i] == "" ? "" : opNames[i] + " ");
                    if ((int)so.Comp != 0 || so.CompName != "") sb.AppendLine($"\t\t\t\t{pre}Comp {Sl(so.Comp, so.CompName, CompareMap)}");
                    if ((int)so.PassOp != 0 || so.PassName != "") sb.AppendLine($"\t\t\t\t{pre}Pass {Sl(so.PassOp, so.PassName, StencilOpMap)}");
                    if ((int)so.Fail != 0 || so.FailName != "") sb.AppendLine($"\t\t\t\t{pre}Fail {Sl(so.Fail, so.FailName, StencilOpMap)}");
                    if ((int)so.ZFail != 0 || so.ZFailName != "") sb.AppendLine($"\t\t\t\t{pre}ZFail {Sl(so.ZFail, so.ZFailName, StencilOpMap)}");
                }
                sb.AppendLine("\t\t\t}");
            }
            for (int b = 0; b < p.Blends.Count; b++)
            {
                var bs = p.Blends[b];
                bool named = !string.IsNullOrEmpty(bs.SrcBlendName) || !string.IsNullOrEmpty(bs.DestBlendName);
                bool def = !named && (int)bs.SrcBlend == 1 && (int)bs.DestBlend == 0;
                if (def) continue;
                string lbl = b == 0 ? "Blend" : $"Blend {b}";
                string src = !string.IsNullOrEmpty(bs.SrcBlendName) ? $"[{bs.SrcBlendName}]"
                           : BlendMap.GetValueOrDefault((int)bs.SrcBlend, ((int)bs.SrcBlend).ToString());
                string dst = !string.IsNullOrEmpty(bs.DestBlendName) ? $"[{bs.DestBlendName}]"
                           : BlendMap.GetValueOrDefault((int)bs.DestBlend, ((int)bs.DestBlend).ToString());
                string op = "";
                string opLine = null;
                if (!string.IsNullOrEmpty(bs.BlendOpName))
                {
                    if ((int)bs.BlendOp != 0)
                        opLine = BlendOpMap.GetValueOrDefault((int)bs.BlendOp, ((int)bs.BlendOp).ToString());
                }
                else if ((int)bs.BlendOp != 0)
                    op = ", " + BlendOpMap.GetValueOrDefault((int)bs.BlendOp, ((int)bs.BlendOp).ToString());

                string alpha = "";
                string sa = !string.IsNullOrEmpty(bs.SrcBlendAlphaName) ? $"[{bs.SrcBlendAlphaName}]"
                          : BlendMap.GetValueOrDefault((int)bs.SrcBlendAlpha, ((int)bs.SrcBlendAlpha).ToString());
                string da = !string.IsNullOrEmpty(bs.DestBlendAlphaName) ? $"[{bs.DestBlendAlphaName}]"
                          : BlendMap.GetValueOrDefault((int)bs.DestBlendAlpha, ((int)bs.DestBlendAlpha).ToString());
                if (sa != src || da != dst) alpha = $", {sa} {da}";

                W(lbl, $"{src} {dst}{alpha}{op}");
                if (opLine != null)
                    W(b == 0 ? "BlendOp" : $"BlendOp {b}", opLine);
                if (!string.IsNullOrEmpty(bs.ColMaskName))
                    W(b == 0 ? "ColorMask" : $"ColorMask {b}", $"[{bs.ColMaskName}]");
                else if ((int)bs.ColMask != 15)
                    W(b == 0 ? "ColorMask" : $"ColorMask {b}",
                      ColorMaskMap.GetValueOrDefault((int)bs.ColMask, ((int)bs.ColMask).ToString()));
            }

            if (p.Lighting) W("Lighting", "On");
        }

        private static bool Op(Pass p, int idx, Func<StencilOp, bool> pred)
        {
            if (idx >= p.StencilOps.Count) return false;
            return pred(p.StencilOps[idx]);
        }
    }
}
