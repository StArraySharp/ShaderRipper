using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Antlr.Data;
using ShaderRipper.Antlr.Emit;
using ShaderRipper.Antlr.Resolve;

namespace ShaderRipper.Antlr.Game
{
    /// <summary>
    /// Game data -> HLSL, inside this project.
    ///
    /// The reading layer is the one the main tool uses, copied rather than
    /// referenced: SerializedFile + typetree database to find the Shader
    /// objects, BlobTool to decode Unity's LZ4 blob and carve the program
    /// containers out of it, dxcompiler to disassemble DXBC.  What differs is
    /// what happens after the bytes exist -- this project's grammar-driven
    /// translators (DxbcAsm for DXBC, Glsl for the GLES/GL platforms), not the
    /// hand-written ones.
    ///
    /// Per shader: every pass's keyword variants are recovered with
    /// <c>VariantPlan</c> and emitted as guarded chains inside one
    /// HLSLPROGRAM, the way the original source had them before Unity
    /// flattened them.
    /// </summary>
    internal static class GameExport
    {
        public sealed class Stats
        {
            public int Files, Shaders, Programs, Disassembled, Translated, Skipped, Failed;
            public readonly List<string> Notes = new();
        }

        /// <summary>Everything one pass needs for the ShaderLab wrapper.</summary>
        private sealed class PassBuild
        {
            /// <summary>Keyword keys in first-appearance (blob index) order.</summary>
            public readonly List<string> Keys = new();
            public readonly Dictionary<string, (List<string> kw, string hlsl)> Vs = new();
            public readonly Dictionary<string, (List<string> kw, string hlsl)> Ps = new();
            /// <summary>GLSL variants: one body carries both entry points.</summary>
            public readonly List<string> GKeys = new();
            public readonly Dictionary<string, (List<string> kw, string hlsl)> Glsl = new();

            public void Add(Dictionary<string, (List<string> kw, string hlsl)> into,
                            List<string> keys, string key, List<string> kw, string hlsl)
            {
                if (!into.ContainsKey(key)) keys.Add(key);
                into[key] = (kw, hlsl);
            }
        }

        public static Stats Run(string dataDir, string outDir, string only, bool shaderLab,
                                bool origAsm = false, bool dedup = false)
        {
            var s = new Stats();
            Directory.CreateDirectory(outDir);

            Func<string, int, TypeNode> tpk = (version, classId) => TpkSchema.BuildClass(classId, version);
            var used = new HashSet<string>(StringComparer.Ordinal);

            // Collect first, export second: `--dedup` has to see every copy of a
            // shader before picking the one worth emitting.
            var found = new List<(Shader sh, string name)>();
            foreach (var (inName, inBytes, inPath) in Inputs(dataDir))
            {
                SerializedFile sf;
                try
                {
                    sf = inBytes != null
                        ? SerializedFile.Load(inBytes, null, tpk)
                        : SerializedFile.Load(inPath, null, tpk);
                }
                catch (Exception e) { s.Failed++; s.Notes.Add($"{inName}: {e.Message}"); continue; }
                s.Files++;

                foreach (var sh in sf.Shaders)
                {
                    string name = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                                : !string.IsNullOrEmpty(sh.ParsedForm?.Name) ? sh.ParsedForm.Name
                                : $"pathId_{sh.PathId}";
                    if (only != null && !name.Contains(only, StringComparison.OrdinalIgnoreCase)) continue;
                    found.Add((sh, name));
                }
            }

            IEnumerable<(Shader sh, string name)> export = found;
            if (dedup)
            {
                // The same shader can ship in several data files; keep one per
                // output name.  A copy with a fixed container path wins (it
                // lands on the plain file name), then the lowest path id so the
                // winner never depends on enumeration order.
                var winners = found.GroupBy(t => SafeKey(t.name), StringComparer.Ordinal)
                                   .Select(g => g.OrderByDescending(t => !string.IsNullOrEmpty(t.sh.ContainerPath))
                                                 .ThenBy(t => t.sh.PathId).First())
                                   .OrderBy(t => t.sh.PathId)
                                   .ToList();
                s.Notes.Add($"dedup: {found.Count} -> {winners.Count} shader(s), "
                            + $"{found.Count - winners.Count} duplicate(s) dropped");
                export = winners;
            }

            foreach (var (sh, name) in export)
            {
                s.Shaders++;
                try { ExportShader(sh, name, outDir, used, shaderLab, origAsm, s); }
                catch (Exception e) { s.Failed++; s.Notes.Add($"{name}: {e.Message}"); }
            }
            return s;
        }

