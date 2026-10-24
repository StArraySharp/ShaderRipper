using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Antlr.Data;
using ShaderRipper.Antlr.Emit;

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
            string frag = null;
            int slotNo = 0;

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
                // The blob table is keyed by the blob index the *asset* records,
                // which does not always equal the slot's index (Unity renumbers
                // the kept programs).  Exact key first, then position: the table
                // covers only the kept programs, in register order, which is also
                // the order the decoded slots come out in.
                if (paramCbs.TryGetValue(slot.BlobIndex, out var cbs))
                    ApplyCbNames(module, cbs);
                else if (paramCbs.Count > 0)
                {
                    int at = slotNo++;
                    var ordered = paramCbs.OrderBy(k => k.Key).Select(k => k.Value).ToList();
                    if (at < ordered.Count) ApplyCbNames(module, ordered[at]);
                }

                var r = HlslEmitter.Emit(module);
                s.Translated++;

                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.hlsl");
                File.WriteAllText(file, r.Hlsl);
                if (module.Stage is "ps") frag = r.Hlsl;
            }

            if (shaderLab && frag != null)
            {
                string text = Emit.ShaderLab.Wrap(name, frag, true);
                var errs = Emit.ShaderLab.Check(text);
                if (errs.Count == 0) File.WriteAllText(Path.Combine(outDir, dirName + ".shader"), text);
                else s.Notes.Add($"{name}: shaderlab not well-formed: {errs[0]}");
            }
        }

        /// <summary>
        /// Name constant-buffer rows from the reflection.
        ///
        /// The blob lists the buffers this program kept, in register order, and
        /// the disassembly declares them in the same order, so the two pair up by
        /// position.  Only whole-row float4 members are taken: a member that
        /// shares a row with another cannot be spelled as a float4 and would
        /// change the arithmetic if it were.
        /// </summary>
        private static void ApplyCbNames(Ast.Module m, List<BlobTool.CbInfo> cbs)
        {
            var decls = m.Decls.Where(d => d.Kind == "cbuffer").OrderBy(d => d.Register).ToList();
            if (Environment.GetEnvironmentVariable("SHADERRIP_CBDBG") == "1")
                Console.Error.WriteLine($"[cb] decls={decls.Count} reflect={cbs.Count} "
                    + string.Join("; ", cbs.Select(c => $"{c.Name}({c.Members.Count}m,{c.Size}B)")));
            if (decls.Count != cbs.Count) return;

            for (int i = 0; i < decls.Count; i++)
                foreach (var mem in cbs[i].Members)
                {
                    if (string.IsNullOrEmpty(mem.name)) continue;
                    if (mem.offset % 16 != 0 || mem.dim != 4) continue;
                    m.CbNames[(decls[i].Register, mem.offset / 16)] = mem.name;
                }
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
