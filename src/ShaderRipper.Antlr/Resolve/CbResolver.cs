using System;
using System.Collections.Generic;
using System.Linq;
using ShaderRipper.Antlr.Ast;
using ShaderRipper.Antlr.Data;

namespace ShaderRipper.Antlr.Resolve
{
    /// <summary>
    /// Works out which Unity constant buffer each <c>dcl_constantbuffer CBn</c>
    /// really is, and rewrites <c>cbN[row]</c> accesses to the named uniform that
    /// occupies that slot.
    ///
    /// Nothing here is guessed.  Two independent pieces of evidence are combined:
    ///
    ///   1. the pass's name table (<c>SerializedPass.m_NameIndices</c>) lists the
    ///      constant-buffer names the shader uses -- e.g. "$Globals",
    ///      "UnityPerDraw", "UnityPerFrame";
    ///   2. the row layout of every Unity cbuffer is fixed by the shipped
    ///      <c>CGIncludes/UnityShaderVariables.cginc</c>, and a member is only
    ///      accepted when its name is also present in the name table.
    ///
    /// A cbuffer is renamed only when every row it reads is covered by a
    /// name-confirmed member, and the assignment is a bijection across the stage.
    /// </summary>
    internal sealed class CbResolver
    {
        public readonly Dictionary<int, string> CbName = new();
        public readonly Dictionary<(int cb, int row), string> RowExpr = new();
        /// <summary>Component count the named member actually declares, per row.</summary>
        public readonly Dictionary<(int cb, int row), int> RowWidth = new();
        /// <summary>Scalar members that occupy one lane of a row: (cb,row,component) -> name.</summary>
        public readonly Dictionary<(int cb, int row, int comp), string> CompExpr = new();
        public readonly Dictionary<int, string> TexName = new();
        public readonly Dictionary<int, List<(string name, string type)>> GlobalsDecl = new();
        public readonly HashSet<int> FullyMapped = new();
        public readonly List<string> Notes = new();
        /// <summary>Authoritative byte size of each cbuffer, from the parameter blob.</summary>
        public readonly Dictionary<int, int> CbSize = new();

        /// <summary>
        /// Registers whose identity the asset itself pins down: the parameter blob        /// records the byte size and the extent fits inside it, and -- when the blob
        /// carries its register index list -- that list agrees.  Once a register is
        /// confirmed, its member names follow from the layout table alone, so the
        /// name-table gate is not needed for naming (it exists to decide *which*
        /// buffer a register is).  A register identified by the fallback search is
        /// not confirmed and keeps the stricter gate.
        /// </summary>
        public readonly HashSet<int> ConfirmedCb = new();

        private readonly HashSet<string> _nameTable;

        /// <summary>
        /// Is this member one the shader references?  Array elements are recorded in
        /// the name table without their index (<c>unity_LightColor</c>, not
        /// <c>unity_LightColor[3]</c>), so the index is stripped before lookup.
        /// </summary>
        private bool InTable(string name)
        {
            if (_nameTable.Contains(name)) return true;
            int i = name.IndexOf('[');
            if (i > 0 && _nameTable.Contains(name.Substring(0, i))) return true;
            // Unity's own runtime falls back to its hard-coded built-in tables
            // when a name index is not in the pass table (see
            // ReconstructParamNamesFromTable / GetBuiltinVectorParamName in
            // UnityPlayer).  Engine members (unity_OcclusionMaskSelector,
            // unity_SHAr, ...) are exactly the ones game builds leave out of
            // m_NameIndices, so the official list is the authority for them.
            if (UnityBuiltinParams.IsBuiltin(name)) return true;
            return false;
        }

        /// <summary>Constant-buffer layout recorded in this program's parameter blob.</summary>
        public List<BlobTool.CbInfo> ParamCbs;

        /// <summary>
        /// <c>m_Parameters.m_ConstantBuffers</c> for <c>$Globals</c> from this stage,
        /// straight out of the asset.  When present this is the authoritative member
        /// table -- exact name, exact byte offset, exact component count -- so no
        /// layout has to be inferred from the property list, the disassembly or an
        /// outside source.
        /// </summary>
        public AssetConstantBuffer AssetGlobals;

        /// <summary>
        /// Row span of each $Globals matrix member, gathered from every program of
        /// the shader (see the note where CgProgramWriter builds it).  A buffer's
        /// own reflection is incomplete for some variants, so the span cannot be
        /// inferred from one layout alone.
        /// </summary>
        public Dictionary<string, int> MatrixRows;

        /// <summary>
        /// 整个 shader 所有子程序的参数块。每个子程序只记自己读到的成员，单看一份
        /// 算不出"到下一个成员的距离"，矩阵就判不出来；按缓冲尺寸合起来才完整。
        /// </summary>
        private List<BlobTool.CbInfo> AllParamCbs;

        /// <summary>本次解析确认过的 $Globals 布局（供同一 shader 的另一个子程序复用）。</summary>
        public TmpLayouts.Layout ChosenLayout;

        /// <summary>同一 shader 里别的子程序已经确认的布局（只当线索，仍要过覆盖检查）。</summary>
        private TmpLayouts.Layout _layoutHint;

        /// <summary>Curated $Globals row names for this shader (nullable).</summary>
        public GlobalsNames Names;

        private CbResolver(HashSet<string> nameTable) { _nameTable = nameTable; }

        private Ast.Module _prog;

        /// <summary>Texture properties declared by the shader (name + declared type).</summary>
        public static readonly HashSet<int> TextureTypes = new() { 4, 5, 6, 7, 8 };

        /// <summary>Widest component index (+1) a swizzle touches.</summary>
        private static int MaxComponent(string sw)
        {
            int m = 1;
            foreach (char c in sw)
                m = Math.Max(m, c switch { 'x' => 1, 'y' => 2, 'z' => 3, 'w' => 4, _ => 4 });
            return m;
        }

        /// <summary>Bitmask of the components a swizzle touches (bit 0 = x).</summary>
        private static int ComponentMask(string sw)
        {
            // The grammar's SWIZZLE token carries its own dot (`.xyxx`), so only the
            // component letters count.  Reading the dot as component 0 marked lane x
            // as read on every swizzled operand, and a row a program only touched in
            // .y then arrived at the declaration layer as "x is read too" -- which is
            // where the `_cbN_rowK_x` placeholders came from.
            if (string.IsNullOrEmpty(sw)) return 0xF;
            int m = 0;
            foreach (char c in sw)
            {
                int bit = c switch { 'x' => 0, 'y' => 1, 'z' => 2, 'w' => 3, _ => -1 };
                if (bit >= 0) m |= 1 << bit;
            }
            return m == 0 ? 0xF : m;
        }

        /// <summary>register -> row -> component mask this stage reads.</summary>
        public Dictionary<int, SortedDictionary<int, int>> RowUse = new();

        /// <summary>Registers read with an index that is not a literal.</summary>
        private HashSet<int> _dynamicIndex = new();

        /// <summary>
        /// True when every lane this stage reads from the row carries a name, so the
        /// program can drop the raw <c>cbN[...]</c> declaration without leaving an
        /// undefined identifier behind.
        /// </summary>
        /// <summary>
        /// True when some row of this register is read but still has no name, so the
        /// emitted program needs the raw <c>cbN[...]</c> array.  Decided at emit time
        /// because cross-variant filling can name rows after the mapping pass ran;
        /// trusting the earlier snapshot left a dead array declaration (and a
        /// duplicate of the buffer) in front of fully named code.
        /// </summary>
        public bool HasDynamicIndex(int reg) => _dynamicIndex.Contains(reg);

        public bool NeedsRawArray(int reg, int extent = int.MaxValue)
        {
            // A register indexed by a runtime expression cannot be expressed as
            // named members at all, so the array has to stay even when every
            // constant row is named.
            if (_dynamicIndex.Contains(reg)) return true;
            if (!RowUse.TryGetValue(reg, out var ru)) return false;
            foreach (var kv in ru)
            {
                // Rows past the emitted declaration's own length cannot be read
                // through it anyway (the same bound the fill pass applies).
                if (kv.Key >= extent) continue;
                if (!RowFullyNamed(reg, kv.Key)) return true;
            }
            return false;
        }

