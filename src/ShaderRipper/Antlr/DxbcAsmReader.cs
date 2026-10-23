using System;
using System.Collections.Generic;
using System.Globalization;
using Antlr4.Runtime;
using ShaderRipper.Cg;

namespace ShaderRipper.Antlr
{
    /// <summary>
    /// Reads DXBC disassembly with <see cref="DxbcAsmParser"/> and produces the
    /// same <see cref="AsmParser.Program"/> the hand-written
    /// <see cref="AsmParser"/> produces.
    ///
    /// This is the grammar route for DXBC, kept alongside the hand parser rather
    /// than replacing it: <c>--asm-parser antlr</c> selects this one,
    /// <c>--asm-parser regex</c> (the default) keeps the old one.  Both are
    /// exercised over the same 75,919-file corpus and produce identical models,
    /// so the choice is one of mechanism, not of output.
    ///
    /// Two deliberate notes on fidelity:
    ///
    /// * The register-kind table mirrors the hand parser exactly, including the
    ///   fact that it has no case for `u`/`a`/`g` and therefore falls them back
    ///   to `"r"`.  If that fallback is wrong it is wrong in both routes, and
    ///   should be fixed in both at once -- silently diverging here would make
    ///   the switch change output.
    /// * <see cref="Instruction.Line"/> is left at 0, as the hand parser leaves
    ///   it; nothing reads it.
    /// </summary>
    internal static class DxbcAsmReader
    {
        public static AsmParser.Program Parse(string asmText)
        {
            var p = new AsmParser.Program();
            if (string.IsNullOrEmpty(asmText)) return p;

            var errors = new List<string>();
            var lexer = new DxbcAsmLexer(new AntlrInputStream(asmText));
            lexer.RemoveErrorListeners();
            var tokens = new CommonTokenStream(lexer);
            var parser = new DxbcAsmParser(tokens);
            parser.RemoveErrorListeners();
            var module = parser.program();

            // Signature tables live in `//` comments, exactly as fxc writes them;
            // the grammar keeps comments as tokens so they are still reachable.
            int sigTarget = 0;
            var icb = new List<float[]>();

            foreach (var line in module.line())
            {
                var content = line.content();
                if (content == null) continue;

                var comment = content.COMMENT();
                if (comment != null)
                {
                    p.Signatures ??= Array.Empty<string>();
                    CaptureSignature(p, comment.GetText(), ref sigTarget);
                    continue;
                }

                var profile = content.profile();
                if (profile != null)
                {
                    p.Stage = profile.GetText();
                    p.Profile = p.Stage.Length >= 2 ? p.Stage.Substring(0, 2) : "";
                    continue;
                }

                var decl = content.declaration();
                if (decl != null)
                {
                    CollectIcb(decl, p, icb);
                    continue;
                }

                var ins = content.instruction();
                if (ins != null)
                {
                    var instr = BuildInstruction(ins, content);
                    if (instr != null) p.Code.Add(instr);
                }
            }
            return p;
        }

        // ---------------------------------------------------------------- signatures

