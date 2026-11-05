using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ShaderRipper.Plugins.Hlsl
{
    /// <summary>One member of an HLSL interface struct.</summary>
    /// <remarks>
    /// Plain fields rather than a record so a plugin compiled against an older minor
    /// keeps working when the host adds one.
    /// </remarks>
    public sealed class StructMember
    {
        /// <summary>Declared type, e.g. <c>float4</c>.</summary>
        public string Type { get; set; }

        /// <summary>Member name.</summary>
        public string Name { get; set; }

        /// <summary>D3D semantic after the colon, e.g. <c>TEXCOORD0</c>.</summary>
        public string Semantic { get; set; }

        /// <summary>Renders one member declaration line, without indentation.</summary>
        public override string ToString() => $"{Type} {Name} : {Semantic};";
    }

    /// <summary>
    /// Structural edits on emitted HLSL text, offered to plugins.
    ///
    /// A translator that produces interface structs of its own needs the same two
    /// operations the built-in SPIR-V route does: fold a vertex-output/pixel-input
    /// pair into one struct so the stages link, and rewrite a struct's semantics from
    /// a name table when the backend only recorded location numbers.  Both live here
    /// rather than in the plugin that happens to need them first, because a second
    /// backend (Metal, WebGPU) faces exactly the same shape.
    ///
    /// Everything is a pure function of its arguments -- no static state, no file
    /// access -- so it is safe to call concurrently from any number of translators.
    /// </summary>
    public static class StructTools
    {
        /// <summary>
        /// The full text of a struct declaration, or null when the text has none.
        ///
        /// The match is on the declaration head, so a name that is a prefix of
        /// another (<c>Foo</c> and <c>FooBar</c>) is not confused for the shorter one.
        /// </summary>
        public static string ReadStruct(string hlsl, string name)
        {
            if (string.IsNullOrEmpty(hlsl) || string.IsNullOrEmpty(name)) return null;
            Match head = Regex.Match(hlsl, @"\bstruct\s+" + Regex.Escape(name) + @"\b");
            if (!head.Success) return null;
            int open = hlsl.IndexOf('{', head.Index);
            int close = hlsl.IndexOf('}', open);
            if (open < 0 || close < 0) return null;
            int semi = hlsl.IndexOf(';', close);
            if (semi < 0) return null;
            return hlsl.Substring(head.Index, semi - head.Index + 1);
        }

        /// <summary>
        /// Replace a struct declaration with <paramref name="replacement"/>; an empty
        /// replacement deletes it.  Returns the input unchanged when the struct is not
        /// declared.
        /// </summary>
        public static string ReplaceStruct(string hlsl, string name, string replacement)
        {
            string old = ReadStruct(hlsl, name);
            if (old == null) return hlsl;
            int at = hlsl.IndexOf(old, StringComparison.Ordinal);
            if (at < 0) return hlsl;
            return hlsl.Remove(at, old.Length).Insert(at, replacement ?? "");
        }

        /// <summary>
        /// The members of a struct declaration, in declaration order.  Only the
        /// <c>type name : semantic;</c> shape a translator emits is understood; a
        /// member without a semantic is skipped.
        /// </summary>
        public static IReadOnlyList<StructMember> Members(string structText)
        {
            var result = new List<StructMember>();
            if (string.IsNullOrEmpty(structText)) return result;
            foreach (Match m in Regex.Matches(structText,
                @"^\s*(\w[\w<>, ]*?)\s+(\w+)\s*:\s*(\w+)\s*;", RegexOptions.Multiline))
                result.Add(new StructMember
                {
                    Type = m.Groups[1].Value.Trim(),
                    Name = m.Groups[2].Value,
                    Semantic = m.Groups[3].Value,
                });
            return result;
        }

        /// <summary>
        /// True when the semantic is D3D's own rather than an interpolator:
        /// <c>SV_Position</c> and the render targets sit outside the attribute
        /// numbering and must not consume a slot when a table is walked positionally.
        /// </summary>
        public static bool IsSystemSemantic(string semantic) =>
            semantic == "SV_Position" ||
            semantic == "SV_Target" ||
            (semantic != null && semantic.StartsWith("SV_Target", StringComparison.Ordinal));

        /// <summary>
        /// Rewrite the <c>: semantic</c> of the named struct's members from a
        /// positional table, leaving names and types alone.
        ///
        /// Each table entry is a declaration name in the source language
        /// (<c>in_COLOR0</c>, <c>vs_COLOR0</c>) and is mapped through
        /// <see cref="SemanticOf"/>.  System semantics are skipped and do not consume
        /// a table entry.  When the table runs out, the remaining members keep what
        /// they had -- guessing past the end would invent a binding.
        /// </summary>
        /// <param name="hlsl">Program text to rewrite.</param>
        /// <param name="structName">Struct to patch.</param>
        /// <param name="names">Source-language declaration names, in location order.</param>
        public static string PatchSemantics(string hlsl, string structName, IReadOnlyList<string> names)
        {
            if (string.IsNullOrEmpty(hlsl) || names == null || names.Count == 0) return hlsl;
            string decl = ReadStruct(hlsl, structName);
            if (decl == null) return hlsl;

            var lines = decl.Split('\n');
            int location = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                Match m = Regex.Match(lines[i].Trim(),
                    @"^(\w[\w<>, ]*?)\s+(\w+)\s*:\s*(\w+)\s*;$");
                if (!m.Success) continue;
                if (IsSystemSemantic(m.Groups[3].Value)) continue;

                location++;
                if (location >= names.Count) continue;
                string semantic = SemanticOf(names[location]);
                if (semantic == null || semantic == m.Groups[3].Value) continue;

                string indent = lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length);
                lines[i] = $"{indent}{m.Groups[1].Value} {m.Groups[2].Value} : {semantic};";
            }

            string patched = string.Join("\n", lines);
            return ReplaceStruct(hlsl, structName, patched);
        }

        /// <summary>
        /// The D3D semantic a source-language declaration name stands for:
        /// <c>in_POSITION0</c> / <c>vs_POSITION0</c> is POSITION, <c>in_COLOR0</c> is
        /// COLOR0, <c>vs_TEXCOORD1</c> is TEXCOORD1.  Null for a name whose stem is
        /// not one this understands, so the caller keeps the original spelling.
        /// </summary>
        public static string SemanticOf(string sourceName)
        {
            Match m = Regex.Match(sourceName ?? "", @"^(?:in|vs|out|ps)_([A-Za-z]+?)(\d*)$");
            if (!m.Success) return null;
            string stem = m.Groups[1].Value.ToUpperInvariant();
            string idx = m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : "0";
            return stem switch
            {
                "POSITION" => "POSITION",
                "COLOR" => "COLOR" + idx,
                "NORMAL" => "NORMAL",
                "TANGENT" => "TANGENT",
                "TEXCOORD" => "TEXCOORD" + idx,
                "TEXCOORD0" => "TEXCOORD" + idx,
                _ => null,
            };
        }

        /// <summary>Outcome of <see cref="MergeInterface"/>.</summary>
        public sealed class InterfaceMerge
        {
            /// <summary>The rewritten vertex program, or null when no merge happened.</summary>
            public string Vertex { get; init; }

            /// <summary>The rewritten fragment program, or null when no merge happened.</summary>
            public string Fragment { get; init; }

            /// <summary>Name of the merged struct, once it exists.</summary>
            public string StructName { get; init; }

            /// <summary>True when both programs were rewritten.</summary>
            public bool Merged => Vertex != null && Fragment != null;

            /// <summary>The input pair, for a caller that wants to leave it alone.</summary>
            public static readonly InterfaceMerge Unchanged = new();
        }

        /// <summary>
        /// Fold a vertex-output / pixel-input struct pair into one struct so the two
        /// stages link.
        ///
        /// Unity's pixel-stage link needs the vertex's output members and the
        /// fragment's input members declared as one shape carrying
        /// <c>SV_Position</c>; a pair of separate declarations with the same bytes
        /// does not bind, and the material then draws nothing with no compile error.
        /// The two programs also name their copies independently -- the vertex calls
        /// its colour <c>vs_134</c>, the fragment <c>ps_22</c> -- so the merge keeps
        /// the vertex's spelling and rewrites the fragment's
        /// <c>parameter.&lt;name&gt;</c> accesses through a positional map.
        ///
        /// Declines (returns <see cref="InterfaceMerge.Unchanged"/>) unless the two
        /// member lists cover the same semantics in the same order: anything else is a
        /// pair this pass does not understand, and a wrong merge would be worse than
        /// no merge.
        /// </summary>
        /// <param name="vertex">Vertex program text.</param>
        /// <param name="fragment">Fragment program text.</param>
        /// <param name="vertexOutput">Name of the vertex's output struct.</param>
        /// <param name="pixelInput">Name of the pixel's input struct.</param>
        /// <param name="mergedName">Name for the merged struct.</param>
        /// <param name="parameter">Parameter name both entries use.</param>
        public static InterfaceMerge MergeInterface(string vertex, string fragment,
            string vertexOutput, string pixelInput, string mergedName = "v2f",
            string parameter = "stage_input")
        {
            if (string.IsNullOrEmpty(vertex) || string.IsNullOrEmpty(fragment)) return InterfaceMerge.Unchanged;
            string vsOut = ReadStruct(vertex, vertexOutput);
            string psIn = ReadStruct(fragment, pixelInput);
            if (vsOut == null || psIn == null) return InterfaceMerge.Unchanged;

            var vsMembers = new List<StructMember>();
            foreach (StructMember m in Members(vsOut))
            {
                if (m.Semantic == "SV_Position") continue;
                vsMembers.Add(m);
            }
            var psMembers = new List<StructMember>();
            foreach (StructMember m in Members(psIn))
            {
                if (m.Semantic == "SV_Position") continue;
                psMembers.Add(m);
            }
            if (vsMembers.Count == 0 || vsMembers.Count != psMembers.Count) return InterfaceMerge.Unchanged;
            for (int i = 0; i < vsMembers.Count; i++)
                if (vsMembers[i].Semantic != psMembers[i].Semantic) return InterfaceMerge.Unchanged;

            var rename = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < vsMembers.Count; i++)
                if (psMembers[i].Name != vsMembers[i].Name)
                    rename[psMembers[i].Name] = vsMembers[i].Name;

            var sb = new StringBuilder();
            sb.Append("struct ").Append(mergedName).Append("\n{\n");
            sb.Append("    float4 gl_Position : SV_Position;\n");
            foreach (StructMember m in vsMembers)
                sb.Append("    ").Append(m.Type).Append(' ').Append(m.Name)
                  .Append(" : ").Append(m.Semantic).Append(";\n");
            sb.Append("};");
            string merged = sb.ToString();

            // The vertex side keeps its mesh-stream input and gains the merged
            // declaration in place of the output struct it was returning.
            string vsText = ReplaceStruct(vertex, vertexOutput, merged);
            vsText = Regex.Replace(vsText,
                Regex.Escape(vertexOutput) + @"(\s+\w+\s*\()", mergedName + "$1");
            vsText = Regex.Replace(vsText, @"\b" + Regex.Escape(vertexOutput) + @"\b", mergedName);

            // The merged declaration goes where the pixel input used to be, so it comes
            // before the fragment that takes it.  Substituting in place is what keeps
            // the ordering right; inserting before the first `{` would put it inside
            // whichever struct happens to come first.
            string psText = ReplaceStruct(fragment, pixelInput, merged);

            // The fragment's entry takes the merged struct.  Only the parameter's type
            // changes -- it keeps the name the body already uses -- so the match is
            // "the type, then the parameter name inside the parentheses".
            psText = System.Text.RegularExpressions.Regex.Replace(psText,
                @"\b" + Regex.Escape(pixelInput) + @"(\s+\w+\s*\))",
                mergedName + "$1");

            foreach (var kv in rename)
                psText = Regex.Replace(psText,
                    @"\b" + Regex.Escape(parameter) + @"\." + Regex.Escape(kv.Key) + @"\b",
                    parameter + "." + kv.Value);

            return new InterfaceMerge
            {
                Vertex = vsText,
                Fragment = psText,
                StructName = mergedName,
            };
        }
    }
}