        private static IEnumerable<string> Candidates(string dataDir)
        {
            // resources.assets first: it is where Resources-included shaders live.
            return Directory.EnumerateFiles(dataDir, "*.assets", SearchOption.TopDirectoryOnly)
                            .OrderBy(f => Path.GetFileName(f) == "resources.assets" ? 0 : 1)
                            .ThenBy(f => f, StringComparer.Ordinal);
        }

        /// <summary>
        /// Loose *.assets files plus the SerializedFile entries inside *.bundle
        /// archives -- DLC and scene bundles routinely carry the only copy of a
        /// shader, and the GLES-only builds often ship nothing but bundles.
        /// </summary>
        private static IEnumerable<(string name, byte[] bytes, string path)> Inputs(string dataDir)
        {
            foreach (var f in Candidates(dataDir))
                yield return (Path.GetFileName(f), null, f);
            foreach (var b in Directory.EnumerateFiles(dataDir, "*.bundle", SearchOption.TopDirectoryOnly)
                                       .OrderBy(f => f, StringComparer.Ordinal))
                foreach (var (path, data) in Bundle.SerializedFiles(b))
                    yield return ($"{Path.GetFileName(b)}/{path}", data, b);
        }

        private static string SafeKey(string name)
        {
            string safe = name.Replace('/', '_').Replace('\\', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return safe;
        }

        private static void ExportShader(Shader sh, string name, string outDir,
                                         HashSet<string> used, bool shaderLab, bool origAsm, Stats s)
        {
            string dirName = Safe(name, used);
            string dir = Path.Combine(outDir, dirName);

            var props = sh.ParsedForm?.Props ?? new List<Property>();
            var globals = GlobalsNames.For(name);

            // The keyword table the variant analysis reads: m_KeywordNames, one
            // entry per index a sub-program's m_KeywordIndices can name.
            var kwIndex = new Dictionary<int, string>();
            for (int ki = 0; ki < (sh.ParsedForm?.KeywordNames.Count ?? 0); ki++)
                if (!string.IsNullOrEmpty(sh.ParsedForm.KeywordNames[ki]))
                    kwIndex[ki] = sh.ParsedForm.KeywordNames[ki];

            // Programs by pass, so the ShaderLab wrapper can put a pass's whole
            // variant set in one program block.  Keyed by the pass object itself.
            var perPass = new Dictionary<Pass, PassBuild>();

            // Which pass and sub-program each blob belongs to.  The name table and
            // the asset's own reflection hang off those, and a blob index is the
            // only thing the decoded slot tells us.
            var passOf = new Dictionary<int, Pass>();
            var spOf = new Dictionary<int, SubProgram>();
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
                foreach (var p in ss.Passes)
                    foreach (var sp in p.SubPrograms)
                    {
                        passOf[sp.BlobIndex] = p;
                        // Two passes can share a blob index; the one that declares the
                        // reflection is the useful entry, so keep the richer one.
                        if (!spOf.TryGetValue(sp.BlobIndex, out var have) ||
                            sp.ConstantBuffers.Count > have.ConstantBuffers.Count)
                            spOf[sp.BlobIndex] = sp;
                    }

            // Every platform the game shipped, one at a time: d3d11 drives the
            // DXBC route, the GLES/GL platforms drive the GLSL route, everything
            // else is noted and skipped (no translator for it in this project).
            for (int pi = 0; pi < (sh.Platforms?.Length ?? 0); pi++)
            {
                int pid = (int)sh.Platforms[pi];
                var info = Platforms.Get(pid);
                if (info == null || info.Format is not ("dxbc" or "glsl")) continue;

                byte[] decoded;
                try { decoded = BlobTool.DecodeBlob(sh, pi); }
                catch (Exception e) { s.Skipped++; s.Notes.Add($"{name}/{info.Name}: decode failed: {e.Message}"); continue; }
                if (decoded == null || decoded.Length == 0) { s.Skipped++; continue; }

                if (info.Format == "dxbc") ExportDxbc(sh, name, info, decoded, dir, dirName,
                    passOf, spOf, kwIndex, props, globals, perPass, origAsm, s);
                else ExportGlsl(sh, name, info, decoded, dir, passOf, spOf, kwIndex, perPass, s);
            }

            if (shaderLab && perPass.Count > 0)
            {
                var text = Emit.ShaderLab.Wrap(name, sh.FallbackName,
                                               PassTree(sh, perPass), props);
                var errs = Emit.ShaderLab.Check(text);
                if (errs.Count == 0) File.WriteAllText(Path.Combine(outDir, dirName + ".shader"), text);
                else s.Notes.Add($"{name}: shaderlab not well-formed: {errs[0]}");
            }
        }

        // ------------------------------------------------------------------
        // DXBC route (d3d11): disassemble, translate, keep every variant
        // ------------------------------------------------------------------

        private static void ExportDxbc(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, string dirName, Dictionary<int, Pass> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<int, string> kwIndex, List<Property> props, GlobalsNames globals,
            Dictionary<Pass, PassBuild> perPass, bool origAsm, Stats s)
        {
            var slots = BlobTool.DecodeSlots(decoded, UsedBlobIndices(sh));

            // The disassembly carries no reflection (Unity strips RDEF), so the
            // only source for `cb1[7]`'s name is the asset's own parameter blob.
            var paramCbs = BlobTool.ParseParamBlobs(decoded);
            var allParamCbs = paramCbs.Values.SelectMany(x => x).ToList();
            var matrixRows = MatrixSpans(sh);

            foreach (var slot in slots)
            {
                s.Programs++;
                if (slot.DxbcSize <= 0 || slot.DxbcOffset + slot.DxbcSize > decoded.Length) continue;

                var container = new byte[slot.DxbcSize];
                Array.Copy(decoded, slot.DxbcOffset, container, 0, slot.DxbcSize);

                string asm = BlobTool.Disassemble(container);
                if (asm == null) { s.Skipped++; continue; }
                s.Disassembled++;

                var module = Ast.Module.Read(asm);
                if (module.Code.Count == 0) { s.Skipped++; continue; }

                spOf.TryGetValue(slot.BlobIndex, out var sub);
                passOf.TryGetValue(slot.BlobIndex, out var pass);
                module.Cb = NameConstantBuffers(module, name, pass, sub, props, paramCbs, allParamCbs,
                                                globals, matrixRows);

                var r = HlslEmitter.Emit(module);
                s.Translated++;

                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.hlsl");
                File.WriteAllText(file, r.Hlsl);

                // The original disassembly, kept beside the translation so the two
                // can be compared instruction for instruction (see
                // scripts/cmp_dxbc.py).  It is the same text the translator read.
                if (origAsm)
                    File.WriteAllText(Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.orig.asm"), asm);

                if (pass == null) continue;
                if (!perPass.TryGetValue(pass, out var build)) perPass[pass] = build = new PassBuild();
                var kw = VariantPlan.KeywordsOf(sub, kwIndex);
                var key = VariantPlan.Key(kw);
                if (module.Stage is "ps") build.Add(build.Ps, build.Keys, key, kw, r.Hlsl);
                else if (module.Stage is "vs") build.Add(build.Vs, build.Keys, key, kw, r.Hlsl);
            }
        }

        // ------------------------------------------------------------------
        // GLSL route (gles20/gles3/glcore): extract text, translate, per variant
        // ------------------------------------------------------------------

        private static void ExportGlsl(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, Dictionary<int, Pass> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<int, string> kwIndex, Dictionary<Pass, PassBuild> perPass, Stats s)
        {
            // Slot i of the payload belongs to the i-th used blob index -- the
            // same convention the DXBC slot table follows.
            var order = UsedBlobIndices(sh).OrderBy(x => x).ToList();
            var progs = ProgramSource.Extract(decoded);

            foreach (var pr in progs)
            {
                if (pr.Format != BlobFormat.Glsl || string.IsNullOrEmpty(pr.Text)) { s.Skipped++; continue; }
                int bi = pr.BlobIndex < order.Count ? order[pr.BlobIndex] : pr.BlobIndex;

                var r = GlslTranslator.Translate(pr.Text);
                s.Programs++;
                if (string.IsNullOrWhiteSpace(r.Hlsl)) { s.Skipped++; continue; }
                s.Translated++;

                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"{bi}_glsl.hlsl"), r.Hlsl);

                spOf.TryGetValue(bi, out var sub);
                passOf.TryGetValue(bi, out var pass);
                if (pass == null) continue;
                if (!perPass.TryGetValue(pass, out var build)) perPass[pass] = build = new PassBuild();
                var kw = VariantPlan.KeywordsOf(sub, kwIndex);
                build.Add(build.Glsl, build.GKeys, VariantPlan.Key(kw), kw, r.Hlsl);
            }
        }

