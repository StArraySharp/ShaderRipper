using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShaderRipper.Cg
{
    /// <summary>Translates parsed DXBC asm into readable CG/HLSL for Unity CGPROGRAM.</summary>
    internal sealed class CgTranslator
    {
        private readonly AsmParser.Program _p;
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        public CgTranslator(AsmParser.Program p) { _p = p; _indent = 1; }
        public CgTranslator(AsmParser.Program p, int indent) { _p = p; _indent = indent; }

        /// <summary>
        /// When the translated stream is nested inside a generated entry point,
        /// the DXBC `ret` must be dropped: the wrapper supplies the real return.
        /// </summary>
        public bool SuppressRet { get; set; }

        /// <summary>
        /// Expression to return for a *conditional* ret (one inside an if/loop).
        /// The final ret at block depth 0 is always dropped -- the generated
        /// entry-point wrapper supplies the epilogue.  But an early return (e.g.
        /// FloorMesh's shadow path writes o0 and rets inside the branch) must
        /// return the assembled output struct here, or the code after the branch
        /// overwrites the result and the effect silently breaks.
        /// </summary>
        public string ReturnExpr { get; set; }

        /// <summary>Nesting depth of if/loop blocks, for the early-return check.</summary>
        private int _blockDepth;

        /// <summary>Named uniforms recovered from the constant-buffer layout (nullable).</summary>
        public CbResolver Cb { get; set; }

        /// <summary>
        /// Instructions that fell through to the TODO branch.  Process-wide
        /// because the emitter constructs a translator per entry point, and the
        /// run summary wants one honest total (0 = full coverage).
        /// </summary>
        public static int UnsupportedTotal;

        /// <summary>Highest component index a swizzle string touches (-1 when empty).</summary>
        private static int MaxComp(string sw)
        {
            int m = -1;
            foreach (char c in sw ?? "")
                m = Math.Max(m, c switch { 'x' => 0, 'y' => 1, 'z' => 2, 'w' => 3, _ => 3 });
            return m;
        }

        /// <summary>
        /// <c>cbN[row]</c> rewritten to the Unity uniform that occupies the slot,
        /// e.g. <c>unity_ObjectToWorld[1]</c>, <c>unity_MatrixVP[0]</c>, <c>_Time</c>.
        /// Null when the slot could not be identified from evidence, or when the
        /// access reaches past the width the named member declares.
        /// </summary>
        private string CbRow(Operand o, int maxComp = 3)
        {
            if (o.ArrayIndex < 0 || Cb == null) return null;
            var key = (o.Index, o.ArrayIndex);
            if (!Cb.RowExpr.TryGetValue(key, out var e)) return null;
            if (Cb.RowWidth.TryGetValue(key, out int w) && maxComp >= w) return null;
            return e;
        }

        /// <summary>The declared sampler name for tN, or "tN" when unknown.</summary>
        private string Tex(int reg) =>
            Cb != null && Cb.TexName.TryGetValue(reg, out var n) ? n : "t" + reg;

        private static string F(float f)
        {
            if (f == Math.Floor(f) && Math.Abs(f) < 1e7)
                return ((long)f).ToString(CultureInfo.InvariantCulture) + ".0";
            return f.ToString("0.0######", CultureInfo.InvariantCulture);
        }

        public string Translate()
        {
            EmitPreamble();
            return TranslateBody();
        }

        /// <summary>
        /// The translated instruction stream only, at the current indent.  The
        /// surrounding struct + entry-point scaffolding is produced by
        /// <see cref="CgProgramWriter"/>.
        /// </summary>
        /// <summary>
        /// 每条指令前面插入一行 <c>// &lt;原始 DXBC 反汇编&gt;</c>。
        /// 直译最容易错的就是"哪条汇编生成了哪几行 CG"，逐指令注释让这件事可核对；
        /// 整份反汇编贴在末尾的做法没有这个信息量（侧车 .cg 已经有）。
        /// </summary>
        public bool AnnotateAsm { get; set; }

        public string TranslateBody()
        {
            foreach (var ins in _p.Code)
            {
                int at = _sb.Length;
                Emit(ins);
                // 只在真正产出了代码的指令前注释，纯声明/空操作不刷屏
                if (AnnotateAsm && _sb.Length > at && !string.IsNullOrWhiteSpace(ins.Raw))
                    _sb.Insert(at, new string('\t', _indent) + "// " + ins.Raw + Environment.NewLine);
            }
            return _sb.ToString();
        }

        private void EmitPreamble()
        {
            _sb.AppendLine("// translated from DXBC by ShaderRipper");
            _sb.AppendLine($"// profile: {_p.Stage}");
            _sb.AppendLine();

            foreach (var d in _p.Decls)
            {
                if (d.Kind != null && d.Kind.StartsWith("texture"))
                {
                    string ty = d.TexDim switch
                    {
                        1 => "sampler1D", 2 => "sampler2D", 3 => "sampler3D",
                        4 => "samplerCUBE", 5 => "sampler2DArray", 6 => "samplerCUBE",
                        _ => "sampler2D"
                    };
                    _sb.AppendLine($"{ty} t{d.Register};");
                }
            }
            foreach (var d in _p.Decls)
                if (d.Kind == "sampler") _sb.AppendLine($"SamplerState s{d.Register};");
            _sb.AppendLine();

            // DXBC can declare the same register more than once (e.g. "dcl_output
            // o0.xyzw" together with "dcl_output_siv o0.xyzw, position"), so
            // de-duplicate by register number before emitting the declarations.
            var seenV = new HashSet<int>();
            foreach (var d in _p.Decls)
                if ((d.Kind == "input" || d.Kind == "vinput") && seenV.Add(d.Register))
                {
                    string mask = string.IsNullOrEmpty(d.InputMask) ? "xyzw" : d.InputMask;
                    _sb.AppendLine($"float4 v{d.Register};  // input .{mask}");
                }
            var seenO = new HashSet<int>();
            foreach (var d in _p.Decls)
                if (d.Kind == "output" && seenO.Add(d.Register))
                    _sb.AppendLine($"float4 o{d.Register}" +
                        (string.IsNullOrEmpty(d.OutputSemantic) ? ";" : $" : {d.OutputSemantic};"));
            _sb.AppendLine();

            int temps = 0;
            foreach (var d in _p.Decls) if (d.Kind == "temps") temps = Math.Max(temps, d.Count);
            for (int i = 0; i < temps; i++) _sb.AppendLine($"float4 r{i};");
            foreach (var d in _p.Decls)
                if (d.Kind == "xtemp")
                {
                    // dcl_indexableTemp x0[N], 4 -- N is already the element count
                    int n = Math.Max(1, d.Elements);
                    _sb.AppendLine($"float4 x{d.Register}[{n}];");
                }
            foreach (var d in _p.Decls)
                if (d.Kind == "icb" && d.IcbValues != null && d.IcbValues.Count > 0)
                {
                    var rows = new List<string>();
                    foreach (var v in d.IcbValues)
                    {
                        var c = new string[4];
                        for (int i = 0; i < 4; i++) c[i] = F(i < v.Length ? v[i] : 0f);
                        rows.Add("float4(" + string.Join(", ", c) + ")");
                    }
                    _sb.AppendLine($"static const float4 icb[{d.IcbValues.Count}] = {{");
                    for (int i = 0; i < rows.Count; i++)
                        _sb.AppendLine("    " + rows[i] + (i + 1 < rows.Count ? "," : ""));
                    _sb.AppendLine("};");
                }
            if (temps > 0) _sb.AppendLine();
        }

        private string RegName(Operand o)
        {
            return o.Kind switch
            {
                "r" => "r" + o.Index,
                "v" => "v" + o.Index,
                "o" => "o" + o.Index,
                "t" => "t" + o.Index,
                "s" => "s" + o.Index,
                _ => o.Kind + o.Index
            };
        }

        /// <summary>Array subscript for cb#/x#/icb operands: literal, or the dynamic expression.</summary>
        private static string Arr(Operand o) =>
            o.ArrayIndex >= 0 ? o.ArrayIndex.ToString()
                              : IdxExpr((o.ArrayExpr ?? "0").Replace(" ", ""));

        private string SrcExpr(Operand o)
        {
            string s;
            bool swizzled = false;
            switch (o.Kind)
            {
                case "l":
                    s = "float4(" + string.Join(", ", Array.ConvertAll(o.Imm, F)) + ")";
                    break;
                case "null":
                    return "0.0";
                case "cb":
                    {
                        if (o.ArrayIndex >= 0 && Cb != null &&
                            Cb.CompExpr.TryGetValue((o.Index, o.ArrayIndex, CompIndex(FirstSwz(o.Swizzle))), out var cn))
                        {
                            // A scalar member already names the one component the
                            // access wants, so the swizzle would be noise
                            // (`_IconAngle.x` on a float).
                            s = cn;
                            if (o.Swizzle != null && o.Swizzle.Length <= 1) swizzled = true;
                            break;
                        }
                        s = CbRow(o, MaxComp(o.Swizzle)) ?? $"cb{o.Index}[{Arr(o)}]";
                        break;
                    }
                case "x":
                    {
                        string idx = Arr(o);
                        s = $"x{o.Index}[{idx}]";
                        break;
                    }
                case "icb":
                    {
                        string idx = Arr(o);
                        s = $"icb[{idx}]";
                        break;
                    }
                default:
                    s = RegName(o);
                    break;
            }
            if (o.Negate) s = "-(" + s + ")";
            if (!swizzled && !string.IsNullOrEmpty(o.Swizzle)) s = s + "." + o.Swizzle;
            return s;
        }

        private string DstExpr(Operand d)
        {
            string s;
            if (d.Kind == "x")
            {
                string idx = Arr(d);
                s = $"x{d.Index}[{idx}]";
            }
            else s = RegName(d);
            if (!string.IsNullOrEmpty(d.Swizzle)) s = s + "." + d.Swizzle;
            return s;
        }

        /// <summary>
        /// Component-wise source list honouring swizzle length.
        /// DXBC semantics: the destination lane with absolute component index L
        /// (from the write mask) reads source component swizzle[L] when the
        /// source carries its own swizzle; with no source swizzle it reads the
        /// same component it writes (the dst mask char at that position).
        /// The old "shift by first dest lane" model was wrong — e.g.
        /// `add r0.xz, r0.zzwz, r1.yywy` must give (r0.z+r1.y, r0.w+r1.y).
        /// </summary>
        private List<string> Comps(Operand o, int want, string dstSw = null)
        {
            var res = new List<string>();
            // absolute component index of each destination lane
            int[] laneIdx = new int[want];
            if (string.IsNullOrEmpty(dstSw) || dstSw.Length != want)
            {
                for (int i = 0; i < want; i++) laneIdx[i] = i;   // full mask: x,y,z,w
            }
            else
            {
                for (int i = 0; i < want; i++)
                    laneIdx[i] = dstSw[i] switch { 'x' => 0, 'y' => 1, 'z' => 2, _ => 3 };
            }
            string srcSw = o.Swizzle;

            if (o.Kind == "l")
            {
                for (int i = 0; i < want; i++)
                {
                    int k = laneIdx[i];
                    string s = srcSw;
                    if (string.IsNullOrEmpty(s)) s = dstSw;
                    if (!string.IsNullOrEmpty(s))
                    {
                        char c = s.Length == 1 ? s[0] : s[Math.Min(k, s.Length - 1)];
                        k = c switch { 'x' => 0, 'y' => 1, 'z' => 2, 'w' => 3, _ => i };
                    }
                    res.Add(F(o.Imm[Math.Min(k, o.Imm.Length - 1)]));
                }
                return res;
            }
            // component each destination lane reads
            var lanes = new char[want];
            for (int i = 0; i < want; i++)
            {
                if (!string.IsNullOrEmpty(srcSw))
                    lanes[i] = srcSw.Length == 1 ? srcSw[0] : srcSw[Math.Min(laneIdx[i], srcSw.Length - 1)];
                else if (!string.IsNullOrEmpty(dstSw) && dstSw.Length == want)
                    lanes[i] = dstSw[i];
                else
                    lanes[i] = "xyzw"[laneIdx[i]];
            }
            string sw = new string(lanes);
            string effSw = sw;
            string baseName = o.Kind switch
            {
                "cb" => CbRow(o, MaxComp(effSw)) ?? $"cb{o.Index}[{Arr(o)}]",
                "x" => $"x{o.Index}[" + (Arr(o)) + "]",
                "icb" => "icb[" + (Arr(o)) + "]",
                "null" => "0.0",
                _ => RegName(o)
            };
            if (o.Kind == "null")
            {
                for (int i = 0; i < want; i++) res.Add("0.0");
                return res;
            }
            for (int i = 0; i < want; i++)
            {
                char c = sw[Math.Min(i, sw.Length - 1)];
                // a scalar member that owns exactly this lane gets its own name
                if (o.Kind == "cb" && o.ArrayIndex >= 0 && Cb != null &&
                    Cb.CompExpr.TryGetValue((o.Index, o.ArrayIndex, CompIndex(c)), out var cn))
                {
                    res.Add(o.Negate ? "-(" + cn + ")" : cn);
                    continue;
                }
                string e = baseName + "." + c;
                if (o.Negate) e = "-(" + e + ")";
                res.Add(e);
            }
            return res;
        }

        private static int CompIndex(char c) => c switch { 'x' => 0, 'y' => 1, 'z' => 2, 'w' => 3, _ => 0 };

        private static char FirstSwz(string sw) => string.IsNullOrEmpty(sw) ? 'x' : sw[0];

        private void Line(string s) => _sb.AppendLine(new string('\t', _indent) + s);

        private static readonly HashSet<string> ReadsOperand0 = new()
        {
            "if_nz", "if_z", "breakc_nz", "breakc_z", "discard_nz",
            "loop", "endloop", "else", "endif", "ret",
        };

        private void Emit(Instruction ins)
        {
            var o = ins.Operands;
            if (string.IsNullOrWhiteSpace(ins.Opcode)) return;

            // Operand 0 is a *source* for the control-flow / predicate opcodes;
            // for everything else it is the destination, and a `null` destination
            // means the result is discarded ("sincos null, r7.x, r7.x").
            if (o.Count > 0 && o[0].Kind == "null" && !ReadsOperand0.Contains(ins.Opcode))
                return;

            switch (ins.Opcode)
            {
                case "mov": Assign(o[0], Comps(o[1], N(o[0]), o[0].Swizzle)); break;
                case "mov_sat": SatAssign(o[0], Comps(o[1], N(o[0]), o[0].Swizzle)); break;
                case "mul_sat": SatBin(o, "*"); break;
                case "add_sat": SatBin(o, "+"); break;
                case "sub_sat": SatBin(o, "-"); break;
                case "div_sat": SatBin(o, "/"); break;
                case "mad_sat": SatMad(o); break;
                case "dp2_sat": DpSat(o, 2); break;
                case "dp3_sat": DpSat(o, 3); break;
                case "dp4_sat": DpSat(o, 4); break;
                case "xor": IBin(o, "^"); break;
                case "add": Bin(o, "+"); break;
                case "sub": Bin(o, "-"); break;
                case "mul": Bin(o, "*"); break;
                case "div": Bin(o, "/"); break;
                case "min": Fn2(o, "min"); break;
                case "max": Fn2(o, "max"); break;
                case "lt": Cmp(o, "<"); break;
                case "le": Cmp(o, "<="); break;
                case "ge": Cmp(o, ">="); break;
                case "gt": Cmp(o, ">"); break;
                case "eq": Cmp(o, "=="); break;
                case "ne": Cmp(o, "!="); break;
                case "and": AndOr(o, true); break;
                case "or": AndOr(o, false); break;
                case "mad": Mad(o); break;
                case "iadd": IBin(o, "+"); break;
                case "isub": IBin(o, "-"); break;
                case "imul": IBin(o, "*"); break;
                case "imad": IMad(o); break;
                case "ineg": INeg(o); break;
                case "ilt": ICmp(o, "<"); break;
                case "ige": ICmp(o, ">="); break;
                case "ieq": ICmp(o, "=="); break;
                case "ine": ICmp(o, "!="); break;
                case "ishl": IBin(o, "<<"); break;
                case "ushr": IBin(o, ">>"); break;
                case "imax": Fn2(o, "max"); break;
                case "imin": Fn2(o, "min"); break;
                case "udiv": UDiv(o); break;
                case "dp2": Dp(o, 2); break;
                case "dp3": Dp(o, 3); break;
                case "dp4": Dp(o, 4); break;
                case "rsq": Un(o, "rsqrt"); break;
                case "sqrt": Un(o, "sqrt"); break;
                case "exp": Un(o, "exp2"); break;
                case "log": Un(o, "log2"); break;
                case "frc": Un(o, "frac"); break;
                case "round_ni": Un(o, "floor"); break;
                case "round_pi": Un(o, "ceil"); break;
                case "round_ne": Un(o, "round"); break;
                case "round_z": Un(o, "trunc"); break;
                case "sincos": Sincos(o); break;
                case "itof": Un(o, "float"); break;
                case "ftoi": Un(o, "int"); break;
                case "movc": Movc(o, false); break;
                case "movc_sat": Movc(o, true); break;
                case "sample": Sample(o, null); break;
                case "sample_l": Sample(o, o.Count > 4 ? o[4] : null); break;
                // SM4.1 "indexable" spelling of the same sampling ops
                case "sample_indexable": Sample(o, null); break;
                case "sample_l_indexable": Sample(o, o.Count > 4 ? o[4] : null); break;
                case "sample_c_lz": Sample(o, null); break;
                case "utof": Un(o, "float"); break;
                case "rcp":
                    {
                        var s = Comps(o[1], N(o[0]), o[0].Swizzle);
                        var r = new List<string>();
                        foreach (var x in s) r.Add($"1.0 / ({x})");
                        Assign(o[0], r);
                        break;
                    }
                case "ubfe": Ubfe(o); break;
                case "break": Line("break;"); break;
                case "continue": Line("continue;"); break;
                case "ld_structured_indexable": LdStructured(ins); break;
                case "discard_nz": Line($"if (any({SrcExpr(o[0])})) discard;"); break;
                case "ret":
                    if (_blockDepth > 0 && !string.IsNullOrEmpty(ReturnExpr))
                        Line($"return {ReturnExpr};");
                    break;
                case "loop": Line("for (;;) {"); _indent++; _blockDepth++; break;
                case "endloop": _blockDepth--; _indent--; Line("}"); break;
                case "if_nz": Line($"if (any({SrcExpr(o[0])})) {{"); _indent++; _blockDepth++; break;
                case "if_z": Line($"if (!any({SrcExpr(o[0])})) {{"); _indent++; _blockDepth++; break;
                case "else": _indent--; Line("} else {"); _indent++; break;
                case "endif": _blockDepth--; _indent--; Line("}"); break;
                case "breakc_nz": Line($"if (any({SrcExpr(o[0])})) break;"); break;
                case "breakc_z": Line($"if (!any({SrcExpr(o[0])})) break;"); break;
                default: UnsupportedTotal++; Line($"/* TODO [{ins.Opcode}] {ins.Raw} */"); break;
            }
        }

        private static int N(Operand d)
        {
            string sw = string.IsNullOrEmpty(d.Swizzle) ? "xyzw" : d.Swizzle;
            return sw.Length;
        }

        private void Assign(Operand dst, List<string> src)
        {
            string d = DstExpr(dst);
            if (src.Count == 1) Line($"{d} = {src[0]};");
            else Line($"{d} = float{src.Count}({string.Join(", ", src)});");
        }

        private void Bin(List<Operand> o, string op)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"{a[i]} {op} {b[i]}");
            Assign(o[0], r);
        }

        // ---- _sat variants (clamp to [0,1]) ----

        private void SatAssign(Operand dst, List<string> src)
        {
            var r = src.ConvertAll(s => $"saturate({s})");
            Assign(dst, r);
        }

        private void SatBin(List<Operand> o, string op)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"saturate({a[i]} {op} {b[i]})");
            Assign(o[0], r);
        }

        private void SatMad(List<Operand> o)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var c = Comps(o[3], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"saturate(mad({a[i]}, {b[i]}, {c[i]}))");
            Assign(o[0], r);
        }

        private void DpSat(List<Operand> o, int k)
        {
            var a = Comps(o[1], k, "xyzw");
            var b = Comps(o[2], k, "xyzw");
            var terms = new List<string>();
            for (int i = 0; i < k; i++) terms.Add($"{Strip(a[i])} * {Strip(b[i])}");
            Line($"{DstExpr(o[0])} = saturate({string.Join(" + ", terms)});");
        }

        private void Fn2(List<Operand> o, string fn)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"{fn}({a[i]}, {b[i]})");
            Assign(o[0], r);
        }

        private void Un(List<Operand> o, string fn)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"{fn}({a[i]})");
            Assign(o[0], r);
        }

        private void Cmp(List<Operand> o, string op)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"({a[i]} {op} {b[i]}) ? 1.0 : 0.0");
            Assign(o[0], r);
        }

        /// <summary>
        /// One lane of a literal immediate, selected exactly the way
        /// <see cref="Comps"/> selects it: the destination mask picks the lane,
        /// so <c>and r0.yw, x, l(0,1,0,1)</c> pairs lane y with 1.0 -- not with
        /// the literal's first element.
        /// </summary>
        private static bool ImmLane(Operand o, int lane, string dstSw, out float value)
        {
            value = 0f;
            if (o == null || o.Kind != "l" || o.Imm == null || o.Imm.Length == 0) return false;

            int shift = 0;
            if (!string.IsNullOrEmpty(o.Swizzle) && !string.IsNullOrEmpty(dstSw) && dstSw.Length < 4)
            {
                int li = "xyzw".IndexOf(dstSw[0]);
                if (li > 0) shift = li;
            }
            string effSw = !string.IsNullOrEmpty(o.Swizzle) ? o.Swizzle : (dstSw ?? "xyzw");

            int k = lane;
            if (effSw.Length > 0)
            {
                char c = effSw[(shift + lane) % effSw.Length];
                k = c switch { 'x' => 0, 'y' => 1, 'z' => 2, 'w' => 3, _ => lane };
            }
            value = o.Imm[Math.Min(k, o.Imm.Length - 1)];
            if (o.Negate) value = -value;
            return true;
        }

        /// <summary>
        /// <c>and</c> / <c>or</c> are bitwise on the operands' raw patterns, but
        /// fxc emits them almost exclusively as bool-to-float conversion:
        ///
        ///     and r1.xy, r1.xyxx, l(0x3f800000, 0x3f800000, 0, 0)
        ///
        /// where the literal is the *mask* (1.0 for a bool, but also 6.0, -1.0,
        /// -pi, ...).  A truthiness-only translation loses the mask, so a literal
        /// lane becomes <c>(x != 0) ? mask : 0</c>, which is exactly what the
        /// bitwise AND computes for a 0 / all-ones source.  Both-literal lanes
        /// constant-fold; two registers stay boolean.
        /// </summary>
        private void AndOr(List<Operand> o, bool isAnd)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++)
            {
                bool am = ImmLane(o[1], i, o[0].Swizzle, out float av);
                bool bm = ImmLane(o[2], i, o[0].Swizzle, out float bv);
                int ab = BitConverter.SingleToInt32Bits(av);
                int bb = BitConverter.SingleToInt32Bits(bv);

                if (isAnd)
                {
                    if (am && bm) r.Add(F(BitConverter.Int32BitsToSingle(ab & bb)));
                    else if (bm && bv == 0f) r.Add("0.0");       // and with a zero mask is dead
                    else if (am && av == 0f) r.Add("0.0");
                    else if (bm) r.Add($"((({a[i]}) != 0.0) ? {F(bv)} : 0.0)");
                    else if (am) r.Add($"((({b[i]}) != 0.0) ? {F(av)} : 0.0)");
                    else r.Add($"(((({a[i]}) != 0.0) && (({b[i]}) != 0.0)) ? 1.0 : 0.0)");
                }
                else
                {
                    if (am && bm) r.Add(F(BitConverter.Int32BitsToSingle(ab | bb)));
                    else if (bm && bv == 0f) r.Add($"(({a[i]}) != 0.0 ? 1.0 : 0.0)");
                    else if (am && av == 0f) r.Add($"(({b[i]}) != 0.0 ? 1.0 : 0.0)");
                    else r.Add($"(((({a[i]}) != 0.0) || (({b[i]}) != 0.0)) ? 1.0 : 0.0)");
                }
            }
            Assign(o[0], r);
        }

        private void Mad(List<Operand> o)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var c = Comps(o[3], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"mad({a[i]}, {b[i]}, {c[i]})");
            Assign(o[0], r);
        }

        // ---- integer ops (operate on int-typed temps; we keep them explicit) ----

        private void IBin(List<Operand> o, string op)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"((int)({a[i]}) {op} (int)({b[i]}))");
            Assign(o[0], r);
        }

        private void IMad(List<Operand> o)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var c = Comps(o[3], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"(((int)({a[i]}) * (int)({b[i]})) + (int)({c[i]}))");
            Assign(o[0], r);
        }

        private void INeg(List<Operand> o)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"(-(int)({a[i]}))");
            Assign(o[0], r);
        }

        /// <summary>
        /// Unsigned integer division.
        ///
        /// The DXBC spec defines udiv with a zero divisor as returning
        /// 0xFFFFFFFF, and HLSL rejects a literal zero divisor outright
        /// ("Unsigned integer divide by zero"), so a plain `/` is not
        /// expressible when the dividend is the constant 0 -- which is what
        /// CameraFilterPack_FX_DigitalMatrix actually does:
        ///     udiv r0.x, null, r0.y, r0.x     (srcA = null = 0)
        /// Encode the spec behaviour explicitly instead: the result is -1 when
        /// the divisor is zero and 0 otherwise, because a zero numerator can
        /// never produce anything else.
        /// </summary>
        private void UDiv(List<Operand> o)
        {
            int n = N(o[0]);
            // Four-operand form: dst(quotient), dst(remainder or null),
            // src0 = numerator, src1 = divisor. The null second destination is
            // how the disassembler spells "remainder discarded" -- treating it
            // as a source turned `udiv r0.x, null, r0.y, r0.x` into 0 / r0.y.
            var num = o.Count > 2 ? o[2] : null;
            var den = o.Count > 3 ? o[3] : null;

            bool zeroNumerator =
                num != null && (num.Kind == "null" ||
                    (num.Imm != null && AllZero(num.Imm)));

            if (zeroNumerator && den != null)
            {
                var b = Comps(den, n, o[0].Swizzle);
                var r = new List<string>();
                for (int i = 0; i < n; i++)
                    r.Add($"(((int)({b[i]}) == 0) ? -1 : 0)");
                Assign(o[0], r);
                return;
            }

            // Four-operand form: the sources are operands 2 and 3, not 1 and 2.
            // IBin would read the discarded remainder destination as a source.
            if (num != null && den != null)
            {
                var a = Comps(num, n, o[0].Swizzle);
                var b = Comps(den, n, o[0].Swizzle);
                var r = new List<string>();
                for (int i = 0; i < n; i++) r.Add($"((int)({a[i]}) / (int)({b[i]}))");
                Assign(o[0], r);
                return;
            }

            IBin(o, "/");
        }

        private static bool AllZero(float[] v)
        {
            foreach (var x in v) if (x != 0f) return false;
            return true;
        }

        private void ICmp(List<Operand> o, string op)
        {
            int n = N(o[0]);
            var a = Comps(o[1], n, o[0].Swizzle);
            var b = Comps(o[2], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++) r.Add($"(((int)({a[i]}) {op} (int)({b[i]})) ? 1 : 0)");
            Assign(o[0], r);
        }

        private void Movc(List<Operand> o, bool sat)
        {
            int n = N(o[0]);
            var c = Comps(o[1], n, o[0].Swizzle);
            var a = Comps(o[2], n, o[0].Swizzle);
            var b = Comps(o[3], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++)
            {
                // movc branches on the condition being non-zero, not on it clearing 0.5
                string x = $"{c[i]} != 0.0 ? {a[i]} : {b[i]}";
                r.Add(sat ? $"saturate({x})" : x);
            }
            Assign(o[0], r);
        }

        /// <summary>
        /// <c>ubfe dst, width, offset, src</c> -- unsigned bit-field extract.
        /// </summary>
        private void Ubfe(List<Operand> o)
        {
            int n = N(o[0]);
            int width = (int)(o[1].Imm != null && o[1].Imm.Length > 0 ? o[1].Imm[0] : 32f);
            int off = (int)(o[2].Imm != null && o[2].Imm.Length > 0 ? o[2].Imm[0] : 0f);
            uint mask = width >= 32 ? 0xFFFFFFFFu : ((1u << width) - 1u);
            var src = Comps(o[3], n, o[0].Swizzle);
            var r = new List<string>();
            for (int i = 0; i < n; i++)
                r.Add($"((uint)({src[i]}) >> {off}) & {mask}u");
            Assign(o[0], r);
        }

        /// <summary>
        /// <c>ld_structured_indexable(structured_buffer, stride=S)(...) dst, index, byteOffset, tN.xyzw</c>
        /// -- read consecutive floats starting at the byte offset inside element
        /// <c>index</c>.  The stride is not a multiple of 16, so the element is
        /// declared as a flat float array of stride/4 entries, which makes every
        /// access land on its exact float regardless of packing.
        /// </summary>
        private void LdStructured(Instruction ins)
        {
            if (ins.Operands.Count < 4) return;
            int n = N(ins.Operands[0]);
            int reg = ins.Operands[3].Index;
            string idx = SrcExpr(ins.Operands[1]);
            int off = (int)(ins.Operands[2].Imm != null && ins.Operands[2].Imm.Length > 0
                ? ins.Operands[2].Imm[0] : 0f);
            var r = new List<string>();
            for (int i = 0; i < n; i++)
                r.Add($"t{reg}[{idx}].v[{off / 4 + i}]");
            Assign(ins.Operands[0], r);
        }

        private void Dp(List<Operand> o, int k)
        {
            // A dot product pairs component i of each source; the destination
            // swizzle does NOT select source components.  Passing o[0].Swizzle here
            // made "dp3 r5.x, l(1,2,1,0), v" index every source component by 'x'.
            var a = Comps(o[1], k, "xyzw");
            var b = Comps(o[2], k, "xyzw");
            // drop the swizzle from the operand prefix: components already carry .x/.y/...
            var terms = new List<string>();
            for (int i = 0; i < k; i++) terms.Add($"{Strip(a[i])} * {Strip(b[i])}");
            string expr = string.Join(" + ", terms);
            Line($"{DstExpr(o[0])} = {expr};");
        }

        /// <summary>Comps() returns "base.swz.c"; collapse a single-component tail for readability.</summary>
        private static string Strip(string s)
        {
            // "r0.xyxx.x" -> "r0.x" ; "0.222" stays.
            // The swizzle only ever follows a closing bracket, so any dot inside
            // "[...]" must not be mistaken for the previous swizzle separator:
            // "icb[(int)(r0.z)+0].z" used to collapse to "icb[(int)(r0.z".
            int lastDot = s.LastIndexOf('.');
            if (lastDot < 0) return s;
            string tail = s.Substring(lastDot + 1);
            if (tail.Length != 1) return s;
            int swzStart = s.LastIndexOf(']', lastDot) + 1;
            int prevDot = s.LastIndexOf('.', lastDot - 1);
            if (prevDot < swzStart) return s;
            string swz = s.Substring(prevDot + 1, lastDot - prevDot - 1);
            if (swz.Length > 1 && swz.Contains(tail)) return s.Substring(0, prevDot) + "." + tail;
            return s;
        }

        private void Sincos(List<Operand> o)
        {
            // Two encodings appear in fxc output:
            //   sincos dst.xy, src           -> dst.x = sin(src), dst.y = cos(src)
            //   sincos dstSin, dstCos, src   -> separate sin/cos destinations,
            //                                   either of which may be `null`
            Operand sinDst, cosDst, src;
            if (o.Count >= 3) { sinDst = o[0]; cosDst = o[1]; src = o[2]; }
            else { sinDst = o[0]; cosDst = null; src = o[1]; }

            string x = SrcExpr(src);

            if (cosDst == null)
            {
                string sw = string.IsNullOrEmpty(sinDst.Swizzle) ? "xy" : sinDst.Swizzle;
                string name = RegName(sinDst);
                Line($"{name}.{sw[0]} = sin({x});");
                if (sw.Length >= 2) Line($"{name}.{sw[1]} = cos({x});");
                return;
            }

            if (sinDst.Kind != "null") Line($"{DstExpr(sinDst)} = sin({x});");
            if (cosDst.Kind != "null") Line($"{DstExpr(cosDst)} = cos({x});");
        }

        /// <summary>Turn "r1.x+0" into "(int)(r1.x)+0" for indexable-temp indexing.</summary>
        private static string IdxExpr(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "0";
            string e = raw.Replace(" ", "");
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < e.Length)
            {
                char c = e[i];
                if ((c == 'r' || c == 'x') && i + 1 < e.Length && char.IsDigit(e[i + 1]))
                {
                    int j = i + 1;
                    while (j < e.Length && char.IsDigit(e[j])) j++;
                    string reg = e.Substring(i, j - i);
                    if (j + 1 < e.Length && e[j] == '.')
                    {
                        char comp = e[j + 1];
                        sb.Append("(int)(").Append(reg).Append('.').Append(comp).Append(')');
                        j += 2;
                    }
                    else sb.Append(reg);
                    i = j;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Texture dimension declared for register tN (2 = 2D, 3 = 3D, 4 = cube, 1 = 1D).</summary>
        private int TexDimOf(int reg)
        {
            foreach (var d in _p.Decls)
                if (d.Kind != null && d.Kind.StartsWith("texture") && d.Register == reg)
                    return d.TexDim;
            return 2;
        }

        private void Sample(List<Operand> o, Operand lod)
        {
            string dst = DstExpr(o[0]);
            int texIdx = o[2].Index;
            int dim = TexDimOf(texIdx);
            int ncomp = dim == 1 ? 1 : dim == 2 ? 2 : 3;

            var coordOp = o[1];
            List<string> cc = new List<string>();
            if (coordOp.Kind == "l")
            {
                for (int i = 0; i < ncomp; i++)
                    cc.Add(F(coordOp.Imm[Math.Min(i, coordOp.Imm.Length - 1)]));
            }
            else
            {
                string bn = coordOp.Kind switch
                {
                    "cb" => CbRow(coordOp) ?? $"cb{coordOp.Index}[{Arr(coordOp)}]",
                    "x" => $"x{coordOp.Index}[" + (Arr(coordOp)) + "]",
                    _ => RegName(coordOp)
                };
                string sw = string.IsNullOrEmpty(coordOp.Swizzle) ? "xyzw" : coordOp.Swizzle;
                for (int i = 0; i < ncomp; i++)
                    cc.Add($"{bn}.{sw[Math.Min(i, sw.Length - 1)]}");
            }
            string coord = ncomp == 1 ? cc[0] : $"float{ncomp}({string.Join(", ", cc)})";

            // tex2Dlod takes one scalar level; a vector immediate must be reduced
            // to its first component or the constructor exceeds four components.
            string lodScalar = "";
            if (lod != null)
            {
                lodScalar = lod.Kind == "l"
                    ? F(lod.Imm.Length > 0 ? lod.Imm[0] : 0f)
                    : SrcExpr(lod);
            }

            string fn;
            if (lod != null)
            {
                fn = dim == 4
                    ? $"texCUBElod({Tex(texIdx)}, float4({coord}, {lodScalar}))"
                    : dim == 3
                        ? $"tex3Dlod({Tex(texIdx)}, float4({coord}, {lodScalar}))"
                        : $"tex2Dlod({Tex(texIdx)}, float4({coord}, 0, {lodScalar}))";
            }
            else
            {
                fn = dim switch
                {
                    1 => $"tex1D({Tex(texIdx)}, {coord})",
                    3 => $"tex3D({Tex(texIdx)}, {coord})",
                    4 => $"texCUBE({Tex(texIdx)}, {coord})",
                    _ => $"tex2D({Tex(texIdx)}, {coord})",
                };
            }
            Line($"{dst} = {fn};");
        }
    }
}





