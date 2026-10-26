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
            /// <summary>Shaders actually written to disk (a shader whose ShaderLab
            /// fails the well-formedness check is counted as processed but not
            /// emitted, so this can be lower than <see cref="Shaders"/>).</summary>
            public int Emitted;
            /// <summary>Unity's own shaders left out of the export.</summary>
            public int BuiltinSkipped;
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

            /// <summary>
            /// Highest shader model any program of this pass was compiled for, as
            /// <c>40</c> or <c>50</c>.  The translated HLSL is register level -- it
            /// declares <c>Texture2D</c> + <c>SamplerState</c>, samples with
            /// <c>.Sample()</c> and uses <c>asuint</c> -- so the pass has to ask for
            /// at least the model the original was built with; Unity's default
            /// target is 2.5, which rejects all of it.
            /// </summary>
            public int Target;

            public void Add(Dictionary<string, (List<string> kw, string hlsl)> into,
                            List<string> keys, string key, List<string> kw, string hlsl)
            {
                // `keys` is the pass's order, shared by both stages, so it is the
                // list the duplicate check has to be against: testing `into` (which
                // is per stage) appended every key a second time when the fragment
                // stage repeated a keyword combination the vertex stage already had,
                // and the guard chain then emitted the same branch twice.
                if (!keys.Contains(key)) keys.Add(key);
                into[key] = (kw, hlsl);
            }
        }

        /// <summary>`vs_5_0` -> 50, `ps_4_0` -> 40, unreadable -> 0.</summary>
        private static int ShaderModel(string profile)
        {
            int at = profile?.IndexOf('_') ?? -1;
            if (at < 0 || at + 1 >= profile.Length) return 0;
            string[] parts = profile[(at + 1)..].Split('_');
            if (parts.Length < 2 || !int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor)) return 0;
            return major * 10 + minor;
        }

        public static Stats Run(string dataDir, string outDir, string only, bool shaderLab,
                                bool origAsm = false, bool dedup = false, bool builtin = false,
                                bool programs = false, IReadOnlyList<string> files = null,
                                bool scanAll = false)
        {
            var s = new Stats();
            Directory.CreateDirectory(outDir);

            Func<string, int, TypeNode> tpk = (version, classId) => TpkSchema.BuildClass(classId, version);
            var used = new HashSet<string>(StringComparer.Ordinal);

            // Resources.Load paths only exist in globalgamemanagers, whose
            // ResourceManager m_Container holds {build path -> .assets file +
            // pathID}.  Without it a Resources shader can only be named after
            // itself; with it the original "Assets/Resources/..." layout comes
            // back.  Loading is best-effort: a bundle-only input has no such file.
            var resRoot = files != null && files.Count > 0
                ? Path.GetDirectoryName(Path.GetFullPath(files[0]))
                : dataDir;
            Dictionary<(string file, long pathId), string> resMap =
                TryLoadResourceMap(resRoot, tpk, s);

            // Collect first, export second: `--dedup` has to see every copy of a
            // shader before picking the one worth emitting.
            var found = new List<(Shader sh, string name)>();
            foreach (var (inName, inBytes, inPath) in Inputs(dataDir, files))
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
                    // Unity's own shaders come with the editor and ship in every
                    // build; ripping them back out produces a file that cannot be
                    // re-imported (the engine's names are already taken).  The check
                    // is against the engine's own resource listing --
                    // data/unity_builtin_shaders.txt, dumped from
                    // Resources/unity_builtin_extra -- not against name prefixes.
                    if (!builtin && BuiltinIndex.Contains(name)) { s.BuiltinSkipped++; continue; }
                    // Which .assets file this object came from: the Resources map is
                    // keyed by it, and a bundle node's name is the only thing that
                    // identifies the file once it has been extracted.
                    sh.SourceFile ??= inName;
                    found.Add((sh, name));
                }
            }

            IEnumerable<(Shader sh, string name)> export = found;
            if (dedup)
            {
                // The same shader can ship in several data files; keep one per
                // output path.  A copy with a fixed container path wins (it lands
                // on the path the asset itself names), then the lowest path id so
                // the winner never depends on enumeration order.
                var winners = found.GroupBy(t => OutKey(t.sh, t.name, resMap), StringComparer.Ordinal)
                                   .Select(g => g.OrderByDescending(t => HasFixedPath(t.sh, resMap))
                                                 .ThenBy(t => t.sh.PathId).First())
                                   .OrderBy(t => t.sh.PathId)
                                   .ToList();
                s.Notes.Add($"dedup: {found.Count} -> {winners.Count} shader(s), "
                            + $"{found.Count - winners.Count} duplicate(s) dropped");
                export = winners;
            }

            // Whole output paths are claimed in a fixed order (the order a
            // sequential run would use) so two shaders that map to the same file
            // never race: the first keeps the plain name, later ones get a suffix.
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (sh, name) in export)
            {
                s.Shaders++;
                try { ExportShader(sh, name, outDir, used, claimed, resMap, shaderLab, programs, origAsm, s); }
                catch (Exception e) { s.Failed++; s.Notes.Add($"{name}: {e.Message}"); }
            }
            return s;
        }

        /// <summary>
        /// Best-effort read of the Resources container map.  A player build has
        /// globalgamemanagers next to its .assets files; a bundle-only input has
        /// no such file, and the map stays empty (names fall back to the shader's
        /// own name).
        /// </summary>
        private static Dictionary<(string file, long pathId), string> TryLoadResourceMap(
            string dir, Func<string, int, TypeNode> tpk, Stats s)
        {
            var empty = new Dictionary<(string, long), string>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return empty;
            var gg = Path.Combine(dir, "globalgamemanagers");
            if (!File.Exists(gg)) { s.Notes.Add("resources map: globalgamemanagers not found"); return empty; }
            try
            {
                var map = ResourceMap.Load(File.ReadAllBytes(gg), null, tpk);
                s.Notes.Add($"resources map: {map.Count} entries");
                return map;
            }
            catch (Exception e)
            {
                s.Notes.Add("resources map: " + e.Message);
                return empty;
            }
        }

        /// <summary>Does this shader's destination come from the asset itself?</summary>
        private static bool HasFixedPath(Shader sh,
            Dictionary<(string file, long pathId), string> resMap) =>
            !string.IsNullOrEmpty(sh.ContainerPath) ||
            (resMap != null && sh.SourceFile != null &&
             resMap.ContainsKey((Path.GetFileName(sh.SourceFile), sh.PathId)));

        /// <summary>
        /// The file name a shader gets, matching the main tool exactly.
        ///
        /// The shader's own name is the only input: the hierarchy separators in
        /// "UI/Pause Planets" become underscores ("UI_Pause Planets"), and no
        /// other character is touched.  Shader names are Unity asset names, so
        /// they are not paths -- turning "/" into a directory would invent a
        /// tree the project never had, and would split one asset across folders.
        /// </summary>
        private static string SafeOf(Shader sh, string name)
        {
            string safe = (name ?? "").Replace('/', '_').Replace('\\', '_');
            foreach (char c in Path.GetInvalidFileNameChars())
                if (c != '/' && c != '\\') safe = safe.Replace(c, '_');
            return safe.Length == 0 ? "Shader_" + sh.PathId : safe;
        }

        /// <summary>
        /// The relative path a shader is written to.
        ///
        /// Everything lands under <c>Assets/</c>, which is what an AssetRipper
        /// extraction of the same build looks like -- the tree can be dropped
        /// straight over an ExportedProject.  Two sources name the rest, in
        /// order:
        ///   * the Resources map (globalgamemanagers m_Container) or the bundle's
        ///     own container path, which restore the project path the asset had
        ///     before it was packed ("Assets/Resources/camerafilterpack_*.shader");
        ///   * otherwise <c>Assets/Shader/&lt;flat name&gt;.shader</c>, the same
        ///     fallback the main tool uses.
        /// </summary>
        private static string OutPath(Shader sh, string name,
            Dictionary<(string file, long pathId), string> resMap)
        {
            string flat = SafeOf(sh, name);

            // A Resources container path is the asset's real project location.
            if (resMap != null && sh.SourceFile != null &&
                resMap.TryGetValue((Path.GetFileName(sh.SourceFile), sh.PathId), out var mapped) &&
                !string.IsNullOrEmpty(mapped))
                return CleanRel("Assets/Resources/" + mapped.Replace('\\', '/'), flat);

            // A bundle node path is project-relative and already starts at
            // "assets/" in the cases Unity writes; anything else is a bare
            // archive entry name and belongs on the flat path instead.
            if (!string.IsNullOrEmpty(sh.ContainerPath))
            {
                string rel = sh.ContainerPath.Replace('\\', '/').TrimStart('/');
                if (rel.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                    return CleanRel(rel, flat);
            }
            return CleanRel("Assets/Shader/" + flat, flat);
        }

        /// <summary>The path used for dedup grouping: case- and slash-insensitive.</summary>
        private static string OutKey(Shader sh, string name,
            Dictionary<(string file, long pathId), string> resMap) =>
            OutPath(sh, name, resMap).ToLowerInvariant();

        /// <summary>
        /// A container path as a safe relative path: forced to .shader, slashes
        /// normalised, illegal characters replaced, no leading separators or dots
        /// (a "../.." in an asset path must not escape the output directory).
        /// </summary>
        private static string CleanRel(string raw, string fallbackName)
        {
            string rel = (raw ?? "").Replace('\\', '/').TrimStart('/');
            foreach (string part in rel.Split('/'))
                if (part == "..") { rel = fallbackName; break; }
            if (!rel.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)) rel += ".shader";
            var parts = rel.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                foreach (char c in Path.GetInvalidFileNameChars()) parts[i] = parts[i].Replace(c, '_');
                if (parts[i].Length == 0) parts[i] = "_";
            }
            return string.Join('/', parts);
        }

        private static IEnumerable<string> Candidates(string dataDir)
        {            // resources.assets first: it is where Resources-included shaders live.
            return Directory.EnumerateFiles(dataDir, "*.assets", SearchOption.TopDirectoryOnly)
                            .OrderBy(f => Path.GetFileName(f) == "resources.assets" ? 0 : 1)
                            .ThenBy(f => f, StringComparer.Ordinal);
        }

        /// <summary>
        /// Unity's own shaders, by the path prefix Unity gives them.
        ///
        /// These ship with the editor and appear in every build, so ripping them
        /// back out returns the engine's own source -- and a re-import of that file
        /// fails, because the identifiers it declares belong to the engine.  A game
        /// that genuinely wants them (to diff against a newer Unity) can pass
        /// <c>--builtin</c>.
        /// </summary>
        private static bool IsBuiltinShader(string name)
        {
            string[] prefixes =
            {
                "Standard", "Legacy Shaders/", "Mobile/", "Particles/", "Unlit/",
                "UI/", "Sprites/", "Skybox/", "Nature/", "FX/", "Hidden/",
                "TextMeshPro/", "TMP/", "ImageEffects/", "Internal-",
                "Autodesk Interactive", "VertexLit", "Diffuse", "Specular",
            };
            foreach (string p in prefixes)
                if (name.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Input serialized files.
        ///
        /// Two modes.  With <paramref name="files"/> the caller named them
        /// explicitly (<c>--file</c>): each is a SerializedFile, a bundle, or a
        /// wildcard, and the directory is not scanned at all.  Without it the
        /// whole data directory is read: loose *.assets files plus the
        /// SerializedFile entries inside *.bundle archives -- DLC and scene
        /// bundles routinely carry the only copy of a shader, and the GLES-only
        /// builds often ship nothing but bundles.
        /// </summary>
        private static IEnumerable<(string name, byte[] bytes, string path)> Inputs(
            string dataDir, IReadOnlyList<string> files)
        {
            if (files != null && files.Count > 0)
            {
                foreach (string raw in ExpandFiles(files))
                {
                    // Sniffed by content, not by extension: an Android build is a
                    // .apk (a zip of bundles) and a player build may name its
                    // archives anything at all.
                    switch (Sniff(raw))
                    {
                        case InputKind.Bundle:
                            foreach (var (path, data) in Bundle.SerializedFiles(raw))
                                yield return ($"{Path.GetFileName(raw)}/{path}", data, raw);
                            break;

                        case InputKind.Zip:
                            foreach (var (name, data) in ZipEntries(raw))
                                yield return ($"{Path.GetFileName(raw)}/{name}", data, raw);
                            break;

                        default:
                            yield return (Path.GetFileName(raw), null, raw);
                            break;
                    }
                }
                yield break;
            }

            foreach (var f in Candidates(dataDir))
                yield return (Path.GetFileName(f), null, f);
            foreach (var b in Directory.EnumerateFiles(dataDir, "*.bundle", SearchOption.TopDirectoryOnly)
                                       .OrderBy(f => f, StringComparer.Ordinal))
                foreach (var (path, data) in Bundle.SerializedFiles(b))
                    yield return ($"{Path.GetFileName(b)}/{path}", data, b);
        }

        /// <summary>What an input file is, decided from its first bytes.</summary>
        private enum InputKind
        {
            /// <summary>A Unity SerializedFile (version is at offset 8).</summary>
            Serialized,
            /// <summary>A UnityFS container.</summary>
            Bundle,
            /// <summary>A zip archive — an .apk, or a zip of bundles.</summary>
            Zip,
            /// <summary>None of the above; the exporter will try it as a SerializedFile.</summary>
            Unknown,
        }

        private static InputKind Sniff(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var head = new byte[12];
                int got = fs.Read(head, 0, head.Length);
                if (got >= 7 && head[0] == 'U' && head[1] == 'n' && head[2] == 'i' &&
                    head[3] == 't' && head[4] == 'y' && head[5] == 'F' && head[6] == 'S')
                    return InputKind.Bundle;
                if (got >= 4 && head[0] == 'P' && head[1] == 'K' &&
                    (head[2] == 3 || head[2] == 5 || head[2] == 7))
                    return InputKind.Zip;
                if (got >= 12)
                {
                    // A SerializedFile starts with metadataSize/fileSize/version,
                    // all big-endian and all small.  Version lives at offset 8.
                    uint version = (uint)(head[8] << 24 | head[9] << 16 | head[10] << 8 | head[11]);
                    if (version > 5 && version < 1000) return InputKind.Serialized;
                }
                return InputKind.Unknown;
            }
            catch { return InputKind.Unknown; }
        }

        /// <summary>
        /// The shader-bearing entries of a zip: nested bundles are expanded, and
        /// plain SerializedFiles inside the archive are taken as they are.  A zip
        /// inside a zip is not followed — no Unity build nests them that deeply.
        /// </summary>
        private static IEnumerable<(string name, byte[] data)> ZipEntries(string zipPath)
        {
            System.IO.Compression.ZipArchive zip;
            try { zip = System.IO.Compression.ZipFile.OpenRead(zipPath); }
            catch { yield break; }

            using (zip)
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.Length < 12) continue;
                    // Resource payloads are huge and carry no shader objects.
                    if (entry.FullName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
                        continue;

                    byte[] data;
                    try
                    {
                        using var es = entry.Open();
                        using var ms = new MemoryStream((int)entry.Length);
                        es.CopyTo(ms);
                        data = ms.ToArray();
                    }
                    catch { continue; }

                    string name = entry.FullName.Replace('\\', '/');
                    if (data.Length >= 7 && data[0] == 'U' && data[1] == 'n' && data[2] == 'i' &&
                        data[3] == 't' && data[4] == 'y' && data[5] == 'F' && data[6] == 'S')
                    {
                        List<Bundle.Node> nodes;
                        try { nodes = Bundle.Read(data); }
                        catch { continue; }
                        foreach (var nd in nodes)
                        {
                            if (nd.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                                nd.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
                                continue;
                            yield return (name + "/" + nd.Path, nd.Data);
                        }
                        continue;
                    }
                    // Unity data files inside an apk often have no extension at
                    // all (GUID-named); the version field is the only signal.
                    if (data.Length >= 12)
                    {
                        uint v = (uint)(data[8] << 24 | data[9] << 16 | data[10] << 8 | data[11]);
                        if (v > 5 && v < 1000) yield return (name, data);
                    }
                }
            }
        }

        /// <summary>
        /// The --file arguments as existing paths.
        ///
        /// One value can be several entries: comma and semicolon separate a list,
        /// and `*`/`?` expand against the value's own directory.  A wildcard that
        /// matches nothing is kept as-is so the caller reports the missing file
        /// rather than silently exporting nothing.  Duplicates are dropped by
        /// full path, case-insensitively, keeping first-seen order.
        /// </summary>
        private static IEnumerable<string> ExpandFiles(IReadOnlyList<string> values)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawVal in values)
            {
                foreach (string seg in (rawVal ?? "").Split(',', ';'))
                {
                    string v = seg.Trim();
                    if (v.Length == 0) continue;

                    if (v.IndexOf('*') >= 0 || v.IndexOf('?') >= 0)
                    {
                        string dir = Path.GetDirectoryName(Path.GetFullPath(v));
                        string pat = Path.GetFileName(v);
                        if (Directory.Exists(dir))
                        {
                            foreach (string hit in Directory.GetFiles(dir, pat)
                                                            .OrderBy(f => f, StringComparer.Ordinal))
                                if (seen.Add(hit)) yield return hit;
                            continue;
                        }
                    }
                    string full = Path.GetFullPath(v);
                    if (seen.Add(full)) yield return full;
                }
            }
        }

        private static string SafeKey(string name)
        {
            string safe = name.Replace('/', '_').Replace('\\', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return safe;
        }

        /// <summary>
        /// One shader -> the path <see cref="OutPath"/> derives for it.
        ///
        /// The per-program HLSL (and the original disassembly beside it) is
        /// debugging material, not the deliverable: 412 shaders would otherwise
        /// leave 412 folders of fragments next to 412 files.  It is written only
        /// when <paramref name="programs"/> is set (<c>--programs</c>, implied by
        /// <c>--orig-asm</c>), which is what the offline verification scripts read.
        /// </summary>
        private static void ExportShader(Shader sh, string name, string outDir,
                                         HashSet<string> used, HashSet<string> claimed,
                                         Dictionary<(string file, long pathId), string> resMap,
                                         bool shaderLab, bool programs, bool origAsm, Stats s)
        {
            // The destination comes from the asset when the asset knows it.  A
            // bundle's container path and the Resources map both describe the
            // original project layout, so honouring them restores the directory
            // tree instead of flattening 412 shaders into one folder.
            string rel = OutPath(sh, name, resMap);
            if (!claimed.Add(rel))
            {
                // Two shaders resolved to the same file.  The path id is appended
                // rather than the name replaced: the file still says what it is,
                // and the loser is still reachable.
                string stem = rel.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                    ? rel[..^".shader".Length] : rel;
                string alt = stem + "_" + sh.PathId + ".shader";
                int n = 2;
                while (!claimed.Add(alt)) alt = stem + "_" + sh.PathId + "_" + n++ + ".shader";
                s.Notes.Add($"{name}: path taken, written as {alt}");
                rel = alt;
            }

            string full = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            string relDir = Path.GetDirectoryName(full) ?? outDir;
            Directory.CreateDirectory(relDir);

            // Per-program folders sit beside the .shader, named after it.
            string dirName = Safe(name, used);
            string dir = programs || origAsm ? Path.Combine(relDir, dirName) : null;

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
                    passOf, spOf, kwIndex, props, globals, perPass, programs, origAsm, s);
                else ExportGlsl(sh, name, info, decoded, dir, passOf, spOf, kwIndex, perPass, s, origAsm);
            }

            if (shaderLab && perPass.Count > 0)
            {
                // The shader's own Properties block is the authority on which
                // names are the game's: a member declared here stays even when its
                // name happens to look like an engine one.
                Emit.ShaderLab.SetPropertyNames(props);
                var text = Emit.ShaderLab.Wrap(name, sh.FallbackName,
                                               PassTree(sh, perPass), props);
                var errs = Emit.ShaderLab.Check(text);
                if (errs.Count == 0) { File.WriteAllText(full, text); s.Emitted++; }
                else s.Notes.Add($"{name}: shaderlab not well-formed: {errs[0]}");
            }
        }

        // ------------------------------------------------------------------
        // DXBC route (d3d11): disassemble, translate, keep every variant
        // ------------------------------------------------------------------

        private static void ExportDxbc(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, string dirName, Dictionary<int, Pass> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<int, string> kwIndex, List<Property> props, GlobalsNames globals,
            Dictionary<Pass, PassBuild> perPass, bool programs, bool origAsm, Stats s)
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

                if (programs || origAsm)
                {
                    Directory.CreateDirectory(dir);
                    // The translation is only written with --programs; --orig-asm
                    // alone keeps just the disassembly it compares against.
                    if (programs)
                        File.WriteAllText(Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.hlsl"), r.Hlsl);

                    // The original disassembly, kept beside the translation so the two
                    // can be compared instruction for instruction.  It is the same text
                    // the translator read.
                    if (origAsm)
                        File.WriteAllText(Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.orig.asm"), asm);
                }

                if (pass == null) continue;
                if (!perPass.TryGetValue(pass, out var build)) perPass[pass] = build = new PassBuild();
                build.Target = Math.Max(build.Target, ShaderModel(r.Profile));
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
            Dictionary<int, string> kwIndex, Dictionary<Pass, PassBuild> perPass, Stats s,
            bool origSrc = false)
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

                if (dir != null)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, $"{bi}_glsl.hlsl"), r.Hlsl);

                    // The original GLSL, kept beside the translation so the two can be
                    // compared: the translation is only as good as its agreement with
                    // this text, and nothing else records what Unity's HLSLcc emitted.
                    if (origSrc)
                        File.WriteAllText(Path.Combine(dir, $"{bi}_glsl.orig.glsl"), pr.Text);
                }

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
            Shader sh, Dictionary<Pass, PassBuild> programs)        {
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
            pt.Target = pb.Target;

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