        private bool RowFullyNamed(int reg, int row)
        {
            if (RowExpr.ContainsKey((reg, row))) return true;
            int mask = 0xF;
            if (RowUse.TryGetValue(reg, out var ru) && ru.TryGetValue(row, out int m)) mask = m;
            for (int c = 0; c < 4; c++)
                if ((mask & (1 << c)) != 0 && !CompExpr.ContainsKey((reg, row, c)))
                    return false;
            return true;
        }

        /// <summary>
        /// Does the asset's own reflection describe this member as a matrix?
        ///
        /// This replaces the hand-maintained <c>unity_matrix_names.txt</c>: the
        /// name list could only say "a member called unity_WorldToLight is probably
        /// a float4x4", while the reflection says so for this exact compile, with
        /// its row count.  The name list also had to be regenerated whenever Unity
        /// renamed something; the reflection ships inside the data being read.
        ///
        /// Two further sources answer the same question when the reflection cannot:
        /// the engine-name table (a name Unity declares as a <c>float4x4</c>, like
        /// <c>unity_WorldToLight</c>) and the span arithmetic at the call site.  A
        /// stage that reads only one column of a global matrix never sees the other
        /// rows in its parameter blob, so the reflection alone is silent there.
        /// </summary>
        private bool IsDeclaredMatrix(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (MatrixRows != null && MatrixRows.TryGetValue(name, out int rows) && rows >= 2)
                return true;
            if (AssetGlobals != null)
                foreach (var am in AssetGlobals.Members)
                    if (am.Name == name && am.IsMatrix && am.RowCount >= 2) return true;
            return Emit.UnityIncludes.IsMatrixMember(name);
        }

        public static CbResolver Resolve(Ast.Module prog, ICollection<string> nameTable,
                                         IReadOnlyList<Property> props,                                         List<BlobTool.CbInfo> paramCbs = null,
                                         int blobIndex = -1,
                                         GlobalsNames names = null,
                                         AssetConstantBuffer assetGlobals = null,
                                         Dictionary<int, string> identityHint = null,
                                         List<BlobTool.CbInfo> allParamCbs = null,
                                         bool tmpLayout = false,
                                         TmpLayouts.Layout layoutHint = null,
                                         System.Collections.Generic.List<(string Name, int Index, int Sampler)> texParams = null,
                                         Dictionary<string, int> matrixRows = null)
        {
            var r = new CbResolver(new HashSet<string>(nameTable ?? new List<string>(), StringComparer.Ordinal));
            r.MatrixRows = matrixRows;
            r._prog = prog;
            r.ParamCbs = paramCbs;
            r.AllParamCbs = allParamCbs;
            r.Names = names;
            r._layoutHint = layoutHint;
            r.AssetGlobals = assetGlobals;
            if (prog == null) return r;

            var decls = prog.Decls.Where(d => d.Kind == "cbuffer").OrderBy(d => d.Register).ToList();
            var used = new Dictionary<int, SortedSet<int>>();
            var dynamicIndex = new HashSet<int>();
            var use = new Dictionary<int, SortedDictionary<int, int>>();
            foreach (var d in decls) used[d.Register] = new SortedSet<int>();
            foreach (var ins in prog.Code)
                foreach (var o in ins.Args)
                    if (o.Kind == Ast.RegKind.Cb && used.ContainsKey(o.Index))
                    {
                        if (o.Row >= 0)
                        {
                            used[o.Index].Add(o.Row);
                            int mask = ComponentMask(o.Swizzle);
                            if (!use.TryGetValue(o.Index, out var rowUse))
                                use[o.Index] = rowUse = new SortedDictionary<int, int>();
                            rowUse.TryGetValue(o.Row, out int prev);
                            rowUse[o.Row] = prev | mask;
                        }
                        else dynamicIndex.Add(o.Index);
                    }
            r._dynamicIndex = dynamicIndex;
            var candidates = r.CbufferNamesInTable();
            var chosen = r.IdentityFromParamBlob(decls, used) ?? r.AssignBijection(decls, used, candidates);
            r.AssignFromSiblingHint(decls, used, chosen, identityHint);
            if (!chosen.Values.Contains("$Globals") && TmpLayouts.Enabled)
            {
                var hits = new List<int>();
                foreach (var d in decls)
                {
                    if (chosen.ContainsKey(d.Register) || used[d.Register].Count == 0) continue;
                    if (TmpLayouts.Match(d.Count, r._nameTable, used[d.Register], out _, r.TmpAnchors()) != null ||
                        TmpLayouts.MapFrom(r._layoutHint, d.Count, used[d.Register]) != null)
                        hits.Add(d.Register);
                }
                if (Environment.GetEnvironmentVariable("SHADERRIP_TMPDBG") == "1")
                    Console.Error.WriteLine($"[tmpid] prog={prog.Code.Count}ins/{prog.Decls.Count}dcl decls=[{string.Join(",", decls.Select(x => x.Register))}] "
                        + $"hits=[{string.Join(",", hits)}] hint={(r._layoutHint == null ? "null" : r._layoutHint.Rows + "r")} "
                        + $"reads={string.Join(";", decls.Select(x => x.Register + ":" + string.Join("/", used[x.Register])))}");
                if (hits.Count == 1)
                {
                    chosen[hits[0]] = "$Globals";
                    r.ConfirmedCb.Add(hits[0]);
                    r.Notes.Add($"// cb{hits[0]} = $Globals (the compiled layout table covers every row this "
                                + "program reads, and the parameter blob's members line up with it)");
                }
            }
            foreach (var kv in chosen) r.CbName[kv.Key] = kv.Value;
            r.RowUse = use;
            foreach (var d in decls)
            {
                if (!r.CbName.TryGetValue(d.Register, out var cbname)) continue;
                if (cbname == "$Globals")
                {
                    r.MapGlobalsRows(d, used[d.Register], props);
                    continue;
                }
                foreach (int row in used[d.Register])
                {
                    var m = UnityCbuffers.Find(cbname, row);
                    bool known = m != null && (r.InTable(m.Name) || r.ConfirmedCb.Contains(d.Register));
                    string expr = known
                        ? (m.IsMatrix ? MatrixColumn(m.Name, row - m.Row, m.Rows) : m.Name) : null;

                    if (expr != null && (m.FillsRow || m.IsMatrix) &&
                        !UnityCbuffers.RowIsShared(cbname, row))
                    {
                        r.RowExpr[(d.Register, row)] = expr;
                        r.RowWidth[(d.Register, row)] = m.IsMatrix ? 4 : m.IsScalar ? 1 : m.IsVector ? 3 : 4;
                        r.NoteNamed(d.Register, m);
                        continue;
                    }

                    int mask = used[d.Register].Count > 0 &&
                               use.TryGetValue(d.Register, out var rowUse) &&
                               rowUse.TryGetValue(row, out int rm) ? rm : 0xF;
                    for (int c = 0; c < 4; c++)
                    {
                        if ((mask & (1 << c)) == 0) continue;
                        var lane = UnityCbuffers.FindLane(cbname, row, c);
                        if (lane == null || !(r.InTable(lane.Name) || r.ConfirmedCb.Contains(d.Register))) continue;
                        r.CompExpr[(d.Register, row, c)] = lane.IsMatrix
                            // Component c of a matrix column is M[<that row>][<column>].
                            ? $"{lane.Name}[{c - lane.Col}].{"xyzw"[row - lane.Row]}"
                            : lane.Span == 1 ? lane.Name
                            : lane.Name + "." + "xyzw"[c - lane.Col];
                        r.NoteNamed(d.Register, lane);
                    }
                }
                if (!dynamicIndex.Contains(d.Register) &&
                    used[d.Register].All(row => r.RowFullyNamed(d.Register, row)))
                    r.FullyMapped.Add(d.Register);
            }

            r.MapTextures(prog, props, texParams);

            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
            {
                foreach (var d in decls)
                {
                    string rows = string.Join(",", used[d.Register]);
                    string rex = string.Join(" ", r.RowExpr
                        .Where(k => k.Key.cb == d.Register)
                        .Select(k => $"r{k.Key.row}={k.Value}"));
                    string cex = string.Join(" ", r.CompExpr
                        .Where(k => k.Key.cb == d.Register)
                        .Select(k => $"r{k.Key.row}.{"xyzw"[k.Key.comp]}={k.Value}"));
                    Console.Error.WriteLine(
                        $"[cbdbg] cb{d.Register} name={r.CbName.GetValueOrDefault(d.Register, "?")} "
                        + $"asset={(r.AssetGlobals == null ? "null" : r.AssetGlobals.Members.Count.ToString())} "
                        + $"param={(r.ParamCbs == null ? "null" : string.Join("/", r.ParamCbs.Select(c => c.Name + ":" + c.Size + ":" + c.Members.Count + "@" + c.Register)))} "
                        + $"dcl={d.Count} rows=[{rows}] rowExpr=[{rex}] compExpr=[{cex}] "
                        + $"names=[{string.Join(",", r._nameTable.OrderBy(x => x, StringComparer.Ordinal))}]");
                }
            }
            // ---- last resort: the program's own compiled member table ----
            // The parameter blob records the exact members (name, byte offset,
            // type, dim) of every constant buffer this compile kept.  When the
            // curated table, the name table and the heuristic routes all missed
            // a row, its name is still here.  Conservative: only whole-float4
            // rows are named, so a partial read can never pick up a wrong lane.
            if (r.ParamCbs != null && r.ParamCbs.Count > 0)
            {
                var cbDecls = prog.Decls.Where(d => d.Kind == "cbuffer").ToList();
                if (cbDecls.Count == r.ParamCbs.Count)
                {
                    for (int i = 0; i < cbDecls.Count; i++)
                    {
                        var d = cbDecls[i];
                        var pcb = r.ParamCbs[i];
                        if (pcb.Members.Count == 0) continue;
                        if (!used.TryGetValue(d.Register, out var rowsUsed)) continue;
                        foreach (int row in rowsUsed)
                        {
                            var key = (d.Register, row);
                            if (r.RowExpr.ContainsKey(key) ||
                                Enumerable.Range(0, 4).Any(c => r.CompExpr.ContainsKey((d.Register, row, c))))
                                continue;
                            var mem = pcb.Members.FirstOrDefault(mm => mm.offset == row * 16);
                            if (string.IsNullOrEmpty(mem.name) || mem.dim != 4) continue;
                            r.RowExpr[key] = mem.name;
                            r.RowWidth[key] = 4;
                            if (!r.GlobalsDecl.TryGetValue((int)d.Register, out var gdecls))
                                r.GlobalsDecl[(int)d.Register] = gdecls = new List<(string name, string type)>();
                            if (!gdecls.Any(x => x.name == mem.name))
                                gdecls.Add((mem.name, "float4"));
                        }
                    }
                }
            }
            return r;
        }

