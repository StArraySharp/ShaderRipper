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
        /// <summary>
        /// Drop the explicit <c>: register(bN/tN/sN)</c> bindings so Unity assigns
        /// slots itself.
        ///
        /// A translated program carries the registers its backend happened to give
        /// each resource, which is rarely the numbering the original source used and
        /// never the numbering Unity's own property table expects.  Removing them lets
        /// the engine lay the program out from the declared names instead, which is
        /// what makes the file re-importable.  <c>packoffset</c> is kept either way:
        /// that is the member layout, not the binding.
        ///
        /// GLSL passes keep their registers.  That route derives both the buffer
        /// names and the register numbers from the same sequential walk, so the
        /// numbers are the only thing tying a block to the slots the original
        /// program used.
        /// </summary>
        public static bool StripRegisters;
        private static readonly Regex RegisterBinding = new(@"\s*:\s*register\s*\([^)]*\)", RegexOptions.CultureInvariant);

        /// <summary>
        /// Write each pass's original program text into the <c>.shader</c> as a
        /// comment block, right after <c>ENDHLSL</c>.
        ///
        /// The recovered body and the bytes it came from then travel together: a
        /// reader can compare the two without a second directory, a second file naming
        /// scheme or a second tool.  Nothing in the block is compiled -- ShaderLab
        /// ignores comments outside the program -- so the file stays importable.
        /// </summary>
        public static bool EmbedOriginal;

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
            /// <summary>
            /// Keywords each body was compiled with, index-aligned with
            /// <see cref="Bodies"/>.  The guard strings carry the same information,
            /// but a compressed chain has to reason about which variants a subset of
            /// branches covers, and that is a set operation rather than a string one.
            /// Empty when the producer does not supply it, which turns compression off.
            /// </summary>
            public List<HashSet<string>> Keywords = new();
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
            /// <summary>
            /// The original program text this pass was translated from, one entry per
            /// stage, written as a comment block when
            /// <see cref="ShaderLab.EmbedOriginal"/> is on.  Empty for a pass whose
            /// backend has no readable form of its own.
            /// </summary>
            public List<(string label, string text)> Originals = new();
            /// <summary>
            /// Which route produced the bodies -- <c>DXBC</c>, <c>GLSL</c>, or both.
            /// Written above <c>HLSLPROGRAM</c> so a reader knows what the block was
            /// back-translated from.
            /// </summary>
            public string Source = "";
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
            /// <summary>
            /// <c>UsePass</c> arguments, already in <c>Shader/Pass</c> form.  They are
            /// written after the passes so the file keeps the order Unity's own
            /// inspector shows, and because a borrowed pass that also appears in the
            /// body would otherwise shadow the body's copy.
            /// </summary>
            public List<string> UsePasses = new();
        }

        public static string Wrap(string name, string fallback, IReadOnlyList<SubShaderText> subShaders,
                                  IReadOnlyList<Data.Property> props, string customEditor = null)
        {
            var sb = new StringBuilder();
            // The header comment is what WarnIfForeign keys on when re-running
            // into an existing directory: without it every one of our own files
            // reads as foreign and the warning fires on the whole export.
            sb.Append("// Reconstructed ShaderLab by ShaderRipper\n");
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
                // A program can be shared by several passes -- one vertex program
                // feeding three quality levels is the common case -- and each pass
                // would otherwise copy the same listing into the file.  The block is
                // keyed by its label, which names the stage and the blob, so a second
                // pass that translated the same blob writes only a pointer to the
                // first copy.
                var seenOriginals = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var ss in subShaders)
                {
                    sb.Append("    SubShader\n    {\n");
                    if (ss.Tags.Count > 0) sb.Append("        Tags { ").Append(Tags(ss.Tags)).Append(" }\n");
                    if (ss.LOD != 0) sb.Append("        LOD ").Append(ss.LOD).Append('\n');
                    foreach (var p in ss.Passes) AppendPass(sb, p, seenOriginals);
                    foreach (string use in ss.UsePasses)
                        sb.Append("        UsePass \"").Append(use.Replace('"', '_')).Append("\"\n");
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

        private static void AppendPass(StringBuilder sb, PassText p,
                                       Dictionary<string, int> seenOriginals = null)
        {
            if (!StripRegisters || p.Source == "GLSL")
            {
                AppendPassCore(sb, p, seenOriginals);
                return;
            }
            var pass = new StringBuilder();
            AppendPassCore(pass, p, seenOriginals);
            sb.Append(RegisterBinding.Replace(pass.ToString(), ""));
        }

        private static void AppendPassCore(StringBuilder sb, PassText p,
                                           Dictionary<string, int> seenOriginals = null)
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
                AppendOriginals(sb, p);
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
            AppendSource(sb, p);
            sb.Append("            HLSLPROGRAM\n");
            // The target comes first: Unity decides the profile it compiles the
            // kernel for from this line, and a UAV-using program was being built as
            // ps_4_0 ("the target doesn't support UAVs") when the pragma sat after
            // the entry points.
            AppendTarget(sb, p, merged);
            if (p.Vertex != null) sb.Append("            #pragma vertex ").Append(p.Vertex.Entry).Append('\n');
            else if (synthVertex) sb.Append("            #pragma vertex vert\n");
            if (p.Fragment != null) sb.Append("            #pragma fragment ").Append(p.Fragment.Entry).Append('\n');
            foreach (var line in p.Pragmas) sb.Append("            ").Append(line).Append('\n');
            foreach (var note in p.Notes) sb.Append("            // ").Append(note).Append('\n');
            foreach (var line in merged) AppendBodyLine(sb, line);
            sb.Append("            ENDHLSL\n");
            AppendOriginals(sb, p, seenOriginals);
            sb.Append("        }\n\n");
        }

        /// <summary>
        /// The pass's original program text, as a comment block after the program.
        ///
        /// Written only when <see cref="EmbedOriginal"/> is on.  The block is fenced
        /// with <c>BEGIN</c>/<c>END</c> markers and every line is commented out, so
        /// ShaderLab ignores it entirely and a reader (or a script) can lift it back
        /// out by those markers.
        ///
        /// A line that is already a comment keeps its own marker instead of gaining a
        /// second one.  A DXBC disassembly is written entirely in <c>//</c> comments,
        /// and prefixing those produced <c>// // POSITION  0  xyzw ...</c> -- the
        /// signature table then read as doubled punctuation rather than as a table.
        /// The original text is also reproduced verbatim, indentation included: it is
        /// a copy of another file, not code this writer owns.
        /// </summary>
        private static void AppendOriginals(StringBuilder sb, PassText p,
                                            Dictionary<string, int> seenOriginals = null)
        {
            if (!EmbedOriginal || p.Originals.Count == 0) return;
            foreach (var (label, text) in p.Originals)
            {
                if (string.IsNullOrEmpty(text)) continue;

                // A blob shared by several passes is written once.  The label names
                // the stage and the blob, so an earlier copy is the same text -- and
                // repeating a 700-line listing three times per file is most of the
                // file's size for no added information.
                if (seenOriginals != null)
                {
                    if (seenOriginals.TryGetValue(label, out int first))
                    {
                        sb.Append("            // ==== ORIGINAL ").Append(label)
                          .Append(" -- written above (pass ").Append(first).Append(") ====\n");
                        continue;
                    }
                    seenOriginals[label] = seenOriginals.Count;
                }

                sb.Append("            // ==== BEGIN ORIGINAL ").Append(label).Append(" ====\n");
                foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                {
                    string body = line.TrimEnd('\r');
                    if (body.Trim().Length == 0) { sb.Append('\n'); continue; }

                    // The line already carries a `//` comment marker (a DXBC
                    // disassembly is written entirely in them).  Repeating it gives
                    // `// // POSITION 0 xyzw ...`, which reads as doubled punctuation
                    // instead of a signature table, so the existing marker is kept and
                    // only the ShaderLab fence is added.
                    //
                    // Only `//` qualifies: ShaderLab treats `;` as a real token, so a
                    // SPIR-V listing's `;` prefix must be commented out, not trusted.
                    string trimmed = body.TrimStart();
                    bool commented = trimmed.StartsWith("//", StringComparison.Ordinal);
                    sb.Append("            ");
                    sb.Append(commented ? body : "// " + body).Append('\n');
                }
                sb.Append("            // ==== END ORIGINAL ").Append(label).Append(" ====\n");
            }
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
        private static void AppendTarget(StringBuilder sb, PassText p, IEnumerable<string> body = null)
        {
            // The translated body is register level: `Texture2D` + `SamplerState`
            // declarations, `.Sample()` lookups, `asuint`/`asfloat` and `[loop]`.
            // Unity compiles a program with no `#pragma target` as 2.5, which
            // rejects every one of those, so the pass states the model the original
            // program was built for -- and never less than 4.0, because the syntax
            // itself requires it.
            int model = Math.Max(p.Target, 40);
            // Multisampled textures, UAVs and `Texture.Load` are SM5 features.  The
            // target is also what Unity compiles the kernel against, so a 4.0
            // declaration made those programs fail to build -- and the failure is
            // reported as "did not find shader kernel 'frag' to compile", which
            // reads like a missing entry point rather than a wrong target.
            if (body != null && body.Any(l => l != null &&
                    (l.Contains("Texture2DMS") || l.Contains("RWByteAddressBuffer")
                     || l.Contains("RWTexture") || l.Contains("ByteAddressBuffer")
                     || l.Contains("StructuredBuffer") || l.Contains(".Load("))))
                model = Math.Max(model, 50);
            sb.Append("            #pragma target ").Append(model / 10).Append('.')
              .Append(model % 10).Append('\n');
        }

        /// <summary>
        /// The line above <c>HLSLPROGRAM</c> naming what the block was
        /// back-translated from.  Everything inside the block is machine-written, and
        /// a reader deciding whether to trust it needs to know which route produced
        /// it: DXBC is an instruction-by-instruction transliteration, GLSL a
        /// source-level rewrite.
        /// </summary>
        private static void AppendSource(StringBuilder sb, PassText p)
        {
            if (!string.IsNullOrEmpty(p.Source))
                sb.Append("            // Translated from ").Append(p.Source).Append(" language\n");
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

            AppendSource(sb, p);
            sb.Append("            HLSLPROGRAM\n");
            AppendTarget(sb, p, p.Chains.SelectMany(c => c.Bodies));
            if (hasVert) sb.Append("            #pragma vertex vert\n");
            else if (synth != null) sb.Append("            #pragma vertex vert\n");
            if (hasFrag) sb.Append("            #pragma fragment frag\n");
            foreach (var pragma in p.Pragmas) sb.Append("            ").Append(pragma).Append('\n');
            foreach (var note in p.Notes) sb.Append("            // ").Append(note).Append('\n');

            foreach (var chain in p.Chains)
            {
                if (chain.Bodies.Count == 0) continue;

                // A chain with many branches repeats every shared declaration and
                // function once per branch.  Folding those into one guarded cascade
                // keeps the file readable without changing what any variant compiles
                // to; the shrinker declines (and the chain is emitted as-is) whenever
                // it cannot prove the rewrite equivalent.
                bool shrunk = VariantShrink.Apply(chain);
                if (Environment.GetEnvironmentVariable("SHADERRIP_SHRINKDBG") == "1")
                    Console.Error.WriteLine($"chain: bodies={chain.Bodies.Count} shrunk={shrunk}");

                var bodyLines = chain.Bodies
                    .Select(b => (b ?? "").Replace("\r\n", "\n").Split('\n'))
                    .ToList();

                // One body with no guard is unconditional -- either the chain had a
                // single variant to begin with or the shrinker collapsed it -- so the
                // `#if 1` / `#else` wrapper would only repeat it.
                if (bodyLines.Count == 1 && (chain.Guards.Count == 0 || chain.Guards[0].Length == 0))
                {
                    if (synth != null)
                        foreach (var l in synth.Replace("\r\n", "\n").Split('\n'))
                            AppendBodyLine(sb, l);
                    foreach (var l in bodyLines[0]) AppendBodyLine(sb, l);
                    continue;
                }

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
                // The fallback is only needed when a preprocessor state can miss
                // every guard; the guards are exact over the keyword set, so that
                // happens exactly when some guard is not the strict complement of
                // the ones before it.  Testing the last guard is enough: guards are
                // mutually exclusive by construction (each names one keyword state),
                // so if the last one already covers every remaining state, the whole
                // chain does.  Dropping a redundant `#else` also removes the only
                // source of a duplicated cbuffer: branch 0's copy is otherwise
                // written twice, and two declarations of one cbuffer name in a
                // single program is a compile error.
                string lastGuard = chain.Guards.Count > 0 ? chain.Guards[chain.Guards.Count - 1] : "";
                if (!CoversAllStates(lastGuard, chain.Guards))
                {
                    sb.Append("            #else\n");
                    if (synth != null)
                        foreach (var l in synth.Replace("\r\n", "\n").Split('\n'))
                            AppendBodyLine(sb, l);
                    foreach (var l in bodyLines[0]) AppendBodyLine(sb, l);
                }
                sb.Append("            #endif\n");
            }
        }

        /// <summary>Does this program text define the given entry point?</summary>
        private static bool DefinesEntry(string body, string name) =>
            body != null && Regex.IsMatch(body, @"\b" + name + @"\s*\(");

        /// <summary>
        /// Does this guard already cover every preprocessor state the chain leaves?
        ///
        /// The guards come from <c>VariantPlan.Guard</c>, one term per keyword group:
        /// either <c>defined(X)</c> for the alternative the branch was built with, or
        /// <c>!defined(X) && !defined(Y)...</c> for a branch with the group off.  A
        /// guard carries a term for a group only when the branch fixed that group, so
        /// a guard that mentions every keyword of the chain -- positively or negatively
        /// -- is true for exactly one state and false for all the others: the branches
        /// together partition the whole space, and the `#else` fallback behind them
        /// would never compile.  Dropping it also removes the only source of a
        /// duplicated cbuffer: branch 0's copy would otherwise be written a second
        /// time, and two declarations of one cbuffer name in a single program is a
        /// compile error.
        ///
        /// The keyword set is read off the guards themselves: an atom appearing
        /// anywhere in the chain names a group member, and every guard spells a term
        /// for the groups its branch fixed.  A guard that mentions all of those atoms
        /// therefore partitions the space regardless of how many groups are silent.
        /// </summary>
        private static bool CoversAllStates(string guard, IEnumerable<string> allGuards)
        {
            if (string.IsNullOrEmpty(guard)) return false;
            var atoms = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(string.Join("\n", allGuards), @"(!?)\s*defined\((\w+)\)"))
                atoms.Add(m.Groups[2].Value);

            var mine = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(guard, @"(!?)\s*defined\((\w+)\)"))
                mine.Add(m.Groups[2].Value);
            return mine.Count >= atoms.Count && !atoms.Except(mine).Any();
        }

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
                {
                    // A numeric constructor has to be given exactly as many
                    // components as the type has: `float3(uv)` is a GLSL spelling
                    // (`vec3(vec2)` pads z) and fxc rejects it outright, which is
                    // what "incorrect number of arguments to numeric-type
                    // constructor" means here.
                    string v = f.type switch
                    {
                        "float" => "uv.x",
                        "float2" => "uv",
                        "float3" => "float3(uv, 0.0)",
                        "float4" => "float4(uv, 0.0, 0.0)",
                        _ => "uv.x",
                    };
                    sb.Append($"\to.{f.name} = {v};\n");
                }
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
            /// <summary>Register the buffer was declared at, before slots are renumbered.</summary>
            public int Reg = -1;
            /// <summary>Members keyed by the lane each starts at.</summary>
            public readonly Dictionary<(int row, int col), CbLayout.Member> Rows = new();
            /// <summary>
            /// Non-zero when some program of this pass declared the buffer as an
            /// array because it addresses rows with a runtime index.  One merged
            /// declaration has to serve every program in the block, and only the
            /// array form can express `cb2[r0.x + 1]`; the named members of the
            /// other programs are rewritten to `cbN[row]` to match.
            /// </summary>
            public int ArrayRows;
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
                @"^\s*(?:Texture\w*(?:<[^>]*>)?|SamplerState|sampler\w*|Buffer(?:<[^>]*>)?|" +
                @"RWTexture\w*(?:<[^>]*>)?|RWBuffer(?:<[^>]*>)?|struct\s+\w+|" +
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

            // The SPIR-V route's two stages declare their interface separately --
            // the vertex returns `SPIRV_Cross_Output_vs` and the fragment takes
            // `SPIRV_Cross_Input_ps` -- and Unity only links a pixel input that
            // carries `SV_Position` against a vertex output that has it.  Merging
            // the pair into one `v2f` struct (plus the pixel's own output struct for
            // SV_Target) is what makes the link hold: rB rendered, the unmerged
            // export did not, with identical bodies.  The merge rewrites both entry
            // signatures and the member accesses through the old names, so the SSA
            // bodies need no other edit.  A non-SPIRV pair (GLSL, DXBC) has no such
            // structs and passes through untouched.
            (vs, ps) = MergeSpirvInterface(vs, ps);

            // The replacement tally is per pass, not per file.
            UnityIncludes.Reset();

            var buffers = new Dictionary<string, Buf>(StringComparer.Ordinal);
            var structs = new List<List<string>>();
            var structNames = new HashSet<string>(StringComparer.Ordinal);
            // per stage: register -> (buffer name, members as declared there)
            var perStage = new List<(Stage stage, Dictionary<int, (string name, Dictionary<(int row, int col), CbLayout.Member> rows)> own)>();
            var globals = new List<string>();
            var seenGlobal = new HashSet<string>(StringComparer.Ordinal);

            // Comments are lexer noise to the HLSL grammar, so the parse-tree split
            // cannot see them and they would never reach the merged program.  An
            // emitter pass (or a diagnostic the translator emitted) puts them in the
            // body, so they are collected separately and re-appended below -- dropping
            // them made a pass that had run look like it had never been called.
            var carried = new List<string>();
            var carriedSeen = new HashSet<string>(StringComparer.Ordinal);
            void CollectComments(string hlsl)
            {
                if (hlsl == null) return;
                foreach (string raw in hlsl.Replace("\r\n", "\n").Split('\n'))
                {
                    string trimmed = raw.Trim();
                    if (!trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (carriedSeen.Add(trimmed)) carried.Add(trimmed);
                }
            }

            foreach (var stage in new[] { vs, ps })
            {
                if (stage?.Hlsl == null) continue;
                CollectComments(stage.Hlsl);

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
                                buf = new Buf { Header = CbHeader(it.Name, reg), Reg = reg };
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
                            // A program that indexes the buffer at runtime declares it
                            // as `float4 cb2[n]` instead of naming rows.  The merged
                            // block must keep the array form, otherwise the rest of
                            // that program's body reads `cb2[27]` with nothing
                            // declared under that name (X3004).
                            var arr = Regex.Match(it.Text, @"float4\s+cb\d+\s*\[\s*(\d+)\s*\]");
                            if (arr.Success && int.TryParse(arr.Groups[1].Value, out int an) && an > 0)
                                buf.ArrayRows = Math.Max(buf.ArrayRows, an);
                            break;
                        }

                        case "decl":
                        case "semi":
                        case "pp":
                        {
                            string trimmed = it.Text.Trim();
                            if (trimmed.Length == 0) continue;

                            // A comment line is kept, not skipped.  It used to be
                            // dropped here as parser noise, which meant any diagnostic
                            // or provenance line a stage body carried -- including the
                            // output of an emitter pass -- vanished at merge time and
                            // looked like the pass had never run.  Exact duplicates are
                            // still collapsed, so the two stages repeating one comment
                            // does not double it.
                            if (trimmed.StartsWith("//", StringComparison.Ordinal))
                            {
                                if (seenGlobal.Add(trimmed)) globals.Add(it.Text.TrimEnd());
                                continue;
                            }

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

            // Every identifier the program is about to declare, so a demoted member
            // can be given a suffix that is actually free.
            var taken = new HashSet<string>(structNames, StringComparer.Ordinal);
            foreach (var kv in buffers)
                foreach (var row in kv.Value.Rows.Values) taken.Add(row.Name);
            if (_properties != null) taken.UnionWith(_properties);

            // One name can appear in two registers: a stage that could not identify
            // b0 names its rows from the property table and arrives at
            // `_RendererColor`, which the other stage places in `UnityPerDrawSprite`
            // where it belongs.  A member may only be declared once, so the name
            // stays in the buffer that is one Unity defines, and the other
            // occurrence falls back to a suffixed name.
            //
            // A MATERIAL PROPERTY has no such fallback.  Unity fills values by
            // property name, so a copy renamed to `_Color_2` is a name no property
            // answers and it reads zero forever -- under premultiplied-alpha blending
            // that turns the whole material transparent, with no compile error and
            // no warning.  The Vulkan payload records the same property in both
            // stages' buffers (`PGlobals` for the fragment's copy, `VGlobals` for the
            // vertex's), so the duplicate is common; the loser is deleted and the
            // stage bodies that spelled it are rewritten to the winner's name, which
            // reads the same bytes the panel sets.
            var owner = new Dictionary<string, string>(StringComparer.Ordinal);
            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
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
                    if (demote && row.Name.StartsWith("_", StringComparison.Ordinal))
                    {
                        // A property duplicated across the stages' buffers: the copy
                        // here would be renamed to a name no property answers, so it
                        // is deleted and the bodies that spell it are pointed at the
                        // winner -- the same identifier, declared once in the buffer
                        // that kept it.  The rename map runs before the per-row
                        // rewrites, so no `_Color` → `_Color_2` intermediate appears.
                        renames[row.Name] = row.Name;
                        continue;
                    }
                    fixedRows.Add(demote ? Demoted(row, taken) : row);
                }
                kv.Value.Rows.Clear();
                foreach (var r in fixedRows) kv.Value.Rows[(r.Row, r.Col)] = r;

                // The two stages of one variant can name the same member at
                // different offsets: each program keeps its own $Globals layout, and
                // a variant whose vertex stage packed one matrix more shifts every
                // property after it.  Only one declaration can carry the name, so
                // the first keeps it and the others become suffixed members --
                // each body then reads the bytes its own program actually named,
                // instead of every body reading the same (possibly wrong) ones.
                // A property is the exception again: a suffixed copy reads zero, so
                // it is dropped and its readers follow the first declaration.
                var claimed = new Dictionary<string, (int row, int col)>(StringComparer.Ordinal);
                foreach (var key in kv.Value.Rows.Keys.OrderBy(k => k.row).ThenBy(k => k.col).ToList())
                {
                    var row = kv.Value.Rows[key];
                    if (IsPositional(row.Name)) continue;
                    if (claimed.TryAdd(row.Name, key)) continue;
                    if (row.Name.StartsWith("_", StringComparison.Ordinal))
                    {
                        renames[row.Name] = row.Name;
                        kv.Value.Rows.Remove(key);
                        continue;
                    }
                    kv.Value.Rows[key] = Demoted(row, taken);
                }
            }

            var outp = new List<string>();
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

                // An array-form block has no named members at all, which is not the
                // same as an empty one: it still has to be declared.
                if (keptMembers.Count == 0 && kv.Value.ArrayRows == 0) continue;   // the whole block was engine-owned

                string header = kv.Value.Header.Replace(
                    Regex.Match(kv.Value.Header, @"register\(b\d+\)").Value,
                    $"register(b{slotOf[kv.Key]})");
                // The array form wins for the whole block: it is the only spelling
                // that can express a runtime row index, and the named members the
                // other programs use are rewritten to `cbN[row]` below.
                if (kv.Value.ArrayRows > 0)
                {
                    outp.Add(header.Trim());
                    outp.Add("{");
                    outp.Add($"    float4 cb{slotOf[kv.Key]}[{kv.Value.ArrayRows}];");
                    outp.Add("};");
                    outp.Add("");
                    continue;
                }
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
                string fn = Function(stage);                // A buffer renumbered during the merge (`b1` becoming `b4` because a
                // pass drew from more of them) is still `cb1[...]` in every body that
                // addressed it as an array.  The declaration sits under the new slot,
                // so the references have to move with it.
                //
                // Which name a register had is a PER-STAGE fact: two programs both
                // call their first buffer `b1` and still mean different things
                // (`CB1` vs `UnityPerDraw`), so walking the merged map would rewrite
                // one stage's bodies with the other stage's slot.
                foreach (var kv in own)
                {
                    if (kv.Value.name == null) continue;
                    if (!buffers.TryGetValue(kv.Value.name, out var b)) continue;
                    if (b.Reg < 0 || b.Reg == slotOf[kv.Value.name]) continue;
                    fn = Regex.Replace(fn, @"\bcb" + b.Reg + @"\[", $"cb{slotOf[kv.Value.name]}[");
                }
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
                    // A block declared as an array has no named members left: every
                    // reference is `cbN[row]`, addressed the way the program that
                    // forced the array form addresses it.
                    if (kv.Value.ArrayRows > 0)
                    {
                        string cbName = $"cb{slotOf[kv.Key]}";
                        foreach (var row in mine)
                            fn = Regex.Replace(fn, @"\b" + Regex.Escape(row.Value.Name) + @"\b",
                                               $"{cbName}[{row.Key.Item1}]");
                        continue;
                    }
                    foreach (var row in mine)
                    {
                        if (!kv.Value.Rows.TryGetValue(row.Key, out var merged)) continue;
                        if (merged.Name == row.Value.Name) continue;
                        // A matrix row is addressed with `[k]`, never by bare name:
                        // the include declares `float4x4 unity_ObjectToWorld`, and
                        // `unity_ObjectToWorld.xyzw` is not a swizzle a matrix has
                        // ("invalid subscript 'xyzw'").  The stage's own float4 name
                        // for that row is what gets rewritten, so the row index comes
                        // from the merged member's base.
                        string target = merged.IsArray && row.Value.Col == 0
                            ? $"{merged.Name}[{row.Key.Item1 - merged.Row}]"
                            : merged.Name;
                        // The body may already index the row -- `_5_m0[2]` for the third
                        // row of a matrix, which the emitter writes for a member it could
                        // only describe row by row.  Appending our own `[k]` in front of
                        // that produces `M[0][2]`, and a `float4[4]` declaration has no
                        // second dimension to index ("invalid subscript").  Folding the
                        // two indices into one keeps the declaration the flat array it is.
                        if (merged.IsArray && row.Value.Col == 0)
                        {
                            int baseRow = row.Key.Item1 - merged.Row;
                            fn = Regex.Replace(fn,
                                @"\b" + Regex.Escape(row.Value.Name) + @"\s*\[\s*(\d+)\s*\]",
                                m => $"{merged.Name}[{baseRow + int.Parse(m.Groups[1].Value)}]");
                        }
                        fn = Regex.Replace(fn, @"\b" + Regex.Escape(row.Value.Name) + @"\b", target);
                    }
                }
                outp.AddRange(Reindent(fn.Split('\n').ToList(), 0));
                outp.Add("");
            }

            // A folded texture transform calls the macro from `UnityCG.cginc`.  The
            // include is only pulled in when some engine NAME was drawn from it, and
            // a program built from material properties alone draws on nothing -- so
            // the include is added here when the body needs it and nothing else
            // asked for it.
            if (!outp.Any(line => line.Contains("UnityCG.cginc")) &&
                outp.Any(line => line.Contains("TRANSFORM_TEX(")))
            {
                outp.InsertRange(0, new[] { "#include \"UnityCG.cginc\"", "" });
            }

            // Comments the stage bodies carried, put back after the declarations.  They
            // are not part of the program, so position does not matter; what matters is
            // that an emitter pass's output survives the merge instead of vanishing.
            if (carried.Count > 0)
            {
                if (outp.Count > 0 && outp[outp.Count - 1].Length > 0) outp.Add("");
                outp.AddRange(carried);
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

        /// <summary>
        /// A readable name for a member whose own name is already declared by
        /// another buffer of the same program.
        ///
        /// HLSL rejects a second declaration of one member name anywhere in the
        /// translation unit, not just within a block, so one of the two copies has to
        /// be renamed.  The suffix keeps the name a reader can still recognise
        /// (<c>_OutlineSoftness_2</c>) instead of falling back to the row number the
        /// block happens to place it at.
        ///
        /// The member keeps its type and layout: only the identifier changes, and the
        /// caller rewrites the stage bodies that spell the old one.
        /// </summary>
        private static CbLayout.Member Demoted(CbLayout.Member row, HashSet<string> taken)
        {
            string name = row.Name;
            for (int n = 2; !taken.Add(name); n++) name = row.Name + "_" + n;
            return new CbLayout.Member
            {
                Type = row.Type, Rows = row.Rows, IsArray = row.IsArray, Matrix = row.Matrix,
                Row = row.Row, Col = row.Col, Name = name,
            };
        }

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

        /// <summary>
        /// Merge the SPIR-V route's per-stage interface structs into one v2f.
        ///
        /// The vertex translation returns <c>SPIRV_Cross_Output_vs</c>; the fragment
        /// translation takes <c>SPIRV_Cross_Input_ps</c> and returns its own
        /// <c>SPIRV_Cross_Output_ps</c>.  The two interpolator lists carry the same
        /// members under the same names (the merge already collapsed `_Color_2` and
        /// friends), but as two declarations Unity's pixel-stage link does not bind:
        /// the control-variable series showed the unmerged export renders nothing
        /// while the v2f shape renders, with identical bodies.
        ///
        /// The merged text keeps one <c>v2f</c> (vertex members + SV_Position), the
        /// pixel output struct renamed so it cannot collide, and both entries
        /// retyped; every <c>stage_output.</c> / <c>stage_input.</c> access keeps
        /// working because the member names are unchanged.  A stage without the
        /// SPIRV-Cross shape (GLSL, DXBC, a fragment-only pass) passes through.
        /// </summary>
        private static (Stage, Stage) MergeSpirvInterface(Stage vs, Stage ps)
        {
            if (vs?.Hlsl == null || ps?.Hlsl == null) return (vs, ps);
            if (!vs.Hlsl.Contains("struct SPIRV_Cross_Output_vs") ||
                !ps.Hlsl.Contains("struct SPIRV_Cross_Input_ps")) return (vs, ps);

            string vsOut = ReadStruct(vs.Hlsl, "SPIRV_Cross_Output_vs");
            string psIn = ReadStruct(ps.Hlsl, "SPIRV_Cross_Input_ps");
            string psOut = ReadStruct(ps.Hlsl, "SPIRV_Cross_Output_ps");
            if (vsOut == null || psIn == null || psOut == null) return (vs, ps);

            // The interpolator set: the vertex output's members minus its position.
            // The fragment reads a SUBSET of them -- a vertex program routinely emits
            // an interpolator a keyword-off path never consumes, and requiring the two
            // lists to be equal refused exactly those programs and left them unmerged
            // (an unmerged pair does not link, so the material drew nothing).
            // Matching by semantic keeps the vertex's full output and points each
            // fragment member at the vertex member carrying the same semantic; the two
            // modules name their copies independently (the vertex calls the colour
            // `vs_82`, the fragment `ps_105`).
            var members = new List<(string type, string name, string semantic)>();
            foreach (var m in StructMembers(vsOut))
            {
                if (m.semantic == "SV_Position") continue;
                members.Add(m);
            }
            var pixelSide = StructMembers(psIn).Where(m => m.semantic != "SV_Position").ToList();

            // A program with no interpolators at all is legal -- a solid-colour or
            // clear pass writes only the clip-space position and reads nothing
            // interpolated -- and it still has to merge: the pixel input declaring
            // SV_Position as its own struct does not link against the vertex output
            // any more than a richer pair would.
            var bySemantic = new Dictionary<string, (string type, string name, string semantic)>(StringComparer.Ordinal);
            foreach (var m in members)
                if (!bySemantic.ContainsKey(m.semantic)) bySemantic[m.semantic] = m;

            // A fragment member with no vertex counterpart reads an interpolator the
            // vertex never writes: nothing a merged declaration could supply.
            foreach (var m in pixelSide)
                if (!bySemantic.ContainsKey(m.semantic)) return (vs, ps);

            // Fragment spelling -> vertex spelling.
            var pixelNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var m in pixelSide)
            {
                string target = bySemantic[m.semantic].name;
                if (m.name != target) pixelNames[m.name] = target;
            }

            // v2f: position first, then the interpolators in vertex order.
            var sb = new StringBuilder();
            sb.Append("struct v2f\n{\n");
            sb.Append("    float4 gl_Position : SV_Position;\n");
            foreach (var (type, name, semantic) in members)
                sb.Append($"    {type} {name} : {semantic};\n");
            sb.Append("};");
            string v2f = sb.ToString();

            // The pixel output keeps its own name, tagged so no variant can collide
            // with the vertex side or with a second variant.
            string pixelOut = psOut.Replace("SPIRV_Cross_Output_ps", "SPIRV_Cross_Output_pixel");

            string ApplyVertex(string hlsl)
            {
                // Struct declarations: the vertex input (mesh streams) stays; the
                // vertex output folds into v2f.  The pixel-side structs are untouched
                // here -- their text lives in the fragment stage.
                hlsl = ReplaceStruct(hlsl, "SPIRV_Cross_Output_vs", "");

                hlsl = System.Text.RegularExpressions.Regex.Replace(
                    hlsl, @"SPIRV_Cross_Output_vs\s+vert\s*\(",
                    "v2f vert(");
                hlsl = hlsl.Replace("SPIRV_Cross_Output_vs stage_output;", "v2f stage_output;");
                return hlsl;
            }

            string ApplyFragment(string hlsl)
            {
                // The pixel input folds into v2f, whose members spell the vertex's
                // names; every `stage_input.<pixel name>` moves to the vertex name.
                hlsl = ReplaceStruct(hlsl, "SPIRV_Cross_Input_ps", "");
                hlsl = ReplaceStruct(hlsl, "SPIRV_Cross_Output_ps", pixelOut);
                int at = hlsl.IndexOf("struct SPIRV_Cross_Output_pixel", StringComparison.Ordinal);
                hlsl = hlsl.Insert(at < 0 ? 0 : at, v2f + "\n\n            ");

                hlsl = System.Text.RegularExpressions.Regex.Replace(
                    hlsl, @"SPIRV_Cross_Output_ps\s+frag\s*\(\s*SPIRV_Cross_Input_ps\s+(\w+)\s*\)",
                    "SPIRV_Cross_Output_pixel frag(v2f $1)");
                hlsl = hlsl.Replace("SPIRV_Cross_Output_ps stage_output;", "SPIRV_Cross_Output_pixel stage_output;");

                foreach (var kv in pixelNames)
                    hlsl = System.Text.RegularExpressions.Regex.Replace(
                        hlsl, @"\bstage_input\." + System.Text.RegularExpressions.Regex.Escape(kv.Key) + @"\b",
                        "stage_input." + kv.Value);
                return hlsl;
            }

            return (new Stage { Hlsl = ApplyVertex(vs.Hlsl), Entry = vs.Entry },
                       new Stage { Hlsl = ApplyFragment(ps.Hlsl), Entry = ps.Entry });
        }

        /// <summary>The struct declaration's full text, or null when absent.</summary>
        private static string ReadStruct(string hlsl, string name) =>
            Plugins.Hlsl.StructTools.ReadStruct(hlsl, name);

        /// <summary>Remove one struct declaration from the text.</summary>
        private static string ReplaceStruct(string hlsl, string name, string replacement) =>
            Plugins.Hlsl.StructTools.ReplaceStruct(hlsl, name, replacement);

        /// <summary>
        /// The members of a struct body: type, name, semantic.  Only the
        /// `type name : semantic;` shape the translators emit is understood.
        /// </summary>
        private static List<(string type, string name, string semantic)> StructMembers(string structText)
        {
            var result = new List<(string, string, string)>();
            foreach (Plugins.Hlsl.StructMember m in Plugins.Hlsl.StructTools.Members(structText))
                result.Add((m.Type, m.Name, m.Semantic));
            return result;
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
                char lane = m.Groups[5].Success ? m.Groups[5].Value[0] : 'x';
                // A `float4x4` member is four rows, spelled as a type instead of as
                // an array; the row accounting has to see the same span either way.
                bool matrix = type == "float4x4";
                if (matrix) rows_ = 4;
                rows[(row, "xyzw".IndexOf(lane))] = new CbLayout.Member
                {
                    Name = name, Type = matrix ? "float4" : type, Rows = rows_,
                    IsArray = rows_ > 1, Matrix = matrix, Row = row,
                    Col = "xyzw".IndexOf(lane),
                };
            }
            return rows;
        }

        /// <summary>
        /// `float4 _Color : packoffset(c2.x);` / `float4 _M[4] : packoffset(c0.x);`
        /// / `float4 _Color : packoffset(c2);`
        ///
        /// The lane is optional in HLSL and means `x` when omitted: an emitter that
        /// only lays members out by row writes the short form, and refusing it here
        /// dropped every such member -- the whole block then looked empty and was
        /// not declared at all.
        /// </summary>
        private static readonly Regex MemberLine = new(
            @"^(\w+)\s+([A-Za-z_]\w*)\s*(?:\[\s*(\d+)\s*\])?\s*:\s*packoffset\s*\(\s*c(\d+)\s*(?:\.\s*([xyzw]))?\s*\)\s*;$",
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
