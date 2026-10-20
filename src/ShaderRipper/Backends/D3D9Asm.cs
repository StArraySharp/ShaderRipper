using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShaderRipper
{
    /// <summary>
    /// D3D9 (SM2/SM3) shader bytecode 反汇编器。
    ///
    /// d3d9 的程序不是 DXBC 容器，而是裸 token 流：版本 token（0xFFFE____=vs、
    /// 0xFFFF____=ps）开头，之后每条指令带长度字段（bit24-27，**不含指令 token
    /// 本身**），END=0x0000FFFF。d3dcompiler 的 D3DDisassemble 只认 DXBC，所以
    /// 这里自己解码 token（字段布局按 d3d9types.h）：
    ///
    ///   指令 token：opcode=bit0-15，opcode 特定控制=bit16-23（ifc/breakc/setp 的
    ///               比较码、texld 的 _pp/_bx），长度=bit24-27；
    ///   注释 token：bit0-15=0xFFFE，bit16-30=DWORD 数；
    ///   操作数 token：bit31=1，寄存器号=bit0-10，寄存器类型=bit28-30（低 3 位）
    ///               + bit11-12（高 2 位），src swizzle=bit16-23，src 修饰=bit24-27，
    ///               dest 写掩码=bit16-19，dest 修饰=bit20-23，相对寻址=bit13。
    ///
    /// 输出形如 fxc 的老式汇编：
    /// <code>
    ///     ps_3_0
    ///     dcl_2d s0
    ///     texld r0, t0, s0
    ///     mul oC0, r0, c0
    /// </code>
    ///
    /// 只做**反汇编**，不做 Cg 翻译 —— SM2/3 与 DXBC 的指令方言差距太大
    /// （texld/mova/绝对寄存器号 vs dcl_ 前缀 + r# 相对编号），直译进
    /// CgTranslator 得不偿失。
    /// </summary>
    internal static class D3D9Asm
    {
        private const uint EndToken = 0x0000FFFF;
        private const int D3DSIO_COMMENT = 0xFFFE;

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

        private static readonly string[] Cmp = { "", "gt", "eq", "ge", "lt", "ne", "le" };

        private static readonly string[] Usage =
        {
            "position", "blendweight", "blendindices", "normal", "psize", "texcoord",
            "tangent", "binormal", "tessfactor", "positiont", "color", "fog", "depth", "sample",
        };

        private static readonly string[] Swz = { "x", "y", "z", "w" };

        /// <summary>反汇编一段 d3d9 token 流；失败返回 null。</summary>
        public static string Disassemble(byte[] b, int off, int len)
        {
            if (b == null || off + 4 > b.Length) return null;
            int end = Math.Min(off + len, b.Length);
            uint ver = BitConverter.ToUInt32(b, off);
            uint hi = ver >> 16;
            if (hi != 0xFFFE && hi != 0xFFFF) return null;
            bool isVs = hi == 0xFFFE;
            uint major = (ver >> 8) & 0xFF, minor = ver & 0xFF;
            if (major < 1 || major > 3) return null;

            var sb = new StringBuilder();
            sb.Append("    ").Append(isVs ? "vs_" : "ps_").Append(major);
            if (minor == 0) sb.Append("_0");
            else if (minor == 0x0A) sb.Append("_a");
            else if (minor == 0x0B) sb.Append("_b");
            else if (minor == 0x0F) sb.Append(isVs ? "_sw" : "_x");
            else sb.Append('_').Append(minor.ToString("x"));
            sb.AppendLine();

            int p = off + 4;
            while (p + 4 <= end)
            {
                uint w = BitConverter.ToUInt32(b, p);
                if (w == EndToken) return sb.ToString();
                if ((w & 0xFFFF) == D3DSIO_COMMENT)
                {
                    int cnt = (int)((w >> 16) & 0x7FFF);
                    if (cnt == 0) return null;
                    p += 4 + cnt * 4;
                    continue;
                }
                if ((w & 0x80000000) != 0) return null;

                int opcode = (int)(w & 0xFFFF);
                int length = (int)((w >> 24) & 0xF);
                if (length <= 0) return null;
                int instEnd = p + (length + 1) * 4;
                if (instEnd > end) return null;

                sb.Append("    ");
                if (!Emit(b, p, instEnd, w, opcode, isVs, major, sb)) return null;

                p = instEnd;
            }
            return null;
        }

        private static bool Emit(byte[] b, int p, int instEnd, uint w, int opcode,
                                 bool isVs, uint major, StringBuilder sb)
        {
            Ops.TryGetValue(opcode, out var op);
            string name = op.name ?? "op_" + opcode;
            bool destFirst = op.destFirst;

            int ctrl = (int)((w >> 16) & 0xFF);
            if (opcode == 41) name = "if_" + Cmp[ClampC(ctrl)];
            else if (opcode == 45) name = "break_" + Cmp[ClampC(ctrl)];
            else if (opcode == 94) name = "setp_" + Cmp[ClampC(ctrl)];
            else if (opcode == 66) name = ctrl == 1 ? "texldp" : ctrl == 2 ? "texldb" : "texld";

            int operands = p + 4;

            switch (opcode)
            {
                case 81:
                {
                    var d = FmtOperand(b, ref operands, instEnd, true, isVs);
                    if (d == null) return false;
                    var v = new List<string> { d };
                    for (int k = 0; k < 4 && operands + 4 <= instEnd; k++)
                        v.Add(BitConverter.ToSingle(b, operands).ToString("0.######", CultureInfo.InvariantCulture));
                    sb.Append(name).Append(' ').Append(string.Join(", ", v)).AppendLine();
                    return true;
                }
                case 47:
                {
                    var d = FmtOperand(b, ref operands, instEnd, true, isVs);
                    if (d == null) return false;
                    string v = operands + 4 <= instEnd && BitConverter.ToUInt32(b, operands) != 0 ? "true" : "false";
                    sb.Append(name).Append(' ').Append(d).Append(", ").Append(v).AppendLine();
                    return true;
                }
                case 48:
                {
                    var d = FmtOperand(b, ref operands, instEnd, true, isVs);
                    if (d == null) return false;
                    var v = new List<string> { d };
                    for (int k = 0; k < 4 && operands + 4 <= instEnd; k++)
                        v.Add(BitConverter.ToInt32(b, operands).ToString(CultureInfo.InvariantCulture));
                    sb.Append(name).Append(' ').Append(string.Join(", ", v)).AppendLine();
                    return true;
                }
                case 31:
                {
                    if (operands + 8 > instEnd) return false;
                    uint param = BitConverter.ToUInt32(b, operands);
                    operands += 4;
                    string d = FmtOperand(b, ref operands, instEnd, true, isVs);
                    if (d == null) return false;
                    uint dt = BitConverter.ToUInt32(b, operands - 4);
                    uint dim = (param >> 27) & 0x7;

                    if (dim >= 2 && dim <= 4)
                    {
                        string r = dim == 4 ? "volume" : dim == 3 ? "cube" : "2d";
                        sb.Append("dcl_").Append(r).Append(' ').Append(d).AppendLine();
                        return true;
                    }
                    if (major >= 3)
                    {
                        uint usage = param & 0xF, index = (param >> 16) & 0xF;
                        string u = usage < (uint)Usage.Length ? Usage[usage] : "usage" + usage;
                        sb.Append("dcl_").Append(u);
                        if (index != 0) sb.Append(index);
                        sb.Append(' ').Append(d).AppendLine();
                        return true;
                    }
                    sb.Append("dcl ").Append(d).AppendLine();
                    return true;
                }
            }

            sb.Append(name);
            bool first = true;
            while (operands + 4 <= instEnd)
            {
                bool asDest = destFirst && first;
                string s = FmtOperand(b, ref operands, instEnd, asDest, isVs);
                if (s == null) return false;
                sb.Append(first ? " " : ", ").Append(s);
                first = false;
            }
            sb.AppendLine();
            return true;
        }

        private static int ClampC(int c) => c > 0 && c < Cmp.Length ? c : 0;

        /// <summary>寄存器类型 = bit28-30（低 3 位）+ bit11-12（高 2 位）。</summary>
        private static uint RegType(uint w) => ((w >> 28) & 0x7) | (((w >> 11) & 0x3) << 3);

        /// <summary>格式化一个操作数 token（bit31=1）；相对寻址（bit13）时再吃一个地址 token。</summary>
        private static string FmtOperand(byte[] b, ref int p, int end, bool isDest, bool isVs)
        {
            if (p + 4 > end) return null;
            uint w = BitConverter.ToUInt32(b, p);
            p += 4;
            if ((w & 0x80000000) == 0) return null;

            int num = (int)(w & 0x7FF);
            uint type = RegType(w);

            string reg;
            switch (type)
            {
                case 0: reg = "r" + num; break;
                case 1: reg = "v" + num; break;
                case 2: reg = "c" + num; break;
                case 3: reg = isVs ? "a" + num : "t" + num; break;
                case 4: reg = num == 0 ? "oPos" : num == 1 ? "oFog" : "oPts"; break;
                case 5: reg = "oD" + num; break;
                case 6: reg = isVs ? "o" + num : "oT" + num; break;
                case 7: reg = "i" + num; break;
                case 8: reg = "oC" + num; break;
                case 9: reg = "oDepth"; break;
                case 10: reg = "s" + num; break;
                case 11: reg = "c" + (num + 2048); break;
                case 12: reg = "c" + (num + 4096); break;
                case 13: reg = "c" + (num + 6144); break;
                case 14: reg = "b" + num; break;
                case 15: reg = "aL"; break;
                case 17: reg = num == 1 ? "vFace" : "vPos"; break;
                case 18: reg = "L" + num; break;
                case 19: reg = "p" + num; break;
                default: reg = "?t" + type + "_" + num; break;
            }

            string suffix = "";
            string prefix = "";

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
                uint dm = (w >> 20) & 0xF;
                if ((dm & 1) != 0) suffix += "_sat";
                if ((dm & 2) != 0) suffix += "_pp";
                if ((dm & 4) != 0) suffix += "_centroid";
            }
            else
            {
                uint sw = (w >> 16) & 0xFF;
                int x = (int)(sw & 3), y = (int)((sw >> 2) & 3), z = (int)((sw >> 4) & 3), ww = (int)((sw >> 6) & 3);
                if (!(x == 0 && y == 1 && z == 2 && ww == 3))
                    suffix += "." + Swz[x] + Swz[y] + Swz[z] + Swz[ww];

                uint sm = (w >> 24) & 0xF;
                switch (sm)
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

            string s = prefix + reg + suffix;

            if (!isDest && (w & 0x2000) != 0)
            {
                string addr = FmtOperand(b, ref p, end, false, isVs);
                if (addr == null) return null;
                s = prefix + reg + "[" + addr + "]" + suffix;
            }
            return s;
        }

        /// <summary>--selftest-d3d9：对预编译的 SM2/SM3 fixture 跑反汇编并做基本校验。
        /// 返回 0 = 全部通过。</summary>
        public static int Selftest()
        {
            int failures = 0;
            foreach (var (target, code) in D3D9Fixtures.Cases)
            {
                string asm = Disassemble(code, 0, code.Length);
                if (asm == null)
                {
                    Console.WriteLine($"[FAIL] {target}: disassembly returned null ({code.Length} bytes)");
                    var hex = new StringBuilder();
                    for (int k = 0; k + 4 <= code.Length; k += 4)
                        hex.Append(BitConverter.ToUInt32(code, k).ToString("x8")).Append(k % 16 == 12 ? "\n" : " ");
                    Console.WriteLine(hex.ToString());
                    failures++;
                    continue;
                }

                var problems = new List<string>();
                if (!asm.Contains(target)) problems.Add("missing version header");
                if (asm.Contains("op_")) problems.Add("unknown opcode");
                if (asm.Contains("?t")) problems.Add("unknown register type");
                int insts = 0;
                foreach (var ln in asm.Split('\n'))
                    if (ln.Trim().Length > 0 && !ln.Contains(target) && !ln.TrimStart().StartsWith("dcl"))
                        insts++;
                if (insts == 0) problems.Add("no instructions");

                Console.WriteLine($"[{(problems.Count == 0 ? "OK" : "FAIL")}] {target}  ({code.Length} B, {insts} inst)");
                Console.WriteLine(asm);
                foreach (var pr in problems) { Console.WriteLine("   !! " + pr); failures++; }
            }
            Console.WriteLine(failures == 0 ? "selftest: all passed" : $"selftest: {failures} problem(s)");
            return failures == 0 ? 0 : 1;
        }
    }
}
