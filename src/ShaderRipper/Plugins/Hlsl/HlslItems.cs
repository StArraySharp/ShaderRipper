using System.Collections.Generic;
using ShaderRipper.Emit;

namespace ShaderRipper.Plugins.Hlsl
{
    /// <summary>One top-level item of a translated HLSL program.</summary>
    public sealed class HlslItem
    {
        /// <summary>
        /// <c>pp</c> (preprocessor line), <c>cbuffer</c>, <c>struct</c>,
        /// <c>decl</c> (a global declaration), <c>func</c> (a function definition) or
        /// <c>semi</c> (a stray semicolon).
        /// </summary>
        public string Kind { get; init; }

        /// <summary>The item's text, taken verbatim from the input.</summary>
        public string Text { get; init; }

        /// <summary>Declared name, when the item has one (struct name, entry point).</summary>
        public string Name { get; init; }

        /// <summary>Register a cbuffer names, or -1.</summary>
        public int Register { get; init; }
    }

    /// <summary>
    /// Splits a program body into its top-level items, for plugin passes that need to
    /// reason about declarations rather than lines.
    ///
    /// The split runs on the project's <c>Hlsl.g4</c> parse tree, so a
    /// <c>float4 _Color;</c> inside a function body is never mistaken for a global --
    /// which is exactly the bug a line scanner produces.  The item texts come from the
    /// original source over the line spans the tree reports, so joining them
    /// reproduces the input.
    ///
    /// A body the grammar cannot carry comes back as one item of kind <c>raw</c>, and
    /// the split reports how many errors the parser logged.  A pass that only wants
    /// "declarations before the first function" can stop at the first <c>func</c>
    /// either way.
    /// </summary>
    public static class HlslItems
    {
        /// <summary>Split one program body.</summary>
        /// <param name="hlsl">Program text, without the surrounding ShaderLab tags.</param>
        /// <param name="parseErrors">Number of parse errors the grammar reported.</param>
        public static IReadOnlyList<HlslItem> Split(string hlsl, out int parseErrors)
        {
            List<HlslTree.Item> items = HlslTree.Split(hlsl, out parseErrors);
            var result = new List<HlslItem>(items.Count);
            foreach (HlslTree.Item it in items)
                result.Add(new HlslItem
                {
                    Kind = it.Kind,
                    Text = it.Text,
                    Name = it.Name,
                    Register = it.Register,
                });
            return result;
        }
    }
}
