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
    /// objects, BlobTool to decode Unity's LZ4 blob and carve the DXBC
    /// containers out of it, dxcompiler to disassemble them.  What differs is
    /// what happens after the disassembly exists — this project's grammar-driven
    /// translator, not the hand-written one.
    ///
    /// Per shader: every pass's sub-program becomes one HLSL file, and the
    /// fragment program (when there is one) is also wrapped in ShaderLab.
    /// </summary>
    internal static class GameExport
    {
        public sealed class Stats
        {
            public int Files, Shaders, Programs, Disassembled, Translated, Skipped, Failed;
            public readonly List<string> Notes = new();
        }

        public static Stats Run(string dataDir, string outDir, string only, bool shaderLab)
        {
            var s = new Stats();
            Directory.CreateDirectory(outDir);

            Func<string, int, TypeNode> tpk = (version, classId) => TpkSchema.BuildClass(classId, version);
            var used = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in Candidates(dataDir))
            {
                SerializedFile sf;
                try { sf = SerializedFile.Load(file, null, tpk); }
                catch (Exception e) { s.Failed++; s.Notes.Add($"{Path.GetFileName(file)}: {e.Message}"); continue; }
                s.Files++;

                foreach (var sh in sf.Shaders)
                {
                    // The shader's name lives in three places depending on how
                    // Unity stored it; take the first that is not empty, so the
                    // output tree is named after the shader rather than a path id.
                    string name = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                                : !string.IsNullOrEmpty(sh.ParsedForm?.Name) ? sh.ParsedForm.Name
                                : $"pathId_{sh.PathId}";
                    if (only != null && !name.Contains(only, StringComparison.OrdinalIgnoreCase)) continue;
                    s.Shaders++;
                    try { ExportShader(sh, name, outDir, used, shaderLab, s); }
                    catch (Exception e) { s.Failed++; s.Notes.Add($"{name}: {e.Message}"); }
                }
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

        private static void ExportShader(Shader sh, string name, string outDir,
                                         HashSet<string> used, bool shaderLab, Stats s)
        {
            string dirName = Safe(name, used);
            string dir = Path.Combine(outDir, dirName);

            // Pick the platform Unity built for D3D11 when it is there; the
            // decoder is indexed by platform slot, not by name.
            int plat = PickPlatform(sh);
            if (plat < 0) { s.Skipped++; return; }

            byte[] decoded;
            try { decoded = BlobTool.DecodeBlob(sh, plat); }
            catch (Exception e) { s.Skipped++; s.Notes.Add($"{name}: decode failed: {e.Message}"); return; }
            if (decoded == null || decoded.Length == 0) { s.Skipped++; return; }

            var slots = BlobTool.DecodeSlots(decoded, UsedBlobIndices(sh));

            // The disassembly carries no reflection (Unity strips RDEF), so the
            // only source for `cb1[7]`'s name is the asset's own parameter blob.
            var paramCbs = BlobTool.ParseParamBlobs(decoded);
            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[cbblob] {name}: blobs={paramCbs.Count} "
                    + string.Join(",", paramCbs.Keys.Take(6))
                    + $" firstMembers={(paramCbs.Count > 0 ? paramCbs.Values.First()[0].Members.Count : -1)}");
            // Programs by pass, so the ShaderLab wrapper can put both stages of a
            // pass in one program block.  Keyed by the pass object itself.
            var perPass = new Dictionary<Pass, (string vs, string ps)>();

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

            var props = sh.ParsedForm?.Props ?? new List<Property>();
            var allParamCbs = paramCbs.Values.SelectMany(x => x).ToList();
            var globals = GlobalsNames.For(name);

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
                module.Cb = NameConstantBuffers(module, name, pass, sub, props, paramCbs, allParamCbs, globals);

                var r = HlslEmitter.Emit(module);
                s.Translated++;

                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.hlsl");
                File.WriteAllText(file, r.Hlsl);
                if (pass != null)
                {
                    perPass.TryGetValue(pass, out var pair);
                    if (module.Stage is "ps") pair.ps = r.Hlsl;
                    else if (module.Stage is "vs") pair.vs = r.Hlsl;
                    perPass[pass] = pair;
                }
            }

            if (shaderLab)
            {
                var text = Emit.ShaderLab.Wrap(name, sh.ParsedForm?.FallbackName,
                                               PassTree(sh, perPass), props);
                var errs = Emit.ShaderLab.Check(text);
                if (errs.Count == 0) File.WriteAllText(Path.Combine(outDir, dirName + ".shader"), text);
                else s.Notes.Add($"{name}: shaderlab not well-formed: {errs[0]}");
            }
        }

        /// <summary>
        /// The shader's own structure, translated into what the wrapper needs:
        /// SubShaders with their tags, Passes with their names, tags and the render
        /// state that differs from ShaderLab's default, and both stages' programs.
        /// </summary>
        private static List<Emit.ShaderLab.SubShaderText> PassTree(
            Shader sh, Dictionary<Pass, (string vs, string ps)> programs)
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

                    if (programs.TryGetValue(p, out var pair))
                    {
                        if (pair.vs != null) pt.Vertex = new Emit.ShaderLab.Stage { Hlsl = pair.vs, Entry = "vert" };
                        if (pair.ps != null) pt.Fragment = new Emit.ShaderLab.Stage { Hlsl = pair.ps, Entry = "frag" };
                    }
                    text.Passes.Add(pt);
                }
                list.Add(text);
            }
            return list;
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
            List<BlobTool.CbInfo> allParamCbs, GlobalsNames globals)
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
                allParamCbs, true, null, sub?.TextureParams);

            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[cb] {shaderName}/{sub?.BlobIndex}: "
                    + $"buffers={r.CbName.Count} table={nameTable.Count} rows={r.RowExpr.Count} "
                    + string.Join("; ", r.CbName.Select(kv => $"b{kv.Key}={kv.Value}")));
            return r;
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

        private static int PickPlatform(Shader sh)
        {
            if (sh.Platforms == null || sh.Platforms.Length == 0) return -1;
            // Unity's platform ids: 4 = d3d11, 5 = d3d9; prefer whichever exists.
            for (int i = 0; i < sh.Platforms.Length; i++)
                if (sh.Platforms[i] == 4) return i;
            return 0;
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
            string safe = name.Replace('/', '_').Replace('\\', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            if (used.Add(safe)) return safe;
            safe = safe + "_" + used.Count;
            used.Add(safe);
            return safe;
        }
    }
}