        // ------------------------------------------------------------------
        // ShaderLab assembly
        // ------------------------------------------------------------------

        /// <summary>
        /// The shader's own structure, translated into what the wrapper needs:
        /// SubShaders with their tags, Passes with their names, tags and the render
        /// state that differs from ShaderLab's default, and the pass's variant
        /// chains.  A pass with exactly one variant per stage and no keywords
        /// takes the single-program path (which cross-stage merges the
        /// declarations); anything richer goes through the guarded chains.
        /// </summary>
        private static List<Emit.ShaderLab.SubShaderText> PassTree(
            Shader sh, Dictionary<Pass, PassBuild> programs)
        {
            var list = new List<Emit.ShaderLab.SubShaderText>();
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
            {
                var text = new Emit.ShaderLab.SubShaderText { LOD = ss.LOD };
                text.Tags.AddRange(ss.Tags);

                foreach (var p in ss.Passes)
                {
                    var pt = new Emit.ShaderLab.PassText { Name = p.StateName };
                    pt.Tags.AddRange(p.Tags);
                    if (p.ZWrite != 1) pt.States.Add(p.ZWrite == 0 ? "ZWrite Off" : "ZWrite On");
                    if (p.ZTest != 4) pt.States.Add($"ZTest {StateName("ZTest", p.ZTest)}");
                    if (p.Culling != 2) pt.States.Add($"Cull {StateName("Cull", p.Culling)}");
                    if (p.Blends.Count > 0 && !(p.Blends[0].SrcBlend == 1 && p.Blends[0].DestBlend == 0))
                        pt.States.Add($"Blend {StateName("Blend", p.Blends[0].SrcBlend)} "
                                      + StateName("Blend", p.Blends[0].DestBlend));

                    if (programs.TryGetValue(p, out var pb)) FillPass(pt, pb);
                    text.Passes.Add(pt);
                }
                list.Add(text);
            }
            return list;
        }