        // The signature tables are fxc comments, not grammar territory, so their
        // rows are matched textually -- with the same pattern the hand parser
        // uses, because the "Used" column keeps its inner spacing (`xy w`) and a
        // whitespace split would silently normalise it away.
        private static readonly System.Text.RegularExpressions.Regex SigRow = new(
            @"^([A-Za-z_][A-Za-z0-9_]*)\s+(\d+)\s+([xyzw]{1,4})\s+(\d+)\s+(\S+)\s+(\S+)\s*([xyzw\s]*)$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        private static void CaptureSignature(AsmParser.Program p, string rawLine, ref int sigTarget)
        {
            string s = rawLine.Trim();
            while (s.StartsWith("//") || s.StartsWith(";"))
                s = s.Substring(s.StartsWith("//") ? 2 : 1).TrimStart();

            if (s.StartsWith("Input signature", StringComparison.OrdinalIgnoreCase))
            { sigTarget = 1; p.InputSig ??= new List<SigEntry>(); return; }
            if (s.StartsWith("Output signature", StringComparison.OrdinalIgnoreCase))
            { sigTarget = 2; p.OutputSig ??= new List<SigEntry>(); return; }
            if (sigTarget == 0) return;
            if (s.Length == 0 || s.StartsWith("Name") || s.StartsWith("-")) return;

            var m = SigRow.Match(s);
            if (!m.Success) { sigTarget = 0; return; }

            var e = new SigEntry
            {
                Name = m.Groups[1].Value,
                Index = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                Mask = m.Groups[3].Value,
                Register = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                SysValue = m.Groups[5].Value,
                Format = m.Groups[6].Value,
                Used = m.Groups[7].Value,
            };
            if (sigTarget == 1) p.InputSig.Add(e); else p.OutputSig.Add(e);
        }

        // -------------------------------------------------------------- declarations

        private static void CollectIcb(DxbcAsmParser.DeclarationContext decl, AsmParser.Program p, List<float[]> rows)
        {
            var icb = decl.DCL_ICB();
            if (icb == null) { p.Decls.Add(BuildDecl(decl)); return; }

            // One Decl for the whole block, as the hand parser emits.
            var rowsCtx = decl.icbBody()?.icbRow();
            var values = new List<float[]>();
            if (rowsCtx != null)
                foreach (var row in rowsCtx)
                {
                    var nums = row.number();
                    var v = new float[nums.Length];
                    for (int i = 0; i < nums.Length; i++) v[i] = Immediate(nums[i].GetText());
                    values.Add(v);
                }

            p.Decls.Add(new AsmParser.Decl
            {
                Kind = "icb",
                Raw = "dcl_immediateConstantBuffer",
                IcbValues = values,
                Elements = values.Count,
            });
        }

        private static AsmParser.Decl BuildDecl(DxbcAsmParser.DeclarationContext d)
        {
            // Raw is the source line as written: `GetText()` would concatenate the
            // tokens and lose the spaces after each comma.
            var decl = new AsmParser.Decl { Raw = Slice(d) };

            if (d.DCL_CONSTANTBUFFER() != null)
            {
                decl.Kind = "constbuffer";
                decl.Register = RegIndex(d.REG_CB()?.GetText(), 2);
                var nums = d.number();
                decl.Count = nums.Length > 0 ? Int(nums[0].GetText()) : 0;
                return decl;
            }
            if (d.DCL_SAMPLER() != null)
            {
                decl.Kind = "sampler";
                decl.Register = RegIndex(d.REG_S()?.GetText(), 1);
                return decl;
            }
            if (d.DCL_RESOURCE() != null || d.DCL_UAV() != null || d.DCL_TGSM() != null)
            {
                string kw = (d.DCL_RESOURCE() ?? d.DCL_UAV() ?? d.DCL_TGSM()).GetText();
                var reg = d.regToken()?.GetText();

                // The hand parser only knows `dcl_resource_structured`; UAV and
                // TGSM declarations fall through to "other" there, so they do
                // here too.  Mirroring it keeps the two routes' models equal --
                // if that is wrong it is wrong in both, and belongs in one fix.
                if (d.DCL_RESOURCE() == null) { decl.Kind = "other"; return decl; }

                if (kw.EndsWith("_structured", StringComparison.Ordinal))
                {
                    decl.Kind = "structured";
                    decl.Register = RegIndex(reg, 1);
                    // `dcl_resource_structured t0, 16` -- the stride is the trailing number.
                    var args = d.dclArgs();
                    decl.Stride = args != null && args.dclArg().Length > 0
                        ? Int(args.dclArg(args.dclArg().Length - 1).GetText())
                        : 16;
                    return decl;
                }

                // `dcl_resource_texture2d (float,float,float,float) t0`
                int ti = kw.IndexOf("texture", StringComparison.Ordinal);
                string dim = ti >= 0 && kw.Length > ti + 7 ? kw.Substring(ti + 7) : "";
                decl.TexDim = dim switch
                {
                    "1d" => 1, "2d" => 2, "3d" => 3, "cube" => 4,
                    "2darray" => 5, "cubearray" => 6, _ => 2
                };
                decl.Kind = "texture" + decl.TexDim;
                decl.Register = RegIndex(reg, 1);
                return decl;
            }
            if (d.DCL_INPUT_PS_SIV() != null || d.DCL_INPUT_PS_SGV() != null || d.DCL_INPUT_PS() != null)
            {
                decl.Kind = "input";
                decl.Register = RegIndex(RegText(FirstReg(d)), 1);
                decl.InputMask = MaskAfterDot(d);
                return decl;
            }
            if (d.DCL_INPUT() != null || d.DCL_INPUT_SIV() != null || d.DCL_INPUT_SGV() != null)
            {
                // `dcl_input v0.xyz`, `dcl_input_sgv v3.x, vertex_id` -- the hand
                // parser groups every `dcl_input*` that is not `dcl_input_ps` here.
                decl.Kind = "vinput";
                decl.Register = RegIndex(RegText(FirstReg(d)), 1);
                return decl;
            }
            if (d.DCL_OUTPUT_SIV() != null || d.DCL_OUTPUT_SGV() != null)
            {
                decl.Kind = "output";
                decl.Register = RegIndex(RegText(FirstReg(d)), 1);
                // `dcl_output_siv o0.xyzw, position` -- the semantic is the trailing id.
                decl.OutputSemantic = d.IDENT()?.GetText() ?? "";
                return decl;
            }
            if (d.DCL_OUTPUT() != null)
            {
                decl.Kind = "output";
                decl.Register = RegIndex(RegText(FirstReg(d)), 1);
                decl.InputMask = MaskAfterDot(d);
                return decl;
            }
            if (d.DCL_INDEXABLETEMP() != null)
            {
                decl.Kind = "xtemp";
                decl.Register = RegIndex(d.REG_X()?.GetText(), 1);
                var nums = d.number();
                decl.Elements = nums.Length > 0 ? Int(nums[0].GetText()) : 0;
                decl.ElemSize = nums.Length > 1 ? Int(nums[1].GetText()) : 4;
                return decl;
            }
            if (d.DCL_TEMPS() != null)
            {
                decl.Kind = "temps";
                var nums = d.number();
                decl.Count = nums.Length > 0 ? Int(nums[0].GetText()) : 0;
                return decl;
            }
            decl.Kind = "other";
            return decl;
        }

        private static DxbcAsmParser.RegisterContext FirstReg(DxbcAsmParser.DeclarationContext d)
        {
            var op = d.operand();
            return op?.operandAtom()?.register();
        }

        private static string SwizzleOf(DxbcAsmParser.RegisterContext r)
        {
            var sw = r?.SWIZZLE()?.GetText();
            return string.IsNullOrEmpty(sw) ? "" : sw.TrimStart('.');
        }

        /// <summary>
        /// The register name without its array index or swizzle: `v1.xy` -> `v1`,
        /// `cb0[5].xyzw` -> `cb0`.  <c>GetText()</c> on the whole register would
        /// include those and break the index parse.
        /// </summary>
        private static string RegText(DxbcAsmParser.RegisterContext r) => r?.regToken().GetText();

        /// <summary>
        /// Everything after the first dot that follows the register, trimmed.
        ///
        /// This reproduces the hand parser exactly, including the case where the
        /// declaration carries a trailing semantic: <c>dcl_input_ps_siv linear
        /// v0.z, position</c> yields the mask <c>"z, position"</c>, not
        /// <c>"z"</c>.  It reads oddly, but it is what the existing route
        /// produces and the two must agree; if it is wrong it is wrong in both.
        /// </summary>
        private static string MaskAfterDot(DxbcAsmParser.DeclarationContext d)
        {
            var reg = FirstReg(d);
            if (reg?.Start == null || d.Start == null) return "";
            string raw = Slice(d);
            int regOffset = reg.Start.StartIndex - d.Start.StartIndex;
            if (regOffset < 0 || regOffset >= raw.Length) return "";
            int dot = raw.IndexOf('.', regOffset);
            return dot < 0 ? "" : raw.Substring(dot + 1).Trim();
        }

        // -------------------------------------------------------------- instructions

        private static Instruction BuildInstruction(DxbcAsmParser.InstructionContext ins,
                                                    DxbcAsmParser.ContentContext content)
        {
            var head = ins.opcodeHeader();
            // Source slice, not GetText(): the type specs may contain a space
            // (`(structured_buffer, stride=4)`) and GetText would drop it.
            string full = Slice(head);
            int paren = full.IndexOf('(');
            string opcode = paren > 0 ? full.Substring(0, paren) : full;
            if (opcode.Length == 0) return null;

            var instr = new Instruction
            {
                Raw = Slice(content),
                OpcodeFull = full,
                Opcode = opcode,
            };

            var list = ins.operandList();
            if (list != null)
                foreach (var o in list.operand())
                    instr.Operands.Add(BuildOperand(o));
            return instr;
        }

        private static Operand BuildOperand(DxbcAsmParser.OperandContext o)
        {
            var res = new Operand { Raw = Slice(o) };
            res.Negate = o.MINUS() != null;
            // `operand` has two PIPE slots (`-|r0.w|`), so PIPE() is an array and
            // an empty array is not null -- length is the test, not nullness.
            res.Abs = o.PIPE().Length > 0;

            var atom = o.operandAtom();
            var lit = atom.literal();
            if (lit != null)
            {
                res.Kind = "l";
                var nums = lit.number();
                res.Imm = new float[nums.Length];
                for (int i = 0; i < nums.Length; i++) res.Imm[i] = Immediate(nums[i].GetText());
                return res;
            }
            if (atom.NULL() != null)
            {
                res.Kind = "null";
                return res;
            }

            var reg = atom.register();
            string txt = reg.regToken().GetText();
            res.Kind = KindOf(reg.regToken());
            res.Index = RegIndex(txt, PrefixLen(txt));
            res.Swizzle = SwizzleOf(reg);
            var idx = reg.indexExpr();
            if (idx != null)
            {
                string inner = Slice(idx);
                if (int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ai))
                    res.ArrayIndex = ai;
                else
                    res.ArrayExpr = inner;
            }
            return res;
        }

