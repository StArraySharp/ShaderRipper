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
    /// <c>unity_WorldToLight[2]</c>, or nothing.  That answer cannot be repeated
    /// verbatim: a constant buffer is laid out member after member, each starting
    /// where the previous one ended, so the declaration has to cover every row from
    /// zero in order.
    ///
    /// Two shapes are produced, per register:
    ///
    ///   * <b>named members</b> -- one declaration per row, with a matrix run
    ///     (<c>unity_WorldToLight[0..3]</c>) collapsed into
    ///     <c>float4 unity_WorldToLight[4];</c> so the reference the resolver wrote
    ///     still resolves, and a positional member for any row nothing pinned.
    ///   * <b>the raw array</b> -- <c>float4 cb1[21];</c> -- only for a buffer read
    ///     with a runtime row index, which no member name can express.
    ///
    /// For a buffer Unity itself defines, the layout comes from the shipped table
    /// rather than from the rows this one program happened to read: a vertex shader
    /// that touches rows 1 and 2 of <c>UnityPerDraw</c> has no matrix row 0 to
    /// anchor on, but the table does.
    /// </summary>
    internal sealed class CbLayout
    {
        public sealed class Member
        {
            public string Name = "";
            public string Type = "float4";
            public int Rows = 1;
            public bool IsArray;
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

        private static readonly Regex MatrixRow =
            new(@"^([A-Za-z_]\w*)\[(\d+)\]$", RegexOptions.CultureInvariant);

        private static readonly Regex Identifier =
            new(@"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant);

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
                if (cb == null) { layout.ArrayForm[reg] = true; layout.Extent[reg] = Math.Max(d.Count, 1); continue; }
                if (cb.HasDynamicIndex(reg))
                {
                    layout.ArrayForm[reg] = true;
                    layout.Extent[reg] = Math.Max(d.Count, 1);
                    layout.Fallback[reg] = "read with a runtime row index";
                    continue;
                }

                int extent = Math.Max(d.Count, 1);
                foreach (var kv in cb.RowExpr.Keys.Where(k => k.cb == reg))
                    extent = Math.Max(extent, kv.row + 1);

                var names = new Dictionary<int, string>();
                string cbname = cb.CbName.TryGetValue(reg, out var n) ? n : null;
                int tableRows = string.IsNullOrEmpty(cbname) ? 0 : UnityCbuffers.RowCount(cbname);

                // A buffer Unity defines gets its real layout, not the subset this
                // program read: a vertex shader that touches rows 1 and 2 of
                // UnityPerDraw has no matrix row 0 to anchor a declaration on, but
                // the shipped table does.  Only trusted when it covers every read
                // row and starts at zero -- otherwise the offsets it implies are not
                // the ones the disassembly indexes.
                if (tableRows > 0 && Covers(tableRows, cb, reg) &&
                    EmitTable(layout, cbname, reg, tableRows, extent))
                {
                    layout.Extent[reg] = extent;
                    continue;
                }

                foreach (var kv in cb.RowExpr.Where(k => k.Key.cb == reg))
                    names[kv.Key.row] = kv.Value;

                layout.Extent[reg] = extent;
                Declare(layout, reg, extent, names);
            }
            return layout;
        }

        /// <summary>Every read row of the register sits inside the table's span.</summary>
        private static bool Covers(int tableRows, Resolve.CbResolver cb, int reg)
        {
            if (!cb.RowUse.TryGetValue(reg, out var use)) return true;
            return use.Keys.All(r => r < tableRows);
        }

        /// <summary>
        /// Fill the row -> name map from the shipped layout of a buffer Unity defines.
        ///
        /// Only a member that owns its whole row can be named here.  A row that
        /// holds several members (<c>UnityPerDrawSprite</c> row 1 is a <c>float2</c>
        /// beside a <c>float</c>) would have to be declared as
        /// <c>float2 _Flip; float _EnableExternalAlpha;</c> and read by component,
        /// and a half-declared row shifts every member after it.  Those rows keep a
        /// positional float4, which is the shape that cannot be wrong.
        /// </summary>
        private static bool EmitTable(CbLayout layout, string cbname, int reg, int tableRows, int extent)
        {
            var names = new Dictionary<int, string>();
            for (int row = 0; row < Math.Min(tableRows, extent); row++)
            {
                var m = UnityCbuffers.Find(cbname, row);
                if (m == null || m.Row != row) continue;
                if (m.IsMatrix) names[row] = $"{m.Name}[{row - m.Row}]";
                else if (m.FillsRow) names[row] = m.Name;
            }

            Declare(layout, reg, extent, names);
            return true;
        }

        /// <summary>
        /// Turn a row -> name map into a member list that keeps the offsets.
        ///
        /// A matrix or array row is spelled `name[i]`; the consecutive run becomes
        /// one `float4 name[n]`, which is both the correct layout and the spelling
        /// the body's references already use.  A run that does not start at index 0
        /// cannot be declared at the right offset, so those rows keep a positional
        /// member and the body keeps the positional spelling.
        /// </summary>
        private static void Declare(CbLayout layout, int reg, int extent, Dictionary<int, string> names)
        {
            var members = new List<Member>();
            var rows = new Dictionary<int, string>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            int row = 0;

            while (row < extent)
            {
                string raw = names.TryGetValue(row, out var v) ? v : null;
                if (string.IsNullOrEmpty(raw))
                {
                    AddPositional(members, rows, reg, row++);
                    continue;
                }

                var mm = MatrixRow.Match(raw);
                if (mm.Success)
                {
                    string bas = mm.Groups[1].Value;
                    int idx = int.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (idx == 0 && !used.Contains(bas))
                    {
                        int run = 0;
                        while (row + run < extent && names.TryGetValue(row + run, out var next) &&
                               MatrixRow.Match(next) is var nm && nm.Success &&
                               nm.Groups[1].Value == bas &&
                               int.Parse(nm.Groups[2].Value, CultureInfo.InvariantCulture) == run)
                            run++;

                        if (run >= 2)
                        {
                            members.Add(new Member { Name = bas, Rows = run, IsArray = true });
                            for (int i = 0; i < run; i++) rows[row + i] = $"{bas}[{i}]";
                            used.Add(bas);
                            row += run;
                            continue;
                        }
                    }
                    AddPositional(members, rows, reg, row++);
                    continue;
                }

                if (!Identifier.IsMatch(raw) || !used.Add(raw))
                {
                    AddPositional(members, rows, reg, row++);
                    continue;
                }

                members.Add(new Member { Name = raw });
                rows[row] = raw;
                row++;
            }

            layout.Members[reg] = members;
            layout.RowName[reg] = rows;
        }

        private static void AddPositional(List<Member> members, Dictionary<int, string> rows, int reg, int row)
        {
            string positional = $"_cb{reg}_row{row}";
            members.Add(new Member { Name = positional });
            rows[row] = positional;
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
