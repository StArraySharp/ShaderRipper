using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Data = ShaderRipper.Data;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// Wraps translated programs in ShaderLab, and checks the result by parsing it
    /// back with <c>ShaderLab.g4</c>.
    ///
    /// Wrapping is a template; the check is the point, because a missing brace is
    /// invisible to the code that wrote it.
    ///
    /// A pass holds both stages in one <c>HLSLPROGRAM</c>, which means merging two
    /// independently emitted programs.  They declare the same globals and often the
    /// same constant buffers with different extents, so the merge is not
    /// concatenation: duplicates are dropped and a buffer declared twice keeps the
    /// longer body.
    /// </summary>
    internal static class ShaderLab
    {
        public sealed class Stage
        {
            public string Hlsl;
            public string Entry;
        }

        /// <summary>
        /// One guard chain of keyword variants.  A DXBC pass produces one chain
        /// whose each branch is the vs+ps pair of one keyword combination,
        /// cross-merged by <see cref="Merge"/> (both stages compile together in
        /// one program, so their declarations must be unified, not duplicated);
        /// a GLSL payload produces one chain whose bodies carry both entry
        /// points.  Guards are exact `#if` expressions over the recovered
        /// multi_compile groups; index 0 is also the `#else` fallback body for
        /// combinations Unity never compiled.
        /// </summary>
        public sealed class VariantChain
        {
            public List<string> Guards = new();
            public List<string> Bodies = new();
        }

        public sealed class PassText
        {
            public string Name = "";
            public List<(string k, string v)> Tags = new();
            public List<string> States = new();
            public Stage Vertex;
            public Stage Fragment;
            /// <summary>Non-null when the pass was built from keyword variants.</summary>
            public List<VariantChain> Chains;
            public List<string> Pragmas = new();
            public List<string> Notes = new();
            /// <summary>GLSL-derived bodies need the cginc includes and target 3.0.</summary>
            public bool GlslIncludes;
            /// <summary>
            /// Shader model the pass was compiled for, as <c>40</c> / <c>50</c>, or 0
            /// when it is unknown.  Emitted as <c>#pragma target</c>: the translated
            /// bodies are register level (SM4 syntax), and Unity's default target 2.5
            /// does not accept them.
            /// </summary>
            public int Target;
        }

        public sealed class SubShaderText
        {
            public int LOD;
            public List<(string k, string v)> Tags = new();
            public List<PassText> Passes = new();
        }

        public static string Wrap(string name, string fallback, IReadOnlyList<SubShaderText> subShaders,
                                  IReadOnlyList<Data.Property> props, string customEditor = null)
        {
            var sb = new StringBuilder();
            sb.Append("Shader \"").Append(name.Replace('"', '_')).Append("\"\n{\n");

            AppendProperties(sb, props);

            // `CustomEditor` names the inspector a shader uses.  Without it the
            // material panel falls back to the generic one, so a shader whose
            // properties are driven by a keyword (TextMeshPro's distance-field
            // settings, the Standard particle options) loses that whole UI even
            // though every property survived.
            if (!string.IsNullOrEmpty(customEditor))
                sb.Append("    CustomEditor \"").Append(customEditor.Replace('"', '_')).Append("\"\n");

            if (subShaders.Count == 0)
            {
                sb.Append("    // No SubShader survived the build for this shader.\n");
            }
            else
            {
                foreach (var ss in subShaders)
                {
                    sb.Append("    SubShader\n    {\n");
                    if (ss.Tags.Count > 0) sb.Append("        Tags { ").Append(Tags(ss.Tags)).Append(" }\n");
                    if (ss.LOD != 0) sb.Append("        LOD ").Append(ss.LOD).Append('\n');
                    foreach (var p in ss.Passes) AppendPass(sb, p);
                    sb.Append("    }\n");
                }
            }

            if (!string.IsNullOrEmpty(fallback))
                sb.Append("    Fallback \"").Append(fallback.Replace('"', '_')).Append("\"\n");
            sb.Append("}\n");
            return Tidy(sb.ToString());
        }

        /// <summary>
        /// Collapse runs of blank lines to one, drop blank edges and strip
        /// trailing whitespace.
        ///
        /// A program block is assembled from several sources -- the merged
        /// structs and buffers, the include lines, one or more variant bodies --
        /// and each of them ends with its own separator blank.  Where two such
        /// sources meet the separators stack, which is how the GLSL route
        /// reached six blank lines between two declarations.  Blank lines carry
        /// no meaning in ShaderLab or HLSL, so this is purely a readability fix.
        /// </summary>
        private static string Tidy(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var outp = new List<string>(lines.Length);
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (line.Length == 0)
                {
                    if (outp.Count == 0 || outp[outp.Count - 1].Length == 0) continue;
                    outp.Add("");
                    continue;
                }
                outp.Add(line);
            }
            while (outp.Count > 0 && outp[outp.Count - 1].Length == 0) outp.RemoveAt(outp.Count - 1);
            return string.Join("\n", outp) + "\n";
        }

        /// <summary>
        /// The Properties block, straight from the shader's own property table.
        /// Without it a re-imported shader has no material properties at all, and
        /// every <c>_Color</c> the body reads would come from nowhere.
        /// </summary>
        private static void AppendProperties(StringBuilder sb, IReadOnlyList<Data.Property> props)
        {
            sb.Append("    Properties\n    {\n");
            foreach (var p in props ?? Array.Empty<Data.Property>())
            {
                if (string.IsNullOrEmpty(p.Name)) continue;
                bool isTexture = p.Type is 4 or 5 or 6 or 7;
                // A texture property's dimension is NOT p.Type: Type 4 means "a
                // texture", and which texture is decided by m_DefTexture.m_TexDim.
                // Reading only the type flattened every cubemap, 3D texture and
                // array into "2D", which the importer then binds to a sampler2D --
                // a cubemap fed through a 2D sampler samples garbage.
                string type = isTexture ? TexDimName(p) : p.Type switch
                {
                    0 => "Color", 1 => "Vector", 2 => "Float",
                    3 => RangeType(p),
                    _ => "Vector",
                };
                string label = string.IsNullOrEmpty(p.Description) ? p.Name : p.Description;
                // A Float or Range property takes a scalar, a Color or Vector a
                // four-component tuple, a texture a name and an empty body.  Writing
                // a tuple for a Float is not valid ShaderLab.
                string value = isTexture
                    ? "\"" + (string.IsNullOrEmpty(p.DefTextureName) ? "white" : p.DefTextureName) + "\" {}"
                    : p.Type switch
                    {
                        2 or 3 => N(p.DefValue[0]),
                        _ => $"({N(p.DefValue[0])}, {N(p.DefValue[1])}, {N(p.DefValue[2])}, {N(p.DefValue[3])})",
                    };
                string attrs = PropertyAttributes(p);
                sb.Append($"        {attrs}{p.Name} (\"{label}\", {type}) = {value}\n");
            }
            sb.Append("    }\n");
        }

        /// <summary>
        /// A <c>Range</c> property's bounds.
        /// The asset does not store min/max -- <c>SerializedProperty</c> has only the
        /// type code and the default value -- so the range is inferred from the
        /// default.  A hard-coded <c>Range(0, 1)</c> would clip the slider for the
        /// many properties that ship defaults above 1, so the upper bound grows to
        /// the default when it is larger.
        /// </summary>
        private static string RangeType(Data.Property p)
        {
            float v = p.DefValue[0];
            if (float.IsNaN(v) || float.IsInfinity(v)) return "Range(0, 1)";
            if (v > 1f) return $"Range(0, {N(v)})";
            if (v < 0f) return $"Range({N(v)}, 1)";
            return "Range(0, 1)";
        }

        /// <summary>
        /// The <c>[Attribute]</c> prefixes a property carries, decoded from Unity's
        /// <c>ShaderPropertyFlags</c> bit set.
        ///
        /// <c>PerRendererData</c> matters beyond panel decoration: it is what tells the
        /// renderer to fill the value from a MaterialPropertyBlock.
        /// <c>[Toggle]</c> is deliberately absent -- it names a keyword rather than a
        /// flag bit, and a build strips the link.
        /// </summary>
        private static string PropertyAttributes(Data.Property p)
        {
            int f = p.Flags;
            if (f == 0) return "";
            var sb = new StringBuilder();
            if ((f & 0x01) != 0) sb.Append("[HideInInspector] ");
            if ((f & 0x02) != 0) sb.Append("[PerRendererData] ");
            if ((f & 0x04) != 0) sb.Append("[NoScaleOffset] ");
            if ((f & 0x08) != 0) sb.Append("[Normal] ");
            if ((f & 0x10) != 0) sb.Append("[HDR] ");
            if ((f & 0x20) != 0) sb.Append("[Gamma] ");
            if ((f & 0x80) != 0) sb.Append("[MainTexture] ");
            if ((f & 0x100) != 0) sb.Append("[MainColor] ");
            return sb.ToString();
        }

        /// <summary>
        /// The ShaderLab texture-type keyword for a texture property.
        ///
        /// <c>m_TexDim</c> is UnityEngine.Rendering.TextureDimension:
        /// None=0, Any=1, Tex2D=2, Tex3D=3, Cube=4, Tex2DArray=5, CubeArray=6.
        /// There is no 1D entry -- Unity does not import 1D textures -- so 0 and 1
        /// both fall back to 2D, which is what the overwhelming majority of
        /// texture properties are.  Writing "1D" is not merely wrong: ShaderLab has
        /// no such property type and the import fails outright.
        /// </summary>
        private static string TexDimName(Data.Property p)
        {
            // Property.Type itself can already carry the answer for the rarer
            // kinds (Unity writes 5/6/7 for 3D/Cube/2DArray in some generations);
            // prefer it when it is not the generic "texture" code.
            switch (p.Type)
            {
                case 5: return "3D";
                case 6: return "Cube";
                case 7: return "2DArray";
            }
            return p.DefTextureDim switch
            {
                3 => "3D",
                4 => "Cube",
                5 => "2DArray",
                6 => "CubeArray",
                _ => "2D",   // 2, 1 (Any), 0 (None) and anything unknown
            };
        }

        private static void AppendPass(StringBuilder sb, PassText p)
        {
            sb.Append("        Pass\n        {\n");
            if (!string.IsNullOrEmpty(p.Name)) sb.Append("            Name \"").Append(p.Name).Append("\"\n");
            if (p.Tags.Count > 0) sb.Append("            Tags { ").Append(Tags(p.Tags)).Append(" }\n");
            // A state entry can be several lines (a Stencil block); the second and
            // later lines carry their own relative indentation, so they are appended
            // as-is apart from the pass-level pad.
            foreach (var s in p.States)
            {
                if (s.IndexOf('\n') < 0) { sb.Append("            ").Append(s).Append('\n'); continue; }
                foreach (var line in s.Replace("\r\n", "\n").Split('\n'))
                    sb.Append(line.Length == 0 ? "\n" : "            " + line + "\n");
            }

            if (p.Chains != null)
            {
                WriteVariantProgram(sb, p);
                sb.Append("            ENDHLSL\n");
                sb.Append("        }\n\n");
                return;
            }

            var merged = Merge(p.Vertex, p.Fragment);
            if (merged == null)
            {
                sb.Append("            // This pass has no program in the build.\n");
                sb.Append("        }\n\n");
                return;
            }

            // A pass can ship only its fragment program: Unity then supplies a
            // full-screen triangle vertex shader at draw time, which is why the
            // fragment's input signature starts at `SV_Position`.  Unity's own
            // compiler rejects such a snippet outright ("Both vertex and fragment
            // programs must be present ... Excluding it from compilation"), so the
            // pass would be dropped on import.  Supplying the same full-screen
            // vertex program the runtime uses keeps the pass alive and leaves the
            // fragment's inputs exactly as the original program saw them.
            bool synthVertex = p.Vertex == null && p.Fragment != null;
            if (synthVertex)
            {
                merged = Merge(new Stage { Hlsl = FullscreenVertex(p), Entry = "vert" }, p.Fragment);
                if (merged == null) return;
            }
            sb.Append("            HLSLPROGRAM\n");
            if (p.Vertex != null) sb.Append("            #pragma vertex ").Append(p.Vertex.Entry).Append('\n');
            else if (synthVertex) sb.Append("            #pragma vertex vert\n");
            if (p.Fragment != null) sb.Append("            #pragma fragment ").Append(p.Fragment.Entry).Append('\n');
            AppendTarget(sb, p);
            foreach (var line in p.Pragmas) sb.Append("            ").Append(line).Append('\n');
            foreach (var note in p.Notes) sb.Append("            // ").Append(note).Append('\n');
            foreach (var line in merged) AppendBodyLine(sb, line);
            sb.Append("            ENDHLSL\n");
            sb.Append("        }\n\n");
        }

        /// <summary>
        /// One line of a translated body, at the program block's indentation.
        ///
        /// An empty line is written as a bare newline and nothing else: the
        /// previous form appended <c>"\n"</c> *and* the empty text *and* another
        /// <c>\n</c>, which is where the two-blank-line gaps between every
        /// declaration came from.
        /// </summary>
        private static void AppendBodyLine(StringBuilder sb, string line)
        {
            if (line.Length == 0) { sb.Append('\n'); return; }
            sb.Append("            ").Append(line).Append('\n');
        }

        /// <summary>
        /// A pass built from keyword variants: one guard chain per stage (DXBC)
        /// or one combined chain (GLSL).  Variant bodies come from the parse
        /// tree, so each `#if` branch re-emits whole grammatical items instead
        /// of glued strings.  Variant 0 is repeated under `#else` as the
        /// fallback for combinations Unity never compiled -- the same shape the
        /// original source had before Unity stripped its variants.
        /// </summary>
        private static void AppendTarget(StringBuilder sb, PassText p)
        {
            // The translated body is register level: `Texture2D` + `SamplerState`
            // declarations, `.Sample()` lookups, `asuint`/`asfloat` and `[loop]`.
            // Unity compiles a program with no `#pragma target` as 2.5, which
            // rejects every one of those, so the pass states the model the original
            // program was built for -- and never less than 4.0, because the syntax
            // itself requires it.
            int model = Math.Max(p.Target, 40);
            sb.Append("            #pragma target ").Append(model / 10).Append('.')
              .Append(model % 10).Append('\n');
        }

        private static void WriteVariantProgram(StringBuilder sb, PassText p)
        {
            // The chain bodies are whole program texts, so which entry points exist
            // is read off them rather than assumed: a pass can ship only a fragment
            // program (Unity supplies the full-screen vertex) or only a vertex one.
            bool hasVert = p.Chains.Any(c => c.Bodies.Any(b => DefinesEntry(b, "vert")));
            bool hasFrag = p.Chains.Any(c => c.Bodies.Any(b => DefinesEntry(b, "frag")));

            // A fragment-only program needs the full-screen vertex Unity binds for
            // it; without an entry point for `#pragma vertex` the pass does not
            // compile at all.
            string synth = hasVert || !hasFrag ? null : FullscreenVertexFor(p.Chains[0].Bodies[0]);

            sb.Append("            HLSLPROGRAM\n");
            if (hasVert) sb.Append("            #pragma vertex vert\n");
            else if (synth != null) sb.Append("            #pragma vertex vert\n");
            if (hasFrag) sb.Append("            #pragma fragment frag\n");
            AppendTarget(sb, p);
            foreach (var pragma in p.Pragmas) sb.Append("            ").Append(pragma).Append('\n');
            foreach (var note in p.Notes) sb.Append("            // ").Append(note).Append('\n');

            foreach (var chain in p.Chains)
            {
                if (chain.Bodies.Count == 0) continue;
                var bodyLines = chain.Bodies
                    .Select(b => (b ?? "").Replace("\r\n", "\n").Split('\n'))
                    .ToList();

                for (int vi = 0; vi < bodyLines.Count; vi++)
                {
                    string guard = vi < chain.Guards.Count ? chain.Guards[vi] : "";
                    string head = vi == 0
                        ? (guard.Length > 0 ? "#if " + guard : "#if 1")
                        : (guard.Length > 0 ? "#elif " + guard : "#elif 1");
                    sb.Append("            ").Append(head).Append('\n');
                    if (synth != null)
                        foreach (var l in synth.Replace("\r\n", "\n").Split('\n'))
                            AppendBodyLine(sb, l);
                    foreach (var l in bodyLines[vi]) AppendBodyLine(sb, l);
                }
                // Combinations the build never shipped: compile the base variant.
                sb.Append("            #else\n");
                if (synth != null)
                    foreach (var l in synth.Replace("\r\n", "\n").Split('\n'))
                        AppendBodyLine(sb, l);
                foreach (var l in bodyLines[0]) AppendBodyLine(sb, l);
                sb.Append("            #endif\n");
            }
        }

        /// <summary>Does this program text define the given entry point?</summary>
        private static bool DefinesEntry(string body, string name) =>
            body != null && Regex.IsMatch(body, @"\b" + name + @"\s*\(");

        /// <summary>
        /// Keys ShaderLab spells in mixed case, no matter how the serialized data
        /// capitalized them.
        ///
        /// The asset stores tag keys upper-cased (<c>LIGHTMODE</c>) because that is how
        /// Unity's serializer writes them, but a ShaderLab source file spells them the
        /// documented way.  Unity's tag reader is case-insensitive, so either parses;
        /// copying the asset's casing just stops the file reading like the shader it
        /// came from.
        /// </summary>
        private static readonly Dictionary<string, string> TagKeyCase =
            new(StringComparer.OrdinalIgnoreCase)
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

        /// <summary>
        /// Values that name a ShaderLab keyword rather than a user string, so their
        /// casing is part of the spelling too.
        ///
        /// The asset stores `FORWARDBASE`; the pass tag everyone writes is
        /// `ForwardBase`, and Unity matches the value exactly (unlike the key, the
        /// value is NOT case-insensitive -- `"FORWARDBASE"` does not select the
        /// forward-base pass).
        /// </summary>
        private static readonly Dictionary<string, string> TagValueCase =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Always"] = "Always",
                ["ForwardBase"] = "ForwardBase",
                ["ForwardAdd"] = "ForwardAdd",
                ["PrepassBase"] = "PrepassBase",
                ["PrepassFinal"] = "PrepassFinal",
                ["Vertex"] = "Vertex",
                ["VertexLMRGBM"] = "VertexLMRGBM",
                ["VertexLM"] = "VertexLM",
                ["ShadowCaster"] = "ShadowCaster",
                ["ShadowCollector"] = "ShadowCollector",
                ["Deferred"] = "Deferred",
                ["MotionVectors"] = "MotionVectors",
                ["DepthOnly"] = "DepthOnly",
                ["DepthNormals"] = "DepthNormals",
                ["DepthNormalsOnly"] = "DepthNormalsOnly",
                ["Meta"] = "Meta",
                ["SRPDefaultUnlit"] = "SRPDefaultUnlit",
                ["UniversalForward"] = "UniversalForward",
                ["UniversalForwardOnly"] = "UniversalForwardOnly",
                ["UniversalGBuffer"] = "UniversalGBuffer",
                ["ShadowCasterPass"] = "ShadowCasterPass",
            };

        private static string Tags(IEnumerable<(string k, string v)> tags) =>
            string.Join(" ", tags.Select(t => $"\"{CanonTagKey(t.k)}\" = \"{CanonTagValue(t.k, t.v)}\""));

        private static string CanonTagKey(string k) =>
            TagKeyCase.TryGetValue(k, out var c) ? c : k;

        private static string CanonTagValue(string k, string v)
        {
            // `LightMode` is the only tag whose value is a keyword; every other tag
            // value is either a user string (`Opaque`, `Transparent`) or a boolean.
            if (k.Equals("LightMode", StringComparison.OrdinalIgnoreCase) &&
                TagValueCase.TryGetValue(v, out var c)) return c;
            if (v.Equals("true", StringComparison.OrdinalIgnoreCase)) return "True";
            if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return "False";
            return v;
        }

        /// <summary>
        /// The full-screen triangle Unity binds when a pass ships only a fragment
        /// program.
        ///
        /// Its output has to match the fragment's input signature, so the struct is
        /// built from the fields the fragment actually declares: `SV_Position`
        /// comes from the triangle, every other semantic is passed through
        /// untouched (a screen-space effect reads them from the interpolator).
        /// </summary>
        private static string FullscreenVertex(PassText p)
        {
            return FullscreenVertexFor(p.Fragment?.Hlsl ?? "");
        }

        /// <summary>
        /// The same synthetic vertex program, built from a program text rather than
        /// a <see cref="Stage"/> -- the variant path only has the bodies.
        /// </summary>
        private static string FullscreenVertexFor(string fragment)
        {
            // The struct is read back out of the fragment program's own
            // `stage_in`, which is where the field names, types and semantics already
            // are -- no second source of truth.
            var fields = new List<(string type, string name, string sem)>();
            var m = Regex.Match(fragment ?? "",
                                @"struct\s+stage_in\s*\{(.*?)\}", RegexOptions.Singleline);
            if (m.Success)
                foreach (Match f in Regex.Matches(m.Groups[1].Value,
                                                  @"(\w+)\s+(\w+)\s*:\s*([A-Za-z_]\w*)\s*;"))
                    fields.Add((f.Groups[1].Value, f.Groups[2].Value, f.Groups[3].Value));

            var sb = new StringBuilder();
            sb.Append("struct fs_out\n{\n");
            if (fields.Count == 0)
                sb.Append("\tfloat4 pos : SV_POSITION;\n");
            else
                foreach (var f in fields)
                    sb.Append($"\t{f.type,-7} {f.name,-12} : {f.sem};\n");
            sb.Append("};\n\n");
            sb.Append("fs_out vert(uint id : SV_VertexID)\n{\n");
            sb.Append("\tfs_out o;\n");
            sb.Append("\tfloat2 uv = float2((id << 1) & 2, id & 2);\n");
            // Every declared field is either the clip position (from the triangle)
            // or a UV the fragment reads; the rest are zero so the struct is fully
            // initialised.
            foreach (var f in fields)
            {
                if (f.sem == "SV_POSITION")
                    sb.Append($"\to.{f.name} = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);\n");
                else if (f.sem.StartsWith("TEXCOORD", StringComparison.Ordinal))
                    sb.Append($"\to.{f.name} = {f.type}(uv);\n");
                else
                    sb.Append($"\to.{f.name} = ({f.type})0;\n");
            }
            sb.Append("\treturn o;\n}\n");
            return sb.ToString();
        }


        /// <summary>
        /// One program block for a pass: buffers merged by NAME, then the remaining
        /// globals once each, then the two entry points.
        ///
        /// Keying by name is load-bearing: fxc assigns slots per stage, so the same
        /// pass can have <c>cb2 = UnityPerDraw</c> in the vertex stage and
        /// <c>cb2 = UnityLighting</c> in the fragment stage.  Unity binds buffers by
        /// name, so the slot number carries no meaning -- what must survive the merge
        /// is each named buffer's member layout.
        /// </summary>
        private sealed class Buf
        {
            public string Header = "";
            /// <summary>Members keyed by the lane each starts at.</summary>
            public readonly Dictionary<(int row, int col), CbLayout.Member> Rows = new();
        }

        /// <summary>
        /// Names the shader's own Properties block declares.
        ///
        /// A member matching one of these belongs to the game, never the engine, even
        /// when the name also appears in <c>unity_engine_names.json</c>: a material
        /// property has to keep its declaration or the material can no longer drive
        /// it.  Only names absent here are handed to the include.
        ///
        /// Per-thread: <see cref="SetPropertyNames"/> runs once per shader and the
        /// answer is read throughout that shader's emission, so a plain static would
        /// let two concurrent exports overwrite each other.
        /// </summary>
        [ThreadStatic]
        private static HashSet<string> _properties;

        internal static void SetPropertyNames(IEnumerable<Data.Property> props)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in props ?? Enumerable.Empty<Data.Property>())
            {
                if (string.IsNullOrEmpty(p.Name)) continue;
                set.Add(p.Name);
                // A texture property also implies `<name>_ST` / `<name>_TexelSize`,
                // which Unity synthesises and a shader routinely reads.
                set.Add(p.Name + "_ST");
                set.Add(p.Name + "_TexelSize");
                set.Add(p.Name + "_HDR");
            }
            _properties = set;
        }

        /// <summary>
        /// Is this member one the include chain could declare instead of us?
        ///
        /// Only engine names qualify.  A name the Properties block lists is a material
        /// property and must stay a member of <c>UnityPerMaterial</c>, because that is
        /// the buffer Unity fills from the Material; handing it to an include produces
        /// a plain global the engine never writes, so it reads zero.
        ///
        /// Non-property names keep the old rules: a name no cginc declares stays, and
        /// so does one the include only reaches behind a preprocessor guard.
        /// </summary>
        internal static bool CanTakeOver(string name)
        {
            if (IsProperty(name)) return false;
            if (!UnityIncludes.IsEngineMember(name)) return false;
            if (UnityIncludes.IsConditional(name)) return false;
            return true;
        }

        /// <summary>
        /// The identifier a global declaration line introduces, or null when the
        /// line declares no plain variable (`static const` tables, function-like
        /// macros, and forward declarations all fall through).
        /// </summary>
        private static string DeclaredName(string line)
        {
            var m = Regex.Match(line ?? "",
                @"^\s*(?:Texture\w*|SamplerState|sampler\w*|Buffer|struct\s+\w+|" +
                @"(?:static\s+|uniform\s+|extern\s+|const\s+|row_major\s+|column_major\s+)*" +
                @"(?:fixed|half|float|int|uint|bool|double|real|[A-Za-z_]\w*)" +
                @"(?:[1-4](?:x[1-4])?)?)\s+([A-Za-z_]\w*)");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static bool IsProperty(string name)
        {
            if (string.IsNullOrEmpty(name) || _properties == null || _properties.Count == 0) return false;
            if (_properties.Contains(name)) return true;
            // `name[2]` and `name.x` both belong to `name`.
            int cut = name.IndexOfAny(new[] { '[', '.' });
            return cut > 0 && _properties.Contains(name[..cut]);
        }

        /// <summary>
        /// Re-indent a declaration block to one level inside the program.
        ///
        /// The translated HLSL is emitted at column zero, so dropping it into a
        /// <c>HLSLPROGRAM</c> unchanged leaves the braces outside the surrounding
        /// indentation.  The body is normalised rather than shifted: the first line
        /// fixes the base column, deeper lines keep their relative offset, and tabs
        /// become spaces.
        /// </summary>
        private static List<string> Reindent(List<string> block, int level)
        {
            string pad = new string(' ', level * 4);
            var lines = block.SelectMany(b => b.Replace("\r\n", "\n").Split('\n')).ToList();

            // Drop leading/trailing blank lines; the caller adds one after.
            while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);

            // Column of the first non-blank line becomes the base.
            int baseCol = 0;
            foreach (string l in lines)
                if (l.Trim().Length > 0) { baseCol = IndentOf(l); break; }

            var outp = new List<string>(lines.Count);
            foreach (string raw in lines)
            {
                if (raw.Trim().Length == 0) { outp.Add(""); continue; }
                int col = Math.Max(0, IndentOf(raw) - baseCol);
                outp.Add(pad + new string(' ', col) + raw.TrimStart(' ', '\t'));
            }
            return outp;
        }

        /// <summary>Width of a line's leading whitespace, counting a tab as 4.</summary>
        private static int IndentOf(string line)
        {
            int col = 0;
            foreach (char c in line)
            {
                if (c == ' ') col++;
                else if (c == '\t') col += 4;
                else break;
            }
            return col;
        }

        internal static List<string> Merge(Stage vs, Stage ps)
        {
            if (vs == null && ps == null) return null;

            // The replacement tally is per pass, not per file.
            UnityIncludes.Reset();

            var buffers = new Dictionary<string, Buf>(StringComparer.Ordinal);
            var structs = new List<List<string>>();
            var structNames = new HashSet<string>(StringComparer.Ordinal);
            // per stage: register -> (buffer name, members as declared there)
            var perStage = new List<(Stage stage, Dictionary<int, (string name, Dictionary<(int row, int col), CbLayout.Member> rows)> own)>();
            var globals = new List<string>();
            var seenGlobal = new HashSet<string>(StringComparer.Ordinal);

            foreach (var stage in new[] { vs, ps })
            {
                if (stage?.Hlsl == null) continue;

                // Sectioning is a parse-tree decision (Hlsl.g4), not a line scan:
                // a cbuffer is whatever the grammar called `cbufferDecl`, a struct
                // whatever it called `structDecl`, and the first `vert`/`frag`
                // function definition ends the declaration section.
                var items = HlslTree.Split(stage.Hlsl, out _);

                var own = new Dictionary<int, (string, Dictionary<(int row, int col), CbLayout.Member>)>();
                foreach (var it in items)
                {
                    if (it.Kind == "func") break;   // declarations end at the entry point

                    switch (it.Kind)
                    {
                        case "struct":
                            if (structNames.Add(it.Name))
                                structs.Add(new List<string> { it.Text });
                            break;

                        case "cbuffer":
                        {
                            if (it.Register < 0) continue;
                            int reg = it.Register;
                            var block = new List<string> { it.Text };
                            var rows = ReadRows(block);
                            own[reg] = (it.Name, rows);
                            if (!buffers.TryGetValue(it.Name, out var buf))
                            {
                                buf = new Buf { Header = CbHeader(it.Name, reg) };
                                buffers[it.Name] = buf;
                                foreach (var r in rows) buf.Rows[r.Key] = r.Value;
                            }
                            else
                            {
                                MergeRows(buf.Rows, rows);
                                // Keep whichever header names the buffer; a "CBn" is the
                                // absence of an answer, not a conflicting one.
                                if (buf.Header.StartsWith("cbuffer CB", StringComparison.Ordinal) &&
                                    !it.Name.StartsWith("CB", StringComparison.Ordinal))
                                    buf.Header = CbHeader(it.Name, reg);
                            }
                            break;
                        }

                        case "decl":
                        case "semi":
                        case "pp":
                        {
                            string trimmed = it.Text.Trim();
                            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                            if (seenGlobal.Add(trimmed)) globals.Add(it.Text);
                            break;
                        }
                    }
                }
                perStage.Add((stage, own));
            }

            // Slot assignment.  Walk the stages in order (vertex first) and give
            // each buffer the register its stage used; if that slot is already
            // taken by another buffer, take the lowest free slot at or after it.
            var slotOf = new Dictionary<string, int>(StringComparer.Ordinal);
            var slotOwner = new Dictionary<int, string>();
            foreach (var (_, own) in perStage)
                foreach (var reg in own.Keys.OrderBy(k => k))
                {
                    string name = own[reg].name;
                    if (slotOf.ContainsKey(name)) continue;
                    int slot;
                    if (!slotOwner.ContainsKey(reg)) slot = reg;
                    else { slot = reg; while (slotOwner.ContainsKey(slot)) slot++; }
                    slotOf[name] = slot;
                    slotOwner[slot] = name;
                }

            // One name can appear in two registers: a stage that could not identify
            // b0 names its rows from the property table and arrives at
            // `_RendererColor`, which the other stage places in `UnityPerDrawSprite`
            // where it belongs.  A member may only be declared once, so the name
            // stays in the buffer that is one Unity defines, and the other
            // occurrence falls back to its row number.
            var owner = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in buffers.OrderBy(k => k.Key))
                foreach (var row in kv.Value.Rows.Values)
                {
                    if (IsPositional(row.Name)) continue;
                    if (!owner.TryGetValue(row.Name, out string reg))
                    {
                        owner[row.Name] = kv.Key;
                        continue;
                    }
                    bool keepMine = kv.Value.Header.StartsWith("cbuffer Unity", StringComparison.Ordinal);
                    bool keepTheirs = buffers[reg].Header.StartsWith("cbuffer Unity", StringComparison.Ordinal);
                    if (keepMine && !keepTheirs) { owner[row.Name] = kv.Key; }
                }
            foreach (var kv in buffers)
            {
                var fixedRows = new List<CbLayout.Member>();
                foreach (var row in kv.Value.Rows.Values)
                {
                    bool demote = !IsPositional(row.Name) &&
                                  owner.TryGetValue(row.Name, out string ownName) && ownName != kv.Key;
                    fixedRows.Add(demote ? Positional(slotOf[kv.Key], row) : row);
                }
                kv.Value.Rows.Clear();
                foreach (var r in fixedRows) kv.Value.Rows[(r.Row, r.Col)] = r;

                // The two stages of one variant can name the same member at
                // different offsets: each program keeps its own $Globals layout, and
                // a variant whose vertex stage packed one matrix more shifts every
                // property after it.  Only one declaration can carry the name, so
                // the first keeps it and the others become positional members --
                // each body then reads the bytes its own program actually named,
                // instead of every body reading the same (possibly wrong) ones.
                var claimed = new Dictionary<string, (int row, int col)>(StringComparer.Ordinal);
                foreach (var key in kv.Value.Rows.Keys.OrderBy(k => k.row).ThenBy(k => k.col).ToList())
                {
                    var row = kv.Value.Rows[key];
                    if (IsPositional(row.Name)) continue;
                    if (claimed.TryAdd(row.Name, key)) continue;
                    kv.Value.Rows[key] = Positional(slotOf[kv.Key], row);
                }
            }

            var outp = new List<string>();
            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var block in structs) { outp.AddRange(Reindent(block, 0)); outp.Add(""); }

            // Unity's own constant buffers are declared by the include chain, not
            // by us.  Re-declaring `unity_ObjectToWorld`, `unity_MatrixVP` and the
            // rest makes the ShaderLab compiler reject the file outright:
            //
            //   Shader has a float property 'unity_MatrixVP' which clashes with a
            //   built-in non-float property name.
            //
            // Unity resolves every identifier it sees in a program block against
            // its built-in property table, and a `cbuffer` member is no exception.
            // Including the file that already declares them is both shorter and
            // correct -- and it is what the original source did.
            bool usingIncludes = UnityIncludes.Enabled;

            // Which cbuffer members the include table can take over.  This is
            // decided before the emit loop because the choice depends on every name
            // the program declares: an include is only usable when it declares
            // nothing the program declares itself.
            var droppable = new HashSet<(string buf, string name)>();
            if (usingIncludes)
            {
                var programNames = new List<string>(structNames);
                foreach (var kv in buffers)
                    foreach (var row in kv.Value.Rows.Values)
                    {
                        if (CanTakeOver(row.Name)) droppable.Add((kv.Key, row.Name));
                        else if (!IsPositional(row.Name)) programNames.Add(row.Name);
                    }
                foreach (string g in globals)
                {
                    string n = DeclaredName(g);
                    if (n != null) programNames.Add(n);
                }
                UnityIncludes.SetProgramNames(programNames);
            }

            foreach (var kv in buffers.OrderBy(k => slotOf[k.Key]))
            {
                // Two stages can each contribute a piece of the same row -- one a
                // whole-footprint member, the other narrower ones sharing it -- and
                // the union would then declare members over the same bytes, which
                // fxc rejects ("packoffset overlaps").  The wider member wins, and
                // every narrower name that overlapped it is rewritten to read
                // through it in *every* stage body, because those names may appear
                // in a variant that never goes through this merge.
                var overlap = CbLayout.OverlapRenames(kv.Value.Rows.Values);
                foreach (var r in overlap)
                {
                    renames[r.Key] = r.Value;
                    kv.Value.Rows.Remove((kv.Value.Rows.First(x => x.Value.Name == r.Key).Key));
                }
                var keptMembers = kv.Value.Rows.Values.OrderBy(m => m.Row).ThenBy(m => m.Col).ToList();

                // A `$Globals` block holds the engine's own globals beside the
                // material's properties, and leaving them in makes the ShaderLab
                // compiler reject the file, so they come in through the include
                // instead.  The check is by member NAME against the engine table, not
                // by buffer name, and a buffer left empty is dropped whole.
                //
                // The Properties block overrules the table: a game may declare a
                // property that merely shares a name with an engine one, and that
                // property must keep its declaration.  Only names the property table
                // does not claim are handed to the include -- except where the include
                // declares the name unconditionally and the property is only a panel
                // entry, in which case keeping the member would declare it twice.
                if (usingIncludes)
                {
                    var survivors = new List<CbLayout.Member>(keptMembers.Count);
                    foreach (var m in keptMembers.OrderBy(x => x.Name, StringComparer.Ordinal))
                    {
                        if (!droppable.Contains((kv.Key, m.Name))) { survivors.Add(m); continue; }
                        if (!UnityIncludes.NoteMember(m.Name)) survivors.Add(m);
                    }
                    keptMembers = survivors
                        .OrderBy(m => m.Row).ThenBy(m => m.Col).ToList();
                }

                if (keptMembers.Count == 0) continue;   // the whole block was engine-owned

                string header = kv.Value.Header.Replace(
                    Regex.Match(kv.Value.Header, @"register\(b\d+\)").Value,
                    $"register(b{slotOf[kv.Key]})");
                outp.Add(header.Trim());
                outp.Add("{");
                // Members sit one level inside the block.  `Wrap` prefixes every
                // line with the program's own indentation, so the relative offset
                // added here is what the reader sees.
                foreach (var m in keptMembers) outp.Add("    " + CbLayout.Line(m).TrimStart());
                outp.Add("};");
                outp.Add("");
            }

            // The includes go before the declarations that remain, so the engine
            // members are visible to every program in the file.  A shader that only
            // touches `unity_MatrixVP` gets one line; a particle shader that needs
            // the billboard helpers gets that file too.
            if (usingIncludes && UnityIncludes.Any)
            {
                var head = new List<string>();
                foreach (string line in UnityIncludes.Lines())
                    head.Add(line.Trim());
                head.Add("");
                head.AddRange(outp);
                outp = head;
            }

            if (globals.Count > 0)
            {
                outp.AddRange(Reindent(globals, 0));
                outp.Add("");
            }

            foreach (var (stage, own) in perStage)
            {
                string fn = Function(stage);
                // Names dropped for overlapping a wider member are rewritten to
                // read through the member that survived.
                foreach (var kv in renames)
                    fn = Regex.Replace(fn, @"\b" + Regex.Escape(kv.Key) + @"\b", kv.Value);
                // The declaration is one per buffer name now, so a body that spells
                // a member the other stage named has to be renamed to match:
                // otherwise it references a member the merged block no longer
                // declares.
                foreach (var kv in buffers)
                {
                    var mine = own.Values.FirstOrDefault(o => o.name == kv.Key).rows;
                    if (mine == null) continue;
                    foreach (var row in mine)
                    {
                        if (!kv.Value.Rows.TryGetValue(row.Key, out var merged)) continue;
                        if (merged.Name == row.Value.Name) continue;
                        fn = Regex.Replace(fn, @"\b" + Regex.Escape(row.Value.Name) + @"\b", merged.Name);
                    }
                }
                outp.AddRange(Reindent(fn.Split('\n').ToList(), 0));
                outp.Add("");
            }
            return outp;
        }

        /// <summary>Does this member cover the given lane of the given row?</summary>
        private static bool Covers(CbLayout.Member m, int row, int col)
        {
            int from = m.Row * 4 + m.Col;
            int to = from + (m.IsArray ? m.Rows * 4 : TypeComps(m.Type));
            int at = row * 4 + col;
            return at >= from && at < to;
        }

        /// <summary>The row-numbered name a demoted member falls back to.</summary>
        private static CbLayout.Member Positional(int slot, CbLayout.Member row) => new()
        {
            Type = row.Type, Rows = row.Rows, IsArray = row.IsArray,
            Row = row.Row, Col = row.Col,
            Name = row.Col > 0
                ? $"_cb{slot}_row{row.Row}_{"xyzw"[row.Col]}"
                : $"_cb{slot}_row{row.Row}",
        };

        private static int TypeComps(string type) => type switch
        {
            "float" => 1, "float2" => 2, "float3" => 3, "float4" => 4, _ => 1,
        };

        private static string CbHeader(string name, int slot) =>
            $"cbuffer {name} : register(b{slot})";

        /// <summary>
        /// A whole variant body: every top-level item the parse tree carries
        /// (cbuffers, structs, globals, functions), with the entry point renamed
        /// to the ShaderLab name.  A variant must be self-contained -- each
        /// `#if` branch declares its own state, exactly like the original
        /// source's per-variant sections did.
        /// </summary>
        private static string WholeStage(Stage stage)
        {
            if (stage?.Hlsl == null) return "";
            var items = HlslTree.Split(stage.Hlsl, out var errs);
            if (items.Count == 1 && items[0].Kind == "raw")
                return items[0].Text;
            var sb2 = new StringBuilder();
            bool renamed = false;
            foreach (var it in items)
            {
                string t = it.Text;
                if (it.Kind == "func" && !renamed && !string.IsNullOrEmpty(stage.Entry))
                {
                    int nl = t.IndexOf('\n');
                    string head = nl < 0 ? t : t[..nl];
                    head = RenameEntry(head, stage.Entry);
                    t = nl < 0 ? head : head + t[nl..];
                    renamed = true;
                }
                sb2.Append(t).Append('\n');
            }
            return sb2.ToString().TrimEnd();
        }

        private static string Function(Stage stage)
        {
            if (stage?.Hlsl == null) return "";
            // The function list comes from the parse tree: whatever Hlsl.g4
            // called `functionDefinition` is emitted verbatim (line spans), so
            // helper functions survive and the entry gets renamed by editing
            // only its header line.
            var items = HlslTree.Split(stage.Hlsl, out var errs);
            var funcs = items.Where(i => i.Kind == "func").ToList();
            if (funcs.Count == 0)
            {
                // Grammar could not carry this body (raw fallback): rename the
                // first line that still looks like an entry header.
                string text = stage.Hlsl.TrimEnd();
                if (errs > 0 && !string.IsNullOrEmpty(stage.Entry))
                {
                    var lines = text.Replace("\r\n", "\n").Split('\n');
                    int at = Array.FindIndex(lines, IsEntryHeader);
                    if (at >= 0) lines[at] = RenameEntry(lines[at], stage.Entry);
                    text = string.Join("\n", lines);
                }
                return text;
            }
            var sb2 = new StringBuilder();
            for (int i = 0; i < funcs.Count; i++)
            {
                string t = funcs[i].Text;
                // Only the entry (the first function the emitter writes) gets
                // the ShaderLab name; helper functions keep their own.
                if (i == 0 && !string.IsNullOrEmpty(stage.Entry))
                {
                    int nl = t.IndexOf('\n');
                    string head = nl < 0 ? t : t[..nl];
                    head = RenameEntry(head, stage.Entry);
                    t = nl < 0 ? head : head + t[nl..];
                }
                sb2.Append(t).Append('\n');
            }
            return sb2.ToString().TrimEnd();
        }

        /// <summary>
        /// The members a buffer block declares, keyed by the lane each one starts
        /// at.  Blocks carry <c>packoffset</c>, so a row is not implied by position:
        /// the members may skip rows entirely and several may share one.
        /// </summary>
        private static Dictionary<(int row, int col), CbLayout.Member> ReadRows(List<string> block)
        {
            var rows = new Dictionary<(int row, int col), CbLayout.Member>();
            // Blocks arrive either as one string per item (parse-tree text with
            // embedded newlines) or one string per line (legacy); flatten both.
            var lines = block.SelectMany(b => b.Replace("\r\n", "\n").Split('\n')).ToList();
            foreach (var line in lines.Skip(1))
            {
                var t = line.Trim();
                if (t.StartsWith("};", StringComparison.Ordinal)) break;
                var m = MemberLine.Match(t);
                if (!m.Success) continue;
                string type = m.Groups[1].Value;
                string name = m.Groups[2].Value;
                int rows_ = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
                int row = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
                char lane = m.Groups[5].Value[0];
                rows[(row, "xyzw".IndexOf(lane))] = new CbLayout.Member
                {
                    Name = name, Type = type, Rows = rows_, IsArray = rows_ > 1, Row = row,
                    Col = "xyzw".IndexOf(lane),
                };
            }
            return rows;
        }

        /// <summary>`float4 _Color : packoffset(c2.x);` / `float4 _M[4] : packoffset(c0.x);`</summary>
        private static readonly Regex MemberLine = new(
            @"^(\w+)\s+([A-Za-z_]\w*)\s*(?:\[\s*(\d+)\s*\])?\s*:\s*packoffset\s*\(\s*c(\d+)\s*\.\s*([xyzw])\s*\)\s*;$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Union two member maps.  A real name beats a positional one, which is what
        /// keeps <c>_Color</c> when the other stage only knew its row number; a wide
        /// member beats a narrow one starting at the same lane, since it covers more
        /// of the row.
        /// </summary>
        private static void MergeRows(Dictionary<(int row, int col), CbLayout.Member> into,
                                      Dictionary<(int row, int col), CbLayout.Member> from)
        {
            foreach (var kv in from)
            {
                if (!into.TryGetValue(kv.Key, out var have)) { into[kv.Key] = kv.Value; continue; }
                bool havePositional = IsPositional(have.Name);
                bool isPositional = IsPositional(kv.Value.Name);
                if (havePositional && !isPositional) { into[kv.Key] = kv.Value; continue; }
                if (havePositional == isPositional && kv.Value.Rows > have.Rows)
                    into[kv.Key] = kv.Value;
            }
        }

        private static bool IsPositional(string name) => name.StartsWith("_cb", StringComparison.Ordinal);

        /// <summary>
        /// The first line of the entry point.  It is whatever signature the program
        /// produced -- `v2f vert(appdata v)`, `float4 frag(stage_in i) : SV_Target`,
        /// or the older `float4 main()` -- so the split looks for the shape rather
        /// than one spelling.
        /// </summary>
        private static bool IsEntryHeader(string line) =>
            line.StartsWith("v2f vert(", StringComparison.Ordinal)
            || line.StartsWith("float4 vert(", StringComparison.Ordinal)
            || line.StartsWith("float4 frag(", StringComparison.Ordinal)
            || line.StartsWith("float4 main(", StringComparison.Ordinal);

        private static string RenameEntry(string line, string entry)
        {
            int open = line.IndexOf('(');
            int space = line.LastIndexOf(' ', open < 0 ? 0 : open);
            if (space < 0 || open < 0) return line;
            return line.Substring(0, space + 1) + entry + line.Substring(open);
        }

        private static string N(float f) => f.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>Re-parse the wrapper; empty when it is well-formed.</summary>
        public static List<string> Check(string shaderLab)
        {
            var errors = new List<string>();
            var lexer = new ShaderLabLexer(new AntlrInputStream(shaderLab));
            lexer.RemoveErrorListeners();
            var parser = new ShaderLabParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new Collector(errors));
            parser.shader();
            return errors;
        }

        private sealed class Collector : BaseErrorListener
        {
            private readonly List<string> _errors;
            public Collector(List<string> errors) { _errors = errors; }
            public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                             IToken offendingSymbol, int line, int charPositionInLine,
                                             string msg, RecognitionException e) =>
                _errors.Add($"line {line}:{charPositionInLine}: {msg}");
        }
    }
}
