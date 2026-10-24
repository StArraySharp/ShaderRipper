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

        public sealed class PassText
        {
            public string Name = "";
            public List<(string k, string v)> Tags = new();
            public List<string> States = new();
            public Stage Vertex;
            public Stage Fragment;
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

        private static string Tags(IEnumerable<(string k, string v)> tags) =>
            string.Join(" ", tags.Select(t => $"\"{t.k}\" = \"{t.v}\""));

        /// <summary>
        /// One program block for a pass: constant buffers from whichever stage
        /// declares each one longest, then the remaining globals once each, then
        /// the two entry points.
        /// </summary>
        private static List<string> Merge(Stage vs, Stage ps)
        {
            if (vs == null && ps == null) return null;

            // Buffers are keyed by register, not by name: the same slot can come out
            // as `UnityGlobals` in one stage and `CB0` in the other, and emitting
            // both blocks binds b0 twice.
            var buffers = new Dictionary<int, (string header, Dictionary<int, string> rows)>();
            var perStage = new List<(Stage stage, Dictionary<int, Dictionary<int, string>> own)>();
            var globals = new List<string>();
            var seenGlobal = new HashSet<string>(StringComparer.Ordinal);

            foreach (var stage in new[] { vs, ps })
            {
                if (stage?.Hlsl == null) continue;
                var lines = stage.Hlsl.Replace("\r\n", "\n").Split('\n');
                int at = Array.FindIndex(lines, l => l.StartsWith("float4 main()", StringComparison.Ordinal));
                if (at < 0) at = lines.Length;

                var own = new Dictionary<int, Dictionary<int, string>>();
                for (int i = 0; i < at; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith("cbuffer ", StringComparison.Ordinal))
                    {
                        var block = new List<string> { line };
                        i++;
                        while (i < at)
                        {
                            block.Add(lines[i]);
                            if (lines[i].StartsWith("};", StringComparison.Ordinal)) break;
                            i++;
                        }
                        var m = Regex.Match(line, @"register\(b(\d+)\)");
                        if (!m.Success) continue;
                        int reg = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        var rows = ReadRows(block);
                        own[reg] = rows;
                        if (buffers.TryGetValue(reg, out var have))
                        {
                            MergeRows(have.rows, rows);
                            // Keep whichever header names the buffer; a "CBn" is the
                            // absence of an answer, not a conflicting one.
                            if (have.header.StartsWith("cbuffer CB", StringComparison.Ordinal) &&
                                !line.StartsWith("cbuffer CB", StringComparison.Ordinal))
                                buffers[reg] = (line, have.rows);
                        }
                        // A copy, not the same dictionary: the stage keeps its own
                        // spelling so the body can be renamed to the merged one
                        // afterwards.  Sharing it made both sides agree by
                        // construction and the rename never fired.
                        else buffers[reg] = (line, new Dictionary<int, string>(rows));
                        continue;
                    }
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (seenGlobal.Add(trimmed)) globals.Add(line);
                }
                perStage.Add((stage, own));
            }

            // One name can appear in two registers: a stage that could not identify
            // b0 names its rows from the property table and arrives at
            // `_RendererColor`, which the other stage places in `UnityPerDrawSprite`
            // where it belongs.  A member may only be declared once, so the name
            // stays in the register that is a buffer Unity defines, and the other
            // occurrence falls back to its row number.
            var owner = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in buffers.OrderBy(k => k.Key))
                foreach (var row in kv.Value.rows)
                {
                    if (!owner.TryGetValue(row.Value, out int reg))
                    {
                        owner[row.Value] = kv.Key;
                        continue;
                    }
                    bool keepMine = kv.Value.header.StartsWith("cbuffer Unity", StringComparison.Ordinal);
                    bool keepTheirs = buffers[reg].header.StartsWith("cbuffer Unity", StringComparison.Ordinal);
                    if (keepMine && !keepTheirs) { owner[row.Value] = kv.Key; }
                }
            foreach (var kv in buffers)
            {
                var fixedRows = new Dictionary<int, string>();
                foreach (var row in kv.Value.rows)
                {
                    bool demote = owner.TryGetValue(row.Value, out int own) && own != kv.Key;
                    fixedRows[row.Key] = demote ? $"_cb{kv.Key}_row{row.Key}" : row.Value;
                }
                kv.Value.rows.Clear();
                foreach (var r in fixedRows) kv.Value.rows[r.Key] = r.Value;
            }

            var outp = new List<string>();
            foreach (var kv in buffers.OrderBy(k => k.Key))
            {
                outp.Add(kv.Value.header);
                outp.Add("{");
                int extent = kv.Value.rows.Count == 0 ? 1 : kv.Value.rows.Keys.Max() + 1;
                outp.AddRange(CbLayout.DeclareLines(kv.Key, extent, kv.Value.rows));
                outp.Add("};");
                outp.Add("");
            }
            outp.AddRange(globals);
            if (globals.Count > 0) outp.Add("");

            foreach (var (stage, own) in perStage)
            {
                string fn = Function(stage);
                // The declaration is one per register now, so a body that spells a
                // row the other stage named has to be renamed to match: otherwise it
                // references a member the merged block no longer declares.
                foreach (var kv in buffers)
                {
                    if (!own.TryGetValue(kv.Key, out var mine)) continue;
                    foreach (var row in mine)
                    {
                        if (!kv.Value.rows.TryGetValue(row.Key, out var merged)) continue;
                        if (merged == row.Value) continue;
                        fn = Regex.Replace(fn, @"\b" + Regex.Escape(row.Value) + @"\b", merged);
                    }
                }
                outp.AddRange(fn.Split('\n'));
                outp.Add("");
            }
            return outp;
        }

        private static string Function(Stage stage)
        {
            var lines = stage.Hlsl.Replace("\r\n", "\n").Split('\n');
            int at = Array.FindIndex(lines, l => l.StartsWith("float4 main()", StringComparison.Ordinal));
            if (at < 0) return "";
            var fn = lines.Skip(at).ToList();
            fn[0] = fn[0].Replace("main()", stage.Entry + "()");
            return string.Join('\n', fn).TrimEnd();
        }

        /// <summary>The rows a buffer block declares, as row -> identifier.</summary>
        private static Dictionary<int, string> ReadRows(List<string> block)
        {
            var rows = new Dictionary<int, string>();
            int row = 0;
            foreach (var line in block.Skip(1))
            {
                var t = line.Trim();
                if (t.StartsWith("};", StringComparison.Ordinal)) break;
                var m = Regex.Match(t, @"^float4 ([A-Za-z_]\w*)(?:\[(\d+)\])?;$");
                if (!m.Success) continue;
                string name = m.Groups[1].Value;
                int n = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
                for (int i = 0; i < n; i++, row++)
                    rows[row] = n > 1 ? $"{name}[{i}]" : name;
            }
            return rows;
        }

        /// <summary>
        /// Union two row maps.  A real name beats a positional one, which is what
        /// keeps <c>_Color</c> when the other stage only knew its row number.
        /// </summary>
        private static void MergeRows(Dictionary<int, string> into, Dictionary<int, string> from)
        {
            foreach (var kv in from)
            {
                if (!into.TryGetValue(kv.Key, out var have)) { into[kv.Key] = kv.Value; continue; }
                bool havePositional = have.StartsWith("_cb", StringComparison.Ordinal);
                bool isPositional = kv.Value.StartsWith("_cb", StringComparison.Ordinal);
                if (havePositional && !isPositional) into[kv.Key] = kv.Value;
            }
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
