using System;
using System.Collections.Generic;
using System.Globalization;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree;

namespace ShaderRipper.Ast
{
    internal enum RegKind
    {
        R, V, O, T, S, Cb, Icb, U, X, A, G, M, Null, Literal,

        /// <summary>
        /// `oDepth` / `oMask`: output registers the disassembler names instead of
        /// numbering.  They carry SV_Depth and SV_Coverage, so they are not a
        /// colour target and cannot share the oN handling.
        /// </summary>
        OSpecial,
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

        /// <summary>The literal's components as floats (what a float opcode sees).</summary>
        public float[] Lit;

        /// <summary>
        /// The literal's 32-bit patterns.
        ///
        /// A DXBC literal is four raw bytes; the disassembler prints them in the
        /// domain of the instruction, so `ieq r0, r0, l(1)` is the INTEGER 1
        /// (0x00000001) while `lt r0, r0, l(1.000000)` is the float 1.0
        /// (0x3F800000).  Reading the integer form through <see cref="Lit"/> turns it
        /// into 1065353216, and an integer compare against that never matches -- fxc
        /// then proves the branch dead and deletes the rest of the shader.
        /// </summary>
        public uint[] LitBits;

        /// <summary>
        /// True when the disassembler printed the literal as integers (`l(1)`), false
        /// for floats (`l(1.000000)`).  The two spell different bit patterns, so a
        /// literal in a FLOAT context has to be emitted as `asfloat(1)`, not `1.0f`.
        /// </summary>
        public bool IntLit;

        public string Raw;

        /// <summary>
        /// For <see cref="RegKind.OSpecial"/>: the register's own name
        /// (`oDepth` / `oMask`).  These have no number, so <see cref="Index"/> is
        /// meaningless and the name is what identifies them.
        /// </summary>
        public string Special;

        /// <summary>
        /// The bracket expression exactly as written, when the register has one:
        /// `3` for <c>x0[3]</c>, `r2.z + 0` for <c>x0[r2.z + 0]</c>.  A constant
        /// index also lands in <see cref="Row"/>; this keeps the dynamic form.
        ///
        /// An indexable temp is an ARRAY, so dropping the expression writes every
        /// element into element 0 -- a 25-entry lookup table became one cell, which
        /// compiles and reads garbage.
        /// </summary>
        public string IndexRaw;
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
        public string Kind;         // temps / input / output / output_special / cbuffer / sampler / texture / other
        public int Register;
        public int Count;
        public int Dimension;
        public string Raw;

        /// <summary>
        /// `dcl_resource_structured t0, 32` -- the record stride in bytes.  A load
        /// address is <c>index * stride + byteOffset</c>, so without it the element
        /// index of a 32-byte record is off by a factor of two.
        /// </summary>
        public int Stride;

        /// <summary>A <c>dcl_resource_structured</c>, which HLSL spells StructuredBuffer.</summary>
        public bool Structured;

        /// <summary>
        /// A multisampled resource (<c>dcl_resource_texture2dms</c>).  Its
        /// <c>GetDimensions</c> overload takes no mip level, so the shape of the
        /// query has to know about it.
        /// </summary>
        public bool Ms;

        /// <summary>
        /// Sample count from <c>dcl_resource_texture2dms(N)</c>; 0 means the
        /// disassembly did not say, which leaves HLSL unable to size the type.
        /// </summary>
        public int Samples;

        /// <summary>For <see cref="Kind"/> <c>output_special</c>: <c>oDepth</c> / <c>oMask</c>.</summary>
        public string Special;
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
        /// Length of each <c>dcl_indexableTemp</c> array, by register number.
        /// A program may declare several (<c>x0[25], 4</c> and <c>x1[8], 4</c>), and
        /// each is a separate array in the emitted source.
        /// </summary>
        public Dictionary<int, int> TempArrays = new();

        /// <summary>
        /// <c>dcl_immediateConstantBuffer</c> rows, in order: the constant table
        /// fxc bakes into the program for things like a blur kernel.  Read from
        /// the declaration itself, because it is the only place the values exist.
        /// </summary>
        public List<float[]> Icb = new();

        /// <summary>
        /// The stage's input and output signature tables, read from the leading
        /// comments of the disassembly (see <see cref="SignatureReader"/>).  Null
        /// when the program has none, which is what a synthetic or truncated
        /// disassembly looks like.
        /// </summary>
        public List<SigEntry> InputSig;
        public List<SigEntry> OutputSig;

