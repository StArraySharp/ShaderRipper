using System.Collections.Generic;

namespace ShaderRipper.Plugins.Platforms
{
    /// <summary>
    /// Unity's built-in constant-buffer layout, for backends whose payload does not
    /// carry one.
    ///
    /// A payload records the member names of the buffers it binds, but not of the
    /// engine's own: <c>UnityPerDraw</c> is recorded as a size with no members at all.
    /// A translated program still reads it, and the engine layout is fixed, so it can
    /// be recovered from the include that declares it rather than left positional.
    /// </summary>
    public static class EngineBuffers
    {
        /// <summary>One member of an engine buffer.</summary>
        public readonly struct Entry
        {
            public Entry(string buffer, string name, int row, int rows, int column, int span)
            {
                Buffer = buffer;
                Name = name;
                Row = row;
                Rows = rows;
                Column = column;
                Span = span;
            }

            /// <summary>Buffer the member belongs to.</summary>
            public string Buffer { get; }

            /// <summary>Member name, as the include declares it.</summary>
            public string Name { get; }

            /// <summary>First row (16-byte slot) the member occupies.</summary>
            public int Row { get; }

            /// <summary>How many rows a matrix spans; 1 otherwise.</summary>
            public int Rows { get; }

            /// <summary>First component within <see cref="Row"/>.</summary>
            public int Column { get; }

            /// <summary>How many components the member occupies.</summary>
            public int Span { get; }
        }

        /// <summary>Every member the layout table knows for one buffer.</summary>
        public static IEnumerable<Entry> ForBuffer(string buffer)
        {
            if (string.IsNullOrEmpty(buffer)) yield break;
            foreach (var m in Data.UnityCbuffers.ForCb(buffer))
                yield return new Entry(m.Cb, m.Name, m.Row, m.Rows, m.Col, m.Span);
        }

        /// <summary>The member covering a row, or null when the table does not describe it.</summary>
        public static Entry? At(string buffer, int row)
        {
            if (string.IsNullOrEmpty(buffer)) return null;
            var m = Data.UnityCbuffers.Find(buffer, row);
            return m == null ? null : new Entry(m.Cb, m.Name, m.Row, m.Rows, m.Col, m.Span);
        }

        /// <summary>True when the layout table describes this buffer at all.</summary>
        public static bool Known(string buffer) =>
            !string.IsNullOrEmpty(buffer) && Data.UnityCbuffers.RowCount(buffer) > 0;
    }
}