        private static void FillPass(Emit.ShaderLab.PassText pt, PassBuild pb)
        {
            bool hasKeywords = pb.Keys.Any(k => k.Length > 0) || pb.GKeys.Any(k => k.Length > 0);

            // GLSL variants: one body per keyword combination, both stages inside.
            if (pb.GKeys.Count > 0)
            {
                var glslKeys = pb.GKeys.Where(k => pb.Glsl.ContainsKey(k)).ToList();
                var groups = VariantPlan.Groups(glslKeys.Select(k => pb.Glsl[k].kw));
                pt.Pragmas.AddRange(groups.Select(VariantPlan.Pragma));
                pt.Notes.AddRange(VariantPlan.Analyze(groups,
                    glslKeys.Select(k => new HashSet<string>(pb.Glsl[k].kw, StringComparer.Ordinal)).ToList())
                    .Notes.Select(n => $"{glslKeys.Count} GLSL keyword variant(s), {groups.Count} multi_compile line(s): " + n));
                pt.GlslIncludes = true;
                pt.Chains = new List<Emit.ShaderLab.VariantChain>
                {
                    new()
                    {
                        Guards = glslKeys.Select(k => VariantPlan.Guard(groups, pb.Glsl[k].kw)).ToList(),
                        Bodies = glslKeys.Select(k => pb.Glsl[k].hlsl).ToList(),
                    }
                };
                return;
            }

            bool single = !hasKeywords && pb.Keys.Count(k => pb.Vs.ContainsKey(k) || pb.Ps.ContainsKey(k)) <= 1
                          && (pb.Vs.Count <= 1 && pb.Ps.Count <= 1);
            if (single)
            {
                // The old path: both stages' declarations cross-merge into one
                // program, which is strictly better output than two chains.
                if (pb.Vs.Count == 1)
                {
                    var v = pb.Vs[pb.Keys.First(k => pb.Vs.ContainsKey(k))];
                    pt.Vertex = new Emit.ShaderLab.Stage { Hlsl = v.hlsl, Entry = "vert" };
                }
                if (pb.Ps.Count == 1)
                {
                    var v = pb.Ps[pb.Keys.First(k => pb.Ps.ContainsKey(k))];
                    pt.Fragment = new Emit.ShaderLab.Stage { Hlsl = v.hlsl, Entry = "frag" };
                }
                return;
            }

            var kwSets = pb.Keys.Where(k => pb.Vs.ContainsKey(k) || pb.Ps.ContainsKey(k))
                                .Select(k => pb.Vs.TryGetValue(k, out var a) ? a.kw
                                            : pb.Ps[k].kw)
                                .ToList();
            var groups2 = VariantPlan.Groups(kwSets);
            pt.Pragmas.AddRange(groups2.Select(VariantPlan.Pragma));
            int total = kwSets.Count();
            pt.Notes.Add($"{total} keyword variant(s), {groups2.Count} multi_compile line(s)");
            pt.Notes.AddRange(VariantPlan.Analyze(groups2,
                kwSets.Select(k => new HashSet<string>(k, StringComparer.Ordinal)).ToList()).Notes);

            // One chain, one branch per keyword combination: the branch body is
            // the vs+ps pair of that combination cross-merged by Merge, so the
            // shared cbuffer/struct declarations exist exactly once per branch.
            // Emitting the stages as two independent guard chains would activate
            // both under the same keyword state and redefine every shared member.
            var chain = new Emit.ShaderLab.VariantChain();
            foreach (var k in pb.Keys)
            {
                bool hasVs = pb.Vs.TryGetValue(k, out var v);
                bool hasPs = pb.Ps.TryGetValue(k, out var pv);
                if (!hasVs && !hasPs) continue;
                var merged = Emit.ShaderLab.Merge(
                    hasVs ? new Emit.ShaderLab.Stage { Hlsl = v.hlsl, Entry = "vert" } : null,
                    hasPs ? new Emit.ShaderLab.Stage { Hlsl = pv.hlsl, Entry = "frag" } : null);
                if (merged == null) continue;
                chain.Guards.Add(VariantPlan.Guard(groups2, hasVs ? v.kw : pv.kw));
                chain.Bodies.Add(string.Join("\n", merged));
            }
            pt.Chains = chain.Bodies.Count > 0 ? new List<Emit.ShaderLab.VariantChain> { chain } : null;
        }