        /// <summary>
        /// Identify each constant buffer from the program's parameter blob.
        ///
        /// The blob lists exactly the buffers this program kept, in register order,
        /// the asset has confirmed the ordering; it is never used to *drop* a buffer,
        /// because not every buffer appears in it.
        ///
        /// Returns null when the blob is absent, lists a different number of buffers,
        /// or cannot explain the read extents.
        /// </summary>
        private Dictionary<int, string> IdentityFromParamBlob(List<Ast.Decl> decls,
                                                             Dictionary<int, SortedSet<int>> used)
        {
            if (ParamCbs == null || ParamCbs.Count == 0) return null;
            bool dbg = Environment.GetEnvironmentVariable("SHADERRIP_TMPDBG") == "1";

            // The blob's buffer list is the shader's, not the program's: it keeps a
            // record for every constant buffer the pass compiled, including the
            // Unity ones this particular program never declares, and in the asset's
            // own order.  `MatchBySize` pairs them by the register the asset
            // recorded where it can, and only falls back to order when there is no
            // register evidence at all.
            var pairs = MatchBySize(decls, ParamCbs);
            if (pairs == null)
            {
                if (dbg) Console.Error.WriteLine($"[blobmiss] count {ParamCbs.Count} != dcl {decls.Count}");
                return null;
            }

            var res = new Dictionary<int, string>();
            foreach (var (d, cb) in pairs)
            {
                if (cb.Name == null || cb.Size <= 0) return null;
                if (Math.Max(1, d.Count) * 16 > cb.Size)
                {
                    if (dbg) Console.Error.WriteLine($"[blobmiss] cb{d.Register} dcl={d.Count} > {cb.Name}:{cb.Size}");
                    return null;
                }
                res[d.Register] = cb.Name;
                CbSize[d.Register] = cb.Size;
                ConfirmedCb.Add(d.Register);
            }

            if (res.Count == 0) return null;

            if (res.Values.Distinct().Count() != res.Count)
            {
                if (dbg) Console.Error.WriteLine("[blobmiss] duplicate buffer names: " + string.Join(",", res.Values));
                return null;
            }

            var listed = ParamCbs.Where(c => c.Register >= 0).ToList();
            if (listed.Count > 0)
            {
                bool agrees = listed.All(c => ParamCbs.IndexOf(c) == c.Register);
                Notes.Add(agrees
                    ? "// cbuffer order confirmed by the asset's own register index"
                    : "// cbuffer order taken from the blob's declaration order;"
                      + " the asset's index list disagrees, so it was ignored");
            }
            return res;
        }

        /// <summary>
        /// Pair each `dcl_constantbuffer` with the blob record that describes it.
        ///
        /// The blob's trailing register map names the slot each buffer was bound
        /// to (`UnityLighting@2`), and that is the program's own `dcl_constantbuffer`
        /// register, so it identifies the buffer outright.  Falling back to list
        /// position is what the old code did for every program whose blob kept a
        /// buffer the program dropped, and the shift then mislabelled every buffer
        /// after it -- a 46-row block read as 7 under another buffer's name.
        /// </summary>
        private static List<(Ast.Decl d, BlobTool.CbInfo cb)> MatchBySize(
            List<Ast.Decl> decls, List<BlobTool.CbInfo> cbs)
        {
            var res = new List<(Ast.Decl, BlobTool.CbInfo)>(decls.Count);

            // A record smaller than one constant-buffer row is a parse artefact of
            // the record scan, not a buffer: nothing can be pinned inside it.
            var real = cbs.Where(c => c.Size >= 16).ToList();
            if (real.Count == 0) return null;
            cbs = real;

            // Registers the asset itself recorded come first: they identify a buffer
            // outright, and equal counts do not make the two lists parallel -- the
            // blob keeps the pass's buffers in its own order, which need not be the
            // declaration order.  Anything the map leaves out ($Globals carries no
            // register) is matched by size afterwards.
            if (cbs.Any(c => c.Register >= 0))
            {
                var used = new HashSet<BlobTool.CbInfo>();
                var byReg = new Dictionary<int, BlobTool.CbInfo>();
                foreach (var c in cbs)
                    if (c.Register >= 0) byReg[c.Register] = c;

                var pending = new List<Ast.Decl>();
                foreach (var d in decls)
                {
                    if (byReg.TryGetValue(d.Register, out var hit) && used.Add(hit))
                    {
                        res.Add((d, hit));
                        continue;
                    }
                    pending.Add(d);
                }
                foreach (var d in pending)
                {
                    int need = Math.Max(1, d.Count) * 16;
                    var pick = cbs.FirstOrDefault(c => !used.Contains(c) && c.Size >= need);
                    // No record left that can hold it: this declaration has no entry
                    // in the blob at all.  Leaving it unnamed is correct; failing the
                    // whole table would throw away the buffers the map did identify.
                    if (pick == null) continue;
                    used.Add(pick);
                    res.Add((d, pick));
                }
                return res;
            }

            if (decls.Count == cbs.Count)
            {
                for (int i = 0; i < decls.Count; i++) res.Add((decls[i], cbs[i]));
                return res;
            }

            int at = 0;
            foreach (var d in decls)
            {
                int need = Math.Max(1, d.Count) * 16;
                while (at < cbs.Count && cbs[at].Size < need) at++;
                if (at >= cbs.Count) return null;
                res.Add((d, cbs[at]));
                at++;
            }
            return res;
        }