        /// <summary>
        /// The constant-buffer analysis for this program, when the game reader ran
        /// one.  Null for a bare <c>--asm</c> file, where nothing outside the
        /// disassembly is known and every buffer stays numeric.
        ///
        /// The disassembly carries no RDEF (Unity strips it), so the identity of
        /// <c>cb1</c> and the name of each of its rows can only come from the
        /// asset: the pass's name table, the program's parameter blob and the
        /// shader's Properties block.  <see cref="Resolve.CbResolver"/> combines
        /// those; this field is where the answer travels to the emitter.
        /// </summary>
        public Resolve.CbResolver Cb;

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

            SignatureReader.Read(m.Source, m);
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
                // `dcl_indexableTemp x0[25], 4` -- a 25-element array of 4-component
                // vectors.  The length is what sizes the declaration; without it the
                // emitter guessed 512 and every `x0[k]` write collapsed into slot 0.
                case DxbcAsmParser.DCL_INDEXABLETEMP:
                    d.Kind = "indexableTemp";
                    var xToken = ctx.REG_X();
                    d.Register = xToken == null ? 0 : IndexOf(xToken.GetText());
                    var xNums = ctx.number();
                    if (xNums != null && xNums.Length > 0) d.Count = Number(xNums[0]);
                    if (xNums != null && xNums.Length > 1) d.Dimension = Number(xNums[1]);
                    TempArrays[d.Register] = d.Count;
                    break;
                case DxbcAsmParser.DCL_CONSTANTBUFFER:
                    d.Kind = "cbuffer";
                    // The slot is the inline REG_CB token (`CB3`), not a regToken:
                    // this alternative spells the register itself.
                    d.Register = IndexOf(ctx.REG_CB().GetText());
                    var rows = ctx.number();
                    d.Count = rows != null && rows.Length > 0 ? Number(rows[0]) : 0;
                    break;
                case DxbcAsmParser.DCL_ICB:
                    d.Kind = "icb";                    var body = ctx.icbBody();
                    if (body != null)
                        foreach (var row in body.icbRow())
                        {
                            var nums = row.number();
                            var vals = new float[nums.Length];
                            for (int i = 0; i < nums.Length; i++) vals[i] = Float(nums[i]);
                            Icb.Add(vals);
                        }
                    d.Count = Icb.Count;
                    break;
                case DxbcAsmParser.DCL_SAMPLER:
                    d.Kind = "sampler";
                    d.Register = ctx.REG_S() == null ? 0 : IndexOf(ctx.REG_S().GetText());
                    break;
                case DxbcAsmParser.DCL_RESOURCE:
                    d.Kind = "texture";
                    d.Register = reg == null ? 0 : IndexOf(reg.GetText());
                    d.Dimension = DimensionOf(ctx.GetText());
                    d.Structured = ctx.GetText().Contains("structured", StringComparison.OrdinalIgnoreCase);
                    d.Ms = ctx.GetText().Contains("texture2dms", StringComparison.OrdinalIgnoreCase)
                        || ctx.GetText().Contains("texture2darrayms", StringComparison.OrdinalIgnoreCase)
                        || ctx.GetText().Contains("ms(", StringComparison.OrdinalIgnoreCase)
                        || ctx.GetText().Contains("ms (", StringComparison.OrdinalIgnoreCase);
                    // `dcl_resource_texture2dms(4)` -- HLSL needs the count to size
                    // the type: an unsized `Texture2DMS<float4>` is rejected on
                    // every target ("Unsized MSAA textures aren't supported").
                    var sc = ctx.sampleCount();
                    if (sc?.number() != null) d.Samples = Number(sc.number());
                    // `dcl_resource_structured t0, 32` -- the stride is the one bare
                    // number in the argument list.
                    var stride = ctx.dclArgs();
                    if (stride != null && d.Structured)
                        foreach (var arg in stride.dclArg())
                            foreach (var atom in arg.dclAtom())
                                if (atom.number() != null && atom.operand() == null && atom.IDENT() == null)
                                    d.Stride = Number(atom.number());
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
                    var outOp = ctx.operand();
                    if (outOp != null && OperandOf(outOp).Kind == RegKind.OSpecial)
                    {
                        d.Kind = "output_special";
                        d.Special = OperandOf(outOp).Special;
                        break;
                    }
                    d.Kind = "output";
                    d.Register = outOp == null ? 0 : OperandOf(outOp).Index;
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
            // The most specific spelling first: `texture2dmsarray` and
            // `texture2darray` both contain "texture2d", so testing 2D first
            // declared an array as a plain 2D texture and the array index in the
            // coordinate was silently dropped.
            if (text.Contains("texture2dmsarray")) return 5;
            if (text.Contains("texture2darray")) return 5;
            if (text.Contains("texture1darray")) return 5;
            if (text.Contains("texturecubearray")) return 4;
            if (text.Contains("texture3d")) return 3;
            if (text.Contains("texturecube")) return 4;
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