        private static readonly Dictionary<float, string> ZTestNames = new()
        {
            [0] = "Off", [1] = "Never", [2] = "Less", [3] = "Equal", [4] = "LEqual",
            [5] = "Greater", [6] = "NotEqual", [7] = "GEqual", [8] = "Always",
        };

        private static readonly Dictionary<float, string> BlendNames = new()
        {
            [0] = "Zero", [1] = "One", [2] = "DstColor", [3] = "SrcColor",
            [4] = "OneMinusDstColor", [5] = "SrcAlpha", [6] = "OneMinusSrcColor",
            [7] = "DstAlpha", [8] = "OneMinusDstAlpha", [9] = "SrcAlphaSaturate",
            [10] = "OneMinusSrcAlpha",
        };

        private static readonly Dictionary<float, string> CullNames = new()
        {
            [0] = "Off", [1] = "Front", [2] = "Back",
        };

        /// <summary>ShaderLab's name for a numeric state value, or the number itself.</summary>
        private static string StateName(string kind, float value)
        {
            var table = kind switch
            {
                "ZTest" => ZTestNames,
                "Cull" => CullNames,
                _ => BlendNames,
            };
            return table.TryGetValue(value, out var n) ? n : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Run the constant-buffer analysis for one program.
        ///
        /// Everything it needs comes from the asset and none of it from the
        /// disassembly, which carries no RDEF: the pass names its buffers
        /// (<c>m_NameIndices</c>), the program's parameter blob gives each buffer's
        /// byte size and members, the shader's Properties block says which of those
        /// names are material properties, and the program's own reflection
        /// (<c>m_Parameters.m_ConstantBuffers</c>) is the authoritative $Globals
        /// member list when Unity kept it.
        /// </summary>
        private static Resolve.CbResolver NameConstantBuffers(
            Ast.Module m, string shaderName, Pass pass, SubProgram sub,
            List<Property> props, Dictionary<int, List<BlobTool.CbInfo>> paramCbs,
            List<BlobTool.CbInfo> allParamCbs, GlobalsNames globals,
            Dictionary<string, int> matrixRows = null)
        {
            var nameTable = pass?.NameIndices != null
                ? new HashSet<string>(pass.NameIndices.Keys, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            List<BlobTool.CbInfo> own = null;
            if (sub != null && sub.ParameterBlobIndex >= 0)
                paramCbs.TryGetValue(sub.ParameterBlobIndex, out own);
            if (own == null && paramCbs.TryGetValue(sub?.BlobIndex ?? -1, out var byBlob))
                own = byBlob;

            var r = Resolve.CbResolver.Resolve(
                m, nameTable, props, own, -1, globals, BestGlobals(sub), null,
                allParamCbs, true, null, sub?.TextureParams, matrixRows);

            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[cb] {shaderName}/{sub?.BlobIndex}: "
                    + $"buffers={r.CbName.Count} table={nameTable.Count} rows={r.RowExpr.Count} "
                    + string.Join("; ", r.CbName.Select(kv => $"b{kv.Key}={kv.Value}")));
            return r;
        }

        /// <summary>
        /// How many rows each <c>$Globals</c> matrix really occupies.
        ///
        /// A single program only sees the columns it reads, so one variant that
        /// touches columns 1 and 2 cannot tell a <c>float4x4</c> from a pair of
        /// <c>float4</c>s.  The asset's own <c>m_ConstantBuffers</c> reflection
        /// records the row count, and every subprogram carries a copy of it, so the
        /// widest answer wins.
        /// </summary>
        private static Dictionary<string, int> MatrixSpans(Shader sh)
        {
            var spans = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
                foreach (var p in ss.Passes)
                    foreach (var sp in p.SubPrograms)
                    {
                        if (sp.ConstantBuffers == null) continue;
                        foreach (var cb in sp.ConstantBuffers)
                        {
                            if (!cb.IsGlobals) continue;
                            foreach (var mem in cb.Members)
                            {
                                if (!mem.IsMatrix || mem.RowCount < 2) continue;
                                if (!spans.TryGetValue(mem.Name, out int have) || mem.RowCount > have)
                                    spans[mem.Name] = mem.RowCount;
                            }
                        }
                    }
            return spans;
        }

        /// <summary>
        /// The most complete $Globals reflection available for this program.
        ///
        /// Each stage's block lists only the members that stage references, so the
        /// longest list is the closest thing to a whole layout.
        /// </summary>
        private static AssetConstantBuffer BestGlobals(SubProgram sub)
        {
            AssetConstantBuffer best = null;
            if (sub?.ConstantBuffers == null) return null;
            foreach (var cb in sub.ConstantBuffers)
            {
                if (!cb.IsGlobals || cb.Members.Count == 0) continue;
                if (best == null || cb.Members.Count > best.Members.Count) best = cb;
            }
            return best;
        }

        private static IReadOnlyCollection<int> UsedBlobIndices(Shader sh)
        {
            var set = new HashSet<int>();
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
                foreach (var p in ss.Passes)
                    foreach (var sp in p.SubPrograms)
                        set.Add(sp.BlobIndex);
            return set;
        }

        private static string Safe(string name, HashSet<string> used)
        {
            string safe = SafeKey(name);
            if (used.Add(safe)) return safe;
            safe = safe + "_" + used.Count;
            used.Add(safe);
            return safe;
        }
    }
}