        /// <summary>
        /// Record a Unity-cbuffer member the program actually references, so the
        /// emitter can declare it when the include does not.
        /// </summary>
        private void NoteNamed(int reg, UnityCbuffers.Member m)
        {
            if (!GlobalsDecl.TryGetValue(reg, out var list))
                GlobalsDecl[reg] = list = new List<(string name, string type)>();
            if (list.Any(x => x.name == m.Name)) return;
            list.Add((m.Name, UnityCbuffers.TypeOf(m)));
        }

        /// <summary>cbuffer names the shader's own name table lists.</summary>
        private List<string> CbufferNamesInTable()
        {
            var res = new List<string>();
            foreach (string n in _nameTable)
                if (n == "$Globals" || UnityCbuffers.Members.Any(m => m.Cb == n))
                    res.Add(n);
            return res.OrderBy(n => n == "$Globals" ? 0 : 1)
                      .ThenBy(n => UnityCbuffers.Members.FindIndex(m => m.Cb == n))
                      .ToList();
        }

        /// <summary>
        /// 用同一个 shader 里**别的子程序**已经确认的寄存器身份做兜底。
        ///
        /// <c>_ZBufferParams</c> 这类成员在编译产物里不留名字（只在
        /// <c>LinearEyeDepth</c> 的内联展开里出现），名字表查不到，双射会放弃整块，
        /// 行号就全留成 cbN[row]。但同一个 shader 的另一个变体往往能确认
        /// "cb1 = UnityPerCamera"，身份在同 shader 内是稳定的——只要读取的行都落在这个
        /// 缓冲的布局里，就可以照抄身份，再按布局逐行命名。
        /// </summary>
        private void AssignFromSiblingHint(List<Ast.Decl> decls,
                                           Dictionary<int, SortedSet<int>> used,
                                           Dictionary<int, string> chosen,
                                           Dictionary<int, string> hint)
        {
            if (hint == null || hint.Count == 0) return;
            foreach (var d in decls)
            {
                if (chosen.ContainsKey(d.Register)) continue;
                if (!hint.TryGetValue(d.Register, out var name)) continue;
                if (name == "$Globals" || UnityCbuffers.RowCount(name) <= 0) continue;
                if (used[d.Register].Any(r => UnityCbuffers.Find(name, r) == null)) continue;

                chosen[d.Register] = name;
                ConfirmedCb.Add(d.Register);
                Notes.Add($"// cb{d.Register} = {name} (identity from a sibling variant of this shader)");
            }
        }

        /// <summary>
        /// Exhaustive assignment of cbuffer registers to candidate names.  A Unity
        /// cbuffer is only allowed when every row the program reads from that
        /// register sits on a member whose name the shader itself references;
        /// $Globals always fits (its layout is not knowable) and scores 0.
        /// </summary>
        private Dictionary<int, string> AssignBijection(List<Ast.Decl> decls,
                                                        Dictionary<int, SortedSet<int>> used,
                                                        List<string> candidates)
        {
            var best = new Dictionary<int, string>();
            int bestScore = -1;

            void Search(int idx, HashSet<string> taken, Dictionary<int, string> cur, int score)
            {
                if (idx == decls.Count)
                {
                    if (score > bestScore || (score == bestScore && cur.Count > best.Count))
                    {
                        bestScore = score;
                        best = new Dictionary<int, string>(cur);
                    }
                    return;
                }
                var d = decls[idx];
                Search(idx + 1, taken, cur, score);

                foreach (string c in candidates)
                {
                    if (taken.Contains(c)) continue;
                    int s = Score(c, used[d.Register]);
                    if (s < 0) continue;
                    taken.Add(c); cur[d.Register] = c;
                    Search(idx + 1, taken, cur, score + s);
                    cur.Remove(d.Register); taken.Remove(c);
                }
            }

            Search(0, new HashSet<string>(), new Dictionary<int, string>(), 0);
            return best;
        }

        /// <summary>
        /// Rows <c>$Globals</c> can hold, from the byte size the asset records for
        /// it, or -1 when no size is available.  Without this bound the fallback
        /// assignment happily parks <c>$Globals</c> on a register whose reads run
        /// to row 46, which is how a 11-row block ended up named on the wrong
        /// register and its real rows stayed numeric.
        /// </summary>
        private int GlobalsRowCount()
        {
            int size = 0;
            var g = ParamCbs?.FirstOrDefault(c => c.Name == "$Globals");
            if (g != null) size = g.Size;
            if (AssetGlobals != null) size = Math.Max(size, AssetGlobals.Size);
            return size > 0 ? (size + 15) / 16 : -1;
        }

        /// <summary>
        /// Byte size of this program's own <c>$Globals</c>: the parameter blob
        /// records it per program, and that is what distinguishes keyword
        /// variants that packed the same properties differently (272 B vs 288 B
        /// in one commercial game's MergeSprite).  The asset reflection keeps only one copy.
        /// </summary>
        private int GlobalsSize()
        {
            var g = ParamCbs?.FirstOrDefault(c => c.Name == "$Globals");
            if (g != null && g.Size > 0) return g.Size;
            return AssetGlobals?.Size ?? -1;
        }

        /// <summary>Confirmed-row count, or -1 when the candidate cannot explain the reads.</summary>
        private int Score(string cb, SortedSet<int> rows)
        {
            if (cb == "$Globals")
            {
                int n = GlobalsRowCount();
                if (n > 0 && rows.Count > 0 && rows.Max() >= n) return -100;
                if (!InTable("$Globals")) return 0;
                return 1 + (rows.Count > 0 && rows.Max() >= 24 ? 2 : 0);
            }
            int hits = 0;
            foreach (int row in rows)
            {
                var m = UnityCbuffers.Find(cb, row);
                if (m == null) return -1;
                if (!InTable(m.Name)) return -1;
                hits++;
            }
            return rows.Count == 0 ? 0 : hits;
        }

        /// <summary>
        /// Name $Globals rows from the parameter blob's member table.  Each record
        /// carries the member's byte offset inside the cbuffer, so its row and its
        /// first component are both known.
        ///
        /// Width comes from the material property list when the member is a property,
        /// otherwise from the distance to the next member: a member cannot be wider
        /// than the space reserved in front of the next one.  The narrower of the two
        /// is used, so padding after a scalar can never widen it into a float4.
        /// </summary>
        /// <summary>
        /// Name $Globals from the asset's own constant-buffer reflection.  Every
        /// member carries its byte offset and component count, so this needs no
        /// inference at all: a slot is named by the member that starts exactly there.
        /// </summary>
        /// <summary>
        /// One register of a matrix holds a COLUMN, not a row: D3D packs matrices
        /// column-major, so cbuffer row R of a float4x4 is M[0..3][R].  Reading it
        /// as M[R] transposes the matrix, which is what turned a world-position W
        /// into M[1][3] -- zero for an affine transform -- and sent the object off
        /// screen.  The expression is parenthesised so a lane can still be taken
        /// from it ("(M._m01_m11_m21_m31).w"): swizzling a swizzle is not legal.
        /// </summary>
        private static string MatrixColumn(string name, int k, int rows)
        {
            var sb = new System.Text.StringBuilder("(").Append(name).Append('.');
            for (int r = 0; r < rows; r++) sb.Append("_m").Append(r).Append(k);
            return sb.Append(')').ToString();
        }

