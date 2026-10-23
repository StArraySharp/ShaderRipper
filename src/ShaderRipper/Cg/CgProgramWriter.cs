using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Composes a complete, standard-shaped CGPROGRAM body for one pass:
    /// appdata / v2f structs, samplers and uniform declarations, and the named
    /// `vert` / `frag` entry points that standard ShaderLab requires.
    ///
    /// A stage normally carries several sub-programs -- one per shader-keyword
    /// combination.  Every combination is emitted behind its own `#if` guard, with
    /// a `#pragma multi_compile` line per independent keyword toggle, so the
    /// variants are preserved instead of being dropped.
    /// </summary>
    internal static class CgProgramWriter
    {
        /// <summary>在每条指令前插入原始 DXBC 反汇编注释（--no-asm 关闭）。</summary>
        public static bool AnnotateAsm = false;

        public static string Build(Pass pass, Dictionary<int, AsmParser.Program> progs,
                                   IReadOnlyList<(string kh, string kv)> passTags,
                                   IReadOnlyList<Property> props,
                                   IReadOnlyList<string> keywordNames,
                                   Dictionary<int, List<BlobTool.CbInfo>> paramBlobs,
                                   string shaderKey = null, bool hlsl = false,
                                   Dictionary<string, int> shaderMatrixRows = null){
            var names = GlobalsNames.For(shaderKey);
            bool tmpLayout = shaderKey != null &&
                             shaderKey.StartsWith("TextMeshPro", StringComparison.OrdinalIgnoreCase);
            var indexToName = new Dictionary<int, string>();
            for (int i = 0; i < (keywordNames?.Count ?? 0); i++) indexToName[i] = keywordNames[i];

            var vsList = Stage(pass, "Vertex");
            var psList = Stage(pass, "Fragment");
            if (vsList.Count == 0 && psList.Count == 0)
                return "// (no compiled program in this pass)\n";

            var vsByKey = Index(vsList, indexToName);
            var psByKey = Index(psList, indexToName);
            var allKeys = vsByKey.Keys.Union(psByKey.Keys)
                .OrderBy(k => k.Length == 0 ? "" : k, StringComparer.Ordinal).ToList();

            var keywordSets = allKeys.Select(k => (IEnumerable<string>)Split(k)).ToList();
            var groups = VariantPlan.Groups(keywordSets);
            var nameTable = pass.NameIndices?.Keys.ToList() ?? new List<string>();

            var identityHint = CollectIdentityHints(pass, progs, nameTable, props, paramBlobs, names);

            var sb = new StringBuilder();

            var profs = new List<AsmParser.Program>();
            if (vsList.Count > 0) profs.Add(Lookup(progs, vsList[0]));
            if (psList.Count > 0) profs.Add(Lookup(progs, psList[0]));
            foreach (var p in progs.Values)
                if (p != null && !profs.Contains(p)) profs.Add(p);
            int major = 4, minor = 0;
            foreach (var p in profs)
            {
                var m = System.Text.RegularExpressions.Regex.Match(p?.Stage ?? "", @"^[a-z]{2}_(\d)_(\d)$");
                if (!m.Success) continue;
                int ma = int.Parse(m.Groups[1].Value), mi = int.Parse(m.Groups[2].Value);
                if (ma > major || (ma == major && mi > minor)) { major = ma; minor = mi; }
            }
            sb.AppendLine($"#pragma target {major}.{minor}");

            foreach (var g in groups) sb.AppendLine(VariantPlan.Pragma(g));
            if (groups.Count > 0) sb.AppendLine();
            var vanalysis = VariantPlan.Analyze(groups, keywordSets.Select(s => new HashSet<string>(s, StringComparer.Ordinal)).ToList());
            sb.AppendLine($"// {allKeys.Count} keyword variant(s), {groups.Count} multi_compile line(s)");
            foreach (var note in vanalysis.Notes) sb.AppendLine($"// variants: {note}");

            // ---- constant-buffer name consensus across variants ----
            // Each variant's programs carry their own reflection; a row named in
            // one compile can be missed in another (the cb0[5] vs _Color split).
            // Resolve every variant once, keep resolvers in a cache, and build an
            // overlay of row names that all resolving stages agree on.  Variants
            // missing a row then inherit the consensus name; conflicting evidence
            // is never overlaid.
            // How many rows each $Globals member spans, collected across every
            // program of this shader.  A member's type belongs to the shader, not
            // to one variant's buffer: the additive-light variants compile to a
            // 624-byte $Globals whose reflection lists only _LightColor0 and
            // _SpecColor, so _EnvMatrix there looks like a lone float4 at row 17
            // even though the 560-byte layouts record it as float4x4.  Without
            // this table the extra rows stay bare cb0[18]/cb0[19].
            var matrixRows = new Dictionary<string, int>(StringComparer.Ordinal);
            // Start from the shader-wide table: this pass may have compiled to a
            // $Globals layout whose reflection omits the matrix members entirely.
            if (shaderMatrixRows != null)
                foreach (var kv in shaderMatrixRows) matrixRows[kv.Key] = kv.Value;
            foreach (var sp in vsByKey.Values.Concat(psByKey.Values))
            {
                if (sp?.ConstantBuffers == null) continue;
                foreach (var cb in sp.ConstantBuffers)
                {
                    if (!cb.IsGlobals) continue;
                    foreach (var m in cb.Members)
                        if (!string.IsNullOrEmpty(m.Name) && m.IsMatrix && m.RowCount is >= 2 and <= 4)
                        {
                            matrixRows.TryGetValue(m.Name, out int have);
                            if (m.RowCount > have) matrixRows[m.Name] = m.RowCount;
                        }
                }
            }

            var cbCache = new Dictionary<string, (CbResolver VsCb, CbResolver PsCb)>(StringComparer.Ordinal);
            // Clusters of cbuffer layouts that agree on every shared named row
            // ("anchors").  Variants of one pass can compile to different $Globals
            // layouts (112/128/176/256 bytes), so the same row index holds
            // different uniforms across them; a name may only move between
            // layouts whose anchors prove the rows align.
            var cbClusters = new List<CbCluster>();
            if (allKeys.Count > 0)
            {
                foreach (var key in allKeys)
                {
                    vsByKey.TryGetValue(key, out var vsSp0);
                    psByKey.TryGetValue(key, out var psSp0);
                    var vs0 = Lookup(progs, vsSp0);
                    var ps0 = Lookup(progs, psSp0);
                    if (vs0 == null && ps0 == null) continue;
                    var assetGlobals0 = BestGlobals(vsSp0, psSp0);
                    var allParamCbs0 = paramBlobs?.Values.SelectMany(x => x).ToList();
                    var v = CbResolver.Resolve(vs0, nameTable, props, ParamFor(paramBlobs, vsSp0), -1,
                        names, assetGlobals0, identityHint, allParamCbs0, tmpLayout, null, vsSp0?.TextureParams, matrixRows);
                    var p = CbResolver.Resolve(ps0, nameTable, props, ParamFor(paramBlobs, psSp0), -1,
                        names, assetGlobals0, identityHint, allParamCbs0, tmpLayout, v?.ChosenLayout, psSp0?.TextureParams, matrixRows);
                    cbCache[key] = (v, p);
                    foreach (var cb in new[] { v, p })
                    {
                        if (cb == null) continue;
                        foreach (var regKv in cb.CbName)
                        {
                            int extent = cb.CbSize.TryGetValue(regKv.Key, out int bytes) ? bytes / 16 : int.MaxValue;
                            var rows = new Dictionary<int, string>();
                            var lanes = new Dictionary<(int row, int comp), string>();
                            foreach (var kv in cb.RowExpr)
                                if (kv.Key.cb == regKv.Key && kv.Key.row < extent)
                                    rows[kv.Key.row] = kv.Value;
                            foreach (var kv in cb.CompExpr)
                                if (kv.Key.cb == regKv.Key && kv.Key.row < extent)
                                    lanes[(kv.Key.row, kv.Key.comp)] = kv.Value;
                            if (rows.Count == 0 && lanes.Count == 0) continue;

                            // rich-first: the cluster with the most evidence that is
                            // consistent with every shared row AND shares at least
                            // one named row with it ("anchor").  Merging on absence
                            // of conflict alone would glue unrelated layouts
                            // together (a 256-byte layout sharing no rows with the
                            // 112-byte one must not inherit the 112's row names).
                            CbCluster best = null;
                            int bestCount = -1;
                            foreach (var c in cbClusters)
                            {
                                if (c.Name != regKv.Value) continue;
                                bool anchored = rows.Keys.Any(c.Rows.ContainsKey)
                                             || lanes.Keys.Any(c.Lanes.ContainsKey);
                                if (!anchored) continue;
                                bool ok = rows.All(kv => !c.Rows.TryGetValue(kv.Key, out var n) || SameMember(n, kv.Value))
                                       && lanes.All(kv => !c.Lanes.TryGetValue(kv.Key, out var n) || SameMember(n, kv.Value));
                                int count = c.Rows.Count + c.Lanes.Count;
                                if (ok && count > bestCount) { best = c; bestCount = count; }
                            }
                            if (best == null)
                            {
                                best = new CbCluster { Name = regKv.Value };
                                cbClusters.Add(best);
                            }
                            foreach (var kv in rows) KeepRicher(best.Rows, kv.Key, kv.Value);
                            foreach (var kv in lanes) best.Lanes[kv.Key] = kv.Value;
                        }
                    }
                }
            }

            int emitted = 0;
            string fallbackKey = allKeys.Count > 0 ? allKeys[0] : null;
            for (int vi = 0; vi < allKeys.Count; vi++)
            {
                string key = allKeys[vi];
                vsByKey.TryGetValue(key, out var vsSp);
                psByKey.TryGetValue(key, out var psSp);
                var vs = Lookup(progs, vsSp);
                var ps = Lookup(progs, psSp);

                // A combination Unity never compiled completely (one stage missing)
                // must not become its own #elif block: a variant with only one
                // stage renders nothing and Unity reports X3501 for the missing
                // entry point.  Such combos are left to the #else fallback below.
                if (vs == null || ps == null) continue;
                if (fallbackKey == allKeys[0] && fallbackKey != null && emitted == 0)
                    fallbackKey = key;

                var kw = Split(key);
                string guard = VariantPlan.Guard(groups, kw);

                sb.AppendLine();
                sb.AppendLine($"// ---- variant {vi}" +
                              (kw.Count == 0 ? " (base)" : ": " + string.Join(" + ", kw)) + " ----");
                sb.AppendLine(emitted == 0 ? (guard.Length > 0 ? $"#if {guard}" : "#if 1")
                                           : $"#elif {guard}");

                EmitVariant(sb, vs, ps, props, nameTable, vsSp, psSp, paramBlobs, names, identityHint, tmpLayout, hlsl, cbClusters, cbCache, key, matrixRows);
                emitted++;
            }

            if (fallbackKey != null)
            {
                sb.AppendLine();
                sb.AppendLine("// ---- fallback: combinations Unity never compiled ----");
                sb.AppendLine("#else");
                vsByKey.TryGetValue(fallbackKey, out var vsSp);
                psByKey.TryGetValue(fallbackKey, out var psSp);
                EmitVariant(sb, Lookup(progs, vsSp), Lookup(progs, psSp), props, nameTable, vsSp, psSp, paramBlobs, names, identityHint, tmpLayout, hlsl, cbClusters, cbCache, null, matrixRows);
                sb.AppendLine("#endif");
            }

            return sb.ToString();
        }

        private static List<string> Split(string key) =>
            string.IsNullOrEmpty(key) ? new List<string>() : key.Split(',').ToList();

        private static List<SubProgram> Stage(Pass pass, string stage) =>
            pass.SubPrograms.Where(s => s.Stage == stage).ToList();

        private static Dictionary<string, SubProgram> Index(List<SubProgram> subs, Dictionary<int, string> indexToName)
        {
            var res = new Dictionary<string, SubProgram>(StringComparer.Ordinal);
            foreach (var sp in subs)
            {
                string k = VariantPlan.Key(VariantPlan.KeywordsOf(sp, indexToName));
                if (!res.ContainsKey(k)) res[k] = sp;
            }
            return res;
        }

        private static AsmParser.Program Lookup(Dictionary<int, AsmParser.Program> progs, SubProgram sp)
        {
            if (sp == null) return null;
            return progs.TryGetValue(sp.BlobIndex, out var p) ? p : null;
        }

        private static List<BlobTool.CbInfo> ParamFor(Dictionary<int, List<BlobTool.CbInfo>> p, SubProgram sp)
        {
            if (p == null || sp == null || sp.ParameterBlobIndex < 0) return null;
            return p.TryGetValue(sp.ParameterBlobIndex, out var v) ? v : null;
        }

        /// <summary>
        /// The most complete <c>$Globals</c> reflection available for this variant.
        /// Each stage's own block lists only the members that stage references, and
        /// the fragment normally carries the full set, so the richest one wins and
        /// the others are unioned into it.
        /// </summary>
        private static AssetConstantBuffer BestGlobals(params SubProgram[] sps)
        {
            AssetConstantBuffer best = null;
            foreach (var sp in sps)
            {
                if (sp?.ConstantBuffers == null) continue;
                foreach (var cb in sp.ConstantBuffers)
                {
                    if (!cb.IsGlobals || cb.Members.Count == 0) continue;
                    if (best == null) { best = cb; continue; }
                    if (cb.Members.Count > best.Members.Count)
                    {
                        foreach (var m in best.Members)
                            if (!cb.Members.Exists(x => x.Name == m.Name && x.Offset == m.Offset))
                                cb.Members.Add(m);
                        best = cb;
                    }
                    else
                    {
                        foreach (var m in cb.Members)
                            if (!best.Members.Exists(x => x.Name == m.Name && x.Offset == m.Offset))
                                best.Members.Add(m);
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// 跨变体合并整个 pass 的 $Globals 成员表。
        ///
        /// <c>m_ConstantBuffers</c> 是"该子程序用到的成员"，不是缓冲的完整声明：
        /// A 变体列出 <c>_FaceDilate @ 96</c>，B 变体只用 <c>cb0[6]</c> 就没有这一条。
        /// $Globals 的布局由 uniform 声明顺序固定，**尺寸相同即布局相同**，所以可以
        /// 按 byte offset 把别的变体的成员补进来。尺寸不同的表不参与合并——那意味着
        /// 布局可能不同，硬合并会把名字贴到错误的行上。
        /// </summary>
        /// <summary>
        /// 本 pass 内每个子程序各解析一次，收集被参数块确认过的寄存器身份。
        /// 只收 <c>ConfirmedCb</c>（参数块逐条对上的），不猜。
        /// </summary>
        private static Dictionary<int, string> CollectIdentityHints(Pass pass,
            Dictionary<int, AsmParser.Program> progs, List<string> nameTable,
            IReadOnlyList<Property> props, Dictionary<int, List<BlobTool.CbInfo>> paramBlobs,
            GlobalsNames names)
        {
            var hint = new Dictionary<int, string>();
            foreach (var sp in pass.SubPrograms)
            {
                var p = Lookup(progs, sp);
                if (p == null) continue;
                var r = CbResolver.Resolve(p, nameTable, props, ParamFor(paramBlobs, sp), -1, names, null);
                foreach (var reg in r.ConfirmedCb)
                    if (r.CbName.TryGetValue(reg, out var nm) && nm != "$Globals")
                        hint[reg] = nm;
            }
            return hint;
        }

        /// <summary>
        /// A group of cbuffer layouts whose shared named rows all agree ("anchor
        /// rows").  Names may move between layouts of one cluster: the anchors
        /// prove the row indices line up.  Conflicting layouts land in separate
        /// clusters and never exchange names.
        /// </summary>
        private sealed class CbCluster
        {
            public string Name;
            public Dictionary<int, string> Rows = new();
            public Dictionary<(int row, int comp), string> Lanes = new();
        }

        /// <summary>
        /// One member can be written several ways in the emitted text: a matrix row
        /// arrives either as the bare name (<c>_EnvMatrix</c>, the whole-row form) or
        /// as an explicit column (<c>(_EnvMatrix._m00_m10_m20_m30)</c>).  Those name
        /// the same uniform at the same row, so cluster conflict detection must
        /// compare members, not literal strings -- otherwise one layout written in
        /// the column form and another in the whole-row form look like disagreeing
        /// evidence and get split into separate clusters, which blocks all name
        /// inheritance between them (that split is what left _LightColor0 numeric).
        /// </summary>
        private static string MemberBase(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return expr;
            string s = expr.Trim();
            while (s.Length > 1 && s[0] == '(' && s[s.Length - 1] == ')')
                s = s.Substring(1, s.Length - 2).Trim();
            int i = s.IndexOfAny(new[] { '.', '[' });
            return i > 0 ? s.Substring(0, i) : s;
        }

        private static bool SameMember(string a, string b) =>
            string.Equals(MemberBase(a), MemberBase(b), StringComparison.Ordinal);

        /// <summary>Rank a spelling so the explicit column form wins over the bare name.</summary>
        private static int Specificity(string expr) =>
            string.Equals(expr, MemberBase(expr), StringComparison.Ordinal) ? 0 : 1;

        /// <summary>Keep whichever spelling says more about the member.</summary>
        private static void KeepRicher(Dictionary<int, string> rows, int key, string expr)
        {
            if (!rows.TryGetValue(key, out var cur) || Specificity(expr) > Specificity(cur))
                rows[key] = expr;
        }

        /// <summary>
        /// True when this variant already has a name for the row, either as a whole
        /// row or as every lane the stage reads.  Used to decide what a cluster can
        /// still contribute without a cluster-level veto.
        /// </summary>
        private static bool RowSettled(CbResolver cb, int reg, int row)
        {
            if (cb.RowExpr.ContainsKey((reg, row))) return true;
            int mask = 0xF;
            if (cb.RowUse.TryGetValue(reg, out var ru) && ru.TryGetValue(row, out int m)) mask = m;
            for (int c = 0; c < 4; c++)
                if ((mask & (1 << c)) != 0 && !cb.CompExpr.ContainsKey((reg, row, c)))
                    return false;
            return true;
        }

        private static void EmitVariant(StringBuilder sb, AsmParser.Program vs, AsmParser.Program ps,
                                        IReadOnlyList<Property> props, List<string> nameTable,
                                        SubProgram vsSp, SubProgram psSp, Dictionary<int, List<BlobTool.CbInfo>> paramBlobs,
                                        GlobalsNames names, Dictionary<int, string> identityHint,
                                        bool tmpLayout, bool hlsl = false,
                                        List<CbCluster> cbClusters = null,
                                        Dictionary<string, (CbResolver VsCb, CbResolver PsCb)> cbCache = null,
                                        string variantKey = null,
                                        Dictionary<string, int> matrixRows = null)
        {
            var iface = new ShaderInterface();
            iface.BuildAppdata(vs?.InputSig);
            iface.BuildV2f(vs?.OutputSig, ps?.InputSig);

            // The pre-pass stores under the variant keyword string, so the read
            // must use that same key.  A blob-index pair is a different key
            // space and never matched, which silently disabled the cache and
            // made every variant re-resolve from scratch.
            var cacheKey = variantKey ?? ((vsSp?.BlobIndex ?? -1).ToString() + ":" + (psSp?.BlobIndex ?? -1));
            CbResolver vsCb, psCb;
            if (cbCache != null && cbCache.TryGetValue(cacheKey, out var cached))
            {
                vsCb = cached.VsCb;
                psCb = cached.PsCb;
            }
            else
            {
                var assetGlobals = BestGlobals(vsSp, psSp);
                var allParamCbs = paramBlobs?.Values.SelectMany(x => x).ToList();
                vsCb = CbResolver.Resolve(vs, nameTable, props, ParamFor(paramBlobs, vsSp), -1, names, assetGlobals, identityHint, allParamCbs, tmpLayout, null, vsSp?.TextureParams, matrixRows);
                psCb = CbResolver.Resolve(ps, nameTable, props, ParamFor(paramBlobs, psSp), -1, names, assetGlobals, identityHint, allParamCbs, tmpLayout, vsCb?.ChosenLayout, psSp?.TextureParams, matrixRows);
                if (cbCache != null) cbCache[cacheKey] = (vsCb, psCb);
            }

            // inherit row/lane names that sibling variants proved, when this
            // compile missed them.  Names only move between layouts whose
            // anchor rows agree, and never past the target's own extent.
            if (cbClusters != null)
            {
                foreach (var (tag, cb) in new[] { ("vs", vsCb), ("ps", psCb) })
                {
                    if (cb == null) continue;
                    foreach (var regKv in cb.CbName)
                    {
                        int extent = cb.CbSize.TryGetValue(regKv.Key, out int bytes) ? bytes / 16 : int.MaxValue;
                        // Collect every compatible cluster, not just the richest.
                        // Variants of one pass disagree on which rows they happened
                        // to name: the base light variant may pin rows 14/15 while
                        // the cookie variant pins 18/19, and each such variant is
                        // its own cluster.  Picking a single "best" cluster throws
                        // away exactly the evidence the others hold, which left
                        // cb0[2] and cb0[18] numeric in different variants.
                        var compat = new List<CbCluster>();
                        foreach (var c in cbClusters)
                        {
                            if (c.Name != regKv.Value) continue;
                            // A cluster is a candidate when it can still contribute
                            // at least one row this variant has not named.  Rows it
                            // reads differently are simply not taken from it --
                            // conflict is decided per row inside the fill loop.
                            // Rejecting the whole cluster on a single disagreement
                            // threw away every other row it knew: one layout that
                            // spells row 14 differently blocked row 2 (_LightColor0)
                            // as well, which is what left cb0[2] numeric.
                            if (c.Rows.Keys.Any(r => r < extent && !RowSettled(cb, regKv.Key, r)))
                                compat.Add(c);
                        }
                        CbCluster cl = compat.OrderByDescending(c => c.Rows.Count + c.Lanes.Count).FirstOrDefault();
                        if (cl == null) continue;
                        if (!cb.RowUse.TryGetValue(regKv.Key, out var ru)) continue;
                        int filled = 0;
                        foreach (var rowKv in ru)
                        {
                            int row = rowKv.Key;
                            if (row >= extent) continue;
                            // Ask every compatible cluster for this row; the richest
                            // answers first and later ones only fill what is still open.
                            foreach (var cc in compat.OrderByDescending(x => x.Rows.Count + x.Lanes.Count))
                            {
                                if (!cb.RowExpr.ContainsKey((regKv.Key, row)) && cc.Rows.TryGetValue(row, out var rn))
                                { cb.RowExpr[(regKv.Key, row)] = rn; filled++; }
                                for (int c = 0; c < 4; c++)
                                    if (!cb.CompExpr.ContainsKey((regKv.Key, row, c)) && cc.Lanes.TryGetValue((row, c), out var ln))
                                        cb.CompExpr[(regKv.Key, row, c)] = ln;
                            }

                            // A row named only in part (a matrix row is usually
                            // pinned lane by lane) still leaves the unnamed lanes
                            // as bare cbN[row].x references.  When a cluster knows
                            // the whole row, derive the missing lanes from it so
                            // the access becomes a member swizzle instead.
                            if (cb.RowExpr.TryGetValue((regKv.Key, row), out var whole))
                            {
                                string bas = whole;
                                bool swizzleable = bas.Length > 0 && bas.IndexOf('[') < 0
                                                   && bas[bas.Length - 1] != ')';
                                if (swizzleable)
                                    for (int c = 0; c < 4; c++)
                                        if ((rowKv.Value & (1 << c)) != 0 &&
                                            !cb.CompExpr.ContainsKey((regKv.Key, row, c)))
                                            cb.CompExpr[(regKv.Key, row, c)] = bas + "." + "xyzw"[c];
                            }
                        }
                    }
                }
            }

            bool hasVs = vs != null;
            bool hasPs = ps != null;
            if (Environment.GetEnvironmentVariable("SHADERRIP_TMPDBG") == "1")
                foreach (var (tag, cb) in new[] { ("vs", vsCb), ("ps", psCb) })
                    if (cb != null)
                        Console.Error.WriteLine($"[cbmap] {tag} reg0 rows={cb.RowExpr.Count(k => k.Key.cb == 0)} "
                            + $"comps={cb.CompExpr.Count(k => k.Key.cb == 0)} "
                            + $"name={cb.CbName.GetValueOrDefault(0, "?")}");

            if (hasVs && iface.AppdataFields.Count > 0)
                sb.Append(ShaderInterface.StructText("appdata", iface.AppdataFields));
            if (iface.V2fFields.Count > 0)
                sb.Append(ShaderInterface.StructText(hasVs ? "v2f" : "appdata", iface.V2fFields, renumber: true));

            // Everything below belongs to this variant only; DeclareResources uses
            // this mark to tell its own declarations from earlier variants'.
            int variantStart = sb.Length;
            sb.AppendLine();
            int declStart = sb.Length;
            DeclareResources(sb, variantStart, vs, ps, vsCb, psCb, props, hlsl);
            sb.AppendLine();

            int bodyStart = sb.Length;
            if (hasVs) EmitVertex(sb, vs, iface, vsCb, hlsl);
            if (hasPs) EmitFragment(sb, ps, iface, hasVs, psCb, hlsl);

            DeclareMissed(sb, declStart, bodyStart, props, hlsl);
        }

        /// <summary>
        /// A material property can be read by one variant while that variant's own
        /// constant-buffer reflection does not name the row -- the ETC1 branch of a
        /// sprite shader reads <c>_EnableExternalAlpha</c> and the sibling branch is
        /// where the row got its name.  The body then carries an identifier nothing
        /// declares and Unity refuses to compile it.  Fall back to the shader's own
        /// Properties block, which states the type, for any such name.
        /// </summary>
        private static void DeclareMissed(StringBuilder sb, int declStart, int bodyStart,
                                          IReadOnlyList<Property> props, bool hlsl = false)
        {
            if (props == null || props.Count == 0) return;
            if (bodyStart <= declStart || bodyStart > sb.Length) return;

            string declText = sb.ToString(declStart, bodyStart - declStart);
            string bodyText = sb.ToString(bodyStart, sb.Length - bodyStart);

            var have = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         declText, @"\b(?:(?:float|int|uint|bool)[234]?(?:x[234])?|sampler1D|sampler2D|sampler3D|samplerCUBE|sampler2DArray|Texture1D|Texture2D|Texture3D|TextureCube|Texture2DArray|SamplerState)\s+(\w+)"))
                have.Add(m.Groups[1].Value);

            var extra = new StringBuilder();
            foreach (Property pr in props)
            {
                if (have.Contains(pr.Name)) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(bodyText,
                        @"\b" + System.Text.RegularExpressions.Regex.Escape(pr.Name) + @"\b"))
                    continue;
                if (pr.Type >= 4)
                {
                    foreach (var line in CgSyntax.DeclareTexture(SamplerType(pr), pr.Name, hlsl, "\t"))
                        extra.Append(line)
                             .Append("  // from the Properties block (this variant's reflection did not name it)\n");
                }
                else
                {
                    string ty = pr.Type is 0 or 1 ? "float4" : "float";
                    extra.Append('\t').Append(ty).Append(' ').Append(pr.Name)
                         .Append(";  // from the Properties block (this variant's reflection did not name it)\n");
                }
                have.Add(pr.Name);
            }

            if (extra.Length > 0) sb.Insert(bodyStart, extra.ToString());
        }

        private static void DeclareResources(StringBuilder sb, int variantStart, AsmParser.Program vs,
                                             AsmParser.Program ps, CbResolver vsCb, CbResolver psCb,
                                             IReadOnlyList<Property> props, bool hlsl = false)
        {
            var seenTex = new HashSet<string>();
            var seenCb = new Dictionary<int, int>();
            var seenXt = new HashSet<int>();
            var seenSb = new HashSet<int>();
            bool icbDone = false;

            foreach (var (p, res) in new[] { (vs, vsCb), (ps, psCb) })
            {
                if (p == null) continue;
                foreach (var d in p.Decls)
                {
                    if (d.Kind != null && d.Kind.StartsWith("texture"))
                    {
                        string nm = res.TexName.TryGetValue(d.Register, out var tn) ? tn : "t" + d.Register;
                        string ty = d.TexDim switch
                        {
                            1 => "sampler1D", 3 => "sampler3D", 4 => "samplerCUBE",
                            _ => "sampler2D"
                        };
                        if (seenTex.Add(nm))
                            foreach (var line in CgSyntax.DeclareTexture(ty, nm, hlsl))
                                sb.AppendLine(line);
                    }
                    else if (d.Kind == "structured")
                    {
                        // vs and ps often share the same buffer register — emit once.
                        if (seenSb.Add(d.Register))
                        {
                            int n = Math.Max(1, d.Stride / 4);
                            sb.AppendLine($"struct SB{d.Register} {{ float v[{n}]; }};");
                            sb.AppendLine($"StructuredBuffer<SB{d.Register}> t{d.Register};");
                        }
                    }
                    else if (d.Kind == "constbuffer")
                    {
                        int size = Math.Max(1, d.Count);
                        if (seenCb.TryGetValue(d.Register, out int prev) && prev >= size) continue;
                        seenCb[d.Register] = size;
                    }
                    else if (d.Kind == "xtemp" && seenXt.Add(d.Register))
                        // static: indexable temps get written at runtime, and a
                        // plain global is implicitly constant in HLSL (X3025).
                        // static storage zero-initializes; an array cannot take a
                        // scalar initializer (X3017).
                        sb.AppendLine($"static float4 x{d.Register}[{Math.Max(1, d.Elements)}];");
                    else if (d.Kind == "icb" && !icbDone && d.IcbValues != null && d.IcbValues.Count > 0)
                    {
                        icbDone = true;
                        EmitIcb(sb, d.IcbValues);
                    }
                }
            }

            var emitted = new HashSet<string>(StringComparer.Ordinal);
            bool globalsClaimed = false;
            foreach (var kv in seenCb.OrderBy(k => k.Key))
            {
                int reg = kv.Key, size = kv.Value, bytes = 0;
                foreach (var res in new[] { vsCb, psCb })
                    if (res != null && res.CbSize.TryGetValue(reg, out int b))
                    {
                        bytes = Math.Max(bytes, b);
                        size = Math.Max(size, b / 16);
                    }
                // The raw cbN[] array exists only to bind reads that have no name.  Ask the
                // resolvers directly: cross-variant filling can name rows after the
                // mapping pass recorded FullyMapped, and trusting that stale snapshot
                // left a dead array (plus a duplicate of the whole buffer) in front of
                // fully named code.
                bool needRaw = NeedsRawArrayMerged(reg, size, vsCb, psCb);
                // "Fully mapped" means exactly "no read lane lacks a name", which is
                // what needRaw answers now.  The FullyMapped set is a snapshot taken
                // before cross-variant filling ran, so combining it here re-introduced
                // the very stale verdict the check above exists to avoid.
                bool full = !needRaw;
                string vsName = vs != null && vsCb.CbName.TryGetValue(reg, out var vsn) ? vsn : null;
                string psName = ps != null && psCb.CbName.TryGetValue(reg, out var psn) ? psn : null;
                bool globalsHere = vsName == "$Globals" || psName == "$Globals";
                var cbname = globalsHere ? "$Globals" : CbName(reg, vs, vsCb, ps, psCb);
                if (cbname == "$Globals")
                {
                    if (globalsClaimed) cbname = null;
                    else globalsClaimed = true;
                }
                var gnames = cbname != null ? SharedGlobals(reg, cbname, vs, vsCb, ps, psCb) : null;
                if (gnames != null)
                    gnames = gnames.Where(g => emitted.Add(g.name) && !UnityCbuffers.IncludeDeclared.Contains(g.name)).ToList();

                if (cbname == "$Globals")
                {
                    if (gnames != null && gnames.Count > 0)
                    {
                        sb.AppendLine($"// cb{reg} = $Globals (material properties); "
                                      + $"{gnames.Count} of them are named below");
                        foreach (var g in gnames) sb.AppendLine($"{g.type} {g.name};");
                        if (!full) sb.AppendLine($"float4 cb{reg}[{size}];  // remaining $Globals rows");
                    }
                    else
                        sb.AppendLine($"float4 cb{reg}[{size}];  // $Globals (material properties)");
                    EmitGlobalsNote(sb, reg, size, bytes, props, vs, vsCb, ps, psCb);
                }
                else if (full && cbname != null && UnityCbuffers.SelfDeclared.Contains(cbname)
                         && gnames != null && gnames.Count > 0)
                {
                    sb.AppendLine($"// cb{reg} = {cbname}  (not in UnityCG.cginc; declared here)");
                    foreach (var g in gnames) sb.AppendLine($"{g.type} {g.name};");
                }
                else if (full && cbname != null)
                    sb.AppendLine($"// cb{reg} = {cbname}  (declared by UnityCG.cginc)");
                else
                {
                    if (cbname != null) sb.AppendLine($"// cb{reg} = {cbname}");
                    sb.AppendLine($"float4 cb{reg}[{size}];");
                }
            }

            // Only this variant's own text: sb also holds every earlier variant of
            // the pass, and a name declared there does not exist here.
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         sb.ToString(variantStart, sb.Length - variantStart),
                         @"(?m)^\s*(?:(?:float|int|uint|bool)[234]?|sampler1D|sampler2D|sampler3D|samplerCUBE|sampler2DArray|Texture1D|Texture2D|Texture3D|TextureCube|Texture2DArray)\s+(\w+)"))
                declared.Add(m.Groups[1].Value);
            foreach (var p in props ?? new List<Property>())
            {
                if (string.IsNullOrEmpty(p.Name)) continue;
                bool tex = p.Type >= 4;
                if (declared.Add(p.Name))
                {
                    if (tex)
                        foreach (var line in CgSyntax.DeclareTexture(SamplerType(p), p.Name, hlsl))
                            sb.AppendLine(line);
                    else sb.AppendLine((p.Type is 0 or 1 ? "float4 " : "float ") + p.Name + ";");
                }
                if (!tex) continue;
                if (declared.Add(p.Name + "_ST")) sb.AppendLine($"float4 {p.Name}_ST;");
                if (declared.Add(p.Name + "_TexelSize")) sb.AppendLine($"float4 {p.Name}_TexelSize;");
            }
            var notes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var res in new[] { vsCb, psCb })
                foreach (var n in res?.Notes ?? new List<string>())
                    if (notes.Add(n)) sb.AppendLine(n);
        }

        /// <summary>属性表里的纹理维度 → Cg sampler 类型。</summary>
        private static string SamplerType(Property p) => p.DefTextureDim switch
        {
            3 => "sampler3D", 4 => "samplerCUBE", 5 => "sampler2DArray", 6 => "samplerCUBE",
            _ => "sampler2D",
        };

        /// <summary>
        /// State, for a $Globals block, exactly which rows are read, how wide each
        /// access is, and which of them carry a name.  Rows are only named when the
        /// evidence forces it, so whatever is left numeric is listed with the reason.
        /// </summary>
        private static void EmitGlobalsNote(StringBuilder sb, int reg, int size, int bytes,
                                            IReadOnlyList<Property> props,
                                            AsmParser.Program vs, CbResolver vsCb,
                                            AsmParser.Program ps, CbResolver psCb)
        {
            var use = new SortedDictionary<int, int>();
            foreach (var res in new[] { vsCb, psCb })
            {
                if (res == null || !res.CbName.TryGetValue(reg, out var n) || n != "$Globals") continue;
                if (!res.RowUse.TryGetValue(reg, out var r)) continue;
                foreach (var kv in r)
                {
                    use.TryGetValue(kv.Key, out int prev);
                    use[kv.Key] = prev | kv.Value;
                }
            }
            if (use.Count == 0) return;

            const string comps = "xyzw";

            bool Named(int row, int mask)
            {
                for (int c = 0; c < 4; c++)
                {
                    if ((mask & (1 << c)) == 0) continue;
                    bool hit = false;
                    foreach (var res in new[] { vsCb, psCb })
                    {
                        if (res == null) continue;
                        if (res.RowExpr.ContainsKey((reg, row)) ||
                            res.CompExpr.ContainsKey((reg, row, c)))
                        { hit = true; break; }
                    }
                    if (!hit) return false;
                }
                return true;
            }

            var unnamed = use.Where(k => !Named(k.Key, k.Value)).Select(k => k.Key).ToList();
            string sizeTxt = bytes > 0 ? $"{size} row(s) / {bytes} bytes" : $"{size} row(s)";
            sb.AppendLine($"// $Globals cb{reg}[{size}] = {sizeTxt}; {use.Count} row(s) read, "
                          + $"{use.Count - unnamed.Count} named.");

            string Lanes(int mask)
            {
                string s = "";
                for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) s += comps[i];
                return s;
            }

            var whole = use.Where(k => k.Value == 0xF).Select(k => k.Key).ToList();
            var part = use.Where(k => k.Value != 0xF)
                          .Select(k => $"row {k.Key}.{Lanes(k.Value)}").ToList();
            if (whole.Count > 0)
                sb.AppendLine($"//   read whole    : rows {string.Join(", ", whole)}");
            if (part.Count > 0)
                sb.AppendLine($"//   read partial  : {string.Join(" ", part)}");

            if (unnamed.Count == 0)
            {
                sb.AppendLine("// Every read row is named.  The block also holds rows neither stage");
                sb.AppendLine("// touches; they stay undeclared because nothing can pin them.");
                return;
            }

            sb.AppendLine("//   still numeric : " + string.Join(" ",
                          use.Where(k => unnamed.Contains(k.Key))
                             .Select(k => $"row {k.Key}.{Lanes(k.Value)}")));
            sb.AppendLine("// fxc decides the row order when it packs the shader's uniform declarations.");
            sb.AppendLine("// Where this asset records no member table for the block, a slot is only named");
            sb.AppendLine("// when the code itself forces the assignment; the rest stay numeric.");

            var vec = new List<string>();
            var sca = new List<string>();
            var already = new HashSet<string>(StringComparer.Ordinal);
            foreach (var res in new[] { vsCb, psCb })
                if (res != null && res.GlobalsDecl.TryGetValue(reg, out var gl))
                    foreach (var g in gl) already.Add(g.name);

            foreach (var p in props ?? new List<Property>())
            {
                if (already.Contains(p.Name)) continue;
                if (p.Type is 0 or 1) vec.Add(p.Name);
                else if (p.Type is 2 or 3) sca.Add(p.Name);
            }
            if (vec.Count > 0)
                sb.AppendLine($"//   unnamed whole rows can only be one of the {vec.Count} Color/Vector prop(s): "
                              + string.Join(", ", vec));
            if (sca.Count > 0)
                sb.AppendLine($"//   unnamed single lanes are shared by the Float/Range prop(s): "
                              + string.Join(", ", sca));
        }

        private static string CbName(int reg, AsmParser.Program vs, CbResolver vsCb,
                                     AsmParser.Program ps, CbResolver psCb)
        {
            if (vs != null && vsCb.CbName.TryGetValue(reg, out var a)) return a;
            if (ps != null && psCb.CbName.TryGetValue(reg, out var b)) return b;
            return null;
        }

        /// <summary>
        /// Whether the shared <c>cbN[]</c> declaration is still needed.
        ///
        /// The two stages emit into one HLSL unit and the declaration is shared, so
        /// a lane is covered when <em>either</em> resolver named it -- the same
        /// rule <see cref="EmitGlobalsNote"/> uses when it reports "N row(s) read,
        /// M named".  Asking each resolver on its own reported the array as needed
        /// even though the other stage had already named every lane, which left a
        /// dead <c>float4 cb0[35];</c> duplicating the buffer in two passes.
        /// </summary>
        private static bool NeedsRawArrayMerged(int reg, int extent, CbResolver vsCb, CbResolver psCb)
        {
            if ((vsCb == null || vsCb.RowUse.Count == 0) && (psCb == null || psCb.RowUse.Count == 0))
                return false;
            // A dynamic index has no named form at all, so it always needs the array.
            if (vsCb != null && vsCb.NeedsRawArray(reg, extent) && vsCb.HasDynamicIndex(reg)) return true;
            if (psCb != null && psCb.NeedsRawArray(reg, extent) && psCb.HasDynamicIndex(reg)) return true;

            var rows = new SortedSet<int>();
            foreach (var res in new[] { vsCb, psCb })
                if (res != null && res.RowUse.TryGetValue(reg, out var ru))
                    foreach (var kv in ru)
                        if (kv.Key < extent) rows.Add(kv.Key);

            foreach (int row in rows)
            {
                int mask = 0;
                foreach (var res in new[] { vsCb, psCb })
                    if (res != null && res.RowUse.TryGetValue(reg, out var ru) && ru.TryGetValue(row, out int m))
                        mask |= m;
                if (mask == 0) mask = 0xF;
                for (int c = 0; c < 4; c++)
                {
                    if ((mask & (1 << c)) == 0) continue;
                    bool named = false;
                    foreach (var res in new[] { vsCb, psCb })
                    {
                        if (res == null) continue;
                        if (res.RowExpr.ContainsKey((reg, row)) || res.CompExpr.ContainsKey((reg, row, c)))
                        { named = true; break; }
                    }
                    if (!named) return true;
                }
            }
            return false;
        }

        private static bool IsFullyMapped(int reg, AsmParser.Program p, CbResolver res)
        {
            if (p == null) return true;
            bool uses = p.Decls.Any(d => d.Kind == "constbuffer" && d.Register == reg);
            return !uses || res.FullyMapped.Contains(reg);
        }

        private static List<(string name, string type)> SharedGlobals(int reg, string cbname,
                                                  AsmParser.Program vs, CbResolver vsCb,
                                                  AsmParser.Program ps, CbResolver psCb)
        {
            List<(string name, string type)> Pick(AsmParser.Program p, CbResolver res)
            {
                if (p == null || res == null) return null;
                if (!res.CbName.TryGetValue(reg, out var n)) return null;
                if (cbname == "$Globals")
                {
                    if (n != "$Globals") return null;
                }
                else if (n != cbname) return null;
                return res.GlobalsDecl.TryGetValue(reg, out var g) ? g : null;
            }

            var a = Pick(vs, vsCb);
            var b = Pick(ps, psCb);
            if (a != null && b != null)
            {
                var u = new List<(string name, string type)>(a);
                foreach (var x in b) if (!u.Any(y => y.name == x.name)) u.Add(x);
                return u;
            }
            return a ?? b;
        }

        private static void EmitIcb(StringBuilder sb, List<float[]> rows)
        {
            sb.AppendLine($"static const float4 icb[{rows.Count}] = {{");
            for (int i = 0; i < rows.Count; i++)
            {
                var v = rows[i];
                var c = new string[4];
                for (int k = 0; k < 4; k++) c[k] = Num(k < v.Length ? v[k] : 0f);
                sb.AppendLine("\tfloat4(" + string.Join(", ", c) + ")" + (i + 1 < rows.Count ? "," : ""));
            }
            sb.AppendLine("};");
        }

        private static string Num(float f)
        {
            if (f == Math.Floor(f) && Math.Abs(f) < 1e7)
                return ((long)f).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".0";
            return f.ToString("0.0######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void EmitVertex(StringBuilder sb, AsmParser.Program p, ShaderInterface iface, CbResolver cb,
                                       bool hlsl = false)
        {
            string inStruct = iface.AppdataFields.Count > 0 ? "appdata" : "v2f";
            string args = iface.V2fFields.Count > 0 ? $"in {inStruct} v" : $"{inStruct} v";
            sb.AppendLine($"v2f vert ({args})");
            sb.AppendLine("{");

            var binds = iface.BindInputs(iface.VsInReg, "v", r => NeededComps(p, r)).ToList();
            foreach (var line in binds)
                sb.AppendLine(line);

            foreach (int r in OutRegisters(p))
                sb.AppendLine($"\tfloat4 o{r} = 0;" + Comment(iface, r));

            DeclareRegisters(sb, p);
            sb.AppendLine();

            string vret = "new v2f { " + string.Join(", ", iface.BindOutputs(iface.VsOutReg, "o")
                .Select(l => l.Trim().TrimEnd(';').Replace("o.", ""))) + " }";
            sb.Append(CollapseMvp(new CgTranslator(p, 1) { Cb = cb, AnnotateAsm = AnnotateAsm, ReturnExpr = vret, Hlsl = hlsl }.TranslateBody(),
                                  string.Join("\n", binds)));

            sb.AppendLine();
            sb.AppendLine("\tv2f o;");
            foreach (var line in iface.BindOutputs(iface.VsOutReg, "o"))
                sb.AppendLine(line);
            sb.AppendLine("\treturn o;");
            sb.AppendLine("}");
        }

        private static void EmitFragment(StringBuilder sb, AsmParser.Program p, ShaderInterface iface,
                                         bool hasVs, CbResolver cb, bool hlsl = false)
        {
            string inStruct = hasVs ? "v2f" : "appdata";
            sb.AppendLine($"float4 frag (in {inStruct} i) : SV_Target");
            sb.AppendLine("{");

            foreach (var line in iface.BindInputs(iface.PsInReg, "i", r => NeededComps(p, r)))
                sb.AppendLine(line);

            var oRegs = OutRegisters(p);
            int outReg = oRegs.Count > 0 ? oRegs[0] : 0;
            foreach (int r in oRegs) sb.AppendLine($"\tfloat4 o{r} = 0;");

            DeclareRegisters(sb, p);
            sb.AppendLine();

            sb.Append(new CgTranslator(p, 1) { Cb = cb, AnnotateAsm = AnnotateAsm, ReturnExpr = $"o{outReg}", Hlsl = hlsl }.TranslateBody());

            sb.AppendLine($"\treturn o{outReg};");
            sb.AppendLine("}");
        }

        /// <summary>
        /// hlslcc and fxc both spell the model-view-projection product as eight explicit
        /// mul/mad/add lines over <c>unity_ObjectToWorld[0..3]</c> and
        /// <c>unity_MatrixVP[0..3]</c>.  Re-emitting that as array indexing does not
        /// behave like <c>mul(M, v)</c> under Unity's compiler -- on a real project it
        /// threw the whole mesh into a fan -- so collapse the pattern back to
        /// <c>UnityObjectToClipPos</c>.
        /// </summary>
        private static string CollapseMvp(string body, string binds)
        {
            var alias = new System.Collections.Generic.Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match am in
                     System.Text.RegularExpressions.Regex.Matches(binds,
                         @"(?m)^\s*float([234])?\s+v(\d+)\s*=\s*v\.(\w+)\s*;"))
                alias["v" + am.Groups[2].Value] = "v." + am.Groups[3].Value;

            // Matrices are read column by column, so the world-position term is
            // "v0.y * (unity_ObjectToWorld._m01_m11_m21_m31).x" and the clip-space
            // write ends in "unity_MatrixVP._m03_m13_m23_m33".
            var re = new System.Text.RegularExpressions.Regex(
                @"(?ms)^\s*(\w+)\.xyzw = float4\(([^;\r\n]*?)\.y \* \(unity_ObjectToWorld\._m01_m11_m21_m31\)\.x[^;\r\n]*\);\r?\n" +
                @".*?^\s*(\w+)\.xyzw = float4\([^;\r\n]*(?:\(unity_MatrixVP\._m03_m13_m23_m33\)\.x|unity_MatrixVP\[3\]\.x)[^;\r\n]*\);\r?\n");

            // Output writes can sit inside the spanned lines -- a shader that also
            // passes world position out (TEXCOORD1 = mul(unity_ObjectToWorld, v))
            // lands there, and the span used to swallow it, leaving that member
            // unassigned ("warning X3578: Output value 'vert' is not completely
            // initialized" in Unity).  Only that one shape is understood; anything
            // else means the span is not a plain MVP block and is left alone.
            var worldPos = new System.Text.RegularExpressions.Regex(
                @"(?m)^[ \t]*(o\d+)\.(\w+)[ \t]*=[ \t]*float3\(mad\((?:unity_ObjectToWorld\[3\]|\(unity_ObjectToWorld\._m03_m13_m23_m33\))\.[^\r\n]*\);");
            var anyOutput = new System.Text.RegularExpressions.Regex(@"(?m)^[ \t]*(o\d+)\.");

            return re.Replace(body, m =>
            {
                string dest = m.Groups[3].Value;
                string reg = m.Groups[2].Value.Trim();
                // The chain can start from a temp (a sprite flips the vertex before
                // transforming it), so an unknown register is not a reason to bail.
                string field = alias.TryGetValue(reg, out var f) ? f : reg;

                // The match starts at the line start, so it swallowed the
                // indentation -- put it back, or the collapsed line lands in
                // column 0 while every neighbour stays one level in.
                int k = 0;
                while (k < m.Value.Length && (m.Value[k] == ' ' || m.Value[k] == '\t')) k++;
                string lead = m.Value.Substring(0, k);

                var prefix = new StringBuilder();
                foreach (System.Text.RegularExpressions.Match om in anyOutput.Matches(m.Value))
                {
                    if (om.Groups[1].Value == dest) continue;          // the clip position itself
                    var wm = worldPos.Match(m.Value, om.Index);
                    if (!wm.Success || wm.Groups[1].Value != om.Groups[1].Value) return m.Value;
                    var sm = System.Text.RegularExpressions.Regex.Match(
                        wm.Value, @"\bv(\d+)\.w\b");
                    if (!sm.Success || !alias.TryGetValue("v" + sm.Groups[1].Value, out var src))
                        return m.Value;
                    prefix.Append(lead).Append(wm.Groups[1].Value).Append('.').Append(wm.Groups[2].Value)
                          .Append(" = mul(unity_ObjectToWorld, float4(").Append(src).Append(".xyz, ")
                          .Append(src).Append(".w)).").Append(wm.Groups[2].Value).Append(';')
                          .Append(Environment.NewLine);
                }

                return prefix + lead + $"{dest} = UnityObjectToClipPos({field}.xyz);" + Environment.NewLine;
            }, 1);
        }
        private static List<int> OutRegisters(AsmParser.Program p) =>
            p.Code.Where(i => i.Operands.Count > 0 && i.Operands[0].Kind == "o")
                  .Select(i => i.Operands[0].Index).Distinct().OrderBy(x => x).ToList();

        private static string Comment(ShaderInterface iface, int reg) =>
            iface.VsOutReg.TryGetValue(reg, out var fs) && fs.Count > 0
                ? "  // -> o." + string.Join(" / o.", fs.Select(x => x.Name))
                : "";

        private static int NeededComps(AsmParser.Program p, int reg)
        {
            int need = 0;
            foreach (var ins in p.Code)
                foreach (var o in ins.Operands)
                    if (o.Kind == "v" && o.Index == reg)
                        need = Math.Max(need, MaxSwizzle(o.Swizzle));
            return need;
        }

        private static int MaxSwizzle(string sw)
        {
            if (string.IsNullOrEmpty(sw)) return 4;
            int m = 1;
            foreach (char c in sw) m = Math.Max(m, c switch { 'x' => 1, 'y' => 2, 'z' => 3, 'w' => 4, _ => 4 });
            return m;
        }

        private static void DeclareRegisters(StringBuilder sb, AsmParser.Program p)
        {
            int temps = 0;
            foreach (var d in p.Decls) if (d.Kind == "temps") temps = Math.Max(temps, d.Count);
            if (temps > 0)
                sb.AppendLine("\tfloat4 " + string.Join(", ", Enumerable.Range(0, temps).Select(i => "r" + i + " = 0")) + ";");
            var used = new SortedSet<int>();
            foreach (var ins in p.Code)
                foreach (var o in ins.Operands)
                    if (o.Kind == "r" && o.Index >= temps) used.Add(o.Index);
            if (used.Count > 0)
                sb.AppendLine("\tfloat4 " + string.Join(", ", used.Select(i => "r" + i + " = 0")) + ";");
        }
    }
}
