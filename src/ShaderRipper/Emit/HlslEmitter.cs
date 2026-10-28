using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShaderRipper.Ast;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// Emits HLSL from a <see cref="Module"/> -- that is, from the DXBC parse tree.
    ///
    /// The output is register-level on purpose (<c>float4 r0</c> locals, one
    /// statement per instruction, real <c>cbuffer</c> blocks bound by register,
    /// <c>Texture2D</c> + <c>SamplerState</c>), because that is what makes the
    /// translation checkable.  Pretty names need the constant-buffer reflection,
    /// which is a later layer.
    ///
    /// An instruction without a handler becomes a <c>// TODO</c> comment and is
    /// counted, so coverage is reported instead of assumed.
    /// </summary>
    internal sealed class HlslEmitter
    {
        public sealed class Result
        {
            public string Hlsl = "";
            /// <summary>The shader profile the program was compiled for (`ps_4_0`).</summary>
            public string Profile = "";
            public int Instructions;
            public int Translated;
            public int Todo;
            public int Blocks;
            public List<string> Missing = new();
        }

        private readonly Module _m;
        private readonly StringBuilder _sb = new();
        private readonly Result _r = new();
        private int _indent = 1;
        private int _temps;
        private int _loops;

        /// <summary>One entry per open <c>switch</c>, outermost last.</summary>
        private readonly Stack<SwitchState> _switches = new();

        private sealed class SwitchState
        {
            /// <summary>Variable holding the switch condition, read once.</summary>
            public string Subject;
        }

        /// <summary>Distinct name suffix per switch in this program.</summary>
        private int _switchSeq;

        /// <summary>Distinct name suffix per aliasing <c>sincos</c> in this program.</summary>
        private int _sincosSeq;

        private readonly CbLayout _lay;
        private ShaderInterface _iface;
        /// <summary>
        /// `oDepth` / `oMask` this program writes, or null.  They are not colour
        /// targets: <c>SV_Depth</c> and <c>SV_Coverage</c> have to be fields of the
        /// returned struct (or the single <c>SV_Depth</c> return of a program with
        /// no colour target at all), so the body cannot just assign a local.
        /// </summary>
        private readonly List<string> _specialOut = new();

        /// <summary>
        /// Output registers of a pixel program with more than one render target, so
        /// the stage can return a struct of `SV_TargetN` fields.  Null for the
        /// ordinary single-target case and for vertex programs.
        /// </summary>
        private List<Decl> _targets;

        /// <summary>
        /// Everything the emitter needs to know about comparison masks in one program.
        ///
        /// The decision is per WRITE, because the same lane usually holds a mask once
        /// and a number later.  For every comparison, walk forward until the lane is
        /// rewritten and classify each read in that window: only branches, selects and
        /// bitwise combinations means <c>1.0f</c> is safe; float arithmetic wants
        /// <c>-1.0f</c> (the number the bit pattern stands for, without the NaN); an
        /// integer read across a branch keeps the 0xFFFFFFFF pattern, because a linear
        /// scan cannot prove the lane still holds this comparison.
        /// </summary>
        private sealed class MaskInfo
        {
            public readonly HashSet<(int reg, char lane)> BoolLanes = new();
            public readonly HashSet<int> BoolDefs = new();
            public readonly HashSet<(int reg, char lane)> Lanes = new();
            public readonly HashSet<int> BitDefs = new();

            /// <summary>
            /// Per instruction, the mask write that reaches it: lane -> comparison.
            /// A missing entry means "not provably a mask here", which is the answer
            /// that keeps `asint(...)` instead of guessing a literal.
            /// </summary>
            public Dictionary<(int reg, char lane), int>[] Reach;
            public int[] Block;
        }

        private static MaskInfo AnalyseMasks(Module m)
        {
            static bool MakesMask(Instruction ins) => ins.Opcode is
                "lt" or "le" or "gt" or "ge" or "eq" or "ne" or
                "ilt" or "ige" or "ieq" or "ine" or "uge" or "ult";

            // A bitwise op on two masks produces a mask, so the lane set has to
            // propagate through `and`/`or`/`not` as well -- otherwise the chain
            // `lt -> or -> and` stops being recognisable after the first `or`.
            static bool ProducesMask(Instruction ins, HashSet<(int, char)> masks,
                                     Func<Operand, string> maskOf)
            {
                if (MakesMask(ins)) return true;
                // `mov` is a copy, so mask-ness flows through it.  Without this the
                // copy is just a float that happens to hold -1.0f, `asint` of it reads
                // 0xBF800000, and the integer chain built on the copy is garbage.
                if (ins.Opcode is not ("and" or "or" or "not" or "mov")) return false;
                for (int i = 1; i < ins.Args.Count; i++)
                {
                    var a = ins.Args[i];
                    if (a.Kind != RegKind.R) return false;
                    foreach (char c in maskOf(a))
                        if (!masks.Contains((a.Index, c))) return false;
                }
                return true;
            }

            static string MaskOf(Operand o)
            {
                string s = o.Swizzle?.TrimStart('.') ?? "";
                return s.Length == 0 ? "xyzw" : s;
            }

            static bool BooleanUse(Instruction ins) => ins.Opcode is
                "movc" or "if_nz" or "if_z" or "breakc_nz" or "breakc_z"
                or "continuec_nz" or "continuec_z" or "discard_nz"
                or "and" or "or" or "not"
                || MakesMask(ins);

            // Instructions that read the operand's BITS, so a mask has to carry
            // 0xFFFFFFFF there rather than the number -1.
            static bool ReadsBits(Instruction ins) => ins.Opcode is
                "iadd" or "isub" or "imul" or "imin" or "imax" or "imad" or "ineg"
                or "umin" or "umax" or "iumin" or "iumax"
                or "ieq" or "ine" or "ilt" or "ige" or "uge" or "ult"
                or "ushr" or "ishl" or "itof" or "ftoi" or "ftou" or "utof"
                or "udiv" or "ubfe" or "ibfe" or "bfi" or "bfrev"
                or "countbits" or "firstbit_hi" or "firstbit_lo";

            var info = new MaskInfo();
            var lanes = info.Lanes;

            // Every lane a comparison writes is a mask, whatever happens to it
            // afterwards.  Reached by fixed point: a bitwise op whose sources are all
            // masks produces a mask too, so `lt -> or -> and` stays recognisable past
            // the first `or`.
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var ins in m.Code)
                {
                    if (ins.Args.Count == 0) continue;
                    var d0 = ins.Args[0];
                    if (d0.Kind != RegKind.R) continue;
                    if (!ProducesMask(ins, lanes, MaskOf)) continue;
                    foreach (char lane in MaskOf(d0))
                        if (lanes.Add((d0.Index, lane))) grew = true;
                }
            }

            info.Block = BlockIds(m);

            // Reaching mask write per lane.  A linear forward pass is enough here
            // because an entry is only trusted when the read sits in the same basic
            // block as the write it names -- inside one block there is no second
            // path, so the scan cannot be wrong.  Across a branch the entry may be
            // stale, and the caller falls back to `asint(...)`.
            var reach = new List<Dictionary<(int reg, char lane), int>>(m.Code.Count);
            var cur = new Dictionary<(int reg, char lane), int>();
            foreach (var ins in m.Code)
            {
                reach.Add(new Dictionary<(int reg, char lane), int>(cur));
                if (ins.Args.Count == 0) continue;
                var d0 = ins.Args[0];
                if (d0.Kind != RegKind.R) continue;
                foreach (char lane in MaskOf(d0)) cur.Remove((d0.Index, lane));
                if (ProducesMask(ins, lanes, MaskOf))
                    foreach (char lane in MaskOf(d0)) cur[(d0.Index, lane)] = reach.Count - 1;
            }
            info.Reach = reach.ToArray();

            for (int pc = 0; pc < m.Code.Count; pc++)
            {
                var def = m.Code[pc];
                if (def.Args.Count == 0) continue;
                var dst = def.Args[0];
                if (dst.Kind != RegKind.R) continue;
                if (!ProducesMask(def, lanes, MaskOf)) continue;

                bool allBool = true, needsBits = false;
                foreach (char lane in MaskOf(dst))
                {
                    bool boolOnly = true;
                    for (int k = pc + 1; k < m.Code.Count; k++)
                    {
                        var ins = m.Code[k];
                        // Classify the read BEFORE the death test: the instruction
                        // that kills a value usually reads it too (`iadd r0.y,
                        // -r0.y, r0.z` both reads and writes r0.y).  Testing death
                        // first ended the window without ever asking what kind of
                        // read it was, so every in-place arithmetic update of a mask
                        // counted as "boolean only" -- and the lane was emitted as
                        // 1.0f, which `asint` then reads as 0x3F800000.
                        bool read = false;
                        for (int i = 1; i < ins.Args.Count; i++)
                        {
                            var a = ins.Args[i];
                            if (a.Kind != RegKind.R || a.Index != dst.Index) continue;
                            if (MaskOf(a).Contains(lane)) { read = true; break; }
                        }
                        if (read && !BooleanUse(ins))
                        {
                            boolOnly = false;
                            // A bit read from another block cannot be answered with a
                            // literal, because the linear scan cannot prove which
                            // write reaches that read.
                            if (ReadsBits(ins) && info.Block[k] != info.Block[pc])
                                needsBits = true;
                        }

                        if (ins.Args.Count > 0 && ins.Args[0].Kind == RegKind.R &&
                            ins.Args[0].Index == dst.Index &&
                            MaskOf(ins.Args[0]).Contains(lane))
                            break;                  // the value dies here
                    }
                    if (boolOnly) info.BoolLanes.Add((dst.Index, lane));
                    else allBool = false;
                }
                if (allBool) info.BoolDefs.Add(pc);
                else if (needsBits) info.BitDefs.Add(pc);
            }
            return info;
        }

        /// <summary>
        /// A block id per instruction: a new id starts at every branch, loop and
        /// return marker, so two instructions share an id only when nothing can jump
        /// between them.
        /// </summary>
        private static int[] BlockIds(Module m)
        {
            var ids = new int[m.Code.Count];
            int id = 0;
            for (int i = 0; i < m.Code.Count; i++)
            {
                if (FlowOpcodes.Contains(m.Code[i].Opcode)) id++;
                ids[i] = id;
            }
            return ids;
        }

        private static readonly HashSet<string> FlowOpcodes = new()
        {
            "if_nz", "if_z", "else", "endif", "loop", "endloop", "break",
            "breakc_nz", "breakc_z", "continue", "continuec_nz", "continuec_z",
            "switch", "case", "default", "endswitch", "ret",
        };

        /// <summary>
        /// Lanes whose mask value is only ever consumed as a boolean.
        ///
        /// See <see cref="MaskInfo"/> for how the set is computed.  Emitting `1.0f`
        /// for these removes the IEEE NaN that -1.0 would carry, without changing any
        /// decision the program makes.
        /// </summary>
        private readonly HashSet<(int reg, char lane)> _boolLanes;

        /// <summary>
        /// Lanes whose value is a COMPARISON RESULT (0 or ±1), regardless of how it
        /// is later consumed.
        ///
        /// `_boolLanes` answers "may I spell this 1.0f", which is narrower; this one
        /// answers "is this a mask rather than a number", which is what decides
        /// whether `asuint(a) | asuint(b)` is a logical or or a bitwise one.
        /// </summary>
        private readonly HashSet<(int reg, char lane)> _maskLanes;

        /// <summary>
        /// Comparison writes whose mask must keep the 0xFFFFFFFF bit pattern.
        ///
        /// The only masks that still need it: ones an integer opcode reads across a
        /// branch, where the linear scan cannot prove which write reaches the read
        /// and therefore cannot answer <c>asint(...)</c> with a literal.  Everything
        /// else is spelled <c>-1.0f</c>.
        /// </summary>
        private readonly HashSet<int> _bitDefs;

        /// <summary>Per instruction: which comparison write reaches each lane.</summary>
        private readonly Dictionary<(int reg, char lane), int>[] _reach;
        private readonly int[] _block;

        /// <summary>The instruction being emitted, for the mask lookups.</summary>
        private int _pc;

        /// <summary>
        /// Registers that an integer opcode writes, so they get a companion
        /// <c>int4 iN</c> beside their <c>float4 rN</c>.
        ///
        /// DXBC has one register file and no types.  Re-deriving an integer from the
        /// float copy does not survive the trip through HLSL -- the float with bit
        /// pattern 1 is a denormal that fxc folds to 0 -- so the integer is kept in an
        /// integer variable, which also removes the <c>asint(...)</c> around every
        /// read.
        /// </summary>
        private readonly HashSet<int> _intRegs = new();

        /// <summary>
        /// Per instruction: which lanes of which register have their <c>iN</c> in
        /// sync with <c>rN</c> (bit set = in sync).
        /// </summary>
        private int[][] _intSync = Array.Empty<int[]>();

        /// <summary>The snapshot for the instruction being emitted, or null.</summary>
        private int[] _intNow;

        /// <summary>Registers whose lanes are read in a FLOAT context somewhere.</summary>
        private bool[] _intFloatRead = Array.Empty<bool>();

        /// <summary>
        /// Instruction indices whose mask may be spelled <c>1.0f</c>.
        ///
        /// Per-INSTRUCTION rather than per register: the same lane usually holds a
        /// mask once and a number later, so the question is only answerable for one
        /// write at a time.  See <see cref="AnalyseMasks"/>.
        /// </summary>
        private readonly HashSet<int> _boolDefs;

        /// <summary>
        /// The mask constant for a destination lane set: <c>1.0f</c> where every lane
        /// is boolean-only, <c>-1.0f</c> otherwise.
        ///
        /// DXBC's mask is the bit pattern 0xFFFFFFFF, which is a NaN when read as a
        /// float.  Its numeric value is -1, and integer reads are answered with a
        /// literal instead (see <see cref="AsInt"/>), so nothing needs the NaN bits.
        /// </summary>
        private string MaskFor(Operand dst, Instruction ins = null)
        {
            int at = ins == null ? _pc : InstructionIndex(ins);
            if (_boolDefs.Contains(at)) return "1.0f";
            if (_bitDefs.Contains(at)) return "asfloat(0xFFFFFFFFu)";
            if (dst.Kind != RegKind.R) return "-1.0f";
            string mask = DestMask(dst);
            if (mask.Length == 0) mask = "xyzw";
            return mask.All(c => _boolLanes.Contains((dst.Index, c)))
                ? "1.0f"
                : "-1.0f";
        }

        /// <summary>
        /// Opcodes whose result is an integer bit pattern, so the destination gets an
        /// <c>int4</c> companion.
        ///
        /// Only opcodes whose emission always goes through <see cref="IntResult"/>
        /// belong here: the shadow is declared from this set in the pre-pass, so one
        /// that sometimes writes a float (<c>and</c> is a masked select when a side is
        /// a comparison result) would leave a stale integer behind.
        /// </summary>
        private static readonly HashSet<string> IntWrites = new(StringComparer.Ordinal)
        {
            "iadd", "isub", "imul", "imad", "ineg",
            "imin", "imax", "umin", "umax", "udiv", "ushr", "ishl",
            "ftoi", "ftou",
        };

        /// <summary>
        /// Opcodes that READ their sources as integers.  The conversions are excluded:
        /// they read their source through the float spelling.
        /// </summary>
        private static readonly HashSet<string> IntReads = new(StringComparer.Ordinal)
        {
            "iadd", "isub", "imul", "imad", "ineg",
            "imin", "imax", "umin", "umax", "udiv", "ushr", "ishl",
        };

        /// <summary>
        /// The constant-buffer lanes this program only ever reads as integers.
        ///
        /// <c>itof r1.x, cb0[12].y</c> converts an INTEGER to a float, so the slot
        /// holds an integer bit pattern and the declaration has to say so.  Only
        /// integer-only opcodes count, since they have no float spelling in HLSL;
        /// bitwise ops are excluded because they also combine comparison results.  A
        /// lane read as a float anywhere keeps its float declaration.
        /// </summary>
        private static Dictionary<(int reg, int row, int col), string> IntCbLanes(Module m)
        {
            var ints = new Dictionary<(int reg, int row, int col), string>();
            var floats = new HashSet<(int reg, int row, int col)>();
            foreach (var ins in m.Code)
            {
                if (ins.Args.Count < 2) continue;
                bool conv = ins.Opcode is "itof" or "utof";
                bool integer = conv || IntReads.Contains(ins.Opcode) || IntCompare.ContainsKey(ins.Opcode);
                string kind = ins.Opcode is "utof" or "uge" or "ult" or "udiv" or "ushr"
                              || ins.Opcode is "umin" or "umax" ? "uint" : "int";
                for (int k = 1; k < ins.Args.Count; k++)
                {
                    var o = ins.Args[k];
                    if (o == null || o.Kind != RegKind.Cb) continue;
                    int row = Math.Max(o.Row, 0);
                    string lanes = o.Swizzle is { Length: > 1 } s ? s.TrimStart('.') : "xyzw";
                    if (lanes.Length == 0) lanes = "xyzw";
                    foreach (char c in lanes)
                    {
                        int col = "xyzw".IndexOf(c);
                        if (col < 0) continue;
                        var key = (o.Index, row, col);
                        if (!integer) { floats.Add(key); continue; }
                        ints[key] = kind;
                    }
                }
            }
            foreach (var k in floats) ints.Remove(k);
            return ints;
        }

        /// <summary>
        /// Work out which registers need an integer shadow, and where in the program
        /// the shadow is still in sync with the float copy.
        ///
        /// "In sync" is a property of the VALUE, not of control flow: the shadow is
        /// written by every integer opcode and invalidated by every other write of the
        /// same lane, so a forward scan over the instruction list is exact wherever
        /// the emission is (the emitter re-emits the program in order).
        /// </summary>
        private void PlanIntRegisters(Module m)
        {
            var code = m.Code;
            _intSync = new int[code.Count][];
            _intFloatRead = new bool[_temps];
            var live = new int[_temps];
            // Both `float4 rN = 0.0` and `int4 iN = 0` start at the same bits.
            for (int i = 0; i < _temps; i++) live[i] = 0xF;

            for (int pc = 0; pc < code.Count; pc++)
            {
                _intSync[pc] = (int[])live.Clone();
                var ins = code[pc];
                if (ins.Args.Count == 0) continue;
                var a = ins.Args;
                if (a[0]?.Kind == RegKind.R)
                {
                    string mask = DestMask(a[0]);
                    if (mask.Length == 0) mask = "xyzw";
                    int save = _pc;
                    _pc = pc;
                    bool integer = IntWrites.Contains(ins.Opcode) || TakesBitPath(ins);
                    // A plain `mov` carries whatever the source held, integer included:
                    // a loop counter is copied around the register file like any other
                    // value, and losing track of it is what forced `asint(r0.z)` on
                    // every read.
                    int carry = !integer && ins.Opcode == "mov" ? IntMoveCarry(ins, live) : 0;
                    _pc = save;
                    if (integer || carry != 0) _intRegs.Add(a[0].Index);
                    for (int c = 0; c < 4; c++)
                    {
                        int bit = 1 << c;
                        if (mask.IndexOf("xyzw"[c]) < 0) continue;
                        if (integer || (carry & bit) != 0) live[a[0].Index] |= bit;
                        else live[a[0].Index] &= ~bit;
                    }
                }
                if (IntReads.Contains(ins.Opcode)) continue;
                for (int k = 1; k < a.Count; k++)
                    if (a[k]?.Kind == RegKind.R) _intFloatRead[a[k].Index] = true;
            }
        }

        /// <summary>
        /// Does this `and` / `or` / `xor` / `not` end up as bit arithmetic?
        ///
        /// Those four have a masked-select spelling that writes a float, so the shadow
        /// can only be claimed for the branch that really does bit work.  The test
        /// mirrors the one in <see cref="Bitwise"/> / <see cref="Not"/>; `BitmapPath`
        /// is only consulted while planning, so `_pc` is the instruction under plan.
        /// </summary>
        private bool TakesBitPath(Instruction ins)
        {
            var a = ins.Args;
            if (a.Count < 2) return false;
            switch (ins.Opcode)
            {
                case "and":
                    if (a.Count < 3) return false;
                    if (a[0].Kind == RegKind.R && IsMaskLane(a[1]) && IsMaskLane(a[2])) return false;
                    if (MaskShape(a[1]) != null || MaskShape(a[2]) != null) return false;
                    return true;
                case "or":
                case "xor":
                    if (a.Count < 3) return false;
                    return !(a[0].Kind == RegKind.R && IsMaskLane(a[1]) && IsMaskLane(a[2]));
                case "not":
                    return !(IsBool(a[0]) && IsBool(a[1]));
            }
            return false;
        }

        /// <summary>
        /// The <c>iN</c> spelling of an operand, when its lanes are integers that are
        /// still there.  Null means "spell it the old way".
        /// </summary>
        private string IntShadow(Operand o, bool unsigned)
        {
            if (o == null || o.Kind != RegKind.R) return null;
            if (o.Negate || o.Abs) return null;
            if (!_intRegs.Contains(o.Index)) return null;

            string sw = o.Swizzle?.TrimStart('.') ?? "";
            if (sw.Length == 0) sw = "xyzw";
            bool single = sw.All(c => c == sw[0]);

            // A one-letter swizzle reads one lane; `xyzw` reads the whole register and
            // only matches a full-width destination.  Anything in between is a
            // projection, and `Expr` is the only thing that knows how to spell it.
            string lanes;
            if (single) lanes = sw[..1];
            else if (sw == "xyzw" && DestMaskLen(_m.Code[_pc].Args[0]) == 4) lanes = "xyzw";
            else return null;

            var sync = _intNow;
            if (sync == null || sync.Length <= o.Index) return null;
            foreach (char c in lanes)
                if ((sync[o.Index] & (1 << "xyzw".IndexOf(c))) == 0) return null;

            string text = $"i{o.Index}.{lanes}";
            return unsigned ? $"asuint({text})" : text;
        }

        /// <summary>Lane bits of a destination mask; an empty mask means all four.</summary>
        private static int LaneBits(string mask)
        {
            if (string.IsNullOrEmpty(mask)) return 0xF;
            int bits = 0;
            foreach (char c in mask)
            {
                int i = "xyzw".IndexOf(c);
                if (i >= 0) bits |= 1 << i;
            }
            return bits;
        }

        /// <summary>
        /// The destination lanes a plain `mov` carries an integer through; 0 when the
        /// move is not a pure integer carry.
        ///
        /// Only unambiguous shapes qualify: one lane copied from an explicit lane, or
        /// the whole register copied from <c>xyzw</c>.  A projection is left alone
        /// because its letter order is positional against the destination mask, which
        /// is not what <see cref="IntShadow"/> models.
        /// </summary>
        private static int IntMoveCarry(Instruction ins, int[] live)
        {
            var a = ins.Args;
            if (a.Count < 2) return 0;
            var dst = a[0];
            var src = a[1];
            if (dst.Kind != RegKind.R || dst.Negate || dst.Abs || ins.Saturate) return 0;
            if (src == null || src.Negate || src.Abs) return 0;

            string mask = DestMask(dst);
            int width = string.IsNullOrEmpty(mask) ? 4 : mask.Length;
            int bits = LaneBits(mask);
            string sw = src.Swizzle is { Length: > 0 } s ? (s[0] == '.' ? s[1..] : s) : "";

            if (src.Kind == RegKind.Literal)
            {
                if (!src.IntLit || src.LitBits is not { Length: > 0 } lit) return 0;
                // A uniform literal means the same thing in every lane, so the
                // destination mask and the operand swizzle cannot change the answer:
                // `mov r2.zw, l(0,0,0,0)` seeds two integer lanes.  A non-uniform one
                // is printed in the order the destination reads it, which is a mapping
                // this does not model -- except for the whole-register identity form
                // (`mov r1, l(0,0,0,-1)`), where that order is plain x,y,z,w.
                if (lit.All(b => b == lit[0])) return bits;
                if (width == 4 && lit.Length == 4 && (sw.Length == 0 || sw == "xyzw")) return bits;
                return width == 1 && sw.Length == 0 && lit.Length == 1 ? bits : 0;
            }

            if (src.Kind != RegKind.R) return 0;
            if (live == null || src.Index >= live.Length) return 0;

            if (width == 1)
            {
                if (sw.Length == 0) return 0;                       // implicit lane, not modelled
                if (sw.Length > 1 && sw.Any(c => c != sw[0])) return 0;
                int lane = "xyzw".IndexOf(sw[0]);
                if (lane < 0) return 0;
                return (live[src.Index] & (1 << lane)) != 0 ? bits : 0;
            }

            if (sw.Length != 0 && sw != "xyzw") return 0;
            return (live[src.Index] & bits) == bits ? bits : 0;
        }

        /// <summary>
        /// An integer literal as the contents of an `int4`, for a `mov` that carries
        /// one into the shadow.
        ///
        /// `l(0, 0, 0, -1)` seeds four lanes with two different values, so the spelling
        /// has to be a constructor; a uniform one broadcasts as a scalar.
        /// </summary>
        private static string IntLiteralText(Operand src, int width)
        {
            var lb = src?.LitBits;
            if (lb == null || lb.Length == 0) return "0";
            string N(int i) =>
                unchecked((int)lb[Math.Min(i, lb.Length - 1)]).ToString(CultureInfo.InvariantCulture);
            if (lb.All(b => b == lb[0])) return N(0);
            var parts = Enumerable.Range(0, Math.Max(width, 1)).Select(N).ToArray();
            string ctor = parts.Length switch { 2 => "int2", 3 => "int3", _ => "int4" };
            return $"{ctor}({string.Join(", ", parts)})";
        }

        /// <summary>
        /// Emit an integer result.  Writes the shadow, and the float copy only when
        /// something reads the register as a float.
        /// </summary>
        private void IntResult(Instruction ins, string expr, bool unsigned = false)
        {
            var dst = ins.Args[0];
            if (dst.Kind != RegKind.R || dst.Negate || dst.Abs)
            {
                Line($"{Expr(dst)} = asfloat({expr});");
                return;
            }
            int reg = dst.Index;
            string lanes = DestMask(dst);
            if (lanes.Length == 0) lanes = "xyzw";
            // No constructor around the value: HLSL broadcasts a scalar to the
            // destination's width, and every operand the callers pass is already the
            // width the mask asks for.
            string value = unsigned ? $"asint({expr})" : expr;
            Line($"i{reg}.{lanes} = {value};");
            if (_intFloatRead[reg]) Line($"r{reg}.{lanes} = asfloat(i{reg}.{lanes});");
        }

        /// <summary>
        /// An operand as an integer, for the `asint(...)` the integer opcodes need.
        ///
        /// A mask lane reads as -1 when the comparison held and 0 when it did not, so
        /// the reinterpretation is replaced by that conditional.  It has to be a
        /// RUNTIME conditional: the comparison's answer is not known here, and
        /// substituting the literal -1 would turn "x &lt; 0" into "true".
        ///
        /// This is what lets a mask be stored as -1.0f instead of the 0xFFFFFFFF NaN
        /// pattern: `asint(-1.0f)` is 0xBF800000, not -1, so the value has to be
        /// rebuilt from the truth value rather than reinterpreted.
        /// </summary>
        private string AsInt(Operand o) =>
            IntShadow(o, false) ?? LitInt(o) ?? MaskAsInt(o, false)
            ?? IntReinterpret(o, false);

        private string AsUInt(Operand o) =>
            IntShadow(o, true) ?? LitUInt(o) ?? MaskAsInt(o, true)
            ?? IntReinterpret(o, true);

        /// <summary>
        /// The `asint(...)` an integer operand needs, with the sign modifier
        /// applied AFTER the reinterpretation.
        ///
        /// DXBC's `-` modifies the operand in the domain of the instruction, so
        /// `imax r0.x, r0.x, -r0.x` negates an INTEGER: two's complement.  Applying
        /// it inside, `asint(-r0.x)`, negates the float instead -- that only flips
        /// the sign bit, so the bits of -5 (0xFFFFFFFB, a NaN pattern) answer
        /// 0x7FFFFFFB instead of 5, and `max(x, asint(-x))` stops being `abs(x)`.
        /// Negating the int is the operation the original performs.
        /// </summary>
        private string IntReinterpret(Operand o, bool unsigned)
        {
            if (o == null) return null;
            // An int member is already an integer: `-_AbsoluteInset` is a negate
            // in the right domain, and wrapping it would add a conversion.
            if (CbIsInt(o)) return Expr(o);
            if (!o.Negate)
                return unsigned ? $"asuint({Expr(o)})" : $"asint({Expr(o)})";
            var plain = Clone(o, o.Swizzle);
            plain.Negate = false;
            return unsigned ? $"-asuint({Expr(plain)})" : $"-asint({Expr(plain)})";
        }

        /// <summary>
        /// Does this operand name a constant-buffer member the layout declares
        /// <c>int</c>?
        ///
        /// The program reads such a slot with an integer instruction (`itof` and
        /// friends), so the member is declared as an integer instead of a float that
        /// gets reinterpreted.  `asint` around it would then be the identity, and the
        /// surrounding conversion is the implicit one.
        /// </summary>
        private bool CbIsInt(Operand o)
        {
            if (o == null || o.Kind != RegKind.Cb || _lay.IsArray(o.Index)) return false;
            int row = Math.Max(o.Row, 0);
            string lanes = o.Swizzle is { Length: > 1 } s ? s.TrimStart('.') : "xyzw";
            if (lanes.Length == 0) return false;
            foreach (char c in lanes)
            {
                int col = "xyzw".IndexOf(c);
                if (col < 0 || !_lay.IsIntLane(o.Index, row, col)) return false;
            }
            return true;
        }

        /// <summary>The literal `-1` / `0xFFFFFFFFu` a mask stands for, as a select.</summary>
        private string MaskAsInt(Operand o, bool unsigned)
        {
            if (MaskShape(o) == null) return null;
            // -(-1) and |-1| are 1, so a negated or absolute mask selects +1.
            bool flipped = o.Negate || o.Abs;
            string one = unsigned
                ? (flipped ? "1u" : "0xFFFFFFFFu")
                : (flipped ? "1" : "-1");
            string zero = unsigned ? "0u" : "0";
            return $"({Expr(o)} != 0.0 ? {one} : {zero})";
        }

        /// <summary>
        /// A literal operand as the integer its BITS spell.
        ///
        /// `ieq r0, l(1), r1` compares 0x00000001 with r1's bits, not 1.0f's
        /// 0x3F800000 -- the disassembler prints a constant in the domain of the
        /// instruction.  Printing the integer keeps the comparison exact while
        /// removing the pointless `asint(0.0f)` the emitter used to wrap around every
        /// literal.
        /// </summary>
        private static string LitInt(Operand o)
        {
            if (o == null || o.Kind != RegKind.Literal) return null;
            if (o.LitBits == null || o.LitBits.Length == 0)
            {
                if (o.Lit == null || o.Lit.Length == 0) return null;
                return unchecked((int)BitConverter.SingleToUInt32Bits(o.Lit[0]))
                    .ToString(CultureInfo.InvariantCulture);
            }
            int bits = unchecked((int)o.LitBits[0]);
            return bits.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The literal's bits as an unsigned integer, or null.</summary>
        private static string LitUInt(Operand o)
        {
            if (o == null || o.Kind != RegKind.Literal) return null;
            uint bits = o.LitBits is { Length: > 0 }
                ? o.LitBits[0]
                : BitConverter.SingleToUInt32Bits(o.Lit is { Length: > 0 } l ? l[0] : 0f);
            return bits.ToString(CultureInfo.InvariantCulture) + "u";
        }

        /// <summary>
        /// The swizzle of a provable mask operand, or null.
        ///
        /// "Provable" means every lane is reached by a comparison write that sits in
        /// the SAME basic block as this read: only then is the linear scan
        /// trustworthy, because no second path can reach the read.
        /// </summary>
        private string MaskShape(Operand o)
        {
            if (o == null || o.Kind != RegKind.R) return null;
            string m = o.Swizzle?.TrimStart('.') ?? "";
            if (m.Length == 0) m = "xyzw";

            var here = _reach[_pc];
            foreach (char c in m)
            {
                if (!here.TryGetValue((o.Index, c), out int def)) return null;
                if (_block[def] != _block[_pc]) return null;
            }
            return m;
        }

        /// <summary>
        /// The mask as a FLOAT constant: `-1.0f`, never `asfloat(0xFFFFFFFFu)`.
        ///
        /// Same numeric value as the bit pattern, but not a NaN.  Used where the mask
        /// is converted in the float domain (`itof`).
        /// </summary>
        private string MaskFloat(Operand o)
        {
            if (MaskShape(o) == null) return null;
            string one = (o.Negate || o.Abs) ? "1.0f" : "-1.0f";
            return $"({Expr(o)} != 0.0 ? {one} : 0.0f)";
        }

        /// <summary>Index of an instruction object in the program, or -1.</summary>
        private int InstructionIndex(Instruction ins)
        {
            if (_insIndex == null)
            {
                _insIndex = new Dictionary<Instruction, int>(ReferenceEqualityComparer.Instance);
                for (int i = 0; i < _m.Code.Count; i++) _insIndex[_m.Code[i]] = i;
            }
            return _insIndex.TryGetValue(ins, out int at) ? at : -1;
        }

        private Dictionary<Instruction, int> _insIndex;

        /// <summary>Is this operand a boolean at the lane it names?</summary>
        private bool IsBool(Operand o)
        {
            if (o.Kind != RegKind.R) return false;
            string mask = o.Swizzle?.TrimStart('.') ?? "";
            if (mask.Length != 1) return false;
            return _boolLanes.Contains((o.Index, mask[0]));
        }

        /// <summary>
        /// One recognised matrix-vector product.
        ///
        /// <see cref="Steps"/> are the instructions that built it (three rows plus the
        /// completing one, and the completing one need not be adjacent to the other
        /// three).  <see cref="Comp"/> holds the four vector components indexed by ROW,
        /// not by which instruction read them -- that is what makes `mul(M, v)` exact
        /// no matter how fxc shuffled the lanes between registers.
        /// </summary>
        private sealed class Product
        {
            /// <summary>Index of the instruction where the folded statement is emitted.</summary>
            public int At;
            /// <summary>The matrix array name, e.g. `unity_ObjectToWorld`.</summary>
            public string Matrix = "";
            /// <summary>
            /// The columns the run reads of every row: "" for the whole vector, else
            /// the projection (`mul(M, v).xyw`).
            /// </summary>
            public string Mask = "";
            public int Width = 4;
            /// <summary>Destination register number, or -1 when it is an output.</summary>
            public int DstReg = -1;
            public string DstText = "";
            /// <summary>The accumulator the running sum lives in.</summary>
            public int AccReg = -1;
            /// <summary>The four vector components, already spelled for HLSL.</summary>
            public string[] Comp = new string[4];
            /// <summary>The vector's register, when the whole vector is one temp.</summary>
            public int VecReg = -1;

            /// <summary>The statement the product becomes, `mul` spelling.</summary>
            public string Line = "";
            /// <summary>
            /// The same product spelled out over the row array, for matrices that have
            /// no matrix type (per-material arrays, builtin float4 arrays).
            /// </summary>
            public string Expanded = "";
            /// <summary>The first three rows only, writing the accumulator.</summary>
            public string PartialLine;
            public string PartialExpanded;

            /// <summary>Every instruction the fold consumes.</summary>
            public List<int> Steps = new();
            /// <summary>
            /// Other completions of the same sum: `dst`, the columns it writes, the
            /// component that scales the last row, and whether that row is the implicit
            /// homogeneous one.
            /// </summary>
            public List<(int at, string dst, string dm, string w, bool one)> Extras = new();
            /// <summary>Row order of the original terms, for the row-sum spelling.</summary>
            public List<int> Order = new();
            /// <summary>The closing row can be re-spelled as `mul` without losing the sum.</summary>
            public bool FoldTail;
            /// <summary>Rows 0..2 are still needed, so they get their own statement.</summary>
            public bool KeepSum;
            /// <summary>
            /// The whole product is the three-row sum: no row reads a fourth
            /// component, so the statement replaces all three instructions and there
            /// is nothing left to complete.
            /// </summary>
            public bool PartialOnly;
            /// <summary>Every register the run writes, `DstReg` included.</summary>
            public List<int> Written = new();
            /// <summary>
            /// Locals that keep a vector component alive until the completing row.
            ///
            /// fxc is free to schedule the computation of one component between the
            /// rows that read the others, and that computation often lands on the very
            /// register the earlier rows read.  The fold has to happen at the closing
            /// row, so by then the register no longer holds that component -- pinning
            /// it at the head of the run is what lets the single statement see both
            /// values.  Empty for every product whose components all survive.
            /// </summary>
            public List<(int at, string line)> Pins = new();
        }

        /// <summary>Statements emitted in place of the instructions a fold consumed.</summary>
        private sealed class Fold
        {
            public List<string> Lines = new();
            public int Consumed = 1;
        }

        private readonly List<Product> _products = new();
        private readonly Dictionary<int, Fold> _folded = new();
        /// <summary>
        /// Statements that belong BEFORE the instruction at the key: the pins that
        /// keep a vector component alive across the rows of a folded product.
        /// </summary>
        private readonly Dictionary<int, List<string>> _pre = new();
        /// <summary>Namer for those pins, unique within one program.</summary>
        private int _pinSeq;

        /// <summary>
        /// How far past the third row the completing row may sit.
        ///
        /// fxc schedules the first three rows, then whatever work feeds their vector
        /// components, and only then the closing row -- up to four instructions of
        /// slack in practice.  The scan still stops at the first write of the sum, so
        /// a wider window cannot swallow an unrelated product.
        /// </summary>
        private const int TailSlack = 8;
        private readonly HashSet<int> _skip = new();
        private bool[] _taken = Array.Empty<bool>();

        /// <summary>
        /// True when the emitted file has to define <c>UnityObjectToClipPos</c>
        /// itself.
        ///
        /// In a ShaderLab file the name comes from <c>UnityCG.cginc</c>, whose
        /// matrices are <c>float4x4</c>, and the wrapper's declaration merging must
        /// not see a same-named function.  The standalone <c>--programs</c> file has
        /// no include and declares the matrices as <c>float4 M[4]</c> row arrays, so
        /// <c>mul(matrix, vector)</c> does not type-check there and the helper is
        /// inlined instead (see <see cref="DeclareClipPosHelper"/>).
        /// </summary>
        private bool _needsClipPosHelper;

        /// <summary>
        /// True when the output is a file this route owns end-to-end (the
        /// <c>--programs</c> artifact), rather than a block the ShaderLab wrapper
        /// will merge into a file that includes <c>UnityCG.cginc</c>.  It decides
        /// only how a folded transform is spelled: the standalone file declares its
        /// matrices as row arrays and needs the inlined helper, while the wrapper's
        /// file has both the `float4x4` declarations and the macro.
        /// </summary>
        private readonly bool _standalone;

        private HlslEmitter(Module m, bool standalone = false)
        {
            _m = m;
            _standalone = standalone;
            _lay = CbLayout.Build(m, m.Cb, IntCbLanes(m));
            var masks = AnalyseMasks(m);
            _boolLanes = masks.BoolLanes;
            _boolDefs = masks.BoolDefs;
            _maskLanes = masks.Lanes;
            _bitDefs = masks.BitDefs;
            _reach = masks.Reach;
            _block = masks.Block;
            FoldMatrixMultiplies(m);
        }

        /// <summary>
        /// Rewrite every expanded matrix-vector product as one statement.
        ///
        /// fxc unrolls <c>mul(M, v)</c> into one instruction per row, and a vertex
        /// shader usually completes the same rows-0..2 sum twice -- once with the
        /// homogeneous 1.0 and once with a real w.  So the recogniser works on the
        /// SUM: three contiguous terms build a partial sum, a fourth row completes it
        /// and may sit a few instructions later, and every later reader that is itself
        /// a fourth row is another completion.  When a later reader is NOT a fourth
        /// row the sum is live, and only the first three rows fold.
        /// </summary>
        private void FoldMatrixMultiplies(Module m)
        {
            var code = m.Code;
            _taken = new bool[code.Count];
            for (int i = 0; i + 3 < code.Count; i++)
            {
                if (_taken[i]) continue;
                var p = TryProduct(i);
                if (p != null) Register(p);
            }
            MergeObjectToClipPos();
            FoldNormalTransforms();
        }

        /// <summary>
        /// The product whose expansion starts at <paramref name="i"/>, or null.
        ///
        /// Everything the fold needs is decided here: which rows, which vector
        /// components (keeping the register each one is read from, so a component that
        /// is overwritten before the last row can be pinned into a local), where the
        /// completing row sits, and whether the partial sum survives past it.
        /// </summary>
        private Product TryProduct(int i)
        {
            var code = _m.Code;
            var head = code[i];
            if (head.Opcode != "mul" || head.Args.Count < 3) return null;
            if (head.Args[0].Kind != RegKind.R) return null;
            if (!TryRowTerm(head, out var t0) || t0.Matrix == null) return null;

            int width = DestMaskLen(head.Args[0]);
            if ((t0.Mask.Length == 0 ? 4 : t0.Mask.Length) != width) return null;

            var comp = new string[4];
            var src = new (int reg, char lane, int at)[4];
            var rows = new bool[4];
            var order = new List<int>();
            var steps = new List<int> { i };
            var written = new List<int> { head.Args[0].Index };
            if (!TakeTerm(t0, comp, src, rows, i)) return null;
            order.Add(t0.Row);

            int acc = head.Args[0].Index;
            for (int k = 1; k <= 2; k++)
            {
                int j = i + k;
                if (j >= code.Count || _taken[j]) return null;
                var ins = code[j];
                if (ins.Opcode != "mad" || ins.Args.Count < 4) return null;
                if (!Accumulates(ins, acc)) return null;
                if (!TryRowTerm(ins, out var t)) return null;
                if (!SameTerm(t, t0, width)) return null;
                if (!TakeTerm(t, comp, src, rows, j)) return null;
                if (ins.Args[0].Kind != RegKind.R) return null;
                steps.Add(j);
                order.Add(t.Row);
                acc = ins.Args[0].Index;
                if (!written.Contains(acc)) written.Add(acc);
            }

            // The closing row, and any NARROWER completion that comes first.
            //
            // A sprite shader wants the world position too, and fxc emits that one
            // first because it needs only three columns.  The narrow one is a
            // completion in its own right, so it becomes a line of its own and does
            // not stop the full row from closing the product.
            int tailAt = -1;
            RowTerm t3 = null;
            bool one = false;
            var lead = new List<(int at, string dst, RowTerm term, bool implicitOne)>();
            for (int k = i + 3; k < code.Count && k <= i + 3 + TailSlack; k++)
            {
                if (_taken[k])
                {
                    if (k == i + 3) return null;
                    break;
                }
                var between = code[k];
                if (FlowOpcodes.Contains(between.Opcode)) break;
                if (TryTail(between, acc, t0, width, rows, out var wide, out one))
                {
                    t3 = wide;
                    tailAt = k;
                    break;
                }
                bool narrowOk = false;
                if (TryTail(between, acc, t0, 1, rows, out var narrow, out bool narrowOne,
                            prefix: true))
                {
                    var nd = between.Args[0];
                    int nw = nd.Kind is RegKind.R or RegKind.O ? DestMaskLen(nd) : 0;
                    string ndText = nw > 0 ? Expr(nd) : null;
                    string ndm = nw > 0 ? DestMask(nd) : null;
                    // The narrow completion must not land on the sum itself: the full
                    // product writes that register, and the two would fight for it.
                    narrowOk = ndText != null && nw < width && ndm.Length > 0 &&
                               !(nd.Kind == RegKind.R && nd.Index == acc) &&
                               (narrowOne ? narrow.Row == 3 : true);
                    if (narrowOk) lead.Add((k, ndText, narrow, narrowOne));
                }
                // fxc is free to schedule UNRELATED work between the rows; that keeps
                // the sum intact and the completing row may still follow.  Only a
                // WRITE of the sum ends the search.
                var bd = between.Args.Count > 0 ? between.Args[0] : null;
                if (bd != null && bd.Kind == RegKind.R && bd.Index == acc) break;
            }
            if (tailAt < 0)
            {
                // No completing row at all: then the three rows ARE the product, with
                // the homogeneous row contributing nothing.  Only a run that covers
                // rows 0, 1 and 2 of one array exactly and writes three lanes
                // qualifies -- any other three rows would need the missing one spelled
                // out.
                if (lead.Count > 0 || width != 3) return null;
                if (order.Count != 3 || order[0] + order[1] + order[2] != 3) return null;
                if (t0.Mask.Length != 3) return null;

                var d3 = code[steps[2]].Args[0];
                if (d3.Kind is not (RegKind.R or RegKind.O)) return null;
                if (DestMaskLen(d3) != width) return null;
                string d3Text = Expr(d3);
                if (d3Text == null) return null;

                string sfx = "." + t0.Mask;
                var part = new Product
                {
                    At = steps[2],
                    Matrix = t0.Matrix,
                    Mask = t0.Mask,
                    Width = width,
                    DstReg = d3.Kind == RegKind.R ? d3.Index : -1,
                    DstText = d3Text,
                    AccReg = acc,
                    Comp = comp,
                    Steps = steps,
                    Order = order,
                    PartialOnly = true,
                };
                part.Line =
                    $"{d3Text} = mul({part.Matrix}, float4({comp[0]}, {comp[1]}, {comp[2]}, 0.0)){sfx};";
                part.Expanded =
                    $"{d3Text} = {ExpandedSum(part.Matrix, part.Mask, comp, order, false)};";
                part.Written.AddRange(written);
                return part;
            }
            if (one && t3.Row != 3) return null;      // only the homogeneous row is implicit
            if (!TakeTerm(t3, comp, src, rows, tailAt, one)) return null;
            order.Add(t3.Row);
            for (int r = 0; r < 4; r++) if (!rows[r]) return null;

            // A component that fxc overwrote between its first read and the closing
            // row cannot be re-read at the fold site:
            //
            //     mul r2,     r0.xxxx, VP[1]     -- wants the OLD r0.x
            //     ...
            //     mul r0.x,   v0.y,    O2W[1].w  -- r0.x now holds something else
            //     mad o0,     VP[3],   r0.xxxx, r1
            //
            // The single `mul(VP, float4(...))` reads every component at one point in
            // time, so the overwritten one has to be pinned into a local where it
            // still existed -- right after the instruction that read it.
            var pins = new List<(int at, string line)>();
            for (int r = 0; r < 4; r++)
            {
                if (src[r].reg < 0) continue;
                if (!LaneWrittenBetween(src[r].reg, src[r].lane, src[r].at, tailAt)) continue;
                string pin = "pin" + _pinSeq++;
                pins.Add((src[r].at, $"float {pin} = {comp[r]};"));
                comp[r] = pin;
            }

            var tailDst = code[tailAt].Args[0];
            if (tailDst.Kind is not (RegKind.R or RegKind.O)) return null;
            if (DestMaskLen(tailDst) != width) return null;
            string dstText = Expr(tailDst);
            if (dstText == null) return null;

            string suffix = t0.Mask.Length == 0 ? "" : "." + t0.Mask;
            string vec = $"float4({comp[0]}, {comp[1]}, {comp[2]}, {comp[3]})";

            var p = new Product
            {
                At = tailAt,
                Matrix = t0.Matrix,
                Mask = t0.Mask,
                Width = width,
                DstReg = tailDst.Kind == RegKind.R ? tailDst.Index : -1,
                DstText = dstText,
                AccReg = acc,
                Comp = comp,
                Steps = steps,
                Pins = pins,
            };
            // `mul(M, v).mask` selects the columns of the product, which is exactly
            // what a scalarised `M[k].mask` chain computes.
            p.Line = $"{dstText} = mul({p.Matrix}, {vec}){suffix};";
            p.Expanded = $"{dstText} = {ExpandedSum(p.Matrix, p.Mask, comp, order, one)};";
            p.Written.AddRange(written);

            // The partial sum on its own, for the case where it really is live.
            string accText = Expr(code[steps[2]].Args[0]);
            if (accText != null && p.Mask.Length == 0)
            {
                p.PartialLine =
                    $"{accText} = mul({p.Matrix}, float4({comp[0]}, {comp[1]}, {comp[2]}, 0.0));";
                p.PartialExpanded = $"{accText} = " +
                    ExpandedSum(p.Matrix, p.Mask, comp, order.GetRange(0, 3), false) + ";";
            }

            // The vector's register, when x/y/z really are the .x/.y/.z of one temp:
            // that is what makes a later product reading it a chain.  Read from `src`,
            // not `comp`, because a pinned component's `comp` entry holds a local's
            // name but the lane relationship is unchanged.
            if (p.Mask.Length == 0)
            {
                if (src[0].reg >= 0 && src[0].reg == src[1].reg && src[0].reg == src[2].reg &&
                    src[0].lane == 'x' && src[1].lane == 'y' && src[2].lane == 'z')
                    p.VecReg = src[0].reg;
            }

            p.Order = order;

            // The narrow completions found before the closing row.
            foreach (var (at, ndText, term, implicitOne) in lead)
            {
                string w = implicitOne ? "1.0" : Lane(term.Vec);
                if (w == null) return null;
                p.Extras.Add((at, ndText, DestMask(_m.Code[at].Args[0]), w, implicitOne));
            }

            // Does the partial sum survive?
            //
            // Two separate questions: a COMPLETION never needs the sum to be dead,
            // since it recomputes those rows instead of reading them; the SUM itself
            // only disappears when nothing but completions reads it.  So every
            // completion folds, and the three-row prefix folds too when something else
            // still wants it.
            var live = written.Where(r => r != p.DstReg).ToList();
            bool sumLive = false;
            for (int k = tailAt + 1; k < code.Count && live.Count > 0; k++)
            {
                var ins = code[k];
                if (_taken[k]) continue;
                if (!ReadsAny(ins, live))
                {
                    if (WritesAny(ins, live)) break;      // the sum dies here
                    continue;
                }

                var dst = ins.Args[0];
                string dm = dst.Kind is RegKind.R or RegKind.O ? DestMask(dst) : null;
                string hm = p.Mask.Length == 0 ? "xyzw" : p.Mask;
                bool completion = false;
                RowTerm extra = null;
                bool extraOne = false;
                if (dm != null && (dm.Length == 0 || hm.StartsWith(dm, StringComparison.Ordinal)))
                    completion = TryTail(ins, live.Contains(acc) ? acc : live[0], t0,
                                         dm.Length == 0 ? width : dm.Length, rows,
                                         out extra, out extraOne, prefix: true);
                if (completion)
                {
                    string text = Expr(dst);
                    string w = extraOne ? "1.0" : Lane(extra.Vec);
                    if (text == null || w == null) { sumLive = true; continue; }
                    p.Extras.Add((k, text, dm, w, extraOne));

                    // A completion that writes the sum's own register replaces it --
                    // `mad r0, M[3], v.w, r0` is the common shape -- so the sum is no
                    // longer needed and the partial line would be dead code.
                    if (dst.Kind == RegKind.R && DestMask(dst).Length == 0 &&
                        live.Remove(dst.Index) && live.Count == 0)
                        break;
                    continue;
                }
                sumLive = true;
            }

            // The closing row writes the sum's own register: folding it would destroy
            // the sum, so it can only fold when nothing else reads the sum.
            p.FoldTail = !(p.DstReg == acc && sumLive);
            p.KeepSum = sumLive || !p.FoldTail;
            if (p.KeepSum && p.PartialLine == null) return null;
            return p;
        }

        /// <summary>
        /// Write the fold for one product: the statement(s) that replace it, and which
        /// instructions they consume.
        /// </summary>
        private void Register(Product p)
        {
            // The standalone artifact declares its matrices as `float4 M[4]` row
            // arrays, where `mul` has no overload, so it gets the row-sum spelling.
            bool matrix = !_standalone && IsMulMatrix(p.Matrix);
            foreach (int s in p.Steps) if (s != p.At) _skip.Add(s);
            foreach (int s in p.Steps) _taken[s] = true;

            // The pins go where the component still had its old value -- the
            // instruction that read it -- not at the fold site.
            foreach (var (at, line) in p.Pins)
            {
                if (!_pre.TryGetValue(at, out var list)) _pre[at] = list = new List<string>();
                list.Add(line);
            }

            // A three-row product is the whole statement: nothing completes it, so it
            // is not a merge candidate either (there is no fourth row to name).
            if (p.PartialOnly)
            {
                _folded[p.At] = new Fold
                {
                    Lines = { matrix ? p.Line : p.Expanded },
                    Consumed = p.Steps.Count,
                };
                return;
            }

            // The three-row prefix, when something other than a completion still
            // reads it.  Emitted where the sum was completed, which is the first point
            // the value exists.
            if (p.KeepSum)
                _folded[p.Steps[2]] = new Fold
                {
                    Lines = { matrix ? p.PartialLine : p.PartialExpanded },
                };

            if (!p.FoldTail) return;

            var fold = new Fold();
            fold.Lines.Add(matrix ? p.Line : p.Expanded);
            fold.Consumed = p.Steps.Count;
            foreach (var (at, dst, dm, w, implicitOne) in p.Extras)
            {
                fold.Lines.Add(CompletionLine(p, dst, dm, w, implicitOne, matrix));
                fold.Consumed++;
                _taken[at] = true;
                _skip.Add(at);
            }
            _folded[p.At] = fold;

            // Every product is a merge candidate, including the ones that write an
            // output directly -- the clip-position product usually does.
            if (matrix) _products.Add(p);
        }

        /// <summary>
        /// Collapse three `dot` instructions into one 3x3 matrix multiply.
        ///
        /// Three rows of one matrix, read at the same three columns, dotted against
        /// the same vector, landing on one destination's three lanes (in any order --
        /// the swizzle carries it) become <c>mul(v, (float3x3)M)</c>, the transpose
        /// form Unity writes in <c>UnityObjectToWorldNormal</c>.
        ///
        /// The vector must be a different register than the destination: an in-place
        /// run would read a lane the first dot already overwrote, while the single
        /// `mul` reads all three components before writing anything.
        /// </summary>
        private void FoldNormalTransforms()
        {
            if (_standalone) return;
            var code = _m.Code;
            for (int i = 0; i + 2 < code.Count; i++)
            {
                if (_taken[i] || _taken[i + 1] || _taken[i + 2]) continue;

                string matrix = null;
                string vecText = null;
                int dst = -1;
                // Which destination LANE each row lands in.  fxc writes the three dots
                // in whatever order the register allocator likes, and the lane letters
                // need not be x/y/z: `r3.y = dot(v, M[0])`, `r3.z = dot(v, M[1])`,
                // `r3.x = dot(v, M[2])` is the same product with the result permuted,
                // and a writable swizzle says so exactly.
                var laneOfRow = new char[3];
                bool ok = true;
                for (int k = 0; k < 3 && ok; k++)
                {
                    var ins = code[i + k];
                    ok = ins.Opcode == "dp3" && ins.Args.Count >= 3;
                    if (!ok) break;
                    string dm = DestMask(ins.Args[0]);
                    ok = dm.Length == 1 &&
                         ins.Args[0].Kind == RegKind.R &&
                         (k == 0 || ins.Args[0].Index == dst);
                    if (!ok) break;
                    dst = ins.Args[0].Index;

                    // Which side is the matrix row?
                    var row = new RowTerm();
                    Operand other;
                    if (TryRowOrLane(ins.Args[2], ins.Args[0], 3, row))
                        other = ins.Args[1];
                    else if (TryRowOrLane(ins.Args[1], ins.Args[0], 3, row))
                        other = ins.Args[2];
                    else { ok = false; break; }

                    ok = row.Row < 3 && row.Mask == "xyz" &&
                         other.Kind is not (RegKind.Cb or RegKind.Literal) &&
                         !(other.Kind == RegKind.R && other.Index == dst) &&
                         (matrix == null || row.Matrix == matrix) &&
                         (vecText == null || Expr(other, 3) == vecText);
                    if (!ok) break;
                    for (int p = 0; p < k; p++) if (laneOfRow[p] == dm[0]) ok = false;
                    if (!ok) break;
                    matrix = row.Matrix;
                    vecText = Expr(other, 3);
                    laneOfRow[row.Row] = dm[0];
                }
                if (!ok || matrix == null || vecText == null || !IsMulMatrix(matrix)) continue;

                string lanes = new(laneOfRow);
                _taken[i] = _taken[i + 1] = _taken[i + 2] = true;
                _folded[i] = new Fold
                {
                    Lines = { $"r{dst}.{lanes} = mul({vecText}, (float3x3){matrix});" },
                    Consumed = 3,
                };
                _skip.Add(i + 1);
                _skip.Add(i + 2);
            }
        }

        /// <summary>
        /// Collapse `mul(VP, mul(M, v))` into `UnityObjectToClipPos(v)`, which is
        /// exactly what the macro is.
        ///
        /// Only an exact chain is merged: the second product's vector must be the
        /// first product's destination register, that register must be dead
        /// afterwards, and the first product's own w must be the literal 1.0.
        /// </summary>
        private void MergeObjectToClipPos()
        {
            foreach (var p in _products)
            {
                if (p.Matrix != "unity_ObjectToWorld" || !IsMulMatrix(p.Matrix)) continue;
                if (p.Mask != "" || p.Comp[3] != "1.0" || p.DstReg < 0) continue;
                if (!_folded.TryGetValue(p.At, out var pfold)) continue;

                foreach (var q in _products)
                {
                    if (q.Matrix != "unity_MatrixVP" || !IsMulMatrix(q.Matrix)) continue;
                    if (q.At <= p.At || q.Mask != "" || q.VecReg != p.DstReg) continue;
                    if (!_folded.TryGetValue(q.At, out var qfold)) continue;

                    // A store to the intermediate between the two products means the
                    // second one does not consume this result.
                    bool clobbered = false;
                    for (int k = p.At + 1; k < q.At && !clobbered; k++)
                    {
                        if (p.Steps.Contains(k) || q.Steps.Contains(k)) continue;
                        var mid = _m.Code[k];
                        if (mid.Args.Count > 0 && mid.Args[0].Kind == RegKind.R &&
                            mid.Args[0].Index == p.DstReg) clobbered = true;
                    }
                    if (clobbered) continue;

                    // The world position is often still wanted (`o5.xyz = M[3].xyz *
                    // v.w + r0.xyz`, a view-space depth), and merging deletes the
                    // register it lived in.  When the second product writes the SAME
                    // register it overwrote the world position anyway, so whatever
                    // reads it later sees the clip position either way.
                    if (q.DstReg != p.DstReg && LiveAfter(q.At + 1, p.DstReg)) continue;

                    string merged = $"{q.DstText} = UnityObjectToClipPos(" +
                                    $"float4({p.Comp[0]}, {p.Comp[1]}, {p.Comp[2]}, 1.0));";
                    var lines = new List<string> { merged };
                    lines.AddRange(pfold.Lines.Skip(1));      // other completions
                    lines.AddRange(qfold.Lines.Skip(1));
                    _folded[p.At] = new Fold
                    {
                        Lines = lines,
                        Consumed = pfold.Consumed + qfold.Consumed,
                    };
                    _folded.Remove(q.At);
                    _skip.Add(q.At);
                    _needsClipPosHelper = _standalone;
                    break;
                }
            }
        }

        /// <summary>
        /// One completion of a product: the same four rows, written to a destination
        /// that may cover only some of the columns.
        /// </summary>
        private static string CompletionLine(Product p, string dst, string dm, string w,
                                             bool one, bool matrix)
        {
            string suffix = dm.Length == 0 ? "" : "." + dm;

            // `mul(M, v).w` IS the fourth column: fxc compiles
            // `mul(M, v).w` to `M[0].w*v.x + M[1].w*v.y + …`, the same sum a
            // scalarised dot produces.  Verified against d3dcompiler, not assumed --
            // the row-major reading ("`.w` is the fourth ROW") is wrong for a matrix
            // times a column vector.
            if (matrix)
                return $"{dst} = mul({p.Matrix}, float4({p.Comp[0]}, {p.Comp[1]}, {p.Comp[2]}, {w})){suffix};";
            var comp = new[] { p.Comp[0], p.Comp[1], p.Comp[2], w };
            return $"{dst} = {ExpandedSum(p.Matrix, dm, comp, p.Order, one)};";
        }

        /// <summary>Do these two terms belong to the same product?</summary>
        private static bool SameTerm(RowTerm t, RowTerm head, int width) =>
            t.Matrix == head.Matrix && t.Mask == head.Mask &&
            (t.Mask.Length == 0 ? 4 : t.Mask.Length) == width;

        /// <summary>Record one term's row and vector component.</summary>
        private bool TakeTerm(RowTerm t, string[] comp, (int reg, char lane, int at)[] src,
                              bool[] rows, int at, bool one = false)
        {
            if (t.Row < 0 || t.Row > 3 || rows[t.Row]) return false;
            if (one)
            {
                comp[t.Row] = "1.0";
                src[t.Row] = (-1, 'x', at);
                rows[t.Row] = true;
                return true;
            }
            string lane = Lane(t.Vec);
            if (lane == null) return false;
            var tl = TempLane(lane);
            src[t.Row] = tl is { } v ? (v.reg, v.lane, at) : (-1, 'x', at);
            comp[t.Row] = lane;
            rows[t.Row] = true;
            return true;
        }

        /// <summary>
        /// The term that closes the product: the last row, read against the partial sum.
        ///
        /// <paramref name="one"/> reports the `add` spelling -- the homogeneous row,
        /// which has no vector component at all.
        /// </summary>
        private bool TryTail(Instruction ins, int acc, RowTerm head, int width, bool[] rows,
                             out RowTerm t, out bool one, bool prefix = false)
        {
            t = null;
            one = false;
            if (ins.Args.Count < 3) return false;

            // `prefix` is for a completion that writes FEWER components than the head
            // -- the usual one being the world position, `o5.xyz = M[3].xyz * v.w +
            // r0.xyz`.  Its row is still row 3 of the same array, read at the columns
            // the narrow destination uses, so the term matches when its column
            // projection is a prefix of the product's.
            string headMask = head.Mask.Length == 0 ? "xyzw" : head.Mask;
            bool Match(RowTerm cand) =>
                cand.Matrix == head.Matrix &&
                (prefix ? headMask.StartsWith(cand.Mask.Length == 0 ? "xyzw" : cand.Mask,
                                              StringComparison.Ordinal)
                        : SameTerm(cand, head, width));

            if (ins.Opcode == "add")
            {
                for (int a = 1; a <= 2; a++)
                {
                    if (!ReadsAccumulator(ins.Args[a], acc)) continue;
                    var other = ins.Args[3 - a];
                    var cand = new RowTerm();
                    if (!TryRowOrLane(other, ins.Args[0], cand)) continue;
                    if (!Match(cand)) continue;
                    if (!prefix && rows[cand.Row]) continue;
                    t = cand;
                    one = true;
                    return true;
                }
                return false;
            }

            if (ins.Opcode == "mad" && ins.Args.Count >= 4 && ReadsAccumulator(ins.Args[3], acc))
            {
                if (!TryRowTerm(ins, out var cand)) return false;
                if (!Match(cand)) return false;
                if (!prefix && rows[cand.Row]) return false;
                t = cand;
                return true;
            }
            return false;
        }

        /// <summary>Does this instruction read or write any of these registers?</summary>
        private static bool Touches(Instruction ins, List<int> regs)
        {
            foreach (var a in ins.Args)
            {
                if (a.Kind != RegKind.R) continue;
                if (regs.Contains(a.Index)) return true;
            }
            return false;
        }

        private static bool ReadsAny(Instruction ins, List<int> regs)
        {
            for (int k = 1; k < ins.Args.Count; k++)
            {
                var a = ins.Args[k];
                if (a.Kind == RegKind.R && regs.Contains(a.Index)) return true;
            }
            return false;
        }

        private static bool WritesAny(Instruction ins, List<int> regs)
        {
            if (ins.Args.Count == 0) return false;
            var d = ins.Args[0];
            return d.Kind == RegKind.R && regs.Contains(d.Index);
        }

        /// <summary>Is this lane of this register written in (from, to]?</summary>
        private bool LaneWrittenBetween(int reg, char lane, int from, int to)
        {
            for (int k = from + 1; k <= to && k < _m.Code.Count; k++)
            {
                var ins = _m.Code[k];
                if (ins.Args.Count == 0) continue;
                var d = ins.Args[0];
                if (d.Kind != RegKind.R || d.Index != reg) continue;
                string m = DestMask(d);
                if (m.Length == 0 || m.IndexOf(lane) >= 0) return true;
            }
            return false;
        }
        /// <summary>
        /// May this matrix be handed to `mul`?
        ///
        /// Only when the declaration comes from <c>UnityCG</c>, where the type is
        /// <c>float4x4</c>.  A name the cbuffer header declares itself is a
        /// <c>float4 name[4]</c> row array, which <c>mul</c> does not accept
        /// (<c>error X3013</c>), so those get the row-sum spelling instead.
        /// </summary>
        private static bool IsMulMatrix(string name) =>
            UnityIncludes.IsMatrixMember(name) && ShaderLab.CanTakeOver(name);

        /// <summary>
        /// Is the value in register <paramref name="reg"/> read after instruction
        /// <paramref name="from"/>?
        ///
        /// The scan stops at the next COMPLETE write of the register, since that write
        /// destroys the value.  Scanning the rest of the program instead would refuse
        /// to fold almost every product, because fxc reuses its temps for unrelated
        /// work a few lines later.  A partial write kills only the lanes it covers.
        /// </summary>
        private bool LiveAfter(int from, int reg, string lanes = "xyzw")
        {
            string still = lanes;
            for (int k = from; k < _m.Code.Count; k++)
            {
                var ins = _m.Code[k];
                if (ins.Args.Count == 0) continue;

                for (int i = 1; i < ins.Args.Count; i++)
                {
                    var a = ins.Args[i];
                    if (a.Kind != RegKind.R || a.Index != reg) continue;
                    string read = a.Swizzle?.TrimStart('.') ?? "";
                    if (read.Length == 0 || read.Any(c => still.IndexOf(c) >= 0)) return true;
                }

                var d = ins.Args[0];
                if (d.Kind != RegKind.R || d.Index != reg) continue;
                string w = DestMask(d);
                if (w.Length == 0) return false;         // the whole value dies here
                // A partial write only kills the lanes it covers, so the lanes are
                // dropped one by one and the scan continues while any are still live.
                still = new string(still.Where(c => w.IndexOf(c) < 0).ToArray());
                if (still.Length == 0) return false;
            }
            return false;
        }

        /// <summary>One term of the expansion: a matrix row and the lane it multiplies.</summary>
        private sealed class RowTerm
        {
            public int Row = -1;
            public string Matrix;
            /// <summary>The columns the term reads of that row ("" = all four).</summary>
            public string Mask = "";
            /// <summary>The operand supplying the vector component for this row.</summary>
            public Operand Vec;
        }

        /// <summary>
        /// `mul dst, &lt;lane&gt;, M[k]` / `mad dst, M[k], &lt;lane&gt;, acc` -- one term of the
        /// expansion, in either operand order.
        ///
        /// The lane is accepted whatever its name: which component a row multiplies is
        /// the caller's business (it is the vector's k-th component, not necessarily
        /// `.y` for row 1).
        /// </summary>
        private bool TryRowTerm(Instruction ins, out RowTerm term)
        {
            term = new RowTerm();
            if (ins.Args.Count < 3) return false;

            // Which arguments could be the row?  Decided first, because a cbuffer
            // operand can play either role: `_WorldSpaceCameraPos.yyy` is a scalar,
            // `unity_WorldToObject[1].yyyy` is a row read at one column.  Only the
            // ones that are NOT rows are eligible as the vector component.
            List<int> rowArgs = null;
            for (int k = 1; k < ins.Args.Count; k++)
            {
                var probe = new RowTerm();
                if (!TryRowOrLane(ins.Args[k], ins.Args[0], probe)) continue;
                (rowArgs ??= new List<int>()).Add(k);
            }
            if (rowArgs == null) return false;

            foreach (int k in rowArgs)
            {
                for (int m = 1; m < ins.Args.Count; m++)
                {
                    if (m == k) continue;
                    var o = ins.Args[m];
                    if (o.Kind == RegKind.Literal) continue;
                    if (!SingleLane(o)) continue;
                    // A single-lane cbuffer read is a scalar only when it is not one
                    // of the rows of this very array; `M[k].yyyy` is a row term.
                    if (o.Kind == RegKind.Cb && rowArgs.Contains(m)) continue;
                    if (!TryRowOrLane(ins.Args[k], ins.Args[0], term)) continue;
                    term.Vec = o;
                    return true;
                }
            }
            term = new RowTerm();
            return false;
        }

        /// <summary>
        /// `M[k]` -- or `M[k].xyw`, a projection of that row -- filled into
        /// <paramref name="into"/>.
        ///
        /// A DXBC swizzle is indexed by DESTINATION LANE, not by position: `r.zw = a.xy`
        /// compiles to `mov r0.zw, v0.xxxy`, where the two components that matter sit at
        /// index 2 and 3.  So the columns a row contributes are read at the destination's
        /// own lane letters -- which is also what <see cref="RemapForMask"/> does when
        /// the instruction is emitted.  Reading the leading letters instead turns
        /// `mad o3.zw, cb.xxxy` into a `(x, x)` projection and refuses a perfectly
        /// ordinary product.
        /// </summary>
        private bool TryRowOrLane(Operand o, Operand dst, RowTerm into) =>
            TryRowOrLane(o, dst, 0, into);

        /// <summary>
        /// <paramref name="width"/> &gt; 0 overrides the destination-driven column
        /// choice with the first <c>width</c> components of the row.  That is what a
        /// `dp3` needs: its destination is a scalar, so it cannot say which three
        /// columns the instruction multiplies -- DXBC takes the leading three of the
        /// swizzle, and the assembler pads the fourth.
        /// </summary>
        private bool TryRowOrLane(Operand o, Operand dst, int width, RowTerm into)
        {
            if (o == null || o.Kind != RegKind.Cb || o.Row < 0) return false;
            if (_lay.IsArray(o.Index)) return false;         // raw `cbN[i]`, not named
            string name = _lay.Row(o.Index, o.Row);
            if (string.IsNullOrEmpty(name)) return false;

            // The row reference may carry a swizzle (`unity_ObjectToWorld[1].xyzw`)
            // and may arrive as a lane constructor (`float4(_cb0_row2, ...)`); only the
            // plain `name[k]` form is a matrix row.
            int dot = name.IndexOf('.');
            if (dot > 0) name = name[..dot];
            if (!name.EndsWith("]", StringComparison.Ordinal)) return false;
            int br = name.LastIndexOf('[');
            if (br <= 0) return false;
            if (!int.TryParse(name[(br + 1)..^1], out int index)) return false;
            if (index < 0 || index > 3) return false;

            // The row's OWN swizzle, read at the destination's lane letters: absent or
            // `.xyzw` is the whole row, anything else is the projection the product
            // takes -- `M[k].xyw` for an `xyz` write, `M[k].w` for a scalar one.
            //
            // A repeated letter (`.wwww`) is a LEGAL column projection: fxc writes
            // `mul r0.x, v0.y, cb1[1].w` for the `.w` column of a matrix-vector
            // product, and `mul(M, v).w` compiles back to exactly that sum.  Whether
            // the operand is a row at all is decided by the shape of the instruction,
            // not by how many distinct letters its swizzle has.
            string sw = o.Swizzle?.TrimStart('.') ?? "";
            if (sw.Length == 0) sw = "xyzw";
            if (sw.Any(c => !"xyzw".Contains(c))) return false;
            // A one-letter swizzle replicates; anything short is padded the same way
            // the emitter pads it, so both agree on the letter pulled out.
            if (sw.Length < 4) sw = sw.PadRight(4, sw[^1]);
            string mask;
            if (width > 0)
            {
                mask = sw[..width];
            }
            else
            {
                string dm = DestMask(dst);
                if (dm.Length == 0) dm = "xyzw";
                mask = new string(dm.Select(c => sw["xyzw".IndexOf(c)]).ToArray());
            }
            if (mask == "xyzw") mask = "";

            into.Row = index;
            into.Matrix = name[..br];
            into.Mask = mask;
            return true;
        }

        /// <summary>How many components this destination writes (4 for a full write).</summary>
        private static int DestMaskLen(Operand dst)
        {
            string m = DestMask(dst);
            return m.Length == 0 ? 4 : m.Length;
        }

        /// <summary>Does this operand read exactly one lane (`.x`, `.wwww`)?</summary>
        private static bool SingleLane(Operand o)
        {
            string sw = o?.Swizzle?.TrimStart('.');
            return !string.IsNullOrEmpty(sw) && sw.All(c => c == sw[0]);
        }

        private static bool ReadsAccumulator(Operand o, int reg) =>
            o != null && o.Kind == RegKind.R && o.Index == reg;

        /// <summary>Is the last operand of this `mad` the accumulator the sum lives in?</summary>
        private static bool Accumulates(Instruction ins, int reg) =>
            ins.Args.Count >= 4 && ReadsAccumulator(ins.Args[^1], reg);

        /// <summary>
        /// The component a lane operand denotes, as a scalar expression.
        ///
        /// The swizzle is collapsed to its first letter: `v.wwww` is one value, and
        /// `float4(v.wwww, ...)` would not even type-check.
        /// </summary>
        private string Lane(Operand o)
        {
            string sw = o?.Swizzle?.TrimStart('.');
            if (string.IsNullOrEmpty(sw)) return null;
            return Expr(Clone(o, "." + sw[0]));
        }

        /// <summary>`r7.x` -> (7, 'x'), or null when it is not a plain temp lane.</summary>
        private static (int reg, char lane)? TempLane(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 4) return null;
            if (s[0] != 'r' || s[^2] != '.') return null;
            if (!int.TryParse(s[1..^2], out int reg)) return null;
            char lane = s[^1];
            if (!"xyzw".Contains(lane)) return null;
            return (reg, lane);
        }

        /// <summary>
        /// `M[1]*v.y + M[0]*v.x + ...` -- the product as one expression over the row
        /// array, for matrices that are not declared as `float4x4`.
        ///
        /// Written in the ORIGINAL term order: float addition is not associative, so
        /// reordering the rows into index order would change the last bits of every
        /// transformed vertex.  The same reason the `mul` form is only used where the
        /// compiler will do the same expansion.
        /// </summary>
        private static string ExpandedSum(string matrix, string mask, string[] comp,
                                          List<int> order, bool one)
        {
            string suffix = mask.Length == 0 ? "" : "." + mask;
            var sb = new StringBuilder();
            for (int k = 0; k < order.Count; k++)
            {
                string row = $"{matrix}[{order[k]}]{suffix}";
                string term = one && k == order.Count - 1 ? row : $"{row} * {comp[order[k]]}";
                if (k == 0) sb.Append(term);
                else sb.Append(" + ").Append(term);
            }
            return sb.ToString();
        }

        public static Result Emit(Module m, bool standalone = false)
        {
            var e = new HlslEmitter(m, standalone);
            e.Run();
            e._r.Hlsl = e._sb.ToString();
            e._r.Profile = m.Profile;
            return e._r;
        }

        private void Line(string s)
        {
            if (s.Length == 0) return;
            _sb.Append(new string(' ', _indent * 4)).Append(s).Append('\n');
        }

        private void Run()
        {
            _sb.Append("// translated from a DXBC disassembly by ShaderRipper\n");
            _sb.Append("// grammar -> AST -> HLSL, register level\n");
            _sb.Append("// profile: ").Append(_m.Profile).Append("\n\n");

            DeclareConstantBuffers();
            DeclareResources();
            DeclareClipPosHelper();
            EmitEntry();
        }

        /// <summary>
        /// Define <c>UnityObjectToClipPos</c> when the matrix fold used it.
        ///
        /// This route's standalone program declares the matrices as
        /// <c>float4 M[4]</c>, which <c>mul</c> does not accept, so the two
        /// multiplies are written over the rows the declaration has.  That is the
        /// same arithmetic as the macro in <c>UnityCG.cginc</c>; a file that carries
        /// the include gets the macro and never reaches this code.
        /// </summary>
        private void DeclareClipPosHelper()
        {
            if (!_needsClipPosHelper) return;
            _sb.Append(
                "// UnityObjectToClipPos, over the row-array declaration this file uses.\n" +
                "// A ShaderLab wrapper pulls in UnityCG.cginc, which already defines the\n" +
                "// macro; this guard keeps the two from colliding.\n" +
                "#ifndef UNITY_OBJECT_TO_CLIP_POS_DEFINED\n" +
                "#define UNITY_OBJECT_TO_CLIP_POS_DEFINED\n" +
                "float4 UnityObjectToClipPos(float4 pos)\n" +
                "{\n" +
                "    float4 p = float4(pos.xyz, 1.0);\n" +
                "    float4 w = float4(\n" +
                "        dot(p, float4(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x, unity_ObjectToWorld[3].x)),\n" +
                "        dot(p, float4(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y, unity_ObjectToWorld[3].y)),\n" +
                "        dot(p, float4(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z, unity_ObjectToWorld[3].z)),\n" +
                "        dot(p, float4(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w, unity_ObjectToWorld[3].w)));\n" +
                "    return float4(\n" +
                "        dot(w, float4(unity_MatrixVP[0].x, unity_MatrixVP[1].x, unity_MatrixVP[2].x, unity_MatrixVP[3].x)),\n" +
                "        dot(w, float4(unity_MatrixVP[0].y, unity_MatrixVP[1].y, unity_MatrixVP[2].y, unity_MatrixVP[3].y)),\n" +
                "        dot(w, float4(unity_MatrixVP[0].z, unity_MatrixVP[1].z, unity_MatrixVP[2].z, unity_MatrixVP[3].z)),\n" +
                "        dot(w, float4(unity_MatrixVP[0].w, unity_MatrixVP[1].w, unity_MatrixVP[2].w, unity_MatrixVP[3].w)));\n" +
                "}\n" +
                "#endif\n\n");
        }

        // ------------------------------------------------------------ declarations

        /// <summary>
        /// `dcl_constantbuffer CB1[21]` becomes a real cbuffer bound to b1, with
        /// the declared row count.  Binding it is what makes the emitted
        /// `cb1[3].xyzw` resolve at all: a bare global array compiles but is not a
        /// constant buffer, so its register index would mean nothing.
        /// </summary>
        private void DeclareConstantBuffers()
        {
            var cbs = _m.Decls.Where(d => d.Kind == "cbuffer")
                              .GroupBy(d => d.Register).Select(g => g.First())
                              .OrderBy(d => d.Register).ToList();
            foreach (var d in cbs)
            {
                int reg = d.Register;
                int extent = _lay.Extent.TryGetValue(reg, out int e) ? e : Math.Max(d.Count, 1);
                string name = CbLayout.BufferName(_m.Cb, reg);

                _sb.Append($"cbuffer {name} : register(b{reg})\n{{\n");
                if (_lay.Fallback.TryGetValue(reg, out string why))
                    _sb.Append($"    // {why}: kept as an array\n");
                if (_lay.IsArray(reg))
                {
                    _sb.Append($"    float4 cb{reg}[{extent}];\n}};\n\n");
                    continue;
                }
                foreach (var mem in _lay.For(reg))
                    _sb.Append(CbLayout.Line(mem)).Append('\n');
                _sb.Append("};\n\n");
            }
        }

        private void DeclareResources()
        {
            var textures = _m.Decls.Where(d => d.Kind == "texture")
                                   .GroupBy(d => d.Register).Select(g => g.First())
                                   .OrderBy(d => d.Register).ToList();
            // Unity binds a sampler to its texture by name: `SamplerState
            // sampler_<texture>` is how the compiler knows which resource a sample
            // refers to.  A bare `s0` is rejected outright ("Unrecognized sampler"),
            // so the pair is named from the resource reflection where it is known
            // and positionally where it is not.
            foreach (var d in textures)
            {
                string name = TexName(d.Register);
                if (d.Structured)
                {
                    // A structured resource is a byte-addressed record array.  A
                    // `StructuredBuffer<floatN>.Load(i)` can only reach element
                    // boundaries of its own element size, while DXBC's address is
                    // `index * stride + byteOffset` in bytes -- 12-byte records, or
                    // a 8-byte offset into a 16-byte one, are then unreachable.
                    // `ByteAddressBuffer` takes the byte address directly, so the
                    // address arithmetic stays exactly as the original wrote it.
                    _sb.Append($"ByteAddressBuffer {name} : register(t{d.Register});\n");
                    continue;
                }
                string ty = d.Dimension switch
                {
                    1 => "Texture1D", 3 => "Texture3D", 4 => "TextureCube",
                    // `dcl_resource_texture2dmsarray(N)` is BOTH multisampled and
                    // an array, so its type carries the sample count as well:
                    // a bare `Texture2DMSArray` does not compile.
                    5 => d.Ms ? $"Texture2DMSArray<float4, {Math.Max(d.Samples, 1)}>" : "Texture2DArray",
                    7 => "Buffer",
                    // A multisampled resource is a template in HLSL and has to be
                    // sized: `Texture2DMS<float4>` does not compile ("Unsized MSAA
                    // textures aren't supported on this target") even at 5.0.  The
                    // count comes from the declaration; 1 is the safe stand-in when
                    // the disassembly omitted it, since the count is only read
                    // through GetDimensions/sampleinfo in that case.
                    _ => d.Ms ? $"Texture2DMS<float4, {Math.Max(d.Samples, 1)}>" : "Texture2D",
                };
                _sb.Append($"{ty} {name} : register(t{d.Register});\n");
            }
            // A comparison sampler is a different type in HLSL than a filtering one,
            // and SampleCmp only exists on it.  Which registers need the comparison
            // form is known from the instruction list, so the declaration is right
            // the first time rather than patched afterwards.
            var compareSamplers = new HashSet<int>();
            foreach (var ins in _m.Code)
                if (ins.Opcode is "sample_c" or "sample_c_lz" && ins.Args.Count > 2)
                    compareSamplers.Add(ins.Args[2].Index);

            foreach (var d in textures)
            {
                // A structured buffer is not sampled; a SamplerState for it would be
                // an unused binding.
                if (d.Structured) continue;
                _sb.Append(compareSamplers.Contains(d.Register)
                    ? $"SamplerComparisonState sampler_{TexName(d.Register)} : register(s{d.Register});\n"
                    : $"SamplerState sampler_{TexName(d.Register)} : register(s{d.Register});\n");
            }
            if (textures.Count > 0) _sb.Append('\n');

            // dcl_immediateConstantBuffer is a literal table in the program (a blur
            // kernel, a dither matrix).  Its values are in the declaration, so the
            // table is emitted with them instead of approximated.
            if (_m.Icb.Count > 0)
            {
                // Not `const`: a const array indexed by a runtime value makes fxc
                // unroll the loop to fold the fetch, and it stops with X3504 the
                // moment the index goes past the table.  A static with the same
                // initializer is fetched at runtime, which is what the original
                // program did.
                _sb.Append($"static float4 _icb[{_m.Icb.Count}] =\n{{\n");
                foreach (var row in _m.Icb)
                {
                    var v = row.Length switch
                    {
                        1 => $"float4({Number(row[0])}, 0.0f, 0.0f, 0.0f)",
                        2 => $"float4({Number(row[0])}, {Number(row[1])}, 0.0f, 0.0f)",
                        3 => $"float4({Number(row[0])}, {Number(row[1])}, {Number(row[2])}, 0.0f)",
                        _ => $"float4({Number(row[0])}, {Number(row[1])}, {Number(row[2])}, {Number(row[3])})",
                    };
                    _sb.Append($"    {v},\n");
                }
                _sb.Append("};\n\n");
            }

            // Input registers become function locals bound to the struct fields when
            // the signatures say what those fields are; only a program without
            // signatures falls back to globals, which a real compiler treats as
            // constants and dead-code-eliminates.
            bool bound = (_m.Stage is "vs" ? _m.InputSig : _m.InputSig) is { Count: > 0 };
            if (!bound)
            {
                foreach (var d in _m.Decls.Where(x => x.Kind == "input")
                                          .GroupBy(x => x.Register).Select(g => g.First())
                                          .OrderBy(x => x.Register))
                    _sb.Append($"static float4 v{d.Register};\n");
                if (_m.Decls.Any(x => x.Kind == "input")) _sb.Append('\n');
            }
            // Indexable temps are real arrays whose length the declaration states.
            // A single `_x[512]` for every program was both oversized and shared
            // between what are distinct arrays (`x0`, `x1`), so element 0 of one
            // aliased element 0 of the other.
            foreach (var kv in _m.TempArrays.OrderBy(k => k.Key))
            {
                int len = Math.Max(kv.Value, 1);
                _sb.Append($"static float4 _x{kv.Key}[{len}];\n");
            }
            if (_m.TempArrays.Count > 0) _sb.Append('\n');
            else if (_m.Code.SelectMany(i => i.Args).Any(a => a.Kind == RegKind.X))
                _sb.Append("static float4 _x0[512];\n\n");
        }

        /// <summary>
        /// The entry point, shaped by the program's signature tables when it has
        /// them.
        ///
        /// Without signatures the program is register-level and the inputs are
        /// globals: it compiles, but a real compiler sees constants and deletes the
        /// whole body.  With them, the stage gets the appdata / interpolator struct
        /// its semantics describe and the body is wired to those fields, which is
        /// what makes the output a shader rather than a listing.
        /// </summary>
        private void EmitEntry()
        {
            bool vert = _m.Stage is "vs";
            _iface = new ShaderInterface();
            if (vert)
            {
                _iface.BuildAppdata(_m.InputSig);
                _iface.BuildV2f(_m.OutputSig, null);
            }
            else
            {
                _iface.BuildV2f(null, _m.InputSig);
            }

            bool hasIn = vert ? _iface.AppdataFields.Count > 0 : _iface.V2fFields.Count > 0;
            bool hasOut = vert ? _iface.V2fFields.Count > 0 : _m.OutputSig != null && _m.OutputSig.Count > 0;

            if (vert && _iface.AppdataFields.Count > 0)
                _sb.Append(ShaderInterface.StructText("appdata", _iface.AppdataFields));
            if (_iface.V2fFields.Count > 0)
                _sb.Append(ShaderInterface.StructText(vert ? "v2f" : "stage_in", _iface.V2fFields, renumber: vert));

            // The return type is whatever the stage actually hands over: the
            // interpolator struct from a vertex shader, a target from a fragment.
            string sig;
            if (vert)
                sig = _iface.V2fFields.Count > 0 ? "v2f vert(" + (hasIn ? "appdata v" : "") + ")"
                                                : "float4 vert(" + (hasIn ? "appdata v" : "") + ") : SV_Position";
            else
            {
                // A pixel program may write several render targets.  Returning a
                // single `float4 : SV_Target` throws the others away, and fxc then
                // eliminates the whole body as dead code -- the rebuilt program
                // outputs one constant target instead of the four the original
                // wrote.  With more than one output register the stage returns a
                // struct carrying one `SV_TargetN` field per register.
                var outs = _m.Decls.Where(d => d.Kind == "output")
                                   .GroupBy(d => d.Register).Select(g => g.First())
                                   .OrderBy(d => d.Register).ToList();
                foreach (var d in _m.Decls.Where(d => d.Kind == "output_special"))
                    if (!string.IsNullOrEmpty(d.Special) && !_specialOut.Contains(d.Special))
                        _specialOut.Add(d.Special);

                _targets = outs.Count > 1 ? outs : null;
                // A depth/cov write needs a struct to live in even when the colour
                // target is a single register: `SV_Depth` cannot be the return
                // value alongside `SV_Target`.
                if (_targets != null || _specialOut.Count > 0)
                {
                    _sb.Append("struct frag_out\n{\n");
                    foreach (var d in outs)
                        _sb.Append($"\tfloat4 target{d.Register} : SV_Target{d.Register};\n");
                    foreach (string s in _specialOut)
                        _sb.Append(s == "oMask"
                            ? "\tuint  mask : SV_Coverage;\n"
                            : "\tfloat depth : SV_Depth;\n");
                    _sb.Append("};\n");
                    sig = "frag_out frag(" + (hasIn ? "stage_in i" : "") + ")";
                }
                else sig = "float4 frag(" + (hasIn ? "stage_in i" : "") + ") : SV_Target";
            }

            _sb.Append(sig).Append("\n{\n");
            _indent = 1;

            // Inputs first: the body assumes vN exists and holds the register.
            if (hasIn)
            {
                var map = vert ? _iface.VsInReg : _iface.PsInReg;
                foreach (var line in _iface.BindInputs(map, vert ? "v" : "i", NeededInputComps))
                    Line(line.Trim());
                Line("");
            }

            // The struct is built in place at the `ret`, so it has to exist for the
            // whole function: an early return path assigns it too.
            if (vert && hasOut) Line("v2f o = (v2f)0;");

            _temps = Math.Max(_m.Temps,
                _m.Code.SelectMany(i => i.Args).Where(a => a.Kind == RegKind.R)
                      .Select(a => a.Index).DefaultIfEmpty(0).Max() + 1);
            // Every register stays a float4: the mask is a value, not a type, so
            // `1.0f` works on vectors where a `bool` declaration cannot.
            for (int i = 0; i < _temps; i++) Line($"float4 r{i} = 0.0;");
            // Integer work gets an integer variable to live in; see PlanIntRegisters.
            PlanIntRegisters(_m);
            foreach (int i in _intRegs.OrderBy(x => x)) Line($"int4 i{i} = 0;");
            foreach (var d in _m.Decls.Where(d => d.Kind == "output")
                                      .GroupBy(d => d.Register).Select(g => g.First())
                                      .OrderBy(d => d.Register))
                Line($"float4 o{d.Register} = 0.0;");
            foreach (string s in _specialOut)
                Line(s == "oMask" ? "uint oMask = 0u;" : "float oDepth = 0.0;");
            Line("");

            for (int pc = 0; pc < _m.Code.Count; pc++)
            {
                // A component the fold re-reads later but fxc overwrote in between is
                // captured here, at the last point it still held the row's value.
                if (_pre.TryGetValue(pc, out var pre))
                {
                    foreach (string one in pre) Line(one);
                    _pre.Remove(pc);
                }

                // A recognised matrix-vector expansion is emitted as the single
                // `mul(M, v)` it stands for; the four register instructions it
                // replaced are skipped.  See `_folded`.
                if (_folded.TryGetValue(pc, out var fold))
                {
                    foreach (string one in fold.Lines) Line(one);
                    _r.Instructions += fold.Consumed;
                    _r.Translated += fold.Consumed;
                    continue;
                }
                if (_skip.Contains(pc)) continue;

                var ins = _m.Code[pc];
                _pc = pc;
                _intNow = _intSync.Length > pc ? _intSync[pc] : null;
                _r.Instructions++;
                int at = _sb.Length;
                bool ok = Emit(ins);
                // `_sat` is a result modifier, not an instruction: fxc spells it
                // `mul_sat`, HLSL spells it `saturate(mul(...))`.  When the
                // emission was a single assignment line, wrap its right-hand
                // side; block-shaped emissions (if/loops) are left alone.
                if (ok && ins.Saturate)
                {
                    string written = _sb.ToString(at, _sb.Length - at);
                    // The captured leading whitespace is the line's *full*
                    // indentation -- `Line` put it there.  Carrying it into the
                    // replacement and letting `Line` indent again doubles it,
                    // which is how these lines ended up four columns deeper than
                    // their neighbours.
                    var mm = Regex.Match(written, @"^(\s*)([^=\n]+)= (.+);\r?\n$");
                    if (mm.Success)
                    {
                        _sb.Length = at;
                        Line($"{mm.Groups[2].Value}= saturate({mm.Groups[3].Value});");
                    }
                }
                if (ok) _r.Translated++;
                else
                {
                    _r.Todo++;
                    if (!_r.Missing.Contains(ins.Opcode)) _r.Missing.Add(ins.Opcode);
                    if (_sb.Length == at) Line("// TODO " + ins.Raw);
                }
            }

            // `ret` already wrote the return; only a program whose last instruction
            // is not one needs it here.
            if (_m.Code.Count == 0 || _m.Code[^1].Opcode != "ret")
            {
                _sb.Append('\n');
                ReturnStatement();
            }
            _sb.Append("}\n");
        }

        /// <summary>
        /// The return this stage owes its caller: the interpolator struct filled from
        /// the oN registers for a vertex program, the target register for a fragment.
        /// Repeatable, because a program can return from more than one place.
        /// </summary>
        private void ReturnStatement()
        {
            bool vert = _m.Stage is "vs";
            if (vert && _iface != null && _iface.VsOutReg.Count > 0)
            {
                foreach (var line in _iface.BindOutputs(_iface.VsOutReg, "o")) Line(line.Trim());
                Line("return o;");
                return;
            }
            if (_targets != null || _specialOut.Count > 0)
            {
                Line("frag_out result;");
                if (_targets != null)
                    foreach (var d in _targets) Line($"result.target{d.Register} = o{d.Register};");
                else
                    Line("result.target0 = " + ReturnValue() + ";");
                foreach (string s in _specialOut)
                    Line(s == "oMask" ? "result.mask = oMask;" : "result.depth = oDepth;");
                Line("return result;");
                return;
            }
            Line("return " + ReturnValue() + ";");
        }

        /// <summary>
        /// How many components the body reads from an input register, so a local can
        /// be wide enough for a read that goes past the declaration mask.
        /// </summary>
        private int NeededInputComps(int reg)
        {
            int n = 0;
            foreach (var ins in _m.Code)
                foreach (var a in ins.Args)
                    if (a.Kind == RegKind.V && a.Index == reg)
                        n = Math.Max(n, SwizzleWidth(a.Swizzle));
            return n;
        }

        private static int SwizzleWidth(string sw)
        {
            if (string.IsNullOrEmpty(sw)) return 4;
            return Math.Min(4, sw.TrimStart('.').Length);
        }

        private string ReturnValue()
        {
            if (_m.Decls.Any(d => d.Kind == "output" && d.Register == 0)) return "o0";
            var last = _m.Code.LastOrDefault(i => i.Args.Count > 0 && i.Args[0].Kind == RegKind.O);
            return last != null ? $"o{last.Args[0].Index}" : "r0";
        }

        // --------------------------------------------------------------- operands

        private string Expr(Operand o, int comps = 0)
        {
            string core = Core(o);
            if (core == null) return null;
            string sw = o.Swizzle;

            if (comps > 0)
            {
                // A dot over N components must see exactly N: the disassembly
                // spells four-component masks (`v1.xyzx`) even for dp3, and
                // feeding the trailing component to `dot` turns fxc's dp3 into
                // a dp4 whose fourth product changes the value.
                if (sw == null)
                {
                    if (o.Kind is RegKind.R or RegKind.V or RegKind.Cb or RegKind.X or RegKind.Icb)
                        sw = ".xyzw";
                }
                else if (sw[0] == '.' && sw.Length > 1 + comps)
                {
                    sw = sw.Substring(0, 1 + comps).PadRight(1 + comps, sw[sw.Length - 1]);
                }
            }

            // A constant with a swizzle is a NARROWER constant: `l(0, 0.5, 0.5, 0).yz`
            // is `float2(0.5, 0.5)`, and a replicated one -- `l(1, 1, 1, 1).y` -- is
            // just `1.0f`.  Writing the components that are actually read keeps the
            // operand's width equal to the swizzle's, which is where the
            // `float4(...).y` shapes came from.
            if (o.Kind == RegKind.Literal && o.Lit is { Length: > 0 })
            {
                core = LiteralSlice(o, sw, comps);
                sw = null;
            }
            else if (o.Kind == RegKind.Cb && !_lay.IsArray(o.Index) && sw is { Length: > 1 })
            {
                // A row that holds several separate properties is declared lane by
                // lane, so `float4(_A, _B, _C, _D).yz` is really `float2(_B, _C)`.
                string lanes = sw.TrimStart('.');
                string slice = _lay.Slice(o.Index, Math.Max(o.Row, 0), lanes);
                if (slice != null)
                {
                    core = slice;
                    sw = null;
                }
            }

            if (sw != null) core += sw;

            if (o.Abs) core = $"abs({core})";
            if (o.Negate) core = "-" + core;
            return core;
        }

        /// <summary>
        /// A constant operand narrowed to the components the swizzle reads.
        ///
        /// Returns the constructor itself (`float4(...)`) when nothing is gained,
        /// which is the case when the whole vector is read.
        /// </summary>
        private string LiteralSlice(Operand o, string sw, int comps)
        {
            string full = Literal(o);
            string litSw = (sw ?? "").TrimStart('.');
            if (litSw.Length == 0 || o.Lit == null || o.Lit.Length == 0) return full;

            var want = new List<int>();
            if (litSw.All(c => c == litSw[0]))
            {
                // A replicated swizzle is one value: a scalar covers every width,
                // and a wider context gets the splat spelled out.
                int idx = "xyzw".IndexOf(litSw[0]);
                if (idx < 0) return full;
                string one = LitNum(o, idx);
                if (comps <= 1) return one;
                return $"float{comps}({string.Join(", ", Enumerable.Repeat(one, comps))})";
            }
            foreach (char c in litSw) want.Add("xyzw".IndexOf(c));
            if (comps > 0 && want.Count > comps) want = want.GetRange(0, comps);
            if (want.Count == 0 || want.Count >= o.Lit.Length) return full;
            if (want.Any(i => i < 0)) return full;

            var parts = want.Select(i => LitNum(o, i)).ToArray();
            return parts.Length == 1 ? parts[0] : $"float{parts.Length}({string.Join(", ", parts)})";
        }

        /// <summary>The register an operand names, with no swizzle or modifier.</summary>
        private string Core(Operand o)
        {
            return o.Kind switch
            {
                RegKind.R => $"r{o.Index}",
                RegKind.V => $"v{o.Index}",
                RegKind.O => $"o{o.Index}",
                // `oDepth` / `oMask`: the local the return statement copies into the
                // SV_Depth / SV_Coverage field.
                RegKind.OSpecial => o.Special ?? "oDepth",
                RegKind.Cb => CbCore(o),
                RegKind.Null => "float4(0, 0, 0, 0)",
                RegKind.Literal => Literal(o),
                // Indexable temps: x0[a0.x] indexes an array of float4.
                RegKind.X => IndexableTemp(o),
                RegKind.Icb => IcbRef(o),
                _ => null,
            };
        }

        /// <summary>
        /// One element of an indexable temp array (<c>x0[3]</c>,
        /// <c>x0[r2.z + 0]</c>).
        ///
        /// The constant form is what a baked lookup table is written with, so
        /// ignoring the bracket would collapse every write into element 0.  The
        /// dynamic index is an integer held in a float register and is read back with
        /// <c>asint</c>; the array length comes from the declaration, so fxc can prove
        /// the index in range.
        /// </summary>
        private string IndexableTemp(Operand o)
        {
            string arr = $"_x{o.Index}";
            string raw = o.IndexRaw;
            if (string.IsNullOrEmpty(raw))
            {
                // No bracket at all: the whole array, which HLSL cannot express as a
                // value.  Element 0 is the only sane reading, and no compiler emits
                // this form.
                return $"{arr}[0]";
            }
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int k))
                return $"{arr}[{k}]";

            var m = Regex.Match(raw, @"^(\w+)\.([xyzw])(?:\s*\+\s*(\d+))?$");
            if (m.Success)
            {
                string off = m.Groups[3].Success ? " + " + m.Groups[3].Value : "";
                return $"{arr}[{IntLane(m.Groups[1].Value, m.Groups[2].Value[0])}{off}]";
            }
            return $"{arr}[0]";
        }

        /// <summary>The identifier of a texture: the name the shader's own reflection gives
        /// it, or a positional one when the asset did not record it.
        /// </summary>
        private string TexName(int reg)
        {
            if (_m.Cb != null && _m.Cb.TexName.TryGetValue(reg, out var n) && !string.IsNullOrEmpty(n))
            {
                var sb = new System.Text.StringBuilder(n.Length + 1);
                foreach (char c in n) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
                if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
                if (sb.Length > 0) return sb.ToString();
            }
            return "t" + reg;
        }

        /// <summary>
        /// `icb[r0.w + 0].xyzx` reads the baked table at a computed index.  The
        /// index is an integer kept in a register (the register holds floats), so
        /// it is read back with <c>asint</c>; a plain numeric index is used as is.
        /// </summary>
        private string IcbRef(Operand o)
        {
            if (_m.Icb.Count == 0) return null;
            string raw = o.Raw ?? "";
            int a = raw.IndexOf('['), b = raw.IndexOf(']');
            if (a < 0 || b <= a) return null;
            string idx = raw.Substring(a + 1, b - a - 1).Trim();

            // The index is taken modulo the table size.  fxc unrolls a loop whose
            // body indexes a small table and rejects the whole file (X3504) the
            // moment its counter walks past the end -- even though the program's own
            // break branch makes that impossible at run time.  A modulo makes the
            // index range provable, and since every index the original program can
            // reach is already inside the table, it changes nothing it can execute.
            int n = _m.Icb.Count;
            var m = System.Text.RegularExpressions.Regex.Match(idx, @"^(\w+)\.([xyzw])(?:\s*\+\s*(\d+))?$");
            if (m.Success)
            {
                string off = m.Groups[3].Success ? " + " + m.Groups[3].Value : "";
                // abs() as well as the modulo: `%` keeps the sign of a negative
                // operand, and a negative index is out of bounds again.  Every index
                // the program can actually reach is non-negative, so this changes
                // nothing it executes.
                string e = $"abs({IntLane(m.Groups[1].Value, m.Groups[2].Value[0])}{off})";
                return n == 1 ? "_icb[0]" : $"_icb[{e} % {n}]";
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(idx, @"^\d+$"))
                return n == 1 ? "_icb[0]" : $"_icb[({idx}) % {n}]";
            return null;
        }

        /// <summary>
        /// How to spell a constant-buffer read.  Three cases, in the order the
        /// declaration allows: the raw array when the buffer kept that shape, the
        /// member the resolver named, and the positional member the declaration
        /// emitted for a row nothing pinned.
        ///
        /// A row that holds several scalars is named by a <c>float4(...)</c>
        /// constructor of those scalars, which keeps one answer per row and still
        /// lets a swizzle be applied to it -- `float4(a, b, c, d).zw` is a value,
        /// just like `_Color.zw`.
        /// </summary>
        private string CbCore(Operand o)
        {
            int row = Math.Max(o.Row, 0);
            if (_lay.IsArray(o.Index)) return $"cb{o.Index}[{CbIndex(o, row)}]";
            return _lay.Row(o.Index, row) ?? $"_cb{o.Index}_row{row}";
        }

        /// <summary>
        /// The subscript of a raw constant-buffer array read.
        ///
        /// `cb1[r1.x + 25]` is a RUNTIME row: the register holds an integer bit
        /// pattern, so it is read back with `asint` and the immediate offset is
        /// kept.  Reading only the literal 25 (which is what treating the bracket
        /// as a constant row did) pinned a stereo matrix read to one eye and
        /// silently dropped the other.
        /// </summary>
        private string CbIndex(Operand o, int fallback)
        {
            string raw = o.IndexRaw;
            if (string.IsNullOrEmpty(raw)) return fallback.ToString(CultureInfo.InvariantCulture);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int k))
                return k.ToString(CultureInfo.InvariantCulture);

            var m = Regex.Match(raw, @"^(\w+)\.([xyzw])(?:\s*\+\s*(\d+))?$");
            if (m.Success)
            {
                string off = m.Groups[3].Success ? " + " + m.Groups[3].Value : "";
                return $"{IntLane(m.Groups[1].Value, m.Groups[2].Value[0])}{off}";
            }
            return fallback.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A register lane read as an integer, honouring the integer shadow.
        ///
        /// The row subscript of `cb1[r1.y + 25]` and the index of `icb[r0.w]` are
        /// integers the register holds as bits.  When those bits live in an `int4`
        /// companion the lane is named directly (`i1.y`); otherwise it has to be
        /// reinterpreted, which is exact for a runtime value but is folded to 0 when
        /// the value is a compile-time constant (see <see cref="_intRegs"/>).
        /// </summary>
        private string IntLane(string regText, char lane)
        {
            if (regText.Length > 1 && regText[0] == 'r' &&
                int.TryParse(regText[1..], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out int reg) &&
                _intNow != null && reg < _intNow.Length && _intRegs.Contains(reg) &&
                (_intNow[reg] & (1 << "xyzw".IndexOf(lane))) != 0)
                return $"i{reg}.{lane}";
            return $"asint({regText}.{lane})";
        }

        /// <summary>
        /// A scalar read of an operand: its first component.
        ///
        /// `cos(r0.xyxx)` is a float4 and makes `float4(...)` a five-argument
        /// constructor (X3014), so a swizzled operand is narrowed to its first
        /// lane rather than handed over whole.
        /// </summary>
        private string Scalar(Operand o)
        {
            string core = Core(o);
            if (core == null) return null;

            // A literal has no components to swizzle: `-1.0f.x` is not HLSL, and
            // the scalar reading of a literal is simply its component 0.
            if (o.Kind == RegKind.Literal)
            {
                var lit = o.Lit ?? Array.Empty<float>();
                string one = Number(lit.Length > 0 ? lit[0] : 0f);
                return o.Negate ? "-" + one : one;
            }

            // The SWIZZLE token carries its own dot (`.xyxx`), so the component
            // is the first character after it, not the first character of it.
            string sw = o.Swizzle?.TrimStart('.') ?? "";
            string comp = sw.Length > 0 ? sw[0].ToString() : "x";

            // A lane of a lane-packed row is the property that sits in it:
            // `float4(_A, _B, _C, _D).y` is `_B`, not a constructor with one
            // component taken off it.
            string lane = null;
            if (o.Kind == RegKind.Cb && !_lay.IsArray(o.Index))
                lane = _lay.Slice(o.Index, Math.Max(o.Row, 0), comp);
            string e = lane ?? core + "." + comp;
            if (o.Abs) e = $"abs({e})";
            if (o.Negate) e = "-" + e;
            return e;
        }

        /// <summary>A float literal as HLSL wants it: `1.0f`, never `1f`.</summary>
        private static string Number(float f)
        {
            string s = f.ToString("R", CultureInfo.InvariantCulture);
            if (!s.Contains('.') && !s.Contains('e') && !s.Contains('E')) s += ".0";
            return s + "f";
        }

        private string Literal(Operand o)
        {
            var v = o.Lit ?? Array.Empty<float>();

            // An INTEGER literal is printed without a decimal point (`l(1)`), and its
            // bytes are the integer 1, not the float 1.0.  In a float context that is
            // `asfloat(1)` -- emitting `1.0f` writes 0x3F800000 where the program
            // meant 0x00000001, and a loop counter then reads as 1077936128.
            if (o.IntLit && o.LitBits is { Length: > 0 } bits && bits.Any(b => b != 0))
            {
                string B(int i) => unchecked((int)bits[i]).ToString(CultureInfo.InvariantCulture);
                int n = Math.Min(v.Length, bits.Length);
                return n switch
                {
                    1 => $"asfloat({B(0)})",
                    2 => $"asfloat(int2({B(0)}, {B(1)}))",
                    3 => $"asfloat(int3({B(0)}, {B(1)}, {B(2)}))",
                    _ => $"asfloat(int4({B(0)}, {B(1)}, {B(2)}, {B(3)}))",
                };
            }

            string N(float f) => Number(f);
            return v.Length switch
            {
                1 => N(v[0]),
                2 => $"float2({LitNum(o, 0)}, {LitNum(o, 1)})",
                3 => $"float3({LitNum(o, 0)}, {LitNum(o, 1)}, {LitNum(o, 2)})",
                _ => $"float4({LitNum(o, 0)}, {LitNum(o, 1)}, {LitNum(o, 2)}, {LitNum(o, 3)})",
            };
        }

        /// <summary>
        /// One component of a literal as a FLOAT expression.
        ///
        /// For an integer literal that is `asfloat(n)`: the program means the 32-bit
        /// pattern of the integer, and `3.0f` is a different pattern.
        /// </summary>
        private static string LitNum(Operand o, int i)
        {
            if (o.IntLit && o.LitBits is { Length: > 0 } bits && i < bits.Length)
            {
                int bitsI = unchecked((int)bits[i]);
                return bitsI == 0
                    ? "0.0f"
                    : $"asfloat({bitsI.ToString(CultureInfo.InvariantCulture)})";
            }
            float f = o.Lit != null && i < o.Lit.Length ? o.Lit[i] : 0f;
            return Number(f);
        }

        // ------------------------------------------------------------- statements

        private static readonly Dictionary<string, string> Unary = new(StringComparer.Ordinal)
        {
            ["frc"] = "frac",
            ["exp"] = "exp2",
            ["log"] = "log2",
            ["rsq"] = "rsqrt",
            ["sqrt"] = "sqrt",
            ["round_ne"] = "round",
            ["round_ni"] = "floor",
            ["round_pi"] = "ceil",
            ["round_z"] = "trunc",
        };

        private static readonly Dictionary<string, string> IntArith = new(StringComparer.Ordinal)
        {
            ["iadd"] = "+", ["isub"] = "-", ["imul"] = "*",
        };

        private static readonly Dictionary<string, string> IntCompare = new(StringComparer.Ordinal)
        {
            ["ige"] = ">=", ["ilt"] = "<", ["ieq"] = "==", ["ine"] = "!=",
            ["uge"] = ">=", ["ult"] = "<",
        };

        /// <summary>The block punctuation: no destination, no operands.</summary>
        private bool EmitNoOperand(string opcode)
        {
            switch (opcode)
            {
                case "nop":
                    return true;
                case "ret":
                    ReturnStatement();
                    return true;
                case "discard":
                    Line("discard;");
                    return true;
                case "else":
                    _indent--;
                    Line("}");
                    Line("else");
                    Line("{");
                    _indent++;
                    return true;
                case "endif":
                    _indent--;
                    Line("}");
                    return true;
                case "loop":
                    // A bounded `for` rather than `while (true)`: fxc proves some
                    // of these loops non-terminating (an `ige` on raw float bits
                    // can wrap past its bound) and refuses to compile them at all
                    // (X4029).  The bound does not change any loop that exits.
                    // `[loop]` keeps fxc from unrolling: without it the constant
                    // bound invites a full unroll, and a table fetch inside the
                    // body then trips X3504 ("literal loop terminated early") the
                    // moment the unroller walks past the table's end.
                    Line("[loop]");
                    Line($"for (int _guard{_loops} = 0; _guard{_loops} < 4096; ++_guard{_loops})");
                    _loops++;
                    Line("{");
                    _indent++; _r.Blocks++;
                    return true;
                case "endloop":
                    _indent--;
                    Line("}");
                    return true;
                case "endswitch":
                    _indent--;
                    Line("}");
                    if (_switches.Count > 0) _switches.Pop();
                    return true;
                case "default":
                    // A bare label: HLSL's `default:` takes no indentation change,
                    // and fall-through from the previous case stays as written.
                    Line("default:");
                    return true;
                case "break":
                    Line("break;");
                    return true;
                case "continue":
                    Line("continue;");
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Opcodes whose result is componentwise, so a partial
        /// destination mask selects the same slots on every source operand.</summary>
        private static readonly HashSet<string> Componentwise = new(StringComparer.Ordinal)
        {
            "add", "sub", "mul", "div", "mad", "min", "max", "mov", "movc",
            "iadd", "imul", "imad", "iumin", "iumax", "imin", "imax",
            "ieq", "ine", "ilt", "ige", "ult", "uge", "eq", "ne", "lt", "le", "gt", "ge",
            "and", "or", "xor", "ishl", "ishr", "ushr",
        };

        /// <summary>Components a destination assignment writes (4 when unmasked).</summary>
        private static int DestWidth(Operand dst)
        {
            string m = DestMask(dst);
            return m.Length == 0 ? 4 : m.Length;
        }

        private static string DestMask(Operand dst)
        {
            // The SWIZZLE token carries its own dot, so a full `.xyzw` mask is five
            // characters and a three-component `.xzw` is four.  Comparing the raw
            // length treated `.xzw` as "no mask", and the sources were then left
            // un-remapped: `mul r1.xzw, r0.xxwy, l(...)` wrote r0's x,y,w into
            // r1's x,z,w, while DXBC pairs them positionally -- x with x, z with y,
            // w with w.  Reading the letters after the dot is what makes the two
            // agree.
            string sw = dst.Swizzle;
            if (string.IsNullOrEmpty(sw) || sw[0] != '.') return "";
            string mask = sw[1..];
            return mask.Length >= 4 ? "" : mask;
        }

        /// <summary>
        /// Re-index an operand through the destination mask: DXBC
        /// `div o4.zw, l(0,0,1,1), r0.xxxy` writes slots z,w -- 1/r0.x, 1/r0.y --
        /// while a naive HLSL `o4.zw = l / r0.xxxy` truncates the right side to
        /// x,y and divides by the wrong components.  Remapping every operand's
        /// swizzle through the mask pairs the assignment component-for-component.
        /// </summary>
        private static Operand RemapForMask(Operand o, string mask)
        {
            // A scalar literal already broadcasts to every slot; remapping would
            // spell `0.5f.z`, which is not HLSL.
            if (o.Lit is { Length: <= 1 }) return o;
            string src = (o.Swizzle ?? ".xyzw")[1..];   // strip the leading dot
            if (!System.Text.RegularExpressions.Regex.IsMatch(src, @"^[xyzw]{1,4}$")) return o;
            if (src.Length < 4) src = src.PadRight(4, src[^1]);
            var clone = new Operand
            {
                Kind = o.Kind, Index = o.Index, Row = o.Row,
                Swizzle = "." + string.Concat(mask.Select(c => src["xyzw".IndexOf(c)])),
                Negate = o.Negate, Abs = o.Abs, Lit = o.Lit, Raw = o.Raw,
                LitBits = o.LitBits, IntLit = o.IntLit,
            };
            return clone;
        }

        /// <summary>
        /// A copy of an operand with a different swizzle, modifiers kept.
        /// </summary>
        private static Operand Clone(Operand o, string swizzle) => new()
        {
            Kind = o.Kind, Index = o.Index, Row = o.Row, Special = o.Special,
            Swizzle = swizzle, Negate = o.Negate, Abs = o.Abs, Lit = o.Lit, Raw = o.Raw,
            IndexRaw = o.IndexRaw, LitBits = o.LitBits, IntLit = o.IntLit,
        };

        /// <summary>
        /// The condition of a flow-control instruction, as an HLSL expression that
        /// fxc turns back into a single `if_nz` / `breakc_nz`.
        ///
        /// DXBC's <c>_nz</c> / <c>_z</c> tests are <c>any(operand != 0)</c> over the
        /// components the operand names, not a read of <c>.x</c>: a program tests a
        /// packed pair far more often than a single lane, and looking only at x
        /// silently takes the wrong branch.  The scalar form is the bare register
        /// rather than <c>x != 0.0</c>, because HLSL's implicit conversion already
        /// means "non-zero" and spelling the compare out makes fxc emit an extra `ne`
        /// before every branch.
        /// </summary>
        private string Any(Operand o)
        {
            string mask = o.Swizzle?.TrimStart('.') ?? "";
            if (mask.Length == 1) return Expr(o);
            // A one-component literal is already scalar; `any` on a bool scalar is
            // not HLSL.
            if (o.Kind == RegKind.Literal && (o.Lit?.Length ?? 0) <= 1)
                return Expr(o);
            if (mask.Length == 0)
            {
                // No swizzle: the operand is the whole register, and DXBC's test is
                // over all four components.
                string core = Core(o);
                if (core == null) return Scalar(o);
                string e = core;
                if (o.Abs) e = $"abs({e})";
                if (o.Negate) e = "-" + e;
                return $"any({e})";
            }
            return $"any({Expr(o)})";
        }

        /// <summary>
        /// A comparison instruction.
        ///
        /// Two output shapes, chosen by what the destination register IS:
        ///   * a bool register -> `r0 = a &lt; b;` (the comparison directly);
        ///   * anything else -> the DXBC bit pattern, because the program reads it
        ///     arithmetically and -1 vs 1 is the difference between the right ramp
        ///     and its inverse.
        /// </summary>
        private bool Compare(Instruction ins, string d, List<Operand> a, string op)
        {
            if (a.Count < 3) return false;
            Operand dst = a[0];
            string exp = $"({Expr(a[1])} {op} {Expr(a[2])})";

            // The mask constant depends on what this lane MEANS.  A boolean-only
            // lane gets 1.0f -- the same truth value without the 0xFFFFFFFF NaN --
            // while a lane that is also read as a number keeps the -1 bit pattern so
            // `iadd`/`itof` chains see the sign the original had.
            Line($"{d} = {exp} ? {MaskFor(dst, ins)} : 0.0;");
            return true;
        }

        /// <summary>
        /// A bitwise op: logical when both sides are bool registers, bit arithmetic
        /// otherwise.  `<paramref name="logic"/>` is the HLSL operator for the bool
        /// case (`&amp;&amp;`, `||`, `!=` for xor).
        /// </summary>
        private bool Bitwise(Instruction ins, string d, List<Operand> a, string logic)
        {
            if (a.Count < 3) return false;
            string sym = ins.Opcode switch { "and" => "&", "or" => "|", _ => "^" };
            Operand dst = a[0];

            // `asuint(a) | asuint(b)` is a LOGICAL or only when both sides are
            // comparison results.  When either is an ordinary number -- the merge
            // shader ORs `rsqrt(len)` with a mask -- the instruction is a real
            // bitwise operation on those bits, and turning it into `||` would
            // discard the number entirely.
            if (dst.Kind == RegKind.R && IsMaskLane(a[1]) && IsMaskLane(a[2]))
            {
                Line($"{d} = ({BoolExpr(a[1])} {logic} {BoolExpr(a[2])}) ? {MaskFor(dst)} : 0.0;");
                return true;
            }

            // `mask & x` is a masked select, not bit arithmetic: every bit of the
            // mask is 1, so the result is x where the mask is true and 0 where it is
            // false.  Spelling it as `asfloat(0xFFFFFFFFu & asuint(x))` compiles to
            // the same thing but reads like a NaN factory.
            if (ins.Opcode == "and" &&
                (MaskShape(a[1]) != null || MaskShape(a[2]) != null))
            {
                Operand mOp = MaskShape(a[1]) != null ? a[1] : a[2];
                Operand val = mOp == a[1] ? a[2] : a[1];
                Line($"{d} = ({Expr(mOp)} != 0.0) ? {Expr(val)} : 0.0;");
                return true;
            }

            IntResult(ins, $"{AsUInt(a[1])} {sym} {AsUInt(a[2])}", unsigned: true);
            return true;
        }

        /// <summary>
        /// Is this operand a 0/±1 MASK value (produced by a comparison) rather than
        /// an arbitrary float?
        ///
        /// `asuint(a) | asuint(b)` is only a logical or when both sides are masks.
        /// When one side is `rsqrt(...)` -- an ordinary number -- the OR is a real
        /// bitwise operation on its bit pattern, and that is what the original
        /// instruction did.  Telling them apart is what stops the translator from
        /// turning an arithmetic OR into `a || b`.
        /// </summary>
        private bool IsMaskLane(Operand o)
        {
            if (o.Kind != RegKind.R) return false;
            string mask = o.Swizzle?.TrimStart('.') ?? "";
            if (mask.Length == 0) mask = "xyzw";
            return mask.All(c => _maskLanes.Contains((o.Index, c)));
        }

        private bool Not(Instruction ins, string d, List<Operand> a)
        {
            if (a.Count < 2) return false;
            if (IsBool(a[0]) && IsBool(a[1]))
            {
                Line($"{d} = !{BoolExpr(a[1])};");
                return true;
            }
            IntResult(ins, $"~{AsUInt(a[1])}", unsigned: true);
            return true;
        }

        /// <summary>
        /// A bool operand as an HLSL boolean expression.
        ///
        /// A lane that is a pure mask is used directly; anything else is compared
        /// against zero, which is the same truth value without assuming a type.
        /// </summary>
        private string BoolExpr(Operand o) =>
            IsBool(o) ? Expr(o) : $"{Scalar(o)} != 0.0";

        /// <summary>Emit one instruction; false when there is no handler.</summary>
        private bool Emit(Instruction ins)
        {
            List<Operand> a = new List<Operand>(ins.Args);
            // Instructions that carry no operand are the block punctuation; they
            // have to be handled before any destination is looked at.
            if (a.Count == 0) return EmitNoOperand(ins.Opcode);

            // A `null` destination is normally a discard mask: there is nothing to
            // assign.  `sincos` is the exception -- `sincos null, r3.yw, src` means
            // "write only the sine", and a null first operand is how fxc splits one
            // rotation across two instructions.  Returning here skipped that sine
            // entirely, leaving the next instruction reading a lane nothing wrote.
            if (a[0].Kind == RegKind.Null && ins.Opcode != "sincos") return true;

            if (Componentwise.Contains(ins.Opcode))
            {
                string m = DestMask(a[0]);
                if (m.Length > 0)
                    a = a.Select((o, i) => i == 0 ? o : RemapForMask(o, m)).ToList();
            }

            string d = Expr(a[0]);
            if (d == null) return false;

            switch (ins.Opcode)
            {
                // ---- switch: DXBC spells it `switch <cond>` / `case l(n)` /
                // `default` / `break` / `endswitch`.  HLSL's `switch` takes an
                // integer, so the condition (already an `ftou` result) is read back
                // with `asuint` and the case labels are integer literals.
                case "switch":
                    {
                        var st = new SwitchState { Subject = $"_sw{_switchSeq++}" };
                        _switches.Push(st);
                        Line($"uint {st.Subject} = asuint({Scalar(a[0])});");
                        Line($"switch ({st.Subject})");
                        Line("{");
                        _indent++; _r.Blocks++;
                    }
                    return true;
                case "case":
                    {
                        // `case l(0)`: the disassembler prints the label as a plain
                        // number, so the value is the literal itself.  A label whose
                        // text was hexadecimal is a raw bit pattern instead (that is
                        // how `0xFFFFFFFF` survives -- it parses as a NaN, which is
                        // not an integer in range).
                        string label = "0u";
                        if (a.Count > 0 && a[0].Lit is { Length: > 0 })
                        {
                            float v = a[0].Lit[0];
                            bool integral = v >= 0f && v <= 4294967295f && MathF.Floor(v) == v;
                            label = integral
                                ? ((uint)v).ToString(CultureInfo.InvariantCulture) + "u"
                                : BitConverter.SingleToUInt32Bits(v).ToString(CultureInfo.InvariantCulture) + "u";
                        }
                        else if (a.Count > 0)
                            label = $"asuint({Scalar(a[0])})";
                        Line($"case {label}:");
                    }
                    return true;

                // ---- control flow: the tree supplies the condition, the emitter
                // spells out the block structure.
                case "discard_nz":
                    Line($"if ({Any(a[0])}) discard;");
                    return true;

                case "if_z":
                    Line($"if (!{Any(a[0])})");
                    Line("{");
                    _indent++; _r.Blocks++;
                    return true;
                case "if_nz":
                    Line($"if ({Any(a[0])})");
                    Line("{");
                    _indent++; _r.Blocks++;
                    return true;
                case "breakc_nz":
                    Line($"if ({Any(a[0])}) break;");
                    return true;
                case "breakc_z":
                    Line($"if (!{Any(a[0])}) break;");
                    return true;
                case "continuec_nz":
                    Line($"if ({Any(a[0])}) continue;");
                    return true;
                case "continuec_z":
                    Line($"if (!{Any(a[0])}) continue;");
                    return true;

                case "mov":
                    {
                        // An integer copied lane to lane stays an integer: the shadow
                        // is written instead of the bits being reinterpreted later.
                        if (a[0].Kind == RegKind.R && IntMoveCarry(ins, _intNow) != 0)
                        {
                            string mask0 = DestMask(a[0]);
                            int width0 = string.IsNullOrEmpty(mask0) ? 4 : mask0.Length;
                            string moved = a[1].Kind == RegKind.Literal
                                ? IntLiteralText(a[1], width0)
                                : IntShadow(a[1], false);
                            if (moved != null)
                            {
                                IntResult(ins, moved);
                                return true;
                            }
                        }
                        Line($"{d} = {Expr(a[1])};");
                    }
                    return true;
                case "movc":
                    // movc dst, src0, src1, src2 -> dst = src0 != 0 ? src1 : src2
                    //
                    // The condition is handed over as the bare expression: a float
                    // converts to bool implicitly, and spelling `!= 0.0` makes fxc
                    // emit an extra `ne` before every select.
                    Line($"{d} = {Scalar(a[1])} ? {Expr(a[2])} : {Expr(a[3])};");
                    return true;

                case "add": Line($"{d} = {Expr(a[1])} + {Expr(a[2])};"); return true;
                case "sub": Line($"{d} = {Expr(a[1])} - {Expr(a[2])};"); return true;
                case "mul": Line($"{d} = {Expr(a[1])} * {Expr(a[2])};"); return true;
                case "div": Line($"{d} = {Expr(a[1])} / {Expr(a[2])};"); return true;
                case "mad": Line($"{d} = {Expr(a[1])} * {Expr(a[2])} + {Expr(a[3])};"); return true;
                case "dp2": Line($"{d} = dot({Expr(a[1], 2)}, {Expr(a[2], 2)});"); return true;
                case "dp3": Line($"{d} = dot({Expr(a[1], 3)}, {Expr(a[2], 3)});"); return true;
                case "dp4": Line($"{d} = dot({Expr(a[1])}, {Expr(a[2])});"); return true;
                case "rcp": Line($"{d} = 1.0 / {Scalar(a[1])};"); return true;
                case "sincos":
                    // DXBC's sincos has TWO destinations plus the source:
                    //   sincos dst_sin, dst_cos, src
                    // Either destination may be `null`, which is how fxc splits one
                    // rotation into two instructions.  Reading the form as (dst, src)
                    // drops the sine register entirely and leaves the rotation reading
                    // a lane nothing wrote.
                    //
                    // Lane i of a destination takes source component i, so
                    // `sincos r3.xz, null, r2` means sin(r2.x), sin(r2.y) -- not
                    // sin(r2) splatted twice.  A destination that aliases the source
                    // must be read before it is written, or the naive spelling becomes
                    // sin(cos(x)).
                    {
                        Operand srcOp = a.Count > 2 ? a[2] : a[0];
                        Operand sinOp = a[0];
                        Operand cosOp = a.Count > 2 ? a[1] : null;
                        bool wantSin = sinOp != null && sinOp.Kind != RegKind.Null;
                        bool wantCos = cosOp != null && cosOp.Kind != RegKind.Null;

                        var pairs = new List<(string dst, string func, int at, char lane)>();
                        if (wantSin)
                        {
                            string m = DestMask(sinOp);
                            m = m.Length > 0 ? m : "xyzw";
                            for (int i = 0; i < m.Length; i++)
                                pairs.Add((Core(sinOp) + "." + m[i], "sin", i, m[i]));
                        }
                        if (wantCos)
                        {
                            string m = DestMask(cosOp);
                            m = m.Length > 0 ? m : "xyzw";
                            for (int i = 0; i < m.Length; i++)
                                pairs.Add((Core(cosOp) + "." + m[i], "cos", i, m[i]));
                        }
                        if (pairs.Count == 0) return true;

                        // The source pairs by ABSOLUTE LANE, not by position in the
                        // destination mask: `sincos r1.yz, null, r1.yyzy` writes
                        // sin(r1.y) to r1.y (lane 1 -> yyzy[1] = y) and sin(r1.z) to
                        // r1.z (lane 2 -> yyzy[2] = z).  Reading the swizzle
                        // positionally gives y,y -- the z output silently becomes a
                        // second copy of the y one.
                        string srcSw = srcOp.Swizzle?.TrimStart('.') ?? "";
                        if (srcSw.Length == 0) srcSw = "xyzw";
                        string LaneSrc(char lane)
                        {
                            int at = Math.Min("xyzw".IndexOf(lane), srcSw.Length - 1);
                            return Expr(Clone(srcOp, "." + srcSw[at]));
                        }

                        bool aliased = srcOp.Kind is RegKind.R or RegKind.V or RegKind.O &&
                                       pairs.Any(p => p.dst.StartsWith(Core(srcOp) + ".", StringComparison.Ordinal));
                        if (aliased)
                        {
                            // Only the lanes that are actually read go through the
                            // temporary.  Copying the whole register (`float4 t = r2`)
                            // also reads lanes this program never wrote, which reads
                            // as an uninitialised value even though nothing consumes
                            // it; one scalar per distinct source component keeps the
                            // data flow exactly as the original had it.
                            var tmps = new Dictionary<char, string>();
                            string core = Core(srcOp);
                            string tmpBase = $"_sc{_sincosSeq++}";
                            foreach (var (_, _, _, lane) in pairs)
                            {
                                int at = Math.Min("xyzw".IndexOf(lane), srcSw.Length - 1);
                                char c = srcSw[at];
                                if (tmps.ContainsKey(c)) continue;
                                string name = $"{tmpBase}_{c}";
                                string value = core + "." + c;
                                if (srcOp.Abs) value = $"abs({value})";
                                if (srcOp.Negate) value = "-" + value;
                                Line($"float {name} = {value};");
                                tmps[c] = name;
                            }
                            foreach (var (dst, func, _, lane) in pairs)
                            {
                                int at = Math.Min("xyzw".IndexOf(lane), srcSw.Length - 1);
                                Line($"{dst} = {func}({tmps[srcSw[at]]});");
                            }
                        }
                        else
                        {
                            foreach (var (dst, func, _, lane) in pairs)
                                Line($"{dst} = {func}({LaneSrc(lane)});");
                        }
                    }
                    return true;

                // A comparison produces a bool per component, and a bool vector
                // does not convert to `float4` (X3017).  The ternary broadcasts a
                // scalar over exactly the components the destination assigns, so
                // it is correct for a masked write (`dst.zw`) as well as a full one.
                //
                // TRUE IS ALL-ONES (-1.0) IN DXBC.  A program that reads the mask
                // ARITHMETICALLY (`lt -> iadd(-t, v) -> itof -> ge -> and`, the
                // classic interval selector) needs the bit pattern; a program that
                // only branches on it needs a bool.  `_boolLanes` says which this
                // is, PER LANE: a mask lane is assigned the comparison itself, any
                // other lane gets the float mask `asfloat(0xFFFFFFFFu)`.
                case "lt": return Compare(ins, d, a, "<");
                case "le": return Compare(ins, d, a, "<=");
                case "gt": return Compare(ins, d, a, ">");
                case "ge": return Compare(ins, d, a, ">=");
                case "eq": return Compare(ins, d, a, "==");
                case "ne": return Compare(ins, d, a, "!=");

                // Bitwise ops on a bool register are LOGICAL ops.  `and` of two
                // comparison results is `&&`; keeping it as
                // `asfloat(asuint(a) & asuint(b))` is bit-exact but produces the
                // 0xFFFFFFFF NaN pattern that later arithmetic can poison.
                case "and": return Bitwise(ins, d, a, "&&");
                case "or": return Bitwise(ins, d, a, "||");
                case "xor": return Bitwise(ins, d, a, "!=");
                case "not": return Not(ins, d, a);

                // ---- integer arithmetic.  DXBC keeps integers in the same
                // registers, so every operation is wrapped in asint/asfloat.
                case "iadd":
                case "isub":
                case "imul":
                    IntResult(ins, $"{AsInt(a[1])} {IntArith[ins.Opcode]} {AsInt(a[2])}");
                    return true;
                case "imin":
                    IntResult(ins, $"min({AsInt(a[1])}, {AsInt(a[2])})");
                    return true;
                case "imax":
                    IntResult(ins, $"max({AsInt(a[1])}, {AsInt(a[2])})");
                    return true;
                // The unsigned spellings: same instruction, unsigned comparison.
                // Reading them through `asint` turns every value above 0x7FFFFFFF
                // negative and picks the wrong operand.
                case "umin":
                case "iumin":
                    IntResult(ins, $"min({AsUInt(a[1])}, {AsUInt(a[2])})", unsigned: true);
                    return true;
                case "umax":
                case "iumax":
                    IntResult(ins, $"max({AsUInt(a[1])}, {AsUInt(a[2])})", unsigned: true);
                    return true;
                case "ige":
                case "ilt":
                case "ieq":
                case "ine":
                case "uge":
                case "ult":
                    // Same mask rule as the float comparisons above, but the
                    // operands are integers.  A literal keeps its BIT pattern here
                    // (`ieq l(1.0), r0` compares 0x3F800000), so it is printed as an
                    // integer instead of going through a pointless `asint(0.0f)`.
                    Line($"{d} = ({LitInt(a[1]) ?? AsInt(a[1])} {IntCompare[ins.Opcode]} "
                         + $"{LitInt(a[2]) ?? AsInt(a[2])}) ? {MaskFor(a[0])} : 0.0;");
                    return true;
                case "imad":
                    IntResult(ins, $"{AsInt(a[1])} * {AsInt(a[2])} + {AsInt(a[3])}");
                    return true;
                case "ineg":
                    IntResult(ins, $"-{AsInt(a[1])}");
                    return true;
                case "udiv":
                    {
                        // `udiv dst, dstHigh, src0, src1` -- FOUR operands.  The
                        // second is the high half of the result and is `null` in
                        // every 32-bit program, so reading src0/src1 as a[1]/a[2]
                        // divided the NULL high half by the dividend: the result
                        // was a constant, fxc folded the whole shader away, and
                        // what survived was one texture fetch (a black screen that
                        // still compiled).
                        Operand num = a.Count > 2 ? a[2] : null;
                        Operand den = a.Count > 3 ? a[3] : null;
                        if (num == null || den == null) return false;

                        bool zeroNumerator = num.Kind == RegKind.Null ||
                                             (num.Lit != null && num.Lit.All(v => v == 0f));
                        if (zeroNumerator)
                        {
                            IntResult(ins, $"{AsUInt(den)} == 0u ? 0xffffffffu : 0u", unsigned: true);
                            return true;
                        }

                        // fxc proves the divisor is zero from the constant paths
                        // above and rejects the whole file (X4010) even when a
                        // ternary guards the division -- it type-checks both arms.
                        // Pinning the divisor to at least 1 is what actually
                        // silences it, and the ternary keeps D3D's answer for a
                        // real divide by zero (0xffffffff).
                        IntResult(ins, $"{AsUInt(den)} == 0u ? 0xffffffffu "
                                     + $": {AsUInt(num)} / max({AsUInt(den)}, 1u)", unsigned: true);
                    }
                    return true;
                case "ushr":
                    IntResult(ins, $"{AsUInt(a[1])} >> {AsUInt(a[2])}", unsigned: true);
                    return true;
                case "ishl":
                    IntResult(ins, $"{AsInt(a[1])} << {AsInt(a[2])}");
                    return true;
                case "itof":
                    {
                        // `itof` is a numeric conversion of the integer stored in the
                        // register, and a mask's integer value is -1.  Answering it
                        // with the float constant keeps the conversion exact without
                        // the `asfloat(0xFFFFFFFFu)` NaN in between.
                        //
                        // The conversion is spelled at the DESTINATION's width: a
                        // scalar lane asking for `(float4)asint(x)` builds a four-lane
                        // vector out of one integer and then throws three away.
                        string mask = MaskFloat(a[1]);
                        if (mask == null)
                        {
                            int w = DestWidth(a[0]);
                            string cast = w switch
                            {
                                1 => "float", 2 => "float2", 3 => "float3", _ => "float4",
                            };
                            // An integer that already lives in its `int4` companion is
                            // converted straight from it (`float(i1.x)`); there is no
                            // float copy of it to reinterpret.  A constant buffer slot
                            // declared `int` needs no reinterpretation either -- the
                            // conversion is the implicit one.
                            string shadow = IntShadow(a[1], false);
                            string src;
                            if (shadow == null)
                                src = CbIsInt(a[1]) ? Expr(a[1], w) : $"asint({Expr(a[1], w)})";
                            else if (w > 1 && !shadow.EndsWith(".xyzw", StringComparison.Ordinal))
                                // `itof r3.yw, r1.wwww`: the destination is two lanes and
                                // the shadow is one, so it has to be splatted -- a
                                // constructor takes one argument per component.
                                src = string.Join(", ", Enumerable.Repeat(shadow, w));
                            else
                                src = shadow;
                            mask = $"{cast}({src})";
                        }
                        Line($"{d} = {mask};");
                    }
                    return true;
                case "ftoi":
                    {
                        // The integer conversion matches the destination's width:
                        // `int4(scalar)` is not a legal constructor and `(int4)` on a
                        // vector warns about truncation.  The source has to be
                        // narrowed to the same width too -- `int2(r0.xzxx)` has four
                        // components going into a two-component constructor.
                        int w = DestWidth(a[0]);
                        string cast = w switch { 1 => "int", 2 => "int2", 3 => "int3", _ => "int4" };
                        IntResult(ins, $"{cast}({Expr(a[1], w)})");
                    }
                    return true;

                // ---- derivatives.  `deriv_rtx` / `deriv_rty` are ddx / ddy: the
                // screen-space gradient of the operand.  They are per-quad
                // operations only a pixel program can run, and every use seen so far
                // is an antialiasing width (`1 / fwidth(x)`), so leaving them as a
                // TODO left the next expression reading an uninitialised register.
                case "deriv_rtx":
                    Line($"{d} = ddx({Expr(a[1])});");
                    return true;
                case "deriv_rty":
                    Line($"{d} = ddy({Expr(a[1])});");
                    return true;
                case "deriv_rtx_coarse":
                    Line($"{d} = ddx_coarse({Expr(a[1])});");
                    return true;
                case "deriv_rty_coarse":
                    Line($"{d} = ddy_coarse({Expr(a[1])});");
                    return true;
                case "deriv_rtx_fine":
                    Line($"{d} = ddx_fine({Expr(a[1])});");
                    return true;
                case "deriv_rty_fine":
                    Line($"{d} = ddy_fine({Expr(a[1])});");
                    return true;

                case "ftou":
                    {
                        // float -> uint.  The destination register holds raw bits,
                        // so the result has to come back through asfloat, exactly
                        // like every other integer instruction here.
                        int w = DestWidth(a[0]);
                        string cast = w switch { 1 => "uint", 2 => "uint2", 3 => "uint3", _ => "uint4" };
                        IntResult(ins, $"{cast}({Expr(a[1], w)})", unsigned: true);
                    }
                    return true;

                case "sample":
                    return Sample(a, d, "Sample");
                // `sample_b` is `sample` with an LOD bias: DXBC appends the bias as
                // a fifth operand.  HLSL spells it SampleBias, and dropping the bias
                // (which is what leaving this unhandled did) softens or sharpens
                // every mip the shader picks.
                case "sample_b":
                    if (a.Count < 5) return Sample(a, d, "Sample");
                    Line($"{d} = {TexName(a[2].Index)}.SampleBias(sampler_{TexName(a[2].Index)}, "
                         + $"{Coord(a[1], a[2].Index)}, {Scalar(a[4])});");
                    return true;
                case "sample_l":
                    Line($"{d} = {TexName(a[2].Index)}.SampleLevel(sampler_{TexName(a[2].Index)}, {Coord(a[1], a[2].Index)}, {Scalar(a[4])});");
                    return true;
                case "sample_d":
                    Line($"{d} = {TexName(a[2].Index)}.SampleGrad(sampler_{TexName(a[2].Index)}, {Coord(a[1], a[2].Index)}, "
                         + $"{Coord(a[4], a[2].Index)}, {Coord(a[5], a[2].Index)});");
                    return true;

                // Plain texture fetch: `ld_indexable(texture2d)... r0.xyzw, r1.xyww,
                // t0.xyzw`.  HLSL's Texture.Load takes an int3 (x, y, mip) on a 2D
                // texture; the coordinate register holds float bits, so each
                // component is read back with asint.  Zeroing the unused
                // components is what the DXBC form does implicitly.
                case "ld":
                    {
                        int tex = a[2].Index;
                        Line($"{d} = {TexName(tex)}.Load({IntCoord(a[1], 3)});");
                    }
                    return true;

                // Gather: four texels around the coordinate, returned in one
                // float4.  DXBC spells the four components "red, green, blue,
                // alpha" of the 2x2 footprint; HLSL's Gather returns exactly that.
                case "gather":
                    {
                        int tex = a[2].Index;
                        Line($"{d} = {TexName(tex)}.Gather(sampler_{TexName(tex)}, {Coord(a[1], tex)});");
                    }
                    return true;

                // The comparison sampler: DXBC's sample_c / sample_c_lz take a
                // reference value and return a filtered comparison, which HLSL
                // spells SampleCmp / SampleCmpLevelZero on a SamplerComparisonState.
                // A shadow map is the only thing that uses these, and getting the
                // method name wrong is X4532 ("cannot map expression to vs/ps
                // instruction"), not a silent difference.
                case "sample_c":
                    Line($"{d} = {TexName(a[2].Index)}.SampleCmp(sampler_{TexName(a[2].Index)}, "
                         + $"{Coord(a[1], a[2].Index)}, {Scalar(a[4])});");
                    return true;
                case "sample_c_lz":
                    Line($"{d} = {TexName(a[2].Index)}.SampleCmpLevelZero(sampler_{TexName(a[2].Index)}, "
                         + $"{Coord(a[1], a[2].Index)}, {Scalar(a[4])});");
                    return true;

                // A structured-buffer read: the DXBC form is
                //   ld_structured_indexable(structured_buffer, stride=S, mips=0,
                //                           index, byteOffset, dst)
                // and the non-indexable spelling drops the reflection group.  Both
                // address bytes, not records, so the access goes through the
                // ByteAddressBuffer declared for the resource.
                case "ld_structured_indexable":
                case "ld_structured":
                    return LoadStructured(a, d);

                // A multisampled load: `ldms_indexable(texture2dms)... r0.w,
                // r1.xyzw, t0.yzwx, r0.y` reads sample `a[4]` at location a[1].
                // HLSL spells it Texture2DMS.Load(int2 location, int sample), and
                // both arguments are integers -- the registers hold float bits.
                //
                // `ldms` on a view/array coordinate carries the array index in z,
                // and the count of the `ldms` in the original is a promise the
                // translator makes (8 reads may not become 1), so every one of them
                // is emitted.
                case "ldms_indexable":
                case "ldms":
                    {
                        // Four operands: `ldms dst, address, resource, sampleIndex`.
                        // Reading the sample from a[4] (one past the end) left every
                        // read asking for sample 0, so a resolved-MSAA shader showed
                        // only the first sample -- and the compiler then merged the
                        // identical loads into one.
                        int tex = a[2].Index;
                        var msDecl = _m.Decls.FirstOrDefault(x => x.Kind == "texture" && x.Register == tex);
                        string loc = IntCoord(a[1], msDecl?.Dimension == 5 ? 3 : 2);
                        // The sample index has to be an integer constant fxc can
                        // range-check: `asint(0.0f)` is a runtime conversion and is
                        // rejected outright (X4566, "MSAA Sample Index must be in
                        // the range 0 to 32"), even though it is provably 0.
                        string sample;
                        if (a.Count > 3 && a[3].Lit is { Length: > 0 } sLit)
                        {
                            int sv = (int)MathF.Round(sLit[0]);
                            sample = sv.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (a.Count > 3)
                        {
                            sample = $"asint({Scalar(a[3])})";
                        }
                        else sample = "0";
                        Line($"{d} = {TexName(tex)}.Load({loc}, {sample});");
                    }
                    return true;

                // Texture size query.  `resinfo_indexable(texture2dms)..._uint
                // r0.xy, l(0), t0.xyzw` writes (width, height, depth-or-layers,
                // mip count) into the destination; the `_uint` suffix only says the
                // result is an integer pattern, which the float register already
                // carries.
                case "resinfo_indexable":
                case "resinfo":
                    return ResInfo(a, d);

                // Sample count of a multisampled resource: `sampleinfo_uint r0.z,
                // t0.x`.  HLSL has no intrinsic for it -- a Texture2DMS reports the
                // count as the last output of its GetDimensions overload, whose
                // first argument is the mip (always 0 here).
                case "sampleinfo":
                    {
                        int tex = a[1].Index;
                        string w2 = $"_si{_resInfoSeq}";
                        _resInfoSeq++;
                        Line($"uint {w2}w = 0u, {w2}h = 0u, {w2}n = 0u;");
                        Line($"{TexName(tex)}.GetDimensions({w2}w, {w2}h, {w2}n);");
                        Line($"{d} = asfloat({w2}n);");
                    }
                    return true;

                // uint -> float conversion.  DXBC keeps raw bit patterns apart from
                // numbers; HLSL spells the boundary as asfloat/float(...).  The
                // source is a register holding an integer *bit pattern*, so it has
                // to be reinterpreted first -- `float4(x)` on 0x00000005 is 7e-45,
                // not 5.
                case "utof":
                    {
                        int w2 = DestWidth(a[0]);
                        string cast = w2 switch { 1 => "float", 2 => "float2", 3 => "float3", _ => "float4" };
                        Line($"{d} = {cast}(asuint({Expr(a[1], w2)}));");
                    }
                    return true;

                // Bit-field extract: (value >> offset) & ((1 << width) - 1).
                // DXBC operand order is `ubfe dst, width, offset, value`; HLSL has no
                // intrinsic, so the shift and the mask are written out.
                //
                // The destination is a float register holding an integer bit
                // pattern, so the result goes back through asfloat -- without it
                // every later asuint() would read the numeric value's bits instead.
                case "ubfe":
                    {
                        string cnt = Scalar(a[1]);
                        string off = Scalar(a[2]);
                        string src = Expr(a[3]);
                        // (1 << width) - 1 as an unsigned mask.  A width of 32 is not
                        // representable in a shift of a 32-bit int, so it is handled
                        // as the all-ones case.
                        Line($"{d} = asfloat((asuint({src}) >> asuint({off})) & "
                             + $"({cnt} >= 32u ? 0xFFFFFFFFu : ((1u << asuint({cnt})) - 1u)));");
                    }
                    return true;

                default:
                    if (Unary.TryGetValue(ins.Opcode, out var fn))
                    {
                        Line($"{d} = {fn}({Expr(a[1])});");
                        return true;
                    }
                    if (ins.Opcode is "min" or "max")
                    {
                        Line($"{d} = {ins.Opcode}({Expr(a[1])}, {Expr(a[2])});");
                        return true;
                    }
                    return false;
            }
        }

        private bool Sample(List<Operand> a, string d, string method)
        {
            if (a.Count < 4) return false;
            Line($"{d} = {TexName(a[2].Index)}.{method}(sampler_{TexName(a[2].Index)}, {Coord(a[1], a[2].Index)});");
            return true;
        }

        /// <summary>
        /// A structured-buffer read: <c>ld_structured</c> / <c>ld_structured_indexable</c>,
        /// with arguments (dst, index, byteOffset, resource).
        ///
        /// Both the index and the offset are in the address's own units, so the
        /// address is <c>index * stride + byteOffset</c> bytes.  Dividing by an
        /// element size only works when the stride equals it, and a 12-byte record or
        /// an unaligned offset then reads the wrong data -- so the resource is
        /// declared as a <c>ByteAddressBuffer</c> and the byte address passes through
        /// unchanged.
        /// </summary>
        private bool LoadStructured(List<Operand> a, string d)
        {
            // dst + index + byteOffset + resource
            if (a.Count < 4) return false;

            int tex = a[3].Index;
            var decl = _m.Decls.FirstOrDefault(x => x.Kind == "texture" && x.Register == tex);
            int stride = decl?.Stride ?? 0;

            string index = Expr(a[1]);
            string addr = stride > 0 ? $"(asuint({index}) * {stride}u)" : $"asuint({index})";

            // The byte offset is an immediate constant in every occurrence seen so
            // far (`l(8)`); a computed offset is added as another term.
            if (a[2].Lit is { Length: > 0 } lit)
            {
                uint off = lit[0] >= 0f && MathF.Floor(lit[0]) == lit[0]
                    ? (uint)lit[0] : BitConverter.SingleToUInt32Bits(lit[0]);
                if (off != 0) addr = $"{addr} + {off}u";
            }
            else
            {
                addr = $"{addr} + asuint({Scalar(a[2])})";
            }

            Line($"{d} = {TexName(tex)}.Load{Comps(a[0])}({addr});");
            return true;
        }

        /// <summary>The Load suffix matching a destination's width ("" for a scalar).</summary>
        private static string Comps(Operand dst) => DestWidth(dst) switch
        {
            2 => "2", 3 => "3", 4 => "", _ => "",
        };

        /// <summary>
        /// A texture size query.
        ///
        /// <code>
        ///   resinfo_indexable(texture2d)(float,float,float,float)_uint r0.xy, l(0), t0.xyzw
        /// </code>
        /// Arguments are (dst, mip, resource).  HLSL's <c>GetDimensions</c> hands
        /// back the whole shape at once, so the query goes into a temporary and the
        /// requested lanes are copied out -- the register holds raw bits, hence the
        /// <c>asfloat</c> on the way back.
        ///
        /// A multisampled resource's overload takes the sample count instead of a
        /// mip level, and passes it as the last of four outputs.
        /// </summary>
        private bool ResInfo(List<Operand> a, string d)
        {
            if (a.Count < 3) return false;
            int tex = a[2].Index;
            var decl = _m.Decls.FirstOrDefault(x => x.Kind == "texture" && x.Register == tex);
            bool ms = decl?.Ms ?? false;
            string mip = a[1].Lit is { Length: > 0 } lit ? Number(lit[0]) : Scalar(a[1]);

            string w = $"_ri{_resInfoSeq}";
            _resInfoSeq++;
            if (ms)
            {
                // Texture2DMS has no mip chain: its overload is
                // GetDimensions(w, h, sampleCount).
                Line($"uint {w}w = 0u, {w}h = 0u, {w}n = 0u;");
                Line($"{TexName(tex)}.GetDimensions({w}w, {w}h, {w}n);");
            }
            else
            {
                Line($"uint {w}w = 0u, {w}h = 0u, {w}d = 0u, {w}n = 0u;");
                Line($"{TexName(tex)}.GetDimensions(asuint({mip}), {w}w, {w}h, {w}d, {w}n);");
            }

            // Which lanes the destination asked for: width, height, depth/layers,
            // mip count.  A two-lane destination is the common (w, h) case.
            string mask = DestMask(a[0]);
            if (mask.Length == 0) mask = "xyzw";
            int width = DestWidth(a[0]);
            var parts = new List<string>(mask.Length);
            foreach (char c in mask)
                parts.Add(c switch { 'x' => $"{w}w", 'y' => $"{w}h", 'z' => $"{w}d", _ => $"{w}n" });
            string ctor = width == 1 ? "uint" : "uint" + width;
            Line($"{d} = asfloat({ctor}({string.Join(", ", parts)}));");
            return true;
        }

        /// <summary>Distinct temporary name per <c>resinfo</c> in this program.</summary>
        private int _resInfoSeq;

        /// <summary>
        /// A texture coordinate narrowed to integers, for the loads that address
        /// texels rather than sample them (<c>Texture2DMS.Load</c>).  The register
        /// holds the integer as float bits, so it has to be reinterpreted first.
        /// </summary>
        private string IntCoord(Operand o, int comps)
        {
            string core = Core(o);
            if (core == null) return null;
            string sw = (o.Swizzle ?? ".xyzw")[1..];
            if (sw.Length < comps) sw = sw.PadRight(comps, sw[^1]);
            sw = sw[..comps];
            string cast = comps == 1 ? "int" : "int" + comps;
            return $"{cast}(asint(({core}).{sw}))";
        }

        /// <summary>
        /// The sample coordinate at the width the resource needs: three
        /// components for a 3D or cube texture, two for everything else.  Getting
        /// this wrong is X3013 ("no matching intrinsic"), which is how the game
        /// run found it.
        /// </summary>
        private string Coord(Operand o, int texRegister)
        {
            int dim = _m.Decls.FirstOrDefault(d => d.Kind == "texture" && d.Register == texRegister)?.Dimension ?? 2;
            // A 2D array and a cube both need three components: (u, v, slice) and
            // (u, v, face) respectively.
            int comps = dim is 3 or 4 or 5 ? 3 : 2;
            string e = Expr(o);
            if (e == null) return null;
            // The coordinate components come from the instruction's own swizzle,
            // not the resource dimension: DXBC samples probe volumes with
            // `r0.zwzz`-style sources, and forcing `.xy` there reads the wrong
            // components -- and lets the compiler fold distinct taps together.
            string src = (o.Swizzle ?? ".xyzw")[1..];
            if (src.Length < comps) src = src.PadRight(comps, src[^1]);
            var m = System.Text.RegularExpressions.Regex.Match(e, @"^(\w[\w.\[\]]*)\.([xyzw]+)$");
            if (m.Success) return $"({m.Groups[1].Value})." + src[..comps];
            return $"({e}).{src[..comps]}";
        }
    }
}