        private bool MapGlobalsFromAssetReflection(Ast.Decl d, SortedSet<int> rows, bool fillOnly = false)
        {
            var cb = AssetGlobals;
            if (cb == null || cb.Members.Count == 0) return false;

            // In fillOnly mode this table supplements the compiled member table
            // instead of replacing it: it may only add rows the other left
            // nameless, and only declare members not already declared.  The two
            // disagree in both directions -- this shader's parameter blob omits
            // _LightColor0, while the asset reflection knows _EnvMatrix is a
            // matrix spanning rows 13..16 where the blob only sees row 13 -- so
            // neither table alone covers the block.
            bool RowTaken(int row) =>
                RowExpr.ContainsKey((d.Register, row)) ||
                Enumerable.Range(0, 4).Any(c => CompExpr.ContainsKey((d.Register, row, c)));
            bool Declared(string name) =>
                GlobalsDecl.TryGetValue((int)d.Register, out var g) && g.Any(x => x.name == name);

            int mapped = 0;
            var decls = new List<(string name, string type)>();
            var members = cb.Members.OrderBy(x => x.Offset).ToList();
            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[cbasset] size={cb.Size} members=" +
                    string.Join(" ", members.Select(x => $"{x.Name}@{x.Offset}({(x.IsMatrix ? "M" + x.RowCount : "d" + x.Dim)})")));
            for (int mi = 0; mi < members.Count; mi++)
            {
                var m = members[mi];
                if (string.IsNullOrEmpty(m.Name)) continue;
                if (m.Offset % 4 != 0) continue;
                int row = m.Offset / 16;
                int col = (m.Offset % 16) / 4;

                // A matrix member spans several rows, and a stage rarely reads all
                // of them: one light variant samples _EnvMatrix rows 14 and 15 but
                // never row 13.  Requiring the member's first row to be read threw
                // the whole matrix away for such a stage, so its rows stayed bare
                // cb0[14]/cb0[15].  Coverage is therefore decided per row, below.
                //
                // Whether a member is a matrix is answered by the asset's own
                // reflection (IsMatrix/RowCount); the old name list is gone because
                // a name only says "this is probably a matrix", while the reflection
                // says so per member and per compile.
                if (col == 0 && !m.IsMatrix && IsDeclaredMatrix(m.Name))
                {
                    int next = mi + 1 < members.Count ? members[mi + 1].Offset : cb.Size;
                    int n = (next - m.Offset) / 16;
                    if (n is >= 2 and <= 4)
                    {
                        int used = 0;
                        for (int k = 0; k < n; k++)
                        {
                            if (!rows.Contains(row + k)) continue;
                            if (fillOnly && RowTaken(row + k)) continue;
                            RowExpr[(d.Register, row + k)] = MatrixColumn(m.Name, k, n);
                            RowWidth[(d.Register, row + k)] = 4;
                            used++;
                        }
                        if (used == 0) continue;
                        if (!fillOnly || !Declared(m.Name))
                            decls.Add((m.Name, n == 2 ? "float2x2" : n == 3 ? "float3x3" : "float4x4"));
                        mapped++;
                        continue;
                    }
                }

                if (m.IsMatrix)
                {
                    if (col != 0) continue;
                    int n = Math.Max(1, m.RowCount);
                    int used = 0;
                    for (int k = 0; k < n; k++)
                    {
                        if (!rows.Contains(row + k)) continue;
                        if (fillOnly && RowTaken(row + k)) continue;
                        RowExpr[(d.Register, row + k)] = MatrixColumn(m.Name, k, n);
                        RowWidth[(d.Register, row + k)] = 4;
                        used++;
                    }
                    if (used == 0) continue;
                    if (!fillOnly || !Declared(m.Name))
                        decls.Add((m.Name, n == 2 ? "float2x2" : n == 3 ? "float3x3" : "float4x4"));
                    mapped++;
                    continue;
                }

                if (!rows.Contains(row)) continue;
                int w = m.Dim switch { 4 => 4, 3 => 3, 2 => 2, _ => 1 };
                if (m.ArraySize > 1) w = 4;
                if (col + w > 4) continue;

                // Always float, even when the reflection says the member is an int
                // (type 1) and the code around it converts with itof.  Unity uploads
                // a ShaderLab Float property as float bits; declaring the member int
                // makes the shader read those bits as an integer -- _NumVerts = 352
                // arrives as 1135616 and the ribbon collapses.  Declaring float keeps
                // the emitted float(x) a no-op cast, which is what the GLSL backend's
                // own reconstruction does and what the shader needs to run.
                string scalar = "float";
                string Ty(int n) => n == 1 ? scalar : scalar + (n == 2 ? "2" : n == 3 ? "3" : "4");

                if (w == 4 && col == 0)
                {
                    if (fillOnly && RowTaken(row)) continue;
                    RowExpr[(d.Register, row)] = m.Name;
                    RowWidth[(d.Register, row)] = 4;
                    if (!fillOnly || !Declared(m.Name)) decls.Add((m.Name, Ty(4)));
                }
                else
                {
                    bool any = false;
                    for (int k = 0; k < w; k++)
                    {
                        if (fillOnly && CompExpr.ContainsKey((d.Register, row, col + k))) continue;
                        CompExpr[(d.Register, row, col + k)] =
                            w == 1 ? m.Name : m.Name + "." + "xyzw"[k];
                        any = true;
                    }
                    if (!any) continue;
                    if (!fillOnly || !Declared(m.Name)) decls.Add((m.Name, Ty(w)));
                }
                mapped++;
            }

            if (mapped == 0) return false;
            if (fillOnly && GlobalsDecl.TryGetValue((int)d.Register, out var exist))
            {
                var have = new HashSet<string>(exist.Select(x => x.name), StringComparer.Ordinal);
                exist.AddRange(decls.Where(x => have.Add(x.name)));
            }
            else GlobalsDecl[(int)d.Register] = decls;
            if (rows.All(r => RowFullyNamed(d.Register, r)) && !_dynamicIndex.Contains(d.Register))
                FullyMapped.Add(d.Register);
            Notes.Add($"// $Globals cb{d.Register}: {mapped} member(s) named from the asset's own "
                      + $"constant-buffer reflection ({cb.Size} bytes)");
            return true;
        }

        private bool MapGlobalsFromParamBlob(Ast.Decl d, SortedSet<int> rows, IReadOnlyList<Property> props)
        {
            var source = AllParamCbs ?? ParamCbs;
            var all = source?.Where(x => x.Name == "$Globals" && x.Members.Count > 0).ToList();
            if (all == null || all.Count == 0) return false;

            int need = d.Count > 0 ? d.Count * 16 : (rows.Count > 0 ? (rows.Max() + 1) * 16 : 0);
            var pool = all.Where(x => x.Size >= need).ToList();
            if (pool.Count == 0) pool = all;

            // The program's own `$Globals` is the first choice: its identity pass
            // recorded that size, and a table of a different size describes a
            // different packing of the same properties, with every member after the
            // first discrepancy at another offset.  Only when this program kept no
            // size -- or its own table cannot hold the declaration -- does the
            // search widen to the neighbouring sizes.
            if (CbSize.TryGetValue((int)d.Register, out int own) && own > 0)
            {
                var exact = pool.Where(x => x.Size == own).ToList();
                if (exact.Count > 0) pool = exact;
            }

            // Several programs of one pass compile to the same $Globals size but
            // record different subsets of members (a 624-byte block appears with
            // 16, 23 and 36 members).  Picking the first by size alone can land on
            // the 16-member one, which has no _EnvMatrix and therefore no way to
            // learn that rows 18/19 belong to it.  Same size, richest table.
            var cb = pool.OrderBy(x => x.Size).ThenByDescending(x => x.Members.Count).First();

            var seenOffsets = new HashSet<int>();
            var merged = new List<(string name, int offset, int dim)>();
            foreach (var c in all.Where(x => x.Size == cb.Size).OrderByDescending(x => x.Members.Count))
                foreach (var m in c.Members)
                    if (seenOffsets.Add(m.offset)) merged.Add((m.name, m.offset, m.dim));

            var propWidth = new Dictionary<string, int>(StringComparer.Ordinal);
            if (props != null)
                foreach (var p in props)
                    propWidth[p.Name] = p.Type is 0 or 1 ? 4 : 1;
            foreach (var p in props ?? new List<Property>())
                if (TextureTypes.Contains(p.Type)) propWidth[p.Name + "_ST"] = 4;

            var ordered = merged.OrderBy(m => m.offset).ToList();
            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[cbparam] size={cb.Size} merged={ordered.Count} members=" +
                    string.Join(" ", ordered.Select(x => $"{x.name}@{x.offset}(d{x.dim})")));
            int Width(string name, int index)
            {
                int start = ordered[index].offset;
                int next = index + 1 < ordered.Count ? ordered[index + 1].offset : cb.Size;
                int span = Math.Max(0, next - start);
                int room = 4 - (start % 16) / 4;
                int bySpan = span >= 16 ? 4 : span >= 12 ? 3 : span >= 8 ? 2 : 1;
                int w = ordered[index].dim is >= 1 and <= 4 ? ordered[index].dim : bySpan;
                w = Math.Min(w, room);
                if (propWidth.TryGetValue(name, out int pw)) w = Math.Min(w, pw);
                return Math.Max(1, w);
            }