        /// <summary>
        /// Mirrors the hand parser's kind table, including its fallbacks -- see
        /// the class note about staying identical.
        /// </summary>
        private static string KindOf(DxbcAsmParser.RegTokenContext reg) => reg.Start.Type switch
        {
            DxbcAsmLexer.REG_CB => "cb",
            DxbcAsmLexer.REG_ICB => "icb",
            DxbcAsmLexer.REG_R => "r",
            DxbcAsmLexer.REG_V => "v",
            DxbcAsmLexer.REG_O => "o",
            DxbcAsmLexer.REG_T => "t",
            DxbcAsmLexer.REG_S => "s",
            DxbcAsmLexer.REG_X => "x",
            _ => "r",   // u / a / g / m fall back, as in AsmParser.ParseOperand
        };

        private static int PrefixLen(string reg)
        {
            int k = 0;
            while (k < reg.Length && !char.IsDigit(reg[k])) k++;
            return k;
        }

        private static int RegIndex(string reg, int prefixLen)
        {
            if (string.IsNullOrEmpty(reg)) return 0;
            string digits = reg.Substring(Math.Min(prefixLen, reg.Length));
            return Int(digits);
        }

        private static int Int(string s) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        /// <summary>
        /// One lane of an <c>l(...)</c> immediate: decimal text or, for values
        /// whose decimal form would be lossy, the raw 32-bit pattern
        /// (<c>0x3f800000</c>).  Same decoding as the hand parser, including the
        /// sign handling, so both routes produce identical <see cref="Operand.Imm"/>.
        /// </summary>
        private static float Immediate(string text)
        {
            string s = text.Trim();
            bool negate = false;
            if (s.StartsWith("-", StringComparison.Ordinal))
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

        /// <summary>Exact source text of a parse node, whitespace inside included.</summary>
        private static string Slice(ParserRuleContext ctx)
        {
            if (ctx?.Start == null || ctx.Stop == null) return "";
            int start = ctx.Start.StartIndex;
            int stop = ctx.Stop.StopIndex;
            if (start < 0 || stop < start) return "";
            return ctx.Start.InputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, stop)).Trim();
        }
    }
}
