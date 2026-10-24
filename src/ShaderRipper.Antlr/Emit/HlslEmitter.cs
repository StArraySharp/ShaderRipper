using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShaderRipper.Antlr.Ast;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// Emits HLSL from a <see cref="Module"/> — that is, from the DXBC parse
    /// tree.
    ///
    /// The output is register-level on purpose: <c>float4 r0</c> locals, one
    /// statement per instruction, real <c>cbuffer</c> blocks bound by register,
    /// <c>Texture2D</c> + <c>SamplerState</c>.  Register level is what makes the
    /// translation checkable — D3DCompile either accepts it or names the line
    /// that is wrong — while pretty names need the constant-buffer reflection,
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

        private readonly CbLayout _lay;
        private ShaderInterface _iface;

        private HlslEmitter(Module m)
        {
            _m = m;
            _lay = CbLayout.Build(m, m.Cb);
        }

        public static Result Emit(Module m)
        {
            var e = new HlslEmitter(m);
            e.Run();
            e._r.Hlsl = e._sb.ToString();
            return e._r;
        }

        private void Line(string s)
        {
            if (s.Length == 0) return;
            _sb.Append(new string(' ', _indent * 4)).Append(s).Append('\n');
        }

        private void Run()
        {
            _sb.Append("// translated from a DXBC disassembly by ShaderRipper.Antlr\n");
            _sb.Append("// grammar -> AST -> HLSL, register level\n");
            _sb.Append("// profile: ").Append(_m.Profile).Append("\n\n");

            DeclareConstantBuffers();
            DeclareResources();
            EmitEntry();
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
                string ty = d.Dimension switch
                {
                    1 => "Texture1D", 3 => "Texture3D", 4 => "TextureCube",
                    5 => "Texture2DArray", 7 => "Buffer",
                    _ => "Texture2D",
                };
                string name = TexName(d.Register);
                _sb.Append($"{ty} {name} : register(t{d.Register});\n");
            }
            foreach (var d in textures)
                _sb.Append($"SamplerState sampler_{TexName(d.Register)} : register(s{d.Register});\n");
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
            if (_m.Code.SelectMany(i => i.Args).Any(a => a.Kind == RegKind.X))
                _sb.Append("static float4 _x[512];\n\n");
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
                sig = "float4 frag(" + (hasIn ? "stage_in i" : "") + ") : SV_Target";

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
            for (int i = 0; i < _temps; i++) Line($"float4 r{i} = 0.0;");
            foreach (var d in _m.Decls.Where(d => d.Kind == "output")
                                      .GroupBy(d => d.Register).Select(g => g.First())
                                      .OrderBy(d => d.Register))
                Line($"float4 o{d.Register} = 0.0;");
            Line("");

            foreach (var ins in _m.Code)
            {
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
                    var mm = Regex.Match(written, @"^(\s*)([^=\n]+)= (.+);\r?\n$");
                    if (mm.Success)
                    {
                        _sb.Length = at;
                        Line($"{mm.Groups[1].Value}{mm.Groups[2].Value}= saturate({mm.Groups[3].Value});");
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
            if (sw != null) core += sw;
            if (o.Abs) core = $"abs({core})";
            if (o.Negate) core = "-" + core;
            return core;
        }

        /// <summary>The register an operand names, with no swizzle or modifier.</summary>
        private string Core(Operand o)
        {
            return o.Kind switch
            {
                RegKind.R => $"r{o.Index}",
                RegKind.V => $"v{o.Index}",
                RegKind.O => $"o{o.Index}",
                RegKind.Cb => CbCore(o),
                RegKind.Null => "float4(0, 0, 0, 0)",
                RegKind.Literal => Literal(o),
                // Indexable temps: x0[a0.x] indexes an array of float4.
                RegKind.X => $"_x[{o.Index}]",
                RegKind.Icb => IcbRef(o),
                _ => null,
            };
        }

        /// <summary>
        /// The identifier of a texture: the name the shader's own reflection gives
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
                string e = $"abs(asint({m.Groups[1].Value}.{m.Groups[2].Value}){off})";
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
            if (_lay.IsArray(o.Index)) return $"cb{o.Index}[{row}]";
            return _lay.Row(o.Index, row) ?? $"_cb{o.Index}_row{row}";
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
            string e = core + "." + comp;
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
            string N(float f) => Number(f);
            return v.Length switch
            {
                1 => N(v[0]),
                2 => $"float2({N(v[0])}, {N(v[1])})",
                3 => $"float3({N(v[0])}, {N(v[1])}, {N(v[2])})",
                _ => $"float4({N(v[0])}, {N(v[1])}, {N(v[2])}, {N(v[3])})",
            };
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

        /// <summary>The destination's write mask, "" when it writes all four.</summary>
        private static string DestMask(Operand dst)
        {
            if (dst.Swizzle == null || dst.Swizzle.Length == 4) return "";
            return dst.Swizzle[1..];
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
            };
            return clone;
        }

        /// <summary>Emit one instruction; false when there is no handler.</summary>
        private bool Emit(Instruction ins)
        {
            List<Operand> a = new List<Operand>(ins.Args);
            // Instructions that carry no operand are the block punctuation; they
            // have to be handled before any destination is looked at.
            if (a.Count == 0) return EmitNoOperand(ins.Opcode);

            // A `null` destination is a discard mask: there is nothing to assign.
            if (a[0].Kind == RegKind.Null) return true;

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

                // ---- control flow: the tree supplies the condition, the emitter
                // spells out the block structure.
                case "discard_nz":
                    Line($"if ({Scalar(a[0])} != 0.0) discard;");
                    return true;

                case "if_z":
                    Line($"if ({Scalar(a[0])} == 0.0)");
                    Line("{");
                    _indent++; _r.Blocks++;
                    return true;
                case "if_nz":
                    Line($"if ({Scalar(a[0])} != 0.0)");
                    Line("{");
                    _indent++; _r.Blocks++;
                    return true;
                case "breakc_nz":
                    Line($"if ({Scalar(a[0])} != 0.0) break;");
                    return true;
                case "continuec_nz":
                    Line($"if ({Scalar(a[0])} != 0.0) continue;");
                    return true;

                case "mov":
                    Line($"{d} = {Expr(a[1])};");
                    return true;
                case "movc":
                    // movc dst, src0, src1, src2 -> dst = src0 != 0 ? src1 : src2
                    Line($"{d} = {Expr(a[1])} != 0.0 ? {Expr(a[2])} : {Expr(a[3])};");
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
                    // DXBC writes cos to .x and sin to .y of the destination.
                    Line($"{d} = float4(cos({Scalar(a[2])}), sin({Scalar(a[2])}), 0.0, 0.0);");
                    return true;

                // A comparison produces a bool per component, and a bool vector
                // does not convert to `float4` (X3017).  The ternary broadcasts a
                // scalar over exactly the components the destination assigns, so
                // it is correct for a masked write (`dst.zw`) as well as a full one.
                case "lt": Line($"{d} = ({Expr(a[1])} < {Expr(a[2])}) ? 1.0 : 0.0;"); return true;
                case "le": Line($"{d} = ({Expr(a[1])} <= {Expr(a[2])}) ? 1.0 : 0.0;"); return true;
                case "gt": Line($"{d} = ({Expr(a[1])} > {Expr(a[2])}) ? 1.0 : 0.0;"); return true;
                case "ge": Line($"{d} = ({Expr(a[1])} >= {Expr(a[2])}) ? 1.0 : 0.0;"); return true;
                case "eq": Line($"{d} = ({Expr(a[1])} == {Expr(a[2])}) ? 1.0 : 0.0;"); return true;
                case "ne": Line($"{d} = ({Expr(a[1])} != {Expr(a[2])}) ? 1.0 : 0.0;"); return true;

                case "and": Line($"{d} = asfloat(asuint({Expr(a[1])}) & asuint({Expr(a[2])}));"); return true;
                case "or": Line($"{d} = asfloat(asuint({Expr(a[1])}) | asuint({Expr(a[2])}));"); return true;
                case "xor": Line($"{d} = asfloat(asuint({Expr(a[1])}) ^ asuint({Expr(a[2])}));"); return true;
                case "not": Line($"{d} = asfloat(~asuint({Expr(a[1])}));"); return true;

                // ---- integer arithmetic.  DXBC keeps integers in the same
                // registers, so every operation is wrapped in asint/asfloat.
                case "iadd":
                case "isub":
                case "imul":
                    Line($"{d} = asfloat(asint({Expr(a[1])}) {IntArith[ins.Opcode]} asint({Expr(a[2])}));");
                    return true;
                case "imin":
                    Line($"{d} = asfloat(min(asint({Expr(a[1])}), asint({Expr(a[2])})));");
                    return true;
                case "imax":
                    Line($"{d} = asfloat(max(asint({Expr(a[1])}), asint({Expr(a[2])})));");
                    return true;
                case "ige":
                case "ilt":
                case "ieq":
                case "ine":
                case "uge":
                case "ult":
                    Line($"{d} = asfloat((asint({Expr(a[1])}) {IntCompare[ins.Opcode]} asint({Expr(a[2])})) ? 1 : 0);");
                    return true;
                case "imad":
                    Line($"{d} = asfloat(asint({Expr(a[1])}) * asint({Expr(a[2])}) + asint({Expr(a[3])}));");
                    return true;
                case "ineg":
                    Line($"{d} = asfloat(-asint({Expr(a[1])}));");
                    return true;
                case "udiv":
                    // fxc proves the divisor is zero from the constant paths above
                    // and rejects the whole file (X4010) even when a ternary guards
                    // the division -- it type-checks both arms.  Pinning the divisor
                    // to at least 1 is what actually silences it, and the ternary
                    // keeps D3D's answer for a real divide by zero (0xffffffff).
                    Line($"{d} = asfloat(asuint({Expr(a[2])}) == 0u ? 0xffffffffu "
                         + $": asuint({Expr(a[1])}) / max(asuint({Expr(a[2])}), 1u));");
                    return true;
                case "ushr":
                    Line($"{d} = asfloat(asuint({Expr(a[1])}) >> asuint({Expr(a[2])}));");
                    return true;
                case "ishl":
                    Line($"{d} = asfloat(asint({Expr(a[1])}) << asint({Expr(a[2])}));");
                    return true;
                case "itof":
                    Line($"{d} = (float4)asint({Expr(a[1])});");
                    return true;
                case "ftoi":
                    Line($"{d} = asfloat((int4){Expr(a[1])});");
                    return true;

                case "sample":
                    return Sample(a, d, "Sample");
                case "sample_l":
                    Line($"{d} = {TexName(a[2].Index)}.SampleLevel(sampler_{TexName(a[2].Index)}, {Coord(a[1], a[2].Index)}, {Scalar(a[4])});");
                    return true;
                case "sample_d":
                    Line($"{d} = {TexName(a[2].Index)}.SampleGrad(sampler_{TexName(a[2].Index)}, {Coord(a[1], a[2].Index)}, "
                         + $"{Coord(a[4], a[2].Index)}, {Coord(a[5], a[2].Index)});");
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
        /// The sample coordinate at the width the resource needs: three
        /// components for a 3D or cube texture, two for everything else.  Getting
        /// this wrong is X3013 ("no matching intrinsic"), which is how the game
        /// run found it.
        /// </summary>
        private string Coord(Operand o, int texRegister)
        {
            int dim = _m.Decls.FirstOrDefault(d => d.Kind == "texture" && d.Register == texRegister)?.Dimension ?? 2;
            int comps = dim is 3 or 4 ? 3 : 2;
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