            bool RowTaken(int row) =>
                RowExpr.ContainsKey((d.Register, row)) ||
                Enumerable.Range(0, 4).Any(c => CompExpr.ContainsKey((d.Register, row, c)));
            bool Declared(string name) =>
                GlobalsDecl.TryGetValue((int)d.Register, out var gd) && gd.Any(x => x.name == name);

            int mapped = 0;
            var decls = new List<(string name, string type)>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var (mname, moff, mdim) = ordered[i];
                int row = moff / 16;
                if (moff % 4 != 0) continue;
                if (!rows.Contains(row)) continue;
                int col = (moff % 16) / 4;

                // Whether a member is a matrix is a property of the shader, not of
                // this variant's buffer: _EnvMatrix is float4x4 everywhere, even
                // though the 624-byte layout of the additive-light variants puts it
                // at a different offset than the 560-byte one.  The asset's own
                // reflection records that fact, so consult it before falling back to
                // span arithmetic, which cannot recognise a member whose neighbours
                // are far away (here _EnvMatrix is 176 bytes from the next member and
                // was therefore read as a single float4, leaving its other rows bare).
                int declaredRows = 0;
                if (MatrixRows != null) MatrixRows.TryGetValue(mname, out declaredRows);
                if (declaredRows == 0 && AssetGlobals != null)
                    foreach (var am in AssetGlobals.Members)
                        if (am.Name == mname && am.IsMatrix) { declaredRows = Math.Max(declaredRows, am.RowCount); break; }
                if (col == 0 && (declaredRows is >= 2 and <= 4 || IsDeclaredMatrix(mname)))
                {
                    int span = (i + 1 < ordered.Count ? ordered[i + 1].offset : cb.Size) - moff;
                    int n = declaredRows is >= 2 and <= 4 ? declaredRows : (span >= 32 ? span / 16 : 0);
                    if (n is >= 2 and <= 4)
                    {
                        int used = 0;
                        for (int k = 0; k < n; k++)
                        {
                            if (!rows.Contains(row + k)) continue;
                            if (RowTaken(row + k)) continue;
                            RowExpr[(d.Register, row + k)] = MatrixColumn(mname, k, n);
                            RowWidth[(d.Register, row + k)] = 4;
                            used++;
                        }
                        if (used > 0)
                        {
                            if (!Declared(mname))
                                decls.Add((mname, n == 2 ? "float2x2" : n == 3 ? "float3x3" : "float4x4"));
                            mapped++;
                            continue;
                        }
                    }
                }

                int w = Width(mname, i);
                if (col + w > 4) continue;

