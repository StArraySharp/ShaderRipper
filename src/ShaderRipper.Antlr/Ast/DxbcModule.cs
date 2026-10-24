using System;
using System.Collections.Generic;
using System.Globalization;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree;

namespace ShaderRipper.Antlr.Ast
{
    internal enum RegKind
    {
        R, V, O, T, S, Cb, Icb, U, X, A, G, M, Null, Literal,
    }

    /// <summary>
    /// One instruction operand, read off the tree: kind, register number, an
    /// optional constant-buffer row, the swizzle, and the sign/abs modifiers.
    /// Nothing here re-scans text -- the grammar already split the operand into
    /// these parts, and <see cref="Raw"/> is only kept for diagnostics.
    /// </summary>
    internal sealed class Operand
    {
        public RegKind Kind;
        public int Index;
        public int Row = -1;
        public string Swizzle;
        public bool Negate;
        public bool Abs;
        public float[] Lit;
        public string Raw;
    }

    internal sealed class Instruction
    {
        public string Opcode;       // "add", "sample_indexable(...)(...)" reduced to its base
        public string OpcodeFull;   // as written, including type groups and _sat
        public bool Saturate;
        public bool Precise;
        public List<Operand> Args = new();
        public string Raw;
    }

    internal sealed class Decl
    {
        public string Kind;         // temps / input / output / cbuffer / sampler / texture / other
        public int Register;
        public int Count;
        public int Dimension;
        public string Raw;
    }

    /// <summary>
    /// A whole disassembly, as the grammar sees it.
    ///
    /// This is the project's only program representation: there is no
    /// hand-written text scanner anywhere in it.  <see cref="Read"/> parses with
    /// SLL prediction first (the disassembly is machine-written; a mis-guess
    /// raises and the parse is retried with full LL), then walks the tree once to
    /// fill the typed nodes below.
    /// </summary>
    internal sealed class Module
    {
        public string Profile = "";
        public string Stage = "";       // "ps" / "vs" / "gs" / ...
        public int Temps;
        public int ParseErrors;
        public string Source = "";
        public List<Decl> Decls = new();
        public List<Instruction> Code = new();

        /// <summary>
        /// Names recovered from Unity's parameter blob, keyed by (cbuffer slot,
        /// row).  Populated by the game reader before emission: the disassembly
        /// carries no RDEF data (Unity strips it), so a numeric `cb1[7]` can only
        /// be named from the asset's own constant-buffer reflection.
        /// </summary>
        public Dictionary<(int cb, int row), string> CbNames = new();

        public static readonly Module Empty = new();

        public static Module Read(string asm)
        {
            var m = new Module { Source = asm ?? "" };
            if (string.IsNullOrWhiteSpace(asm)) return m;

            var root = Parse(m);
            if (root == null) return m;

            foreach (var line in root.line())
            {
                var content = line.content();
                if (content == null) continue;

                var profile = content.profile();
                if (profile != null)
                {
                    m.Profile = profile.GetText();
                    m.Stage = m.Profile.Length >= 2 ? m.Profile[..2] : "";
                    continue;
                }

                var decl = content.declaration();
                if (decl != null) { m.ReadDecl(decl); continue; }

                var ins = content.instruction();
                if (ins != null) m.ReadInstruction(ins);
            }
            return m;
        }

        // ---------------------------------------------------------------- parse

        private static DxbcAsmParser.ProgramContext Parse(Module m)
        {
            var tokens = new CommonTokenStream(new DxbcAsmLexer(new AntlrInputStream(m.Source)));
            try
            {
                var fast = new DxbcAsmParser(tokens) { ErrorHandler = new BailErrorStrategy() };
                fast.RemoveErrorListeners();
                fast.Interpreter.PredictionMode = PredictionMode.SLL;
                return fast.program();
            }
            catch (Exception)
            {
                // SLL guessed wrong: re-parse the same token stream with LL.
                tokens.Reset();
                var full = new DxbcAsmParser(tokens);
                var errors = new Counter();
                full.RemoveErrorListeners();
                full.AddErrorListener(errors);
                var root = full.program();
                m.ParseErrors = errors.Count;
                return root;
            }
        }

        private sealed class Counter : BaseErrorListener
        {
            public int Count;
            public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                             IToken offendingSymbol, int line, int charPositionInLine,
                                             string msg, RecognitionException e) => Count++;
        }

        // ---------------------------------------------------------- declarations

        private void ReadDecl(DxbcAsmParser.DeclarationContext ctx)
        {
            var d = new Decl { Raw = ctx.GetText() };
            var reg = ctx.regToken();
            switch (ctx.Start.Type)
            {
                case DxbcAsmParser.DCL_TEMPS:
                    d.Kind = "temps";
                    var temps = ctx.number();
                    d.Count = temps != null && temps.Length > 0 ? Number(temps[0]) : 0;
                    Temps = Math.Max(Temps, d.Count);
                    break;
                case DxbcAsmParser.DCL_CONSTANTBUFFER:
                    d.Kind = "cbuffer";
                    // The slot is the inline REG_CB token (`CB3`), not a regToken:
                    // this alternative spells the register itself.
                    d.Register = IndexOf(ctx.REG_CB().GetText());
                    var rows = ctx.number();
                    d.Count = rows != null && rows.Length > 0 ? Number(rows[0]) : 0;
                    break;
                case DxbcAsmParser.DCL_SAMPLER:
                    d.Kind = "sampler";
                    d.Register = ctx.REG_S() == null ? 0 : IndexOf(ctx.REG_S().GetText());
                    break;
                case DxbcAsmParser.DCL_RESOURCE:
                    d.Kind = "texture";
                    d.Register = reg == null ? 0 : IndexOf(reg.GetText());
                    d.Dimension = DimensionOf(ctx.GetText());
                    break;
                case DxbcAsmParser.DCL_INPUT:
                case DxbcAsmParser.DCL_INPUT_PS:
                case DxbcAsmParser.DCL_INPUT_SIV:
                case DxbcAsmParser.DCL_INPUT_SGV:
                case DxbcAsmParser.DCL_INPUT_PS_SIV:
                case DxbcAsmParser.DCL_INPUT_PS_SGV:
                    d.Kind = "input";
                    d.Register = ctx.operand() == null ? 0 : OperandOf(ctx.operand()).Index;
                    break;
                case DxbcAsmParser.DCL_OUTPUT:
                case DxbcAsmParser.DCL_OUTPUT_SIV:
                case DxbcAsmParser.DCL_OUTPUT_SGV:
                    d.Kind = "output";
                    d.Register = ctx.operand() == null ? 0 : OperandOf(ctx.operand()).Index;
                    break;
                default:
                    d.Kind = "other";
                    break;
            }
            Decls.Add(d);
        }

