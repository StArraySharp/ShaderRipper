using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShaderRipper.Cg
{
    /// <summary>Parses fxc/D3DDisassemble output into a declared program + instructions.</summary>
    internal sealed class AsmParser
    {
        public sealed class Decl
        {
            public string Kind;
            public string Raw;
            public int Register;
            public int Count;
            public string Name;
            public string InputName;
            public int InputIndex;
            public string InputMask;
            public string OutputSemantic;
            public int TexDim;
            public int Stride;
            public int Elements;
            public int ElemSize;
            public List<float[]> IcbValues;
        }

        public sealed class Program
        {
            public string Stage = "";
            public string Profile = "";
            public List<Decl> Decls = new List<Decl>();
            public List<Instruction> Code = new List<Instruction>();
            public string[] Signatures;
            public List<SigEntry> InputSig;
            public List<SigEntry> OutputSig;

            /// <summary>
            /// The parse tree this program was read from, when the grammar route
            /// read it (null under <c>--asm-parser regex</c>).
            ///
            /// It is kept so translation can be driven from the tree itself
            /// rather than from <see cref="Code"/>: <c>--asm-translate antlr</c>
            /// walks the tree and emits per instruction node, while the default
            /// route loops over the list.  Both produce the same text; the model
            /// stays the carrier because declarations, signature tables and the
            /// constant-buffer analysis are whole-program facts and are read from
            /// it either way.
            /// </summary>
            public ShaderRipper.Antlr.DxbcAsmParser.ProgramContext Tree;
        }

        private static readonly System.Text.RegularExpressions.Regex SigRow = new(
            @"^([A-Za-z_][A-Za-z0-9_]*)\s+(\d+)\s+([xyzw]{1,4})\s+(\d+)\s+(\S+)\s+(\S+)\s*([xyzw\s]*)$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        public static Program Parse(string asmText)
        {
            var p = new Program();
            var lines = asmText.Split('\n');
            int icbDepth = 0;
            int sigTarget = 0;
            var icbRows = new List<float[]>();

            foreach (var raw0 in lines)
            {
                string line = raw0.TrimEnd('\r');
                string t = line.Trim();
                if (t.Length == 0 || IsBlank(t)) continue;

                if (icbDepth > 0 || t.StartsWith("dcl_immediateConstantBuffer"))
                {
                    string src = t.StartsWith("dcl_immediateConstantBuffer")
                        ? t.Substring("dcl_immediateConstantBuffer".Length)
                        : t;
                    foreach (System.Text.RegularExpressions.Match m in
                             System.Text.RegularExpressions.Regex.Matches(src, @"\{([^{}]*)\}"))
                    {
                        var parts = m.Groups[1].Value.Split(',');
                        var v = new float[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            float.TryParse(parts[i].Trim(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out v[i]);
                        icbRows.Add(v);
                    }
                    foreach (char c in src)
                    {
                        if (c == '{') icbDepth++;
                        else if (c == '}') icbDepth--;
                    }
                    if (icbDepth <= 0)
                    {
                        icbDepth = 0;
                        p.Decls.Add(new Decl { Kind = "icb", Raw = "dcl_immediateConstantBuffer",
                                               IcbValues = icbRows, Elements = icbRows.Count });
                        icbRows = new List<float[]>();
                    }
                    continue;
                }

                if (t.StartsWith("{") || t.StartsWith("}")) continue;

                if (t.StartsWith("//") || t.StartsWith(";"))
                {
                    p.Signatures ??= Array.Empty<string>();
                    CaptureSignature(p, t, ref sigTarget);
                    continue;
                }
                if (t.StartsWith("vs_") || t.StartsWith("ps_") || t.StartsWith("gs_") ||
                    t.StartsWith("hs_") || t.StartsWith("ds_") || t.StartsWith("cs_"))
                {
                    p.Stage = t;
                    p.Profile = t.Substring(0, 2);
                    continue;
                }
                if (t.StartsWith("dcl_"))
                {
                    p.Decls.Add(ParseDecl(t));
                    continue;
                }
                var ins = ParseInstruction(t);
                if (ins != null) p.Code.Add(ins);
            }
            return p;
        }

        /// <summary>
        /// Collect the "Input signature:" / "Output signature:" tables that
        /// D3DDisassemble emits as comments.  They carry the exact semantic name,
        /// index, declaration mask and register for every shader stage input and
        /// output, which is what the appdata / v2f structs are built from.
        ///
        /// sigTarget is the caller's parse-local section state (0 = none,
        /// 1 = input, 2 = output).  It used to be a static field, which made
        /// concurrent Parse calls corrupt each other's section tracking.
        /// </summary>
        private static void CaptureSignature(Program p, string rawLine, ref int sigTarget)
        {
            string s = rawLine.Trim();
            while (s.StartsWith("//") || s.StartsWith(";"))
                s = s.Substring(s.StartsWith("//") ? 2 : 1).TrimStart();

            if (s.StartsWith("Input signature", StringComparison.OrdinalIgnoreCase))
            { sigTarget = 1; p.InputSig ??= new List<SigEntry>(); return; }
            if (s.StartsWith("Output signature", StringComparison.OrdinalIgnoreCase))
            { sigTarget = 2; p.OutputSig ??= new List<SigEntry>(); return; }
            if (sigTarget == 0) return;
            if (s.Length == 0 || s.StartsWith("Name") || s.StartsWith("-"))
                return;

            var m = SigRow.Match(s);
            if (!m.Success) { sigTarget = 0; return; }

            var e = new SigEntry
            {
                Name = m.Groups[1].Value,
                Index = int.Parse(m.Groups[2].Value),
                Mask = m.Groups[3].Value,
                Register = int.Parse(m.Groups[4].Value),
                SysValue = m.Groups[5].Value,
                Format = m.Groups[6].Value,
                Used = m.Groups[7].Value,
            };
            if (sigTarget == 1) p.InputSig.Add(e); else p.OutputSig.Add(e);
        }

        /// <summary>True when the trimmed line has no printable instruction characters.</summary>
        private static bool IsBlank(string s)
        {
            foreach (char c in s)
                if (c > 32 && c != 0x7F) return false;
            return true;
        }

        private static Decl ParseDecl(string t)
        {
            var d = new Decl { Raw = t };
            string body = t;

            if (body.StartsWith("dcl_constantbuffer"))
            {
                d.Kind = "constbuffer";
                int cb = body.IndexOf("CB", StringComparison.OrdinalIgnoreCase);
                int br = body.IndexOf('[', cb);
                int e = body.IndexOf(']', br);
                d.Register = int.TryParse(body.Substring(cb + 2, br - (cb + 2)).Trim(), out int cbr) ? cbr : 0;
                d.Count = e > br && int.TryParse(body.Substring(br + 1, e - br - 1).Trim(), out int cbc) ? cbc : 0;
                return d;
            }
            if (body.StartsWith("dcl_sampler"))
            {
                d.Kind = "sampler";
                d.Register = ExtractRegIndex(body, "s");
                return d;
            }
            if (body.StartsWith("dcl_resource_structured"))
            {
                d.Kind = "structured";
                d.Register = ExtractRegIndex(body, "t");
                int comma = body.IndexOf(',');
                d.Stride = comma > 0 && int.TryParse(body.Substring(comma + 1).Trim(), out int st) ? st : 16;
                return d;
            }
            if (body.StartsWith("dcl_resource_texture"))
            {
                int ti = body.IndexOf("texture", StringComparison.Ordinal);
                int sp = body.IndexOf(' ', ti);
                string dim = body.Substring(ti + 7, sp - (ti + 7));
                d.TexDim = dim switch
                {
                    "1d" => 1, "2d" => 2, "3d" => 3, "cube" => 4,
                    "2darray" => 5, "cubearray" => 6, _ => 2
                };
                d.Kind = "texture" + d.TexDim;
                d.Register = ExtractRegIndex(body, "t");
                return d;
            }
            if (body.StartsWith("dcl_input_ps") || body.StartsWith("dcl_input_ps_siv") || body.StartsWith("dcl_input_ps_sgv"))
            {
                d.Kind = "input";
                int vi = body.IndexOf('v');
                d.Register = ExtractRegIndex(body, "v");
                int dot = body.IndexOf('.', vi);
                d.InputMask = dot >= 0 ? body.Substring(dot + 1).Trim() : "";
                return d;
            }
            if (body.StartsWith("dcl_input") || body.StartsWith("dcl_input_siv") || body.StartsWith("dcl_input_sgv"))
            {
                d.Kind = "vinput";
                d.Register = ExtractRegIndex(body, "v");
                return d;
            }
            if (body.StartsWith("dcl_output_siv") || body.StartsWith("dcl_output_sgv"))
            {
                d.Kind = "output";
                d.Register = ExtractRegIndex(body, "o");
                int semi = body.LastIndexOf(' ');
                d.OutputSemantic = semi > 0 ? body.Substring(semi + 1).Trim() : "";
                return d;
            }
            if (body.StartsWith("dcl_output"))
            {
                d.Kind = "output";
                d.Register = ExtractRegIndex(body, "o");
                int dot = body.IndexOf('.');
                if (dot >= 0) d.InputMask = body.Substring(dot + 1).Trim();
                return d;
            }
            if (body.StartsWith("dcl_indexableTemp"))
            {
                d.Kind = "xtemp";
                d.Register = ExtractRegIndex(body, "x");
                int br = body.IndexOf('[');
                int e = body.IndexOf(']', br);
                d.Elements = br >= 0 && e > br && int.TryParse(body.Substring(br + 1, e - br - 1).Trim(), out int ec)
                    ? ec : 0;
                int cm = body.IndexOf(',', e);
                d.ElemSize = cm > 0 && int.TryParse(body.Substring(cm + 1).Trim(), out int es) ? es : 4;
                return d;
            }
            if (body.StartsWith("dcl_temps"))
            {
                d.Kind = "temps";
                d.Count = int.TryParse(body.Substring("dcl_temps".Length).Trim(), out int tc) ? tc : 0;
                return d;
            }
            d.Kind = "other";
            return d;
        }

        private static int ExtractRegIndex(string s, string prefix)
        {
            int i = s.IndexOf(prefix, StringComparison.Ordinal);
            while (i >= 0)
            {
                if (char.IsDigit(s[i + prefix.Length]))
                {
                    int j = i + prefix.Length;
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                    return int.Parse(s.Substring(i + prefix.Length, j - (i + prefix.Length)));
                }
                i = s.IndexOf(prefix, i + 1, StringComparison.Ordinal);
            }
            return -1;
        }

        private static readonly System.Text.RegularExpressions.Regex OpcodeRx = new(
            @"^([A-Za-z_][A-Za-z0-9_]*(?:\s*\([^()]*(?:\([^()]*\)[^()]*)*\))*)(?:\s+(.*))?$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        private static Instruction ParseInstruction(string t)
        {
            var ins = new Instruction { Raw = t };
            var m = OpcodeRx.Match(t);
            if (!m.Success)
            {
                ins.Opcode = t;
                return ins;
            }
            string full = m.Groups[1].Value.Trim();
            string rest = m.Groups[2].Success ? m.Groups[2].Value.Trim() : "";
            int paren = full.IndexOf('(');
            ins.OpcodeFull = full;
            ins.Opcode = paren > 0 ? full.Substring(0, paren) : full;
            if (ins.Opcode.Length == 0) return null;

            foreach (var part in SplitOperands(rest))
            {
                if (part.Length == 0) continue;
                ins.Operands.Add(ParseOperand(part));
            }
            return ins;
        }

        private static IEnumerable<string> SplitOperands(string s)
        {
            var cur = new StringBuilder();
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '(' || c == '[') depth++;
                if (c == ')' || c == ']') depth--;
                if (c == ',' && depth == 0)
                {
                    yield return cur.ToString().Trim();
                    cur.Clear();
                    continue;
                }
                cur.Append(c);
            }
            if (cur.Length > 0) yield return cur.ToString().Trim();
        }

        /// <summary>
        /// One lane of an <c>l(...)</c> immediate.
        ///
        /// fxc prints immediates as either decimal text (<c>1.000000</c>) or, for
        /// values whose decimal form would be long or lossy, as the raw 32-bit
        /// pattern (<c>0x3f800000</c>).  Both must decode; a bare
        /// <see cref="float.TryParse(string, out float)"/> silently turns every
        /// hex lane into 0.0, which zeroes the mask of every <c>and</c> the
        /// compiler emits for bool-to-float conversion.
        /// </summary>
        private static float ParseImmediate(string text)
        {
            string s = text.Trim();
            bool negate = false;
            if (s.StartsWith("-"))
            {
                negate = true;
                s = s.Substring(1).Trim();
            }
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint bits))
                {
                    float f = BitConverter.Int32BitsToSingle(unchecked((int)bits));
                    return negate ? -f : f;
                }
            }
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return negate ? -v : v;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                return negate ? -iv : iv;
            return 0f;
        }

        private static Operand ParseOperand(string s)
        {
            var o = new Operand { Raw = s };
            string v = s.Trim();

            if (v == "null") { o.Kind = "null"; return o; }

            if (v.StartsWith("-")) { o.Negate = true; v = v.Substring(1); }
            if (v.StartsWith("|") && v.EndsWith("|") && v.Length > 1) { o.Abs = true; v = v.Substring(1, v.Length - 2); }
            if (v.StartsWith("|")) { o.Negate = true; v = v.Substring(1).TrimEnd('|'); }
            if (v.StartsWith("+")) v = v.Substring(1);
            if (v.StartsWith("-")) { o.Negate = !o.Negate; v = v.Substring(1); }

            if (v.StartsWith("l(") || v.StartsWith("l ("))
            {
                o.Kind = "l";
                int a = v.IndexOf('(');
                int b = v.LastIndexOf(')');
                var parts = v.Substring(a + 1, b - a - 1).Split(',');
                o.Imm = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    o.Imm[i] = ParseImmediate(parts[i]);
                return o;
            }

            int close = v.LastIndexOf(']');
            int dot = close >= 0 ? v.IndexOf('.', close + 1) : v.IndexOf('.');
            string reg = dot >= 0 ? v.Substring(0, dot) : v;
            string swz = dot >= 0 ? v.Substring(dot + 1) : "";
            o.Swizzle = swz;

            int br = reg.IndexOf('[');
            if (br >= 0)
            {
                string basePart = reg.Substring(0, br);
                string idxPart = reg.Substring(br + 1).TrimEnd(']');
                o.Kind = basePart.Substring(0, 1).ToLowerInvariant() switch
                {
                    "c" => "cb", "x" => "x", "t" => "t", "v" => "v", "o" => "o", "r" => "r", "i" => "icb",
                    _ => "r"
                };
                int j = 0;
                while (j < basePart.Length && !char.IsDigit(basePart[j])) j++;
                o.Index = int.TryParse(basePart.Substring(Math.Min(j, basePart.Length)), out int bp) ? bp : 0;
                if (int.TryParse(idxPart, out int ai)) o.ArrayIndex = ai;
                else o.ArrayExpr = idxPart;
                return o;
            }

            if (reg.Length == 0)
            {
                if (Environment.GetEnvironmentVariable("SHADERRIP_TRACE") == "1")
                    Console.Error.WriteLine($"  [operand] empty register in: {s}");
                o.Kind = "r";
                o.Index = 0;
                return o;
            }
            char c0 = char.ToLowerInvariant(reg[0]);
            int k = 0;
            while (k < reg.Length && !char.IsDigit(reg[k])) k++;
            string idxStr = reg.Substring(k);
            o.Kind = c0 switch
            {
                'r' => "r", 'v' => "v", 'o' => "o", 't' => "t", 's' => "s", 'x' => "x", 'i' => "icb",
                _ => "r"
            };
            o.Index = idxStr.Length > 0 && int.TryParse(idxStr, out int idx) ? idx : 0;
            return o;
        }
    }
}
