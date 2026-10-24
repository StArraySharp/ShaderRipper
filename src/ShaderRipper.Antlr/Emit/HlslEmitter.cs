using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
                    _sb.Append(mem.IsArray
                        ? $"    float4 {mem.Name}[{mem.Rows}];\n"
                        : $"    float4 {mem.Name};\n");
                _sb.Append("};\n\n");
            }
        }

        private void DeclareResources()
        {
            var textures = _m.Decls.Where(d => d.Kind == "texture")
                                   .GroupBy(d => d.Register).Select(g => g.First())
                                   .OrderBy(d => d.Register).ToList();
            foreach (var d in textures)
            {
                string ty = d.Dimension switch
                {
                    1 => "Texture1D", 3 => "Texture3D", 4 => "TextureCube",
                    5 => "Texture2DArray", 7 => "Buffer",
                    _ => "Texture2D",
                };
                _sb.Append($"{ty} t{d.Register} : register(t{d.Register});\n");
            }
            foreach (var d in textures)
                _sb.Append($"SamplerState s{d.Register} : register(s{d.Register});\n");
            if (textures.Count > 0) _sb.Append('\n');

            // Input registers are the vertex attributes / pixel inputs the
            // signature table describes; at register level they are plain globals
            // whose values the runtime supplies.
            foreach (var d in _m.Decls.Where(x => x.Kind == "input")
                                      .GroupBy(x => x.Register).Select(g => g.First())
                                      .OrderBy(x => x.Register))
                _sb.Append($"static float4 v{d.Register};\n");
            if (_m.Decls.Any(x => x.Kind == "input")) _sb.Append('\n');
            if (_m.Code.SelectMany(i => i.Args).Any(a => a.Kind == RegKind.X))
                _sb.Append("static float4 _x[512];\n\n");
        }

        private void EmitEntry()
        {
            bool frag = _m.Stage is "ps" or "fx";
            _sb.Append(frag ? "float4 main() : SV_Target\n{\n" : "float4 main() : SV_Position\n{\n");
            _indent = 1;

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
                if (ok) _r.Translated++;
                else
                {
                    _r.Todo++;
                    if (!_r.Missing.Contains(ins.Opcode)) _r.Missing.Add(ins.Opcode);
                    if (_sb.Length == at) Line("// TODO " + ins.Raw);
                }
            }

            _sb.Append("\n    return ").Append(ReturnValue()).Append(";\n}\n");
        }

        private string ReturnValue()
        {
            if (_m.Decls.Any(d => d.Kind == "output" && d.Register == 0)) return "o0";
            var last = _m.Code.LastOrDefault(i => i.Args.Count > 0 && i.Args[0].Kind == RegKind.O);
            return last != null ? $"o{last.Args[0].Index}" : "r0";
        }

        // --------------------------------------------------------------- operands

        private string Expr(Operand o)
        {
            string core = Core(o);
            if (core == null) return null;
            if (o.Swizzle != null) core += o.Swizzle;
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
                _ => null,
            };
        }

        /// <summary>
        /// How to spell a constant-buffer read.  Three cases, in the order the
        /// declaration allows: the raw array when the buffer kept that shape, the
        /// member the resolver named, and the positional member the declaration
        /// emitted for a row nothing pinned.
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
            // The SWIZZLE token carries its own dot (`.xyxx`), so the component
            // is the first character after it, not the first character of it.
            string sw = o.Swizzle?.TrimStart('.') ?? "";
            string comp = sw.Length > 0 ? sw[0].ToString() : "x";
            string e = core + "." + comp;
            if (o.Abs) e = $"abs({e})";
            if (o.Negate) e = "-" + e;
            return e;
        }

        private string Literal(Operand o)
        {
            var v = o.Lit ?? Array.Empty<float>();
            string N(float f)
            {
                string s = f.ToString("R", CultureInfo.InvariantCulture);
                if (!s.Contains('.') && !s.Contains('e') && !s.Contains('E')) s += ".0";
                return s + "f";
            }
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
                    Line("return " + ReturnValue() + ";");
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

        /// <summary>Emit one instruction; false when there is no handler.</summary>
        private bool Emit(Instruction ins)
        {
            var a = ins.Args;
            // Instructions that carry no operand are the block punctuation; they
            // have to be handled before any destination is looked at.
            if (a.Count == 0) return EmitNoOperand(ins.Opcode);

            // A `null` destination is a discard mask: there is nothing to assign.
            if (a[0].Kind == RegKind.Null) return true;

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
                    Line($"{d} = {Scalar(a[3])} != 0.0 ? {Expr(a[1])} : {Expr(a[2])};");
                    return true;

                case "add": Line($"{d} = {Expr(a[1])} + {Expr(a[2])};"); return true;
                case "sub": Line($"{d} = {Expr(a[1])} - {Expr(a[2])};"); return true;
                case "mul": Line($"{d} = {Expr(a[1])} * {Expr(a[2])};"); return true;
                case "div": Line($"{d} = {Expr(a[1])} / {Expr(a[2])};"); return true;
                case "mad": Line($"{d} = {Expr(a[1])} * {Expr(a[2])} + {Expr(a[3])};"); return true;
                case "dp2": Line($"{d} = dot({Expr(a[1])}, {Expr(a[2])});"); return true;
                case "dp3": Line($"{d} = dot({Expr(a[1])}, {Expr(a[2])});"); return true;
                case "dp4": Line($"{d} = dot({Expr(a[1])}, {Expr(a[2])});"); return true;
                case "rcp": Line($"{d} = 1.0 / {Scalar(a[1])};"); return true;
                case "sincos":
                    // DXBC writes cos to .x and sin to .y of the destination.
                    Line($"{d} = float4(cos({Scalar(a[2])}), sin({Scalar(a[2])}), 0.0, 0.0);");
                    return true;

                case "lt": Line($"{d} = (float4)({Expr(a[1])} < {Expr(a[2])});"); return true;
                case "le": Line($"{d} = (float4)({Expr(a[1])} <= {Expr(a[2])});"); return true;
                case "gt": Line($"{d} = (float4)({Expr(a[1])} > {Expr(a[2])});"); return true;
                case "ge": Line($"{d} = (float4)({Expr(a[1])} >= {Expr(a[2])});"); return true;
                case "eq": Line($"{d} = (float4)({Expr(a[1])} == {Expr(a[2])});"); return true;
                case "ne": Line($"{d} = (float4)({Expr(a[1])} != {Expr(a[2])});"); return true;

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
                    Line($"{d} = asfloat(asuint({Expr(a[1])}) / asuint({Expr(a[2])}));");
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
                    Line($"{d} = t{a[2].Index}.SampleLevel(s{a[3].Index}, {Coord(a[1], a[2].Index)}, {Scalar(a[4])});");
                    return true;
                case "sample_d":
                    Line($"{d} = t{a[2].Index}.SampleGrad(s{a[3].Index}, {Coord(a[1], a[2].Index)}, "
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
            Line($"{d} = t{a[2].Index}.{method}(s{a[3].Index}, {Coord(a[1], a[2].Index)});");
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
            var m = System.Text.RegularExpressions.Regex.Match(e, @"^(\w[\w.\[\]]*)\.([xyzw]+)$");
            if (m.Success) return $"({m.Groups[1].Value})." + (comps == 3 ? "xyz" : "xy");
            return comps == 3 ? $"({e}).xyz" : $"({e}).xy";
        }
    }
}