                if (w == 4 && col == 0)
                {
                    RowExpr[(d.Register, row)] = mname;
                    RowWidth[(d.Register, row)] = 4;
                    decls.Add((mname, "float4"));
                }
                else
                {
                    for (int k = 0; k < w; k++)
                        CompExpr[(d.Register, row, col + k)] =
                            w == 1 ? mname : mname + "." + "xyzw"[k];
                    decls.Add((mname, w == 1 ? "float" : w == 2 ? "float2" : "float3"));
                }
                mapped++;
            }

            if (mapped == 0) return false;
            GlobalsDecl[(int)d.Register] = decls;
            if (rows.All(r => RowFullyNamed(d.Register, r)) && !_dynamicIndex.Contains(d.Register))
                FullyMapped.Add(d.Register);
            Notes.Add($"// $Globals cb{d.Register}: {mapped} slot(s) named from the compiled "
                      + "constant-buffer member table");
            return true;
        }

        /// <summary>
        /// $Globals holds the material properties.  Their packing order inside the
        /// block is decided by fxc and is not recorded anywhere, so a row is only
        /// labelled when the assignment is forced: the number of named float4-sized
        /// properties must equal the number of rows the program actually reads.
        /// Scalar (Float/Range/Int) properties share a slot with their neighbours,
        /// so they are never used to name a whole row.
        /// </summary>
        private void MapGlobalsRows(Ast.Decl d, SortedSet<int> rows, IReadOnlyList<Property> props)
        {
            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine("[mgr] enter cb" + d.Register);
            if (rows.Count == 0) return;

            int blobGlobalsSize = ParamCbs?.FirstOrDefault(c => c.Name == "$Globals")?.Size ?? 0;
            int blobSpan = 0;
            if (ParamCbs != null)
                foreach (var c in ParamCbs.Where(x => x.Name == "$Globals"))
                    foreach (var m in c.Members)
                        blobSpan = Math.Max(blobSpan, m.offset + Math.Max(1, m.dim) * 4);
            bool assetUsable = AssetGlobals != null && AssetGlobals.Members.Count > 0 &&
                               (blobGlobalsSize <= 0 || blobGlobalsSize == AssetGlobals.Size) &&
                               AssetGlobals.Size >= (d.Count > 0 ? d.Count * 16 : 0) &&
                               AssetGlobals.Size >= blobSpan;
            bool fromBlob = MapGlobalsFromParamBlob(d, rows, props);
            // The two tables are complementary, not alternatives: whichever one
            // ran first, the asset reflection still fills the rows it left
            // nameless (it knows _LightColor0 and the true extent of matrix
            // members).  Without this the parameter blob alone decides, and its
            // omissions surface as bare cb0[2] / cb0[14] references.
            bool fromAsset = assetUsable &&
                             (fromBlob ? MapGlobalsFromAssetReflection(d, rows, fillOnly: true)
                                       : MapGlobalsFromAssetReflection(d, rows));

            bool fromTable = (fromAsset || fromBlob) ? false : ApplyCuratedNames(d, rows);

            ApplyCuratedNames(d, rows, sizedOnly: true);

            if (Names != null && Names.Override)
                ApplyCuratedNames(d, rows, overridePass: true);

            bool fromTmp = ApplyTmpLayout(d, rows);

            bool fromShape = ApplyClipRectShape(d, rows);

            if (fromAsset || fromBlob || fromTable || fromTmp || fromShape)
            {
                if (!_dynamicIndex.Contains(d.Register) && rows.All(r => RowFullyNamed(d.Register, r)))
                    FullyMapped.Add(d.Register);
                return;
            }

            var named = NamedGlobals(props);

            if (named.Count != rows.Count)
            {
                return;
            }
            var ordered = rows.OrderBy(x => x).ToList();
            for (int i = 0; i < ordered.Count; i++)
                RowExpr[(d.Register, ordered[i])] = named[i];
            GlobalsDecl[(int)d.Register] = named.Select(n => (n, "float4")).ToList();
            FullyMapped.Add(d.Register);
            Notes.Add($"// $Globals cb{d.Register}: rows mapped in property declaration order ["
                      + string.Join(", ", named) + "]");
        }

        /// <summary>
        /// 同一个成员的不同写法：`_EnvMatrix` 与 `_EnvMatrix[0]` 指的是同一条矩阵的
        /// 首行（前者是"整行"写法，后者是矩阵下标写法）。冲突检测只关心是不是同一个
        /// 成员，写法差异不算冲突。
        /// </summary>
        private static bool SameMember(string a, string b)
        {
            if (a == b) return true;
            string Base(string s)
            {
                int i = s.IndexOf('[');
                return i > 0 ? s.Substring(0, i) : s;
            }
            if (Base(a) != Base(b)) return false;
            return a == Base(a) && b == Base(b) + "[0]"
                || b == Base(b) && a == Base(a) + "[0]";
        }

        /// <summary>
        /// $Globals 成员的"名字 → 字节偏移"锚点，来自参数块与资产反射。
        /// 用来在 name table 交集不足时确认布局表挑对了（见 <see cref="TmpLayouts.Match"/>）。
        /// </summary>
        private Dictionary<string, int> TmpAnchors()
        {
            var a = new Dictionary<string, int>(StringComparer.Ordinal);
            if (ParamCbs != null)
                foreach (var c in ParamCbs.Where(x => x.Name == "$Globals"))
                    foreach (var (n, o, _, _) in c.Members)
                        if (!string.IsNullOrEmpty(n)) a[n] = o;
            if (AssetGlobals != null)
                foreach (var m in AssetGlobals.Members)
                    if (!string.IsNullOrEmpty(m.Name)) a[m.Name] = m.Offset;
            return a;
        }

        /// <summary>
        /// 用逐 shader 的 <c>$Globals</c> 布局表还原行号（见 <see cref="TmpLayouts"/>）。
        ///
        /// 表由 Unity 自己的 CGIncludes 真编一遍得到，所以这里不做推导：按 <c>dcl</c>
        /// 行数选表，要求成员名与本程序 name table 有足够交集，且本程序读到的每一行
        /// 都被覆盖。已经命名的行不动，一旦与表里的名字冲突就整块放弃——冲突恰好说明
        /// 这张表不适用于这个程序。
        /// </summary>
        private bool ApplyTmpLayout(Ast.Decl d, SortedSet<int> rows)
        {
            if (!TmpLayouts.Enabled) return false;
            var map = TmpLayouts.Match(d.Count, _nameTable, rows, out var layout, TmpAnchors());
            if (map == null && _layoutHint != null)
            {
                map = TmpLayouts.MapFrom(_layoutHint, d.Count, rows);
                layout = map != null ? _layoutHint : null;
            }
            if (map == null || map.Count == 0)
            {
                if (Environment.GetEnvironmentVariable("SHADERRIP_TMPDBG") == "1")
                    Console.Error.WriteLine($"[tmpmiss] cb{d.Register} dcl={d.Count} "
                        + $"table={TmpLayouts.DebugWhy(d.Count, _nameTable, rows)} reads=[{string.Join(",", rows)}]");
                return false;
            }

            int reg = d.Register;
            foreach (var kv in map)
            {
                int row = kv.Key.Item1, col = kv.Key.Item2;
                if (!rows.Contains(row)) continue;
                var (expr, comps) = kv.Value;
                if (RowExpr.TryGetValue((reg, row), out var have) && !SameMember(have, expr))
                {
                    if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                        Console.Error.WriteLine($"[tmpconflict] r{row} have={have} calc={expr}");
                    return false;
                }
                if (col == 0 && comps == 4) continue;
                for (int k = 0; k < comps; k++)
                    if (CompExpr.TryGetValue((reg, row, col + k), out var haveLane) &&
                        !SameMember(haveLane, comps == 1 ? expr : expr + "." + "xyzw"[k]))
                    {
                        if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                            Console.Error.WriteLine($"[tmpconflict] r{row}.{k} have={haveLane} calc={expr}");
                        return false;
                    }
            }

            var decls = GlobalsDecl.TryGetValue(reg, out var prior)
                ? new List<(string, string)>(prior) : new List<(string, string)>();
            var types = new Dictionary<string, string>(StringComparer.Ordinal);
            if (layout != null)
                foreach (var m in layout.Members) types[m.Name] = m.Type;
            int named = 0;
            foreach (var kv in map.OrderBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2))
            {
                int row = kv.Key.Item1, col = kv.Key.Item2;
                if (!rows.Contains(row)) continue;
                var (expr, comps) = kv.Value;
                bool wrote = false;
                if (col == 0 && comps == 4 &&
                    (!RowExpr.TryGetValue((reg, row), out var prev) || SameMember(prev, expr)))
                {
                    RowExpr[(reg, row)] = expr;
                    RowWidth[(reg, row)] = 4;
                    wrote = true;
                }
                else if (col != 0 || comps != 4)
                {
                    for (int k = 0; k < comps; k++)
                        if (CompExpr.TryAdd((reg, row, col + k),
                                comps == 1 ? expr : expr + "." + "xyzw"[k])) wrote = true;
                }
                if (!wrote) continue;
                string nm = expr.Contains('[') ? expr.Substring(0, expr.IndexOf('[')) : expr;
                string ty = types.TryGetValue(nm, out var t0) ? t0
                    : comps == 1 ? "float" : comps == 2 ? "float2" : comps == 3 ? "float3" : "float4";
                int at = decls.FindIndex(x => x.Item1 == nm);
                if (at < 0) decls.Add((nm, ty));
                else if (decls[at].Item2 != ty) decls[at] = (nm, ty);
                named++;
            }

            if (named == 0) return false;
            if (Environment.GetEnvironmentVariable("SHADERRIP_TMPDBG") == "1")
                Console.Error.WriteLine($"[tmp] reg={reg} layout={layout.Rows}r map={map.Count} "
                    + $"reads=[{string.Join(",", rows)}] wrote={named} "
                    + $"miss=[{string.Join(",", rows.Where(r => !RowFullyNamed(reg, r)))}]");
            GlobalsDecl[reg] = decls;
            if (rows.All(r => RowFullyNamed(reg, r)) && !_dynamicIndex.Contains(reg))
                FullyMapped.Add(reg);
            ChosenLayout = layout;
            Notes.Add($"// $Globals cb{reg}: {named} slot(s) named from the compiled layout table "
                      + $"(tools/gen_tmp_layout.py, {layout.Bytes} bytes / {layout.Rows} rows: {layout.Source})");
            return true;
        }

        /// <summary>
        /// 按指令形态认 <c>_ClipRect</c>。
        ///
        /// Unity 的 UI 着色器统一用 <c>float4 _ClipRect</c> 做矩形裁剪：<c>.xy</c> 是
        /// 最小值、<c>.zw</c> 是最大值，四个分量分别与同一坐标做 <c>ge</c> 比较
        /// （<c>x &gt;= _ClipRect.x</c>、<c>_ClipRect.z &gt;= x</c>，y/w 同理）。
        /// 这个形态在 UI 着色器里没有第二种解释，而 <c>_ClipRect</c> 通常既不在
        /// Properties 里、也不在资产反射里，所以只能从这里反推。
        /// 只认"z、w 在比较左侧、x、y 在右侧"这一种组合，其余不动。
        /// </summary>
        private bool ApplyClipRectShape(Ast.Decl d, SortedSet<int> rows)
        {
            if (_prog == null) return false;

            var lhs = new Dictionary<int, HashSet<char>>();
            var rhs = new Dictionary<int, HashSet<char>>();
            void Mark(Operand o, Dictionary<int, HashSet<char>> side)
            {
                if (o == null || o.Kind != Ast.RegKind.Cb || o.Index != d.Register || o.Row < 0) return;
                if (rows == null || !rows.Contains(o.Row)) return;
                if (!side.TryGetValue(o.Row, out var set)) side[o.Row] = set = new HashSet<char>();
                string sw = string.IsNullOrEmpty(o.Swizzle) ? "x" : o.Swizzle;
                foreach (char c in sw) set.Add(c);
            }

            foreach (var ins in _prog.Code)
            {
                if (ins.Opcode != "ge" || ins.Args.Count < 3) continue;
                Mark(ins.Args[1], lhs);
                Mark(ins.Args[2], rhs);
            }

            int named = 0;
            foreach (var row in rows)
            {
                if (RowExpr.ContainsKey((d.Register, row)) ||
                    Enumerable.Range(0, 4).Any(c => CompExpr.ContainsKey((d.Register, row, c))))
                    continue;
                if (!lhs.TryGetValue(row, out var l) || !rhs.TryGetValue(row, out var r)) continue;
                if (!(l.Contains('z') && l.Contains('w') && r.Contains('x') && r.Contains('y'))) continue;

                RowExpr[(d.Register, row)] = "_ClipRect";
                RowWidth[(d.Register, row)] = 4;
                if (!GlobalsDecl.TryGetValue((int)d.Register, out var decls))
                    GlobalsDecl[(int)d.Register] = decls = new List<(string name, string type)>();
                if (!decls.Any(x => x.name == "_ClipRect")) decls.Add(("_ClipRect", "float4"));
                named++;
            }

            if (named > 0)
                Notes.Add("// $Globals: named _ClipRect from the UI min/max rect comparison pattern");
            return named > 0;
        }

        /// <summary>
        /// Name the rows a curated table covers.  A row covered by one variable of
        /// width &gt; 1 becomes a whole-row name; a row covered by several scalars
        /// names each lane on its own.  Rows the table does not mention are left
        /// numeric on purpose -- the disassembly has nothing to say about them.
        /// </summary>
        private bool ApplyCuratedNames(Ast.Decl d, SortedSet<int> rows,
                                        bool sizedOnly = false, bool overridePass = false)
        {
            bool dbg = Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1";
            if (dbg) Console.Error.WriteLine($"[curated] names={(Names == null ? "null" : "ok")} "
                + $"keys=[{(Names == null ? "" : string.Join(",", Names.Buffers.Keys))}] shader={Names?.Shader}");
            if (Names == null) return false;
            int gsz = GlobalsSize();
            if (!Names.TrySizedTable("$Globals", gsz, out var cbKey)) return false;
            if (sizedOnly && cbKey == "$Globals" && !Names.Override) return false;
            if (overridePass && !Names.Override && cbKey == "$Globals") return false;
            if (cbKey != "$Globals")
                Notes.Add($"// $Globals cb{d.Register}: using the size-specific curated table "
                          + $"{cbKey} (this variant packs the properties differently)");

            var decls = GlobalsDecl.TryGetValue((int)d.Register, out var prior)
                ? new List<(string name, string type)>(prior)
                : new List<(string name, string type)>();
            var known = new HashSet<string>(decls.Select(x => x.name), StringComparer.Ordinal);
            foreach (string n in RowExpr.Values) known.Add(n);
            foreach (string n in CompExpr.Values) known.Add(n);

            bool overrideRows = overridePass || cbKey != "$Globals";
            int added = 0;

            void ClearRow(int reg, int row)
            {
                if (RowExpr.TryGetValue((reg, row), out var oldExpr))
                {
                    var names = new[] { oldExpr.Contains('[') ? oldExpr[..oldExpr.IndexOf('[')] : oldExpr };
                    decls.RemoveAll(x => names.Contains(x.name));
                }
                RowExpr.Remove((reg, row));
                for (int c = 0; c < 4; c++)
                {
                    if (CompExpr.TryGetValue((reg, row, c), out var oldLane))
                    {
                        var dot = oldLane.IndexOf('.');
                        var nm = dot > 0 ? oldLane[..dot] : oldLane;
                        decls.RemoveAll(x => x.name == nm);
                    }
                    CompExpr.Remove((reg, row, c));
                }
            }
            foreach (int row in Names.RowsOf(cbKey))
            {
                if (!overrideRows &&
                    (RowExpr.ContainsKey((d.Register, row)) ||
                     Enumerable.Range(0, 4).Any(c => CompExpr.ContainsKey((d.Register, row, c)))))
                    continue;

                var whole = Names.WholeRow(cbKey, row);
                if (whole != null)
                {
                    if (overrideRows) ClearRow(d.Register, row);
                    else if (RowExpr.ContainsKey((d.Register, row)) ||
                             Enumerable.Range(0, 4).Any(c => CompExpr.ContainsKey((d.Register, row, c))))
                        continue;
                    string nm = whole.Value.name;
                    RowExpr[(d.Register, row)] = nm;
                    RowWidth[(d.Register, row)] = GlobalsNames.WidthOf(whole.Value.type);
                    if (known.Add(nm)) decls.Add((nm, whole.Value.type));
                    added++;
                    continue;
                }

                foreach (var kv in Names.Row(cbKey, row))
                {
                    string ty = GlobalsNames.TypeOf(kv.Value);
                    string nm = GlobalsNames.NameOf(kv.Value);

                    if (ty == "float")
                    {
                        if (kv.Key.Length != 1) continue;
                        int ci = "xyzw".IndexOf(kv.Key[0]);
                        if (ci < 0) continue;
                        if (overrideRows) ClearRow(d.Register, row);
                        else if (CompExpr.ContainsKey((d.Register, row, ci)) ||
                                 RowExpr.ContainsKey((d.Register, row))) continue;
                        CompExpr[(d.Register, row, ci)] = nm;
                        if (known.Add(nm)) decls.Add((nm, ty));
                        added++;
                        continue;
                    }

                    int baseLane = kv.Key.Min(c => "xyzw".IndexOf(c));
                    bool firstLane = true;
                    foreach (char c in kv.Key)
                    {
                        int ci = "xyzw".IndexOf(c);
                        if (ci < 0) continue;
                        CompExpr[(d.Register, row, ci)] = nm + "." + "xyzw"[ci - baseLane];
                        added++;
                        if (firstLane && known.Add(nm)) decls.Add((nm, ty));
                        firstLane = false;
                    }
                }
            }

            if (added == 0) return false;
            GlobalsDecl[(int)d.Register] = decls;
            Notes.Add($"// $Globals cb{d.Register}: row names taken from a hand-written ShaderLab "
                      + "source for the same shader, each one cross-checked against the code");
            return true;
        }

        /// <summary>Material properties that live in $Globals (textures do not).</summary>
        private List<string> NamedGlobals(IReadOnlyList<Property> props)
        {
            var res = new List<string>();
            if (props == null) return res;
            foreach (var p in props)
            {
                if (TextureTypes.Contains(p.Type))
                {
                    string st = p.Name + "_ST";
                    if (_nameTable.Contains(st)) res.Add(st);
                    continue;
                }
                if ((p.Type == 0 || p.Type == 1) && _nameTable.Contains(p.Name)) res.Add(p.Name);
            }
            return res;
        }

        /// <summary>Give sampler registers the property name they belong to.</summary>
        /// <remarks>
        /// Priority: the asset's own <c>m_TextureParams</c> reflection (name + t#
        /// register straight out of the serialized program parameters -- the same
        /// table UnityPlayer's runtime consumes), falling back to property
        /// declaration order only when the asset kept no texture list.  Property
        /// order is NOT register order: one commercial title's FloorMesh declares _TileTex first but
        /// binds it to t2, which silently swapped three samplers and broke the look.
        /// </remarks>
        private void MapTextures(Ast.Module prog, IReadOnlyList<Property> props,
                                 System.Collections.Generic.List<(string Name, int Index, int Sampler)> texParams)
        {
            // A name is trustworthy when the shader itself declares it.  The
            // property table is authoritative for material textures; the name table
            // passed down here is the pass tag list, so it cannot vouch for anything.
            var declared = new HashSet<string>(StringComparer.Ordinal);
            if (props != null) foreach (var p in props) declared.Add(p.Name);

            if (texParams != null && texParams.Count > 0)
            {
                foreach (var (nm, idx, _) in texParams)
                    if (idx >= 0 && idx < 128 && (declared.Contains(nm) || _nameTable.Contains(nm)))
                        TexName[idx] = nm;
                if (TexName.Count > 0) return;
                Notes.Add("// asset texture params present but no name matched -- falling back to property order");
            }

            var texProps = new List<string>();
            if (props != null)
                foreach (var p in props)
                    if (TextureTypes.Contains(p.Type)) texProps.Add(p.Name);

            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[tex] nameTable={_nameTable.Count} props=" +
                    string.Join(",", props?.Select(p => $"{p.Name}:t{p.Type}:{_nameTable.Contains(p.Name)}") ?? Array.Empty<string>()));

            var regs = prog.Decls.Where(d => d.Kind != null && d.Kind.StartsWith("texture"))
                                 .Select(d => d.Register).Distinct().OrderBy(x => x).ToList();
            if (regs.Count == 0) return;
            if (texProps.Count != regs.Count)
            {
                // Fewer properties than registers is the normal shape of a variant
                // that added a built-in sampler -- a soft-particle pass reads the
                // camera depth on top of its material texture.  Mapping the
                // properties that do exist to the leading registers gets the
                // material texture right; the leftovers stay tN and are called out.
                if (texProps.Count > 0 && texProps.Count < regs.Count)
                {
                    for (int i = 0; i < texProps.Count; i++) TexName[regs[i]] = texProps[i];
                    Notes.Add($"// {regs.Count} texture register(s), {texProps.Count} material "
                              + "texture propert(ies): the leading ones are named, the rest stay tN");
                    return;
                }
                Notes.Add($"// {regs.Count} texture register(s) but {texProps.Count} named texture "
                          + "propert(ies) -- samplers left as tN");
                return;
            }
            for (int i = 0; i < regs.Count; i++) TexName[regs[i]] = texProps[i];
        }
    }
}
