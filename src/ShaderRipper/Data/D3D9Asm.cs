using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Disassembler for the d3d9 (SM1/2/3) shader bytecode Unity ships in the
    /// <c>d3d9</c> platform slot.
    ///
    /// A d3d9 program is not a DXBC container but a bare token stream: a version
    /// token (<c>0xFFFE____</c> = vs, <c>0xFFFF____</c> = ps) followed by
    /// instructions that carry their own length in bits 24-27 (the length does
    /// <b>not</b> count the instruction token itself), terminated by
    /// <c>END = 0x0000FFFF</c>.  <c>D3DDisassemble</c> only understands DXBC, so
    /// the tokens are decoded here from the field layout in <c>d3d9types.h</c>:
    ///
    /// <code>
    /// instruction token: opcode = bits 0-15, opcode-specific control = bits
    ///                    16-23 (comparison code for ifc/breakc/setp, texld
    ///                    _pp/_bx), length = bits 24-27
    /// comment token:     bits 0-15 = 0xFFFE, bits 16-30 = DWORD count
    /// operand token:     bit 31 = 1, register number = bits 0-10, register type
    ///                    = bits 28-30 (low 3) plus bits 11-12 (high 2), source
    ///                    swizzle = bits 16-23, source modifier = bits 24-27,
    ///                    destination write mask = bits 16-19, destination
    ///                    modifier = bits 20-23, relative addressing = bit 13
    /// </code>
    ///
    /// Output follows fxc's old spelling:
    /// <code>
    ///     ps_3_0
    ///     dcl_2d s0
    ///     texld r0, t0, s0
    ///     mul oC0, r0, c0
    /// </code>
    ///
    /// Disassembly only: SM2/3 uses a different dialect from DXBC
    /// (texld/mova/absolute register numbers instead of the dcl_ prefix plus
    /// relative r# numbering), so feeding it to <see cref="Emit.HlslEmitter"/>
    /// would mean writing a second translator for a backend whose games are
    /// long out of support.  The slot is therefore handled like DXIL:
    /// disassembly is the deliverable, back-translation is not attempted.
    /// </summary>
    internal static class D3D9Asm
    {
        private const uint EndToken = 0x0000FFFF;
        private const int CommentOpcode = 0xFFFE;

        /// <summary>Opcode table: <c>(mnemonic, first operand is the destination)</c>.</summary>
        private static readonly Dictionary<int, (string name, bool destFirst)> Ops =
            new()
            {
                [0] = ("nop", false), [1] = ("mov", true), [2] = ("add", true),
                [3] = ("sub", true), [4] = ("mad", true), [5] = ("mul", true),
                [6] = ("rcp", true), [7] = ("rsq", true), [8] = ("dp3", true),
                [9] = ("dp4", true), [10] = ("min", true), [11] = ("max", true),
                [12] = ("slt", true), [13] = ("sge", true), [14] = ("exp", true),
                [15] = ("log", true), [16] = ("lit", true), [17] = ("dst", true),
                [18] = ("lrp", true), [19] = ("frc", true), [20] = ("m4x4", true),
                [21] = ("m4x3", true), [22] = ("m3x4", true), [23] = ("m3x3", true),
                [24] = ("m3x2", true), [25] = ("call", false), [26] = ("callnz", false),
                [27] = ("loop", false), [28] = ("ret", false), [29] = ("endloop", false),
                [30] = ("label", false), [31] = ("dcl", true), [32] = ("pow", true),
                [33] = ("crs", true), [34] = ("sgn", true), [35] = ("abs", true),
                [36] = ("nrm", true), [37] = ("sincos", true), [38] = ("rep", false),
                [39] = ("endrep", false), [40] = ("if", false), [41] = ("ifc", false),
                [42] = ("else", false), [43] = ("endif", false), [44] = ("break", false),
                [45] = ("breakc", false), [46] = ("mova", true), [47] = ("defb", true),
                [48] = ("defi", true),
                [64] = ("texcoord", true), [65] = ("texkill", true), [66] = ("texld", true),
                [67] = ("texbem", true), [68] = ("texbeml", true), [69] = ("texreg2ar", true),
                [70] = ("texreg2gb", true), [71] = ("texm3x2pad", true), [72] = ("texm3x2tex", true),
                [73] = ("texm3x3pad", true), [74] = ("texm3x3tex", true), [76] = ("texm3x3spec", true),
                [77] = ("texm3x3vspec", true), [78] = ("expp", true), [79] = ("logp", true),
                [80] = ("cnd", true), [81] = ("def", true), [82] = ("texreg2rgb", true),
                [83] = ("texdp3tex", true), [84] = ("texm3x2depth", true), [85] = ("texdp3", true),
                [86] = ("texm3x3", true), [87] = ("texdepth", true), [88] = ("cmp", true),
                [89] = ("bem", true), [90] = ("dp2add", true), [91] = ("dsx", true),
                [92] = ("dsy", true), [93] = ("texldd", true), [94] = ("setp", true),
                [95] = ("texldl", true), [96] = ("breakp", false),
            };

        /// <summary>Comparison codes for <c>ifc</c> / <c>breakc</c> / <c>setp</c>.</summary>
        private static readonly string[] Comparison = { "", "gt", "eq", "ge", "lt", "ne", "le" };

        /// <summary><c>D3DDECLUSAGE</c>: the usage half of a SM2/3 <c>dcl</c>.</summary>
        private static readonly string[] Usage =
        {
            "position", "blendweight", "blendindices", "normal", "psize", "texcoord",
            "tangent", "binormal", "tessfactor", "positiont", "color", "fog", "depth", "sample",
        };

        private static readonly string[] Swizzle = { "x", "y", "z", "w" };

        /// <summary>Disassemble one d3d9 token stream; null when it does not decode.</summary>
        public static string Disassemble(byte[] b, int off, int len) =>
            Disassemble(b, off, len, out _);

        /// <summary>
        /// Disassemble one d3d9 token stream and report the stage the version
        /// token declares (<c>"vs"</c> / <c>"ps"</c>); null when it does not decode.
        /// </summary>
        public static string Disassemble(byte[] b, int off, int len, out string stage)
        {
            stage = null;
            if (b == null || off < 0 || off + 4 > b.Length) return null;
            int end = Math.Min(off + len, b.Length);

            uint version = BitConverter.ToUInt32(b, off);
            uint kind = version >> 16;
            if (kind != 0xFFFE && kind != 0xFFFF) return null;
            bool isVertex = kind == 0xFFFE;
            stage = isVertex ? "vs" : "ps";
            uint major = (version >> 8) & 0xFF, minor = version & 0xFF;
            // SM1 tokens carry no length field -- the length is implied by the
            // opcode -- so the walk below cannot decode them.  SM2 and SM3 share
            // the encoding this reads.
            if (major < 2 || major > 3) return null;

            var sb = new StringBuilder();
            sb.Append("    ").Append(isVertex ? "vs_" : "ps_").Append(major);
            sb.Append(minor switch
            {
                0 => "_0",
                0x0A => "_a",
                0x0B => "_b",
                0x0F => isVertex ? "_sw" : "_x",
                _ => "_" + minor.ToString("x"),
            }).Append('\n');

            int p = off + 4;
            while (p + 4 <= end)
            {
                uint w = BitConverter.ToUInt32(b, p);
                if (w == EndToken) return sb.ToString();

                if ((w & 0xFFFF) == CommentOpcode)
                {
                    int count = (int)((w >> 16) & 0x7FFF);
                    if (count == 0) return null;
                    p += 4 + count * 4;
                    continue;
                }
                // An operand token where an instruction belongs: the stream is broken.
                if ((w & 0x80000000) != 0) return null;

                int opcode = (int)(w & 0xFFFF);
                int length = (int)((w >> 24) & 0xF);
                if (length <= 0) return null;
                int instEnd = p + (length + 1) * 4;
                if (instEnd > end) return null;

                sb.Append("    ");
                if (!EmitInstruction(b, p, instEnd, w, opcode, isVertex, major, sb)) return null;
                p = instEnd;
            }
            return null;
        }

        private static bool EmitInstruction(byte[] b, int p, int instEnd, uint w, int opcode,
                                            bool isVertex, uint major, StringBuilder sb)
        {
            Ops.TryGetValue(opcode, out var op);
            string name = op.name ?? "op_" + opcode;

            int control = (int)((w >> 16) & 0xFF);
            name = opcode switch
            {
                41 => "if_" + Compare(control),
                45 => "break_" + Compare(control),
                94 => "setp_" + Compare(control),
                66 => control switch { 1 => "texldp", 2 => "texldb", _ => "texld" },
                _ => name,
            };

            int at = p + 4;

            switch (opcode)
            {
                // def: one constant register followed by four float literals.
                case 81:
                {
                    string dest = Operand(b, ref at, instEnd, isDest: true, isVertex, major);
                    if (dest == null) return false;
                    var parts = new List<string> { dest };
                    for (int k = 0; k < 4 && at + 4 <= instEnd; k++, at += 4)
                        parts.Add(BitConverter.ToSingle(b, at).ToString("0.######", CultureInfo.InvariantCulture));
                    sb.Append(name).Append(' ').Append(string.Join(", ", parts)).Append('\n');
                    return true;
                }
                // defb: one boolean literal.
                case 47:
                {
                    string dest = Operand(b, ref at, instEnd, isDest: true, isVertex, major);
                    if (dest == null) return false;
                    string value = at + 4 <= instEnd && BitConverter.ToUInt32(b, at) != 0 ? "true" : "false";
                    sb.Append(name).Append(' ').Append(dest).Append(", ").Append(value).Append('\n');
                    return true;
                }
                // defi: one integer register followed by four integer literals.
                case 48:
                {
                    string dest = Operand(b, ref at, instEnd, isDest: true, isVertex, major);
                    if (dest == null) return false;
                    var parts = new List<string> { dest };
                    for (int k = 0; k < 4 && at + 4 <= instEnd; k++, at += 4)
                        parts.Add(BitConverter.ToInt32(b, at).ToString(CultureInfo.InvariantCulture));
                    sb.Append(name).Append(' ').Append(string.Join(", ", parts)).Append('\n');
                    return true;
                }
                // dcl: a declaration parameter token, then the register it declares.
                case 31:
                {
                    if (at + 4 > instEnd) return false;
                    uint param = BitConverter.ToUInt32(b, at);
                    at += 4;
                    string dest = Operand(b, ref at, instEnd, isDest: true, isVertex, major);
                    if (dest == null) return false;

                    uint dimension = (param >> 27) & 0x7;
                    if (dimension is >= 2 and <= 4)
                    {
                        string kind = dimension == 4 ? "volume" : dimension == 3 ? "cube" : "2d";
                        sb.Append("dcl_").Append(kind).Append(' ').Append(dest).Append('\n');
                        return true;
                    }
                    if (major >= 3)
                    {
                        uint usage = param & 0xF, index = (param >> 16) & 0xF;
                        string u = usage < (uint)Usage.Length ? Usage[usage] : "usage" + usage;
                        sb.Append("dcl_").Append(u);
                        if (index != 0) sb.Append(index);
                        sb.Append(' ').Append(dest).Append('\n');
                        return true;
                    }
                    sb.Append("dcl ").Append(dest).Append('\n');
                    return true;
                }
            }

            sb.Append(name);
            bool first = true;
            while (at + 4 <= instEnd)
            {
                string operand = Operand(b, ref at, instEnd, op.destFirst && first, isVertex, major);
                if (operand == null) return false;
                sb.Append(first ? " " : ", ").Append(operand);
                first = false;
            }
            sb.Append('\n');
            return true;
        }

        private static string Compare(int code) =>
            code > 0 && code < Comparison.Length ? Comparison[code] : "";

        /// <summary>Register type = bits 28-30 (low three) plus bits 11-12 (high two).</summary>
        private static uint RegisterType(uint w) => ((w >> 28) & 0x7) | (((w >> 11) & 0x3) << 3);

        /// <summary>
        /// Format one operand token (bit 31 set).  Relative addressing (bit 13)
        /// consumes a second operand token, which becomes the index expression.
        /// </summary>
        private static string Operand(byte[] b, ref int p, int end, bool isDest,
                                      bool isVertex, uint major)
        {
            if (p + 4 > end) return null;
            uint w = BitConverter.ToUInt32(b, p);
            p += 4;
            if ((w & 0x80000000) == 0) return null;

            int num = (int)(w & 0x7FF);
            string reg = RegisterType(w) switch
            {
                0 => "r" + num,
                1 => "v" + num,
                2 => "c" + num,
                3 => isVertex ? "a" + num : "t" + num,
                4 => num == 0 ? "oPos" : num == 1 ? "oFog" : "oPts",
                5 => "oD" + num,
                // SM3 merged texcrdout into the generic o# registers; SM2 uses oT#.
                6 => isVertex && major < 3 ? "oT" + num : isVertex ? "o" + num : "oT" + num,
                7 => "i" + num,
                8 => "oC" + num,
                9 => "oDepth",
                10 => "s" + num,
                11 => "c" + (num + 2048),
                12 => "c" + (num + 4096),
                13 => "c" + (num + 6144),
                14 => "b" + num,
                15 => "aL",
                17 => num == 1 ? "vFace" : "vPos",
                18 => "L" + num,
                19 => "p" + num,
                _ => "?t" + RegisterType(w) + "_" + num,
            };

            string prefix = "";
            string suffix = "";

            if (isDest)
            {
                uint mask = (w >> 16) & 0xF;
                if (mask != 0 && mask != 0xF)
                {
                    var m = new StringBuilder(".");
                    if ((mask & 1) != 0) m.Append('x');
                    if ((mask & 2) != 0) m.Append('y');
                    if ((mask & 4) != 0) m.Append('z');
                    if ((mask & 8) != 0) m.Append('w');
                    suffix += m.ToString();
                }
                uint modify = (w >> 20) & 0xF;
                if ((modify & 1) != 0) suffix += "_sat";
                if ((modify & 2) != 0) suffix += "_pp";
                if ((modify & 4) != 0) suffix += "_centroid";
            }
            else
            {
                uint swizzle = (w >> 16) & 0xFF;
                int x = (int)(swizzle & 3), y = (int)((swizzle >> 2) & 3);
                int z = (int)((swizzle >> 4) & 3), a = (int)((swizzle >> 6) & 3);
                if (!(x == 0 && y == 1 && z == 2 && a == 3))
                    suffix += "." + Swizzle[x] + Swizzle[y] + Swizzle[z] + Swizzle[a];

                switch ((w >> 24) & 0xF)
                {
                    case 1: prefix = "-"; break;
                    case 2: suffix += "_bias"; break;
                    case 3: prefix = "-"; suffix += "_bias"; break;
                    case 4: prefix = "1-"; break;
                    case 5: suffix += "_bx2"; break;
                    case 6: prefix = "-"; suffix += "_bx2"; break;
                    case 7: prefix = "1-"; suffix += "_bx2"; break;
                    case 8: suffix += "_x2"; break;
                    case 9: prefix = "-"; suffix += "_x2"; break;
                    case 10: suffix += "_dz"; break;
                    case 11: suffix += "_dw"; break;
                }
            }

            if (!isDest && (w & 0x2000) != 0)
            {
                string address = Operand(b, ref p, end, isDest: false, isVertex, major);
                if (address == null) return null;
                return prefix + reg + "[" + address + "]" + suffix;
            }
            return prefix + reg + suffix;
        }
    }
}

