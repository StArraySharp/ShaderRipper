using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ShaderRipper.Ast
{
    /// <summary>One row of a DXBC input/output signature block.</summary>
    internal sealed class SigEntry
    {
        public string Name;
        public int Index;
        public string Mask;
        public int Register;
        public string SysValue;
        public string Format;
        public string Used;

        public bool IsSystem => SysValue != null &&
                                !SysValue.Equals("NONE", StringComparison.OrdinalIgnoreCase);

        /// <summary>Component count implied by the declaration mask.</summary>
        public int Comps => string.IsNullOrEmpty(Mask) ? 4 : Mask.Length;

        public string HlslType => Comps == 1 ? "float" : "float" + Comps;
    }

    /// <summary>
    /// Reads the input and output signature tables that D3DDisassemble emits as
    /// leading comments.
    ///
    /// They are the one part of the disassembly the grammar drops: it treats a
    /// comment as a comment, and rightly so, but these tables are not commentary --
    /// they carry the semantic name, index, declaration mask and register of every
    /// stage input and output, which is what a shader that can be recompiled needs.
    /// So they are read from the text, the way the main tool reads them, and hung on
    /// the module beside the instruction tree.
    /// </summary>
    internal static class SignatureReader
    {
        private static readonly Regex Row = new(
            @"^([A-Za-z_][A-Za-z0-9_]*)\s+(\d+)\s+([xyzw]{1,4})\s+(\d+)\s+(\S+)\s+(\S+)\s*([xyzw\s]*)$",
            RegexOptions.Compiled);

        /// <summary>Fill <paramref name="m"/>'s signature lists; leaves them null when absent.</summary>
        public static void Read(string asm, Module m)
        {
            int section = 0;   // 0 none, 1 input, 2 output
            foreach (string raw in asm.Replace("\r\n", "\n").Split('\n'))
            {
                string s = raw.Trim();
                if (!s.StartsWith("//", StringComparison.Ordinal)) continue;
                s = s.Substring(2).Trim();

                if (s.StartsWith("Input signature", StringComparison.OrdinalIgnoreCase))
                {
                    section = 1;
                    m.InputSig ??= new List<SigEntry>();
                    continue;
                }
                if (s.StartsWith("Output signature", StringComparison.OrdinalIgnoreCase))
                {
                    section = 2;
                    m.OutputSig ??= new List<SigEntry>();
                    continue;
                }
                if (section == 0) continue;
                if (s.Length == 0 || s.StartsWith("Name") || s.StartsWith("-"))
                    continue;

                var mm = Row.Match(s);
                if (!mm.Success) { section = 0; continue; }

                var e = new SigEntry
                {
                    Name = mm.Groups[1].Value,
                    Index = int.Parse(mm.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                    Mask = mm.Groups[3].Value,
                    Register = int.Parse(mm.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture),
                    SysValue = mm.Groups[5].Value,
                    Format = mm.Groups[6].Value,
                    Used = mm.Groups[7].Value.Trim(),
                };
                if (section == 1) m.InputSig.Add(e);
                else m.OutputSig.Add(e);
            }
        }
    }
}
