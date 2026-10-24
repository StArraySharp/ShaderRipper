using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Data = ShaderRipper.Antlr.Data;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// Wraps translated programs in ShaderLab, and checks the result by parsing
    /// it back with <c>ShaderLab.g4</c>.
    ///
    /// Wrapping needs no grammar -- it is a template -- but the check does: a
    /// missing brace or an empty tag is invisible to the code that wrote it, and
    /// this is the cheapest place to catch it, before anyone opens the file.
    ///
    /// A pass holds both stages in one <c>HLSLPROGRAM</c>, which means merging two
    /// programs that were emitted independently.  They declare the same globals
    /// (temporaries, samplers) and often the same constant buffers with different
    /// extents, so the merge is not concatenation: duplicates are dropped, and a
    /// buffer declared twice keeps the longer body, which is a superset of the
    /// shorter one's rows and references.
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
        }

        public sealed class SubShaderText
        {
            public int LOD;
            public List<(string k, string v)> Tags = new();
            public List<PassText> Passes = new();
        }

        public static string Wrap(string name, string fallback, IReadOnlyList<SubShaderText> subShaders,
                                  IReadOnlyList<Data.Property> props)
        {
            var sb = new StringBuilder();
            sb.Append("Shader \"").Append(name.Replace('"', '_')).Append("\"\n{\n");

            AppendProperties(sb, props);

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
            return sb.ToString();
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
                string type = p.Type switch
                {
                    0 => "Color", 1 => "Vector", 2 => "Float", 3 => "Range(0, 1)",
                    4 => "2D", 5 => "3D", 6 => "Cube", 7 => "2DArray",
                    _ => "Vector",
                };
                string label = string.IsNullOrEmpty(p.Description) ? p.Name : p.Description;
                // A Float or Range property takes a scalar, a Color or Vector a
                // four-component tuple, a texture a name and an empty body.  Writing
                // a tuple for a Float is not valid ShaderLab.
                string value = p.Type switch
                {
                    4 or 5 or 6 or 7 => "\"" + (string.IsNullOrEmpty(p.DefTextureName) ? "white" : p.DefTextureName) + "\" {}",
                    2 or 3 => N(p.DefValue[0]),
                    _ => $"({N(p.DefValue[0])}, {N(p.DefValue[1])}, {N(p.DefValue[2])}, {N(p.DefValue[3])})",
                };
                sb.Append($"        {p.Name} (\"{label}\", {type}) = {value}\n");
            }
            sb.Append("    }\n");
        }

        private static void AppendPass(StringBuilder sb, PassText p)
        {
            sb.Append("        Pass\n        {\n");
            if (!string.IsNullOrEmpty(p.Name)) sb.Append("            Name \"").Append(p.Name).Append("\"\n");
            if (p.Tags.Count > 0) sb.Append("            Tags { ").Append(Tags(p.Tags)).Append(" }\n");
            foreach (var s in p.States) sb.Append("            ").Append(s).Append('\n');

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

            sb.Append("            HLSLPROGRAM\n");
            if (p.Vertex != null) sb.Append("            #pragma vertex ").Append(p.Vertex.Entry).Append('\n');
            if (p.Fragment != null) sb.Append("            #pragma fragment ").Append(p.Fragment.Entry).Append('\n');
            foreach (var line in merged) sb.Append("            ").Append(line).Append('\n');
            sb.Append("            ENDHLSL\n");
            sb.Append("        }\n\n");
        }

        /// <summary>
        /// A pass built from keyword variants: one guard chain per stage (DXBC)
        /// or one combined chain (GLSL).  Variant bodies come from the parse
        /// tree, so each `#if` branch re-emits whole grammatical items instead
        /// of glued strings.  Variant 0 is repeated under `#else` as the
        /// fallback for combinations Unity never compiled -- the same shape the
        /// original source had before Unity stripped its variants.
        /// </summary>
        private static void WriteVariantProgram(StringBuilder sb, PassText p)
        {
            sb.Append("            HLSLPROGRAM\n");
            sb.Append("            #pragma vertex vert\n");
            sb.Append("            #pragma fragment frag\n");
            if (p.GlslIncludes)
            {
                sb.Append("            #pragma target 3.0\n");
                sb.Append("            #include \"HLSLSupport.cginc\"\n");
                sb.Append("            #include \"UnityCG.cginc\"\n");
            }
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
                    foreach (var l in bodyLines[vi])
                        sb.Append(l.Length == 0 ? "\n" : "            ").Append(l).Append('\n');
                }
                // Combinations the build never shipped: compile the base variant.
                sb.Append("            #else\n");
                foreach (var l in bodyLines[0])
                    sb.Append(l.Length == 0 ? "\n" : "            ").Append(l).Append('\n');
                sb.Append("            #endif\n");
            }
        }

        private static string Tags(IEnumerable<(string k, string v)> tags) =>
            string.Join(" ", tags.Select(t => $"\"{t.k}\" = \"{t.v}\""));

        /// <summary>
        /// One program block for a pass: constant buffers merged by NAME (not by
        /// register), then the remaining globals once each, then the two entry
        /// points.
        ///
        /// Keying by name is the load-bearing decision.  fxc assigns constant-
        /// buffer slots per stage, so the same pass can legally compile its vertex
        /// stage with `cb2 = UnityPerDraw` and its fragment stage with
        /// `cb2 = UnityLighting` -- merging those two row maps by register folds
        /// two unrelated buffers into one declaration and silently moves every
        /// member the fragment reads.  Unity binds constant buffers by name at
        /// runtime, so the slot numbers themselves carry no meaning; what must
        /// survive the merge is each named buffer's member layout.  A buffer that
        /// only one stage uses keeps that stage's slot when it is free, and takes
        /// the lowest free slot otherwise.
        /// </summary>
        private sealed class Buf
        {
            public string Header = "";
            /// <summary>Members keyed by the lane each starts at.</summary>
            public readonly Dictionary<(int row, int col), CbLayout.Member> Rows = new();
        }

        internal static List<string> Merge(Stage vs, Stage ps)
        {
            if (vs == null && ps == null) return null;

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
            foreach (var block in structs) { outp.AddRange(block); outp.Add(""); }
            foreach (var kv in buffers.OrderBy(k => slotOf[k.Key]))
            {
                outp.Add(kv.Value.Header.Replace(Regex.Match(kv.Value.Header, @"register\(b\d+\)").Value,
                                                 $"register(b{slotOf[kv.Key]})"));
                outp.Add("{");
            // Two stages can each contribute a piece of the same row -- one a
            // whole-footprint member, the other narrower ones sharing it -- and the
            // union would then declare members over the same bytes, which fxc
            // rejects ("packoffset overlaps").  The wider member wins, and every
            // narrower name that overlapped it is rewritten to read through it in
            // *every* stage body, because those names may appear in a variant that
            // never goes through this merge.
            var overlap = CbLayout.OverlapRenames(kv.Value.Rows.Values);
            foreach (var r in overlap)
            {
                renames[r.Key] = r.Value;
                kv.Value.Rows.Remove((kv.Value.Rows.First(x => x.Value.Name == r.Key).Key));
            }
            var keptMembers = kv.Value.Rows.Values.OrderBy(m => m.Row).ThenBy(m => m.Col).ToList();

            foreach (var m in keptMembers) outp.Add(CbLayout.Line(m));
            outp.Add("};");
            outp.Add("");
        }
        outp.AddRange(globals);
        if (globals.Count > 0) outp.Add("");

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
                outp.AddRange(fn.Split('\n'));
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