            // `sampleinfo_uint` / `resinfo_uint` spell the result type as a suffix.
            // It only says the bits are an integer pattern, not which instruction
            // this is, so it goes with the other modifiers.
            foreach (string suffix in new[] { "_uint", "_int", "_sat", "_precise" })
            {
                if (!name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if (suffix == "_sat") saturate = true;
                else if (suffix == "_precise") precise = true;
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
                o.LitBits = new uint[numbers.Length];
                o.IntLit = numbers.Length > 0;
                for (int i = 0; i < numbers.Length; i++)
                {
                    o.Lit[i] = Float(numbers[i]);
                    o.LitBits[i] = Bits(numbers[i]);
                    if (!Integral(numbers[i])) o.IntLit = false;
                }
                return o;
            }

            var reg = atom.register();
            var token = reg.regToken();
            string text = token.GetText();
            o.Kind = KindOf(text);
            o.Index = IndexOf(text);
            if (o.Kind == RegKind.OSpecial) o.Special = text;
            o.Swizzle = reg.SWIZZLE()?.GetText();
            var idx = reg.indexExpr();
            if (idx != null)
            {
                o.IndexRaw = idx.GetText();
                // A register inside the brackets means a RUNTIME row index
                // (`cb1[r1.x + 25]`); the trailing `+ 25` is a direct child of
                // `indexExpr`, so reading `number()` alone would take 25 as a
                // constant row and silently pin a dynamic read to one row.
                if (idx.register() == null)
                {
                    var num = idx.number();
                    if (num != null) o.Row = Number(num);
                }
            }
            return o;
        }

        /// <summary>The line as it was written, spacing preserved.</summary>
        private string Text(ParserRuleContext ctx) =>
            Source.Substring(ctx.Start.StartIndex, ctx.Stop.StopIndex - ctx.Start.StartIndex + 1);

        /// <summary>
        /// The literal's raw 32 bits: a plain integer (`1`, `-1`) is an integer
        /// literal, anything with a decimal point or an exponent is a float one.
        /// </summary>
        /// <summary>Was this number printed as a plain integer (`1`, not `1.0`)?</summary>
        private static bool Integral(DxbcAsmParser.NumberContext ctx)
        {
            string t = ctx.GetText();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return false;
            return t.Length > 0 && t.All(c => char.IsDigit(c) || c == '-' || c == '+');
        }

        private static uint Bits(DxbcAsmParser.NumberContext ctx)
        {
            string t = ctx.GetText();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                                     out uint hex) ? hex : 0u;
            bool integral = t.Length > 0 && t.All(c => char.IsDigit(c) || c == '-' || c == '+');
            if (integral &&
                long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v))
                return unchecked((uint)v);
            return BitConverter.SingleToUInt32Bits(Float(ctx));
        }

        private static float Float(DxbcAsmParser.NumberContext ctx)
        {
            string t = ctx.GetText();
            // `0x3f800000` in a DXBC literal is a bit pattern, not a number: the
            // value it stands for is the float those 32 bits spell.  Reading it as
            // an integer and then converting loses that entirely -- 0x3f800000 is
            // 1.0f, but as an integer it is 1065353216, and `asuint` of the result
            // is a different bit pattern, so a mask like `and r, r, l(0x3f800000)`
            // ends up clearing the value instead of keeping it.
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                                   out uint bits)) return 0f;
                return BitConverter.Int32BitsToSingle(unchecked((int)bits));
            }
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static RegKind KindOf(string tokenText)
        {
            // The named output registers are checked first: `oDepth` would
            // otherwise read as `o` with a non-numeric index and collapse to `o0`.
            if (tokenText is "oDepth" or "oMask") return RegKind.OSpecial;
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
