using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ShaderRipper.Antlr.Ast;
using ShaderRipper.Antlr.Data;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// Turns the resolver's row/lane names into a constant-buffer declaration that
    /// HLSL will accept.
    ///
    /// The resolver answers "what is at cb1 row 7" -- <c>_Color</c>,
    /// <c>unity_WorldToLight[2]</c> -- and, when a row holds several scalars,
    /// "what is at cb1 row 7 lane 2" as well.  Those answers cannot be repeated
    /// verbatim: a matrix column is spelled <c>(M._m00_m10_m20_m30)</c>, which is an
    /// expression rather than a member name.
    ///
    /// The declaration is driven by <c>packoffset</c>.  Without it a constant buffer
    /// is laid out member after member, so the declaration has to cover every row
    /// from zero and one unnamed row forces an opaque placeholder -- 2,340 of them
    /// across one game's shaders.  With it, each member is pinned to its real row
    /// and lane: unnamed rows simply do not appear, and a row that holds
    /// four scalars can declare four scalars instead of one opaque float4.
    ///
    /// Shapes produced, per register:
    ///
    ///   * <b>named members</b> -- <c>float _FaceDilate : packoffset(c6.x);</c>, a
    ///     matrix run collapsed into <c>float4 _EnvMatrix[4]</c> (column-major, so
    ///     one column per row, which is what the resolver's references index), and
    ///     <c>float4 _cbN_rowK</c> only for rows nothing at all pinned.
    ///   * <b>the raw array</b> -- <c>float4 cb1[21];</c> -- only for a buffer read
    ///     with a runtime row index, which no member name can express.
    /// </summary>
    internal sealed class CbLayout
    {
        public sealed class Member
        {
            public string Name = "";
            public string Type = "float4";
            public int Rows = 1;
            public bool IsArray;
            /// <summary>Row this member starts at; the packoffset column is 0.</summary>
            public int Row;
            /// <summary>Lane this member starts at (0..3), for a scalar member.</summary>
            public int Col;
        }

        /// <summary>Rows this register declares (array length, or the member count).</summary>
        public readonly Dictionary<int, int> Extent = new();

        /// <summary>True when the register is emitted as one array, not as members.</summary>
        public readonly Dictionary<int, bool> ArrayForm = new();

        public readonly Dictionary<int, List<Member>> Members = new();

        /// <summary>Register -> row -> the identifier the body should use.</summary>
        public readonly Dictionary<int, Dictionary<int, string>> RowName = new();

        /// <summary>Why a register kept the array shape, for the note in the output.</summary>
        public readonly Dictionary<int, string> Fallback = new();

        /// <summary>
        /// A named constant-buffer row: <c>_EnvMatrix[2]</c> is column 2 of a
        /// matrix, <c>unity_WorldToShadow[1]#0</c> is column 0 of element 1 of a
        /// matrix array.  The two shapes are kept apart because the first folds a
        /// whole matrix into one <c>float4 name[n]</c>, while the second cannot: its
        /// elements are already indexed by the body, so each column stays its own
        /// <c>float4</c>.
        /// </summary>
        private static readonly Regex MatrixRow =
            new(@"^([A-Za-z_]\w*(?:\[\d+\])?)(?:\[(\d+)\]|#(\d+))$",
                RegexOptions.CultureInvariant);

        private static readonly Regex Identifier =
            new(@"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Column index a <see cref="MatrixRow"/> match carries.  A matrix folds its
        /// columns into `<c>name[i]</c>`; an array element spells them
        /// `<c>name[k]#i</c>`, so the two groups are checked in turn.
        /// </summary>
        private static int MatrixCol(Match m) =>
            int.Parse(m.Groups[m.Groups[2].Success ? 2 : 3].Value, CultureInfo.InvariantCulture);

        /// <summary>
        /// A matrix column as the resolver spells it: <c>(_EnvMatrix._m00_m10_m20_m30)</c>
        /// is column 0 of the matrix, i.e. the four floats a single constant-buffer
        /// row holds.  That spelling is an expression, not an identifier, so the
        /// declaration layer cannot use it -- but <c>_EnvMatrix[0]</c> is the same
        /// value once the matrix is declared as an array of <c>float4</c> (which is
        /// exactly the memory layout: column-major, one column per row).
        ///
        /// An array element carries its own index -- <c>(unity_WorldToShadow[2]._m01_...)</c>
        /// is column 2 of element 0 -- so the brackets are part of the name.
        /// </summary>
        private static readonly Regex MatrixColumnExpr =
            new(@"^\(\s*([A-Za-z_]\w*(?:\[\d+\])?)\s*\._m\d(\d)(?:_m\d\d)+\)$",
                RegexOptions.CultureInvariant);

        private static readonly Regex LaneMember =
            new(@"^([A-Za-z_]\w*)\.([xyzw])$", RegexOptions.CultureInvariant);

        private static readonly Regex LaneMatrix =
            new(@"^([A-Za-z_]\w*)\[(\d+)\]\.([xyzw])$", RegexOptions.CultureInvariant);

        public bool IsArray(int reg) => ArrayForm.TryGetValue(reg, out bool a) && a;

        public IReadOnlyList<Member> For(int reg) =>
            Members.TryGetValue(reg, out var m) ? m : Array.Empty<Member>();

        public string Row(int reg, int row) =>
            RowName.TryGetValue(reg, out var rows) && rows.TryGetValue(row, out var n) ? n : null;

        public static CbLayout Build(Module m, Resolve.CbResolver cb)
        {
            var layout = new CbLayout();
            var decls = m.Decls.Where(d => d.Kind == "cbuffer")
                               .GroupBy(d => d.Register).Select(g => g.First())
                               .OrderBy(d => d.Register);

            foreach (var d in decls)
            {
                int reg = d.Register;
                int extent = Math.Max(d.Count, 1);
                if (cb != null)
                    foreach (var kv in cb.RowExpr.Keys.Where(k => k.cb == reg))
                        extent = Math.Max(extent, kv.row + 1);
                layout.Extent[reg] = extent;

                if (cb == null)
                {
                    layout.ArrayForm[reg] = true;
                    continue;
                }
                if (cb.HasDynamicIndex(reg))
                {
                    layout.ArrayForm[reg] = true;
                    layout.Fallback[reg] = "read with a runtime row index";
                    continue;
                }

                // Every row the resolver named, plus every lane it named inside a
                // row.  A row may hold several members (`float _FaceDilate` beside
                // `float _OutlineSoftness`), which is why the lane map exists: the
                // row-level answer alone would have to drop those names.
                var names = new Dictionary<int, string>();
                foreach (var kv in cb.RowExpr.Where(k => k.Key.cb == reg))
                    names[kv.Key.row] = Normalise(kv.Value);

                var lanes = new Dictionary<(int row, int col), string>();
                foreach (var kv in cb.CompExpr.Where(k => k.Key.cb == reg))
                    lanes[(kv.Key.row, kv.Key.comp)] = kv.Value;

                // A buffer Unity itself defines has a published layout.  Where this
                // program reads only part of a row -- a vertex shader that touches
                // lanes 1..2 of UnityPerDraw row 1 -- the resolver cannot name the
                // lanes it never saw, but the table can.  Every entry is pinned by
                // packoffset, so a row the program does not read costs nothing and
                // shifts nothing.
                string cbname = cb.CbName.TryGetValue(reg, out var nm) ? nm : null;
                int tableRows = string.IsNullOrEmpty(cbname) ? 0 : UnityCbuffers.RowCount(cbname);
                var widths = new Dictionary<int, int>();
                if (tableRows > 0) FillFromTable(cbname, tableRows, extent, names, lanes, widths);

                // The rows this program actually reads, with the lanes it touches.
                // A cbuffer member only has to exist when something names it -- an
                // unread row costs a declaration and, worse, a placeholder name that
                // reads like an answer without being one.
                cb.RowUse.TryGetValue(reg, out var readUse);

                var (members, rows) = DeclareMembers(reg, extent, names, lanes, cb.MatrixRows, readUse, widths);
                // A row named both as a whole and lane by lane would be declared
                // twice over the same bytes; the wider member keeps the name and the
                // narrower spelling is redirected through it.
                var clashes = OverlapRenames(members);
                if (clashes.Count > 0)
                {
                    foreach (var kv in clashes)
                    {
                        var gone = members.FirstOrDefault(m => m.Name == kv.Key);
                        if (gone != null) members.Remove(gone);
                        foreach (var r in rows.Where(r => r.Value == kv.Key).ToList())
                            rows[r.Key] = kv.Value;
                    }
                    members = RemoveOverlaps(members);
                }
                layout.Members[reg] = members;
                layout.RowName[reg] = rows;
                if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                    Console.Error.WriteLine($"[lay] cb{reg} extent={extent} members="
                        + string.Join(" ", members.Select(x =>
                            $"{x.Name}@c{x.Row}.{"xyzw"[x.Col]}{(x.IsArray ? $"[{x.Rows}]" : "")}")));
            }
            return layout;
        }

        /// <summary>
        /// Add names the shipped layout supplies but this program did not read.
        ///
        /// Only rows inside the declared extent are added, and only where nothing
        /// is named yet -- the resolver's answers come from this program's own
        /// reflection and always win.  A matrix is added only when all of its rows
        /// fit, since a half-declared matrix would not resolve the <c>name[i]</c>
        /// references the resolver may already have written.
        /// </summary>
        private static void FillFromTable(string cbname, int tableRows, int extent,
                                          Dictionary<int, string> names,
                                          Dictionary<(int row, int col), string> lanes,
                                          Dictionary<int, int> widths = null)
        {
            int limit = Math.Min(tableRows, extent);
            foreach (var m in UnityCbuffers.ForCb(cbname))
            {
                if (m.Row < 0 || m.Row >= limit) continue;
                if (m.IsMatrix)
                {
                    if (m.Row + m.Rows > limit) continue;
                    bool any = false;
                    for (int k = 0; k < m.Rows; k++) any |= names.ContainsKey(m.Row + k);
                    if (any) continue;             // do not overrule what the resolver said
                    for (int k = 0; k < m.Rows; k++) names[m.Row + k] = $"{m.Name}[{k}]";
                    continue;
                }
                if (names.ContainsKey(m.Row)) continue;

                bool anyLane = Enumerable.Range(0, 4).Any(c => lanes.ContainsKey((m.Row, c)));
                if (!anyLane)
                {
                    if (m.FillsRow)
                    {
                        names[m.Row] = m.Name;
                        if (widths != null) widths[m.Row] = 4;
                    }
                    continue;
                }

                // A program that touches `.y` of a `float3` global sees only that one
                // lane, so the resolver names the lane and nothing else.  Declaring
                // just the lane (`float _cbN_rowK_y`) is legal but leaves the body
                // spelling a name that does not exist, so when the table's member is
                // a vector that starts where the row starts and every named lane is
                // one of its components, declare the member itself and read the lane
                // through it.
                if (m.Col != 0 || m.Span < 2) continue;
                bool all = true;
                for (int c = 0; c < 4 && all; c++)
                {
                    if (!lanes.TryGetValue((m.Row, c), out var raw) || string.IsNullOrEmpty(raw)) continue;
                    all = raw == m.Name || raw.StartsWith(m.Name + ".", StringComparison.Ordinal);
                }
                if (!all) continue;
                names[m.Row] = m.Name;
                if (widths != null) widths[m.Row] = m.Span;
                for (int c = 0; c < 4; c++) lanes.Remove((m.Row, c));
            }
        }

        /// <summary>Row-level member name for this lane, when the body can use one directly.</summary>
        private static string LaneExpr(Dictionary<int, string> rows, int row, int col, string raw)
        {
            var mm = LaneMatrix.Match(raw);
            if (mm.Success) return raw;                    // a declared matrix element
            var lm = LaneMember.Match(raw);
            if (lm.Success) return raw;
            if (Identifier.IsMatch(raw)) return raw;
            return null;
        }

        /// <summary>Turn the resolver's expression spelling into a declarable member name.</summary>
        private static string Normalise(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var m = MatrixColumnExpr.Match(raw);
            if (!m.Success) return raw;
            // A matrix that is itself an array element (`unity_WorldToShadow[2]`)
            // keeps its element index: the column indices run per element, so
            // folding them together would print `[2][1]`, which is not a member.
            string bas = m.Groups[1].Value;
            int col = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return bas.Contains('[') ? $"{bas}#{col}" : $"{bas}[{col}]";
        }

        /// <summary>
        /// Turn a row -> name map (plus per-lane names) into members whose
        /// <c>packoffset</c> puts each one exactly where the disassembly reads it.
        ///
        /// The offset is what makes this affordable.  A constant buffer declared
        /// without one is laid out member after member, so the declaration has to
        /// cover every row from zero and a single unnamed row forces a positional
        /// placeholder.  With <c>packoffset</c> each member is pinned to its real
        /// row and lane: unnamed rows simply do not appear, and a row that holds
        /// four scalars can declare four scalars instead of one opaque float4.
        ///
        /// A matrix or an array is a run of consecutive <c>name[i]</c> rows, and
        /// becomes one <c>float4 name[n]</c> -- the same shape the resolver's
        /// references use.  A run that starts at a non-zero index is declared from
        /// its real base so the indices still line up.
        /// </summary>
        private static (List<Member> members, Dictionary<int, string> rows) DeclareMembers(
            int reg, int extent, Dictionary<int, string> names,
            Dictionary<(int row, int col), string> lanes,
            Dictionary<string, int> matrixRows = null,
            SortedDictionary<int, int> readUse = null,
            Dictionary<int, int> widths = null)
        {
            var members = new List<Member>();
            var rows = new Dictionary<int, string>();
            var used = new HashSet<string>(StringComparer.Ordinal);

            // A matrix is only visible through the rows of it this variant reads: a
            // program that touches columns 1 and 2 would leave 0 and 3 unnamed, and
            // the array would be declared too short to resolve `name[3]`.
            //
            // The span comes from the shader's own reflection where it is known, and
            // otherwise from the column indices the resolver produced: a column
            // index of 3 proves at least four rows, and an index of 0 that is
            // followed by gaps still proves the base.  Taking the larger of the two
            // is what turns "columns 1..2 read" into a complete `float4 M[4]`.
            var spans = new Dictionary<string, int>(StringComparer.Ordinal);
            var bases = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in names)
            {
                var m = MatrixRow.Match(kv.Value);
                if (!m.Success) continue;
                string bas = m.Groups[1].Value;
                int idx = MatrixCol(m);
                bases[bas] = kv.Key - idx;
                if (!spans.TryGetValue(bas, out int have) || idx + 1 > have) spans[bas] = idx + 1;
            }
            if (matrixRows != null)
                foreach (var kv in matrixRows)
                {
                    if (kv.Value < 2) continue;
                    if (!spans.TryGetValue(kv.Key, out int have) || kv.Value > have)
                        spans[kv.Key] = kv.Value;
                }

            foreach (var kv in spans)
            {
                string bas = kv.Key;
                int span = kv.Value;
                if (span < 2 || span > 4) continue;
                if (!bases.TryGetValue(bas, out int baseRow)) continue;
                if (baseRow < 0 || baseRow + span > extent) continue;
                // Only extend into rows nothing else already claimed.
                bool clash = false;
                for (int k = 0; k < span; k++)
                {
                    if (!names.TryGetValue(baseRow + k, out var have)) continue;
                    var hm = MatrixRow.Match(have);
                    if (!hm.Success) { clash = true; break; }
                    if (hm.Groups[1].Value != bas) { clash = true; break; }
                }
                if (clash) continue;
                for (int k = 0; k < span; k++) names[baseRow + k] = $"{bas}[{k}]";
            }

            int row = 0;

            while (row < extent)
            {
                string raw = names.TryGetValue(row, out var v) ? v : null;

                if (!string.IsNullOrEmpty(raw))
                {
                    var mm = MatrixRow.Match(raw);
                    if (mm.Success)
                    {
                        string bas = mm.Groups[1].Value;
                        int idx = MatrixCol(mm);
                        int span = spans.TryGetValue(bas, out int sp) ? sp : 0;

                        // A matrix that is itself an array element is already
                        // indexed by the body (`unity_WorldToShadow[0]`), so its
                        // columns cannot be folded into one `float4 name[n]` --
                        // that spelling does not exist.  Each column becomes its
                        // own float4 named after the element, and the bodies read
                        // it by that name.
                        if (bas.Contains('['))
                        {
                            string elem = bas.Replace('[', '_').Replace("]", "");
                            string colName = $"{elem}_col{idx}";
                            if (used.Add(colName))
                            {
                                members.Add(new Member
                                {
                                    Name = colName, Type = "float4", Row = row, Col = 0,
                                });
                                rows[row] = colName;
                                row++;
                                continue;
                            }
                        }

                        // The run has to be measured against the matrix's real
                        // length, not against how many of its rows this variant
                        // happened to read: a program that touches columns 0..2
                        // would otherwise declare `float4 M[3]` and then emit a
                        // placeholder for column 3, which both overlaps the array
                        // and renames the row the body still calls `M[3]`.
                        if (span >= 2 && used.Add(bas))
                        {
                            int baseRow = row - idx;
                            int len = span;
                            if (baseRow >= 0 && baseRow + len <= extent && IsFree(members, baseRow, len))
                            {
                                members.Add(new Member
                                {
                                    Name = bas, Type = "float4", Rows = len, IsArray = true,
                                    Row = baseRow, Col = 0,
                                });
                                for (int i = 0; i < len; i++)
                                    rows[baseRow + i] = $"{bas}[{i}]";
                                // Skip the whole span, including any row of it this
                                // variant did not read.
                                if (baseRow <= row) row = baseRow + len;
                                else row++;
                                continue;
                            }
                        }
                    }
                    else if (Identifier.IsMatch(raw) && !HasLane(lanes, row) && used.Add(raw))
                    {
                        // A published table member has the width the include gives
                        // it (`float3 _WorldSpaceCameraPos`); everything else is a
                        // whole row as far as the disassembly can tell.
                        int w = widths != null && widths.TryGetValue(row, out int ww) ? ww : 4;
                        members.Add(new Member
                        {
                            Name = raw,
                            Type = w == 1 ? "float" : w == 2 ? "float2" : w == 3 ? "float3" : "float4",
                            Row = row, Col = 0,
                        });
                        rows[row] = raw;
                        row++;
                        continue;
                    }
                }

                // Nothing names the whole row -- or the row is named *and* some of
                // its lanes are: either way the row is declared lane by lane, so a
                // whole-row float4 can never overlap the floats that share it.
                int mask = readUse != null && readUse.TryGetValue(row, out int m) ? m : 0xF;
                if (DeclareLanes(members, rows, lanes, reg, row, used, mask))
                {
                    row++;
                    continue;
                }
                if (readUse != null && !readUse.ContainsKey(row))
                {
                    // Nothing reads this row, so it needs no declaration at all.
                    // packoffset lets the block skip it, and the size is pinned
                    // separately below.
                    row++;
                    continue;
                }
                AddPositional(members, rows, reg, row, 0, 4, mask);
                row++;
            }

            // The declared extent of a constant buffer is part of its identity: the
            // program's own `dcl_constantbuffer` states it, and a rebuilt shader that
            // declares fewer rows would report a different size.  One pinned member
            // at the last row is enough to keep it -- and with the rows above now
            // skipped wholesale, this member may be the only thing that does.
            int last = extent - 1;
            if (last >= 0 && !Occupies(members, last))
                AddPositional(members, rows, reg, last, 0, 4, claim: false);

            members.Sort((a, b) => a.Row != b.Row ? a.Row - b.Row : a.Col - b.Col);
            members = RemoveOverlaps(members);
            return (members, rows);
        }

        /// <summary>
        /// Which member names must give way, and what each becomes.
        ///
        /// <c>packoffset</c> lets a declaration skip rows, but two members may not
        /// cover the same byte -- fxc rejects that outright.  A row can be labelled
        /// twice when one stage names it as a whole and the other only some of its
        /// lanes, and only one can be declared: the wider one wins, and every
        /// narrower name is rewritten to read through it, because the body that
        /// spells it still has to compile.
        ///
        /// Keyed by name because that is what the emitted bodies spell.
        /// </summary>
        public static Dictionary<string, string> OverlapRenames(IEnumerable<CbLayout.Member> members)
        {
            var list = members.OrderBy(m => m.Row).ThenBy(m => m.Col).ToList();
            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            var occupied = new Dictionary<int, Member>();

            void Take(Member m)
            {
                int from = m.Row * 4 + m.Col;
                int count = m.IsArray ? m.Rows * 4 : TypeComps(m.Type);
                for (int i = from; i < from + count; i++) occupied[i] = m;
            }

            // Wide members first, so a row declared as a whole keeps its name and
            // the scalars that tried to share it fall away.
            foreach (var m in list.Where(m => m.IsArray || TypeComps(m.Type) == 4)) Take(m);
            foreach (var m in list.Where(m => !(m.IsArray || TypeComps(m.Type) == 4)))
            {
                int from = m.Row * 4 + m.Col;
                int count = TypeComps(m.Type);
                Member host = null;
                for (int i = from; i < from + count; i++)
                    if (occupied.TryGetValue(i, out var h)) { host = h; break; }

                if (host == null) { Take(m); continue; }

                // Read it through the winner: a matrix or array is indexed, a vector
                // is swizzled, and a scalar is itself.
                renames[m.Name] = host.IsArray
                    ? $"{host.Name}[{m.Row - host.Row}].{"xyzw"[m.Col]}"
                    : TypeComps(host.Type) == 1 ? host.Name
                    : $"{host.Name}.{"xyzw"[m.Col - host.Col]}";
            }
            return renames;
        }

        private static bool IsPositional(string name) => name.StartsWith("_cb", StringComparison.Ordinal);

        /// <summary>
        /// Drop any member that would share bytes with an earlier one.
        ///
        /// <c>packoffset</c> lets a declaration skip rows, but it does not let two
        /// members cover the same byte -- fxc rejects that outright ("packoffset
        /// overlaps").  A row can be labelled twice when one stage names it as a
        /// whole and the other names only some lanes, and only one of the two can
        /// survive: the wider one is kept, because it already carries the name the
        /// body uses for the whole row.
        /// </summary>
        public static List<CbLayout.Member> DropOverlaps(IEnumerable<CbLayout.Member> members) =>
            RemoveOverlaps(members.OrderBy(m => m.Row).ThenBy(m => m.Col).ToList());

        private static List<Member> RemoveOverlaps(List<Member> members)
        {
            var kept = new List<Member>(members.Count);
            bool[] taken = new bool[64 * 4];

            void Take(Member m)
            {
                int from = m.Row * 4 + m.Col;
                int to = from + (m.IsArray ? m.Rows * 4 : TypeComps(m.Type));
                if (from < 0 || to > taken.Length) return;
                for (int i = from; i < to; i++) taken[i] = true;
                kept.Add(m);
            }

            void Fits(Member m)
            {
                int from = m.Row * 4 + m.Col;
                int to = from + (m.IsArray ? m.Rows * 4 : TypeComps(m.Type));
                if (from < 0 || to > taken.Length) return;
                for (int i = from; i < to; i++)
                    if (taken[i]) return;
                Take(m);
            }

            // Wide members first, so a row declared as a whole keeps its name and the
            // scalars that tried to share it fall away.
            foreach (var m in members.Where(m => m.IsArray || TypeComps(m.Type) == 4).OrderBy(m => m.Row))
                Fits(m);
            foreach (var m in members.Where(m => !(m.IsArray || TypeComps(m.Type) == 4))
                                     .OrderBy(m => m.Row).ThenBy(m => m.Col))
                Fits(m);
            kept.Sort((a, b) => a.Row != b.Row ? a.Row - b.Row : a.Col - b.Col);
            return kept;
        }

        private static bool HasLane(Dictionary<(int row, int col), string> lanes, int row) =>
            lanes != null && Enumerable.Range(0, 4).Any(c => lanes.ContainsKey((row, c)));

        /// <summary>
        /// Declare the individually named lanes of one row; false when none is usable.
        ///
        /// A lane is only worth declaring on its own account when the row is not
        /// already covered by a whole-row member.  Mixing the two produces a
        /// declaration that reads the same bytes twice (<c>float4 _cb0_row2</c> and
        /// <c>float _X</c> at the same offset), which is legal but makes the row
        /// constructor the body uses multi-valued.
        /// </summary>
        private static bool DeclareLanes(List<Member> members, Dictionary<int, string> rows,
                                         Dictionary<(int row, int col), string> lanes,
                                         int reg, int row, HashSet<string> used, int mask = 0xF)
        {
            if (lanes == null || lanes.Count == 0) return false;

            // The row's own entry is being rebuilt here.  Leaving the incoming value
            // in place lets LaneRef read it back and the constructor reference
            // itself -- `float4(_cb0_row2, _cb0_row2.y, ...)`.
            rows.Remove(row);

            var parts = new string[4];
            bool any = false;
            for (int c = 0; c < 4; c++)
            {
                if (!lanes.TryGetValue((row, c), out var raw) || string.IsNullOrEmpty(raw)) continue;
                any = true;
                parts[c] = raw;
            }
            if (!any) return false;

            // Walk the lanes left to right; a member that starts here owns a run of
            // consecutive lanes (`float2 _Flip` covers x and y).
            int lane = 0;
            while (lane < 4)
            {
                string raw = parts[lane];
                string bas = null;
                int off = 0;
                bool matrix = false;
                if (raw != null)
                {
                    var lm = LaneMember.Match(raw);
                    var mx = LaneMatrix.Match(raw);
                    if (mx.Success) matrix = true;
                    else if (lm.Success) { bas = lm.Groups[1].Value; off = "xyzw".IndexOf(lm.Groups[2].Value, StringComparison.Ordinal); }
                    else if (Identifier.IsMatch(raw)) bas = raw;
                }

                if (matrix || bas == null || off != 0)
                {
                    // Not the start of a declarable member.  A matrix element needs
                    // no declaration of its own -- the matrix is declared from the
                    // row-level names -- but anything else keeps its lane width so
                    // it never overlaps a member that starts later in the row.
                    //
                    // A lane this program never reads needs no declaration either:
                    // packoffset lets the block skip it entirely.
                    if (!matrix && (mask & (1 << lane)) != 0)
                        AddLaneSlot(members, rows, reg, row, lane);
                    lane++;
                    continue;
                }

                int run = 1;
                while (lane + run < 4)
                {
                    string nx = parts[lane + run];
                    if (nx == null) break;
                    var lm = LaneMember.Match(nx);
                    if (!lm.Success) break;
                    if (lm.Groups[1].Value != bas) break;
                    if ("xyzw".IndexOf(lm.Groups[2].Value, StringComparison.Ordinal) != run) break;
                    run++;
                }

                if (used.Add(bas) && lane + run <= 4)
                {
                    members.Add(new Member
                    {
                        Name = bas,
                        Type = run == 1 ? "float" : run == 2 ? "float2" : run == 3 ? "float3" : "float4",
                        Row = row, Col = lane,
                    });
                    lane += run;
                }
                else
                {
                    if ((mask & (1 << lane)) != 0)
                        AddLaneSlot(members, rows, reg, row, lane);
                    lane++;
                }
            }

            // The row's own reference is assembled from its lanes, so a whole-row
            // read still has something to point at.  A float4 constructor can be
            // swizzled like a member, which is what lets the emitter keep its
            // existing `row + swizzle` shape.
            //
            // Every element must be a scalar.  A lane that resolves to a vector --
            // including the row's own whole-row member, which would make the
            // constructor reference itself -- is read through as `member.lane`;
            // anything wider makes the constructor five arguments wide (X3014).
            string At(int col)
            {
                string got = LaneRef(members, lanes, rows, reg, row, col);
                var mem = members.FirstOrDefault(m => m.Name == got && !m.IsArray && m.Row == row);
                if (mem != null && TypeComps(mem.Type) > 1)
                    return $"{mem.Name}.{"xyzw"[col - mem.Col]}";
                // A lane that ended up undeclared (this program never reads it) must
                // not be named in the constructor -- the identifier would not exist.
                bool declared = mem != null ||
                                members.Any(m => !m.IsArray && m.Row == row &&
                                                 col >= m.Col && col < m.Col + TypeComps(m.Type));
                return declared ? got : "0.0f";
            }
            rows[row] = $"float4({At(0)}, {At(1)}, {At(2)}, {At(3)})";
            return true;
        }

        /// <summary>
        /// How the body reads one lane of a row it declared lane by lane.
        ///
        /// The answer is always a *scalar*: it is spliced into a
        /// <c>float4(a, b, c, d)</c> constructor, so handing back the whole row
        /// member would make that constructor five arguments wide and fxc rejects
        /// it (X3014).
        /// </summary>
        private static string LaneRef(List<Member> members, Dictionary<(int row, int col), string> lanes,
                                      Dictionary<int, string> rows, int reg, int row, int col)
        {
            string lane = "xyzw"[col].ToString();

            if (lanes.TryGetValue((row, col), out var raw) && !string.IsNullOrEmpty(raw))
            {
                if (LaneMatrix.IsMatch(raw)) return raw;          // M[i].x -- already scalar
                var lm = LaneMember.Match(raw);
                if (!lm.Success) return Identifier.IsMatch(raw) ? raw : $"_cb{reg}_row{row}_{lane}";

                string bas = lm.Groups[1].Value;
                string comp = lm.Groups[2].Value;
                var mem = members.FirstOrDefault(x => x.Name == bas && x.Row == row);
                if (mem == null)
                    // The base name was never declared for this row -- only the lane
                    // the declaration manager actually emitted exists, so reading
                    // through the base would name an undefined identifier (X3004).
                    return $"_cb{reg}_row{row}_{lane}";
                if (TypeComps(mem.Type) == 1) return bas;                // scalar
                return $"{bas}.{comp}";
            }

            // The lane itself has no name, but the row may: read it through the row.
            // A whole-row member is a float4 (or vector), so the lane is a swizzle.
            if (rows.TryGetValue(row, out var rowName) &&
                !LaneMember.IsMatch(rowName) && !LaneMatrix.IsMatch(rowName))
                return $"{rowName}.{lane}";

            return $"_cb{reg}_row{row}_{lane}";
        }

        private static int TypeComps(string type) => type switch
        {
            "float" => 1, "float2" => 2, "float3" => 3, "float4" => 4, _ => 1,
        };

        /// <summary>
        /// Can a member of <paramref name="len"/> rows start at <paramref name="row"/>?
        ///
        /// Overlap is a real conflict -- two members cannot cover the same bytes --
        /// but an adjacent member is not: with <c>packoffset</c> the declaration
        /// does not lay members out end to end, so a row before or after the run is
        /// simply a different member at a different offset.
        /// </summary>
        private static bool IsFree(List<Member> members, int row, int len)
        {
            for (int r = row; r < row + len; r++)
                if (Occupies(members, r)) return false;
            return true;
        }

        /// <summary>
        /// True when any member already covers this row.
        ///
        /// Any COLUMN counts, not just column 0: a row that holds three named
        /// scalars is fully covered as far as <c>packoffset</c> is concerned, and
        /// adding a whole-row float4 on top of it either collides with those scalars
        /// or -- when the overlap pass then drops one of the two -- takes the names
        /// away from a body that still spells them.
        /// </summary>
        private static bool Occupies(List<Member> members, int row) =>
            row >= 0 && members.Any(m => row >= m.Row && row < m.Row + m.Rows);

        private static void AddPositional(List<Member> members, Dictionary<int, string> rows,
                                          int reg, int row, int col, int width, int mask = 0xF,
                                          bool claim = true)
        {
            // Narrow the placeholder to the lanes the program reads, so a row it
            // only touches in `.z` does not claim a whole float4 that another
            // member might need.
            int want = mask & 0xF;
            if (want == 0) want = 0xF;
            if (width == 4 && col == 0)
            {
                int lo = LowestLane(want), hi = HighestLane(want);
                if (lo > 0 || hi < 3) { col = lo; width = hi - lo + 1; }
            }

            // The lane suffix only disambiguates a member that does not start at x;
            // at column 0 the row number alone names it, whatever its width.
            string positional = col == 0
                ? $"_cb{reg}_row{row}"
                : $"_cb{reg}_row{row}_{"xyzw"[col]}";
            members.Add(new Member
            {
                Name = positional,
                Type = width == 1 ? "float" : width == 2 ? "float2" : width == 3 ? "float3" : "float4",
                Row = row, Col = col,
            });
            if (claim) rows[row] = positional;
        }

        private static int LowestLane(int mask)
        {
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) return i;
            return 0;
        }

        private static int HighestLane(int mask)
        {
            for (int i = 3; i >= 0; i--) if ((mask & (1 << i)) != 0) return i;
            return 3;
        }

        /// <summary>
        /// Reserve one lane of a row that is being declared lane by lane.
        ///
        /// Width matters here: a whole-row placeholder at the same offset as a
        /// later <c>float</c> makes the row's constructor ambiguous, so a lane keeps
        /// exactly its one component.
        /// </summary>
        private static void AddLaneSlot(List<Member> members, Dictionary<int, string> rows,
                                        int reg, int row, int lane)
        {
            string name = $"_cb{reg}_row{row}_{"xyzw"[lane]}";
            members.Add(new Member { Name = name, Type = "float", Row = row, Col = lane });
        }

        /// <summary>
        /// The declaration for a row -> name map, as lines.  Public so the ShaderLab
        /// wrapper can rebuild one buffer from two stages' copies of it.
        /// </summary>
        public static List<string> DeclareLines(int reg, int extent, Dictionary<int, string> names) =>
            DeclareMembers(reg, extent, names, null).members.Select(Line).ToList();

        /// <summary>One member, with the <c>packoffset</c> that pins it where it belongs.</summary>
        public static string Line(Member m)
        {
            string decl = m.IsArray ? $"{m.Type} {m.Name}[{m.Rows}]" : $"{m.Type} {m.Name}";
            return $"    {decl} : packoffset(c{m.Row}.{"xyzw"[m.Col]});";
        }

        /// <summary>The buffer's declared name, made a legal identifier.</summary>
        public static string BufferName(Resolve.CbResolver cb, int reg)
        {
            string n = cb != null && cb.CbName.TryGetValue(reg, out var v) ? v : null;
            if (string.IsNullOrEmpty(n)) return $"CB{reg}";
            // $Globals is fxc's own container name; `$` is not an identifier character.
            if (n == "$Globals") return "UnityGlobals";
            var sb = new System.Text.StringBuilder(n.Length);
            foreach (char c in n)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            if (sb.Length == 0 || (!char.IsLetter(sb[0]) && sb[0] != '_')) sb.Insert(0, '_');
            return sb.ToString();
        }
    }
}