        private static int Number(DxbcAsmParser.NumberContext ctx)
        {
            if (ctx == null) return 0;
            string t = ctx.GetText();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.Parse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        private static int IndexOf(string tokenText)
        {
            int i = 0;
            while (i < tokenText.Length && (char.IsLetter(tokenText[i]))) i++;
            return int.TryParse(tokenText[i..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        private static int DimensionOf(string text)
        {
            if (text.Contains("texture3d")) return 3;
            if (text.Contains("texturecube")) return 4;
            if (text.Contains("texture2darray")) return 5;
            if (text.Contains("texture2d")) return 2;
            if (text.Contains("texture1d")) return 1;
            if (text.Contains("buffer")) return 7;
            return 2;
        }

        // ---------------------------------------------------------- instructions

        private void ReadInstruction(DxbcAsmParser.InstructionContext ctx)
        {
            var header = ctx.opcodeHeader();
            if (header == null) return;

            var ins = new Instruction
            {
                OpcodeFull = header.GetText().Trim(),
                Raw = Text(ctx),
            };
            ins.Opcode = BaseOpcode(ins.OpcodeFull, out bool sat, out bool precise);
            ins.Saturate = sat;
            ins.Precise = precise;

            var list = ctx.operandList();
            if (list != null)
                foreach (var op in list.operand()) ins.Args.Add(OperandOf(op));

            Code.Add(ins);
        }

        /// <summary>
        /// `sample_indexable(texture2d)(float,float,float,float)` -> `sample`;
        /// `add_sat` -> `add` with Saturate set.  Keeping the base name is what
        /// lets one handler serve the indexable/non-indexable spellings of the
        /// same instruction.
        /// </summary>
        private static string BaseOpcode(string full, out bool saturate, out bool precise)
        {
            saturate = false;
            precise = false;
            string name = full;
            int cut = name.IndexOf('(');
            if (cut >= 0) name = name[..cut];

            // `sample_indexable(...)` is the same instruction as `sample`; the
            // postfix says the resource is an indexable one, nothing more.
            if (name.EndsWith("_indexable", StringComparison.Ordinal))
                name = name[..^"_indexable".Length];

            foreach (string suffix in new[] { "_sat", "_precise" })
            {
                if (!name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if (suffix == "_sat") saturate = true; else precise = true;
                name = name[..^suffix.Length];
            }
            return name;
        }

        internal static Operand OperandOf(DxbcAsmParser.OperandContext ctx)
        {
            var o = new Operand { Raw = ctx.GetText(), Negate = ctx.MINUS() != null };
            o.Abs = ctx.PIPE() != null && ctx.PIPE().Length > 0;

            var atom = ctx.operandAtom();
            if (atom == null) return o;
            if (atom.NULL() != null) { o.Kind = RegKind.Null; return o; }

            var lit = atom.literal();
            if (lit != null)
            {
                o.Kind = RegKind.Literal;
                var numbers = lit.number();
                o.Lit = new float[numbers.Length];
                for (int i = 0; i < numbers.Length; i++) o.Lit[i] = Float(numbers[i]);
                return o;
            }

            var reg = atom.register();
            var token = reg.regToken();
            string text = token.GetText();
            o.Kind = KindOf(text);
            o.Index = IndexOf(text);
            o.Swizzle = reg.SWIZZLE()?.GetText();
            var idx = reg.indexExpr();
            if (idx != null)
            {
                var num = idx.number();
                if (num != null) o.Row = Number(num);
            }
            return o;
        }

        /// <summary>The line as it was written, spacing preserved.</summary>
        private string Text(ParserRuleContext ctx) =>
            Source.Substring(ctx.Start.StartIndex, ctx.Stop.StopIndex - ctx.Start.StartIndex + 1);

        private static float Float(DxbcAsmParser.NumberContext ctx)
        {
            string t = ctx.GetText();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.Parse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static RegKind KindOf(string tokenText)
        {
            if (tokenText.StartsWith("cb", StringComparison.Ordinal)) return RegKind.Cb;
            if (tokenText.StartsWith("icb", StringComparison.Ordinal)) return RegKind.Icb;
            return char.ToLowerInvariant(tokenText[0]) switch
            {
                'r' => RegKind.R,
                'v' => RegKind.V,
                'o' => RegKind.O,
                't' => RegKind.T,
                's' => RegKind.S,
                'u' => RegKind.U,
                'x' => RegKind.X,
                'a' => RegKind.A,
                'g' => RegKind.G,
                'm' => RegKind.M,
                _ => RegKind.R,
            };
        }
    }
}
