using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Data;
using ShaderRipper.Emit;
using ShaderRipper.Resolve;

namespace ShaderRipper.Game
{
    /// <summary>
    /// Game data -> HLSL.
    ///
    /// The reading layer finds the Shader objects, decodes Unity's LZ4 blob and
    /// carves the program containers out of it; the grammar-driven translators
    /// (<c>DxbcAsm</c>, <c>Glsl</c>) turn each payload into HLSL.
    ///
    /// Per shader, every pass's keyword variants are recovered with
    /// <c>VariantPlan</c> and emitted as guarded chains inside one HLSLPROGRAM, the
    /// way the original source had them before Unity flattened them.
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

        /// <summary>
        /// One shader queued for export, with the names derived from it already
        /// claimed.
        ///
        /// Resolving both names up front is what lets the export loop run in
        /// parallel: the claim ("who keeps the plain path") is decided by queue
        /// order, and the per-program folder name by first-come first-served, so
        /// both have to happen before any worker starts.
        /// </summary>
        private sealed class Seat
        {
            public Shader Sh;
            public string Name;
            /// <summary>Output path of the `.shader`, relative to the output root.</summary>
            public string Rel;
            /// <summary>Folder holding this shader's per-program files.</summary>
            public string DirName;
        }

        /// <summary>
        /// Export one claimed seat into its own Stats bag.
        ///
        /// The bag is per-seat rather than shared because `ExportShader` counts into
        /// whatever it is handed: giving each worker its own takes every counter off
        /// the shared path, and the bags are merged in queue order afterwards.
        /// </summary>
        private static Stats ExportSeat(Seat seat, string outDir,
                                        Dictionary<(string file, long pathId), string> resMap,
                                        bool shaderLab, bool programs, bool origAsm, int? platform)
        {
            var s = new Stats();
            try
            {
                ExportShader(seat.Sh, seat.Name, seat.Rel, seat.DirName, outDir, resMap,
                             shaderLab, programs, origAsm, platform, s);
            }
            catch (Exception e)
            {
                s.Failed++;
                s.Notes.Add($"{seat.Name}: {e.Message}");
                Log.Error(seat.Name, e.Message);
                if (Environment.GetEnvironmentVariable("SHADERRIP_STACK") == "1")
                    Console.Error.WriteLine(e.ToString());
            }
            return s;
        }

        /// <summary>Fold one seat's counters into the run's totals.</summary>
        private static void Merge(Stats into, Stats from)
        {
            into.Programs += from.Programs;
            into.Disassembled += from.Disassembled;
            into.Translated += from.Translated;
            into.Skipped += from.Skipped;
            into.Emitted += from.Emitted;
            into.Failed += from.Failed;
            into.Notes.AddRange(from.Notes);
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
            /// <summary>
            /// Diagnostics the resolver produced for this pass, written into the
            /// generated file as comments.  They are all "this is an inference, not a
            /// reading" notes -- a texture register the asset never named -- and a
            /// reader of the .shader needs them more than a reader of the console does.
            /// </summary>
            public readonly List<string> Notes = new();
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
                                bool origAsm = false, bool dedup = true, bool builtin = false,
                                bool programs = false, IReadOnlyList<string> files = null,
                                bool overwrite = false, int jobs = 1,
                                System.Threading.CancellationToken cancel = default,
                                int? platform = null)
        {
            var s = new Stats();
            ExportDir.Prepare(outDir, overwrite);

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
                        ? SerializedFile.Load(inBytes, tpk)
                        : SerializedFile.Load(inPath, tpk);
                }
                catch (Exception e)
                {
                    s.Failed++;
                    // A diagnose-able failure says what the reader actually saw:
                    // a header that reads as version 0 or a bogus type count means
                    // the bytes handed in were not the file we thought they were.
                    string probe = inBytes != null
                        ? Probe(inBytes)
                        : (File.Exists(inPath) ? Probe(File.ReadAllBytes(inPath)) : "(missing)");
                    s.Notes.Add($"{inName}: {e.Message}{probe}");
                    if (Environment.GetEnvironmentVariable("SHADERRIP_STACK") == "1")
                        Console.Error.WriteLine($"[{inName}]{probe}\n{e}");
                    continue;
                }
                s.Files++;

                // A heading per input file: a 400-shader run gives no sense of
                // progress without one, and it is the only place a file that
                // contributed nothing can be spotted.  Files with no shaders are
                // left out -- a bundle has dozens of them and they are pure noise.
                if (sf.Shaders.Count > 0) Log.File(ShortSource(inName), sf.Shaders.Count);

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
                    if (!builtin && BuiltinIndex.Contains(name))
                    {
                        s.BuiltinSkipped++;
                        Log.Skip(name, Localization.Strings.Get("LogSkipBuiltin"));
                        continue;
                    }
                    // Which .assets file this object came from: the Resources map is
                    // keyed by it, and a bundle node's name is the only thing that
                    // identifies the file once it has been extracted.
                    sh.SourceFile ??= inName;
                    // The bundle's own AssetBundle.m_Container names the project path
                    // every object came from.  This is the ONLY record of where a
                    // bundle-packed shader used to live: the Shader object carries
                    // nothing but its own name, so without this a "Assets/Resources/
                    // camerafilterpack_rain.shader" collapses to
                    // "Assets/Shader/CameraFilterPack_Atmosphere_Rain.shader".
                    if (sh.ContainerPath == null &&
                        sf.ContainerPaths.TryGetValue(sh.PathId, out var containerPath))
                        sh.ContainerPath = containerPath;
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
                var groups = found.GroupBy(t => OutKey(t.sh, t.name, resMap), StringComparer.Ordinal)
                                  .ToList();
                var winners = groups
                                   .Select(g => g.OrderByDescending(t => HasFixedPath(t.sh, resMap))
                                                 .ThenBy(t => t.sh.PathId).First())
                                   .OrderBy(t => t.sh.PathId)
                                   .ToList();
                s.Notes.Add(Localization.Strings.F("DedupSummary", found.Count,
                                                  winners.Count,
                                                  found.Count - winners.Count));

                // Say which copies were dropped and what kept them out, so a
                // surprising winner can be traced without re-running.
                foreach (var g in groups)
                {
                    var win = g.OrderByDescending(t => HasFixedPath(t.sh, resMap))
                               .ThenBy(t => t.sh.PathId).First();
                    if (g.Count() < 2) continue;
                    string winnerPath = OutPath(win.sh, win.name, resMap);
                    foreach (var (sh, name) in g)
                    {
                        if (ReferenceEquals(sh, win.sh)) continue;
                        Log.Dup(name, winnerPath, win.sh.SourceFile, sh.SourceFile);
                    }
                }

                export = winners;
            }

            // Whole output paths are claimed in a fixed order so two shaders that map
            // to the same file never race.  Fixed paths claim FIRST: a shader the
            // build put at an explicit path must keep it, and letting a name-only
            // shader win the plain path would push the explicit one onto a generated
            // one.  Among equals the lowest path id decides, so the winner never
            // depends on enumeration order.
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var queue = export.ToList();
            Log.Expect(queue.Count);

            // Claiming happens here rather than in the worker: which of two shaders
            // keeps the plain path is decided by queue order, so resolving it inside
            // the parallel loop would make the winner depend on scheduling.  It also
            // gives every shader its own copy of the two derived names, which is
            // what lets the workers run without touching a shared HashSet.
            var seats = new Seat[queue.Count];
            var order = Enumerable.Range(0, queue.Count)
                                  .OrderByDescending(i => HasFixedPath(queue[i].sh, resMap))
                                  .ThenBy(i => queue[i].sh.PathId)
                                  .ToList();
            foreach (int i in order)
            {
                var (sh, name) = queue[i];
                string rel = OutPath(sh, name, resMap);
                if (!claimed.Add(rel))
                {
                    // The shader's own name is the fallback, and Unity keeps it unique
                    // per project: `/` becomes `_`, giving
                    // `Assets/Shader/CameraFilterPackLegacy_Color_GrayScale.shader`.
                    // Only when THAT is taken too does a numeric suffix appear.
                    string flat = SafeOf(sh, name);
                    rel = CleanRel("Assets/Shader/" + flat, flat);
                    int n = 1;
                    while (!claimed.Add(rel))
                    {
                        string stem = rel.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                            ? rel[..^".shader".Length] : rel;
                        rel = stem + "_" + n++ + ".shader";
                    }
                    s.Notes.Add(Localization.Strings.F("PathTaken", name, rel));
                    Log.Warn(name, Localization.Strings.F("PathTaken", name, rel));
                }
                seats[i] = new Seat { Sh = sh, Name = name, Rel = rel, DirName = Safe(name, used) };
            }

            if (jobs > 1)
            {
                // Each worker owns a private Stats (`ExportShader` counts into the bag
                // it is handed) and writes only the files pre-claimed for it, so the
                // shaders share nothing but read-only data.  The bags are merged in
                // queue order, which keeps the notes list in the same order a
                // sequential run produces.
                var per = new Stats[seats.Length];
                System.Threading.Tasks.Parallel.For(0, seats.Length,
                    new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = jobs },
                    i =>
                    {
                        // A cancelled run stops taking new work; what is in flight
                        // finishes, so every file on disk is a whole shader.
                        if (cancel.IsCancellationRequested) return;
                        per[i] = ExportSeat(seats[i], outDir, resMap, shaderLab, programs, origAsm, platform);
                    });
                foreach (var ps in per) if (ps != null) Merge(s, ps);
                s.Shaders = seats.Length;
            }
            else
            {
                foreach (var seat in seats)
                {
                    if (cancel.IsCancellationRequested) break;
                    s.Shaders++;
                    Merge(s, ExportSeat(seat, outDir, resMap, shaderLab, programs, origAsm, platform));
                }
            }

            if (cancel.IsCancellationRequested)
                // No marker: the directory is this tool's, but the export behind it is
                // not the one that was asked for, and the next run should not treat it
                // as complete.
                s.Notes.Add(Localization.Strings.Get("ExportCancelled"));
            else
                ExportDir.Finish(outDir, s.Emitted, programs ? s.Programs : 0);
            return s;
        }

        /// <summary>
        /// Best-effort read of the Resources container map.
        ///
        /// The file lives inside Unity's data directory ("&lt;Game&gt;_Data"), and a
        /// person may point at either that directory or the game root above it, so
        /// both are probed.  A bundle-only input has no such file and the map stays
        /// empty (names fall back to the shader's own name).
        /// </summary>
        private static Dictionary<(string file, long pathId), string> TryLoadResourceMap(
            string dir, Func<string, int, TypeNode> tpk, Stats s)
        {
            var map = new Dictionary<(string, long), string>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                s.Notes.Add(Localization.Strings.Get(ResourceMapMissing));
                return map;
            }

            var found = ResourceMapFiles(dir);
            if (found.Count == 0)
            {
                s.Notes.Add(Localization.Strings.Get(ResourceMapMissing));
                return map;
            }

            foreach (string gg in found)
            {
                Dictionary<(string file, long pathId), string> one;
                try
                {
                    one = ResourceMap.Load(File.ReadAllBytes(gg), tpk);
                }
                catch (Exception e)
                {
                    s.Notes.Add(Localization.Strings.F("ResourcesMapError", e.Message));
                    continue;
                }
                // First file wins: a key the base game's map answered is not
                // overwritten by a mod's copy of the same (file, pathID).
                foreach (var kv in one) map.TryAdd(kv.Key, kv.Value);
            }

            s.Notes.Add(Localization.Strings.F("ResourcesMapLoaded", map.Count));
            // Say when the table was found somewhere other than the directory the
            // user named, so "which data directory did it read?" never has to be
            // guessed from the output names.
            string direct = Path.Combine(dir, "globalgamemanagers");
            foreach (string gg in found)
                if (!string.Equals(gg, direct, StringComparison.OrdinalIgnoreCase))
                    s.Notes.Add(Localization.Strings.F("ResourcesMapFrom", gg));
            return map;
        }

        /// <summary>Note key for "no globalgamemanagers anywhere near the input".</summary>
        private const string ResourceMapMissing = "ResourcesMapNotFound";

        /// <summary>
        /// Every globalgamemanagers reachable from <paramref name="dir"/>.
        ///
        /// Two shapes are normal and both have to work: the data directory itself
        /// (the usual argument, and what <c>--file</c> derives) and the game root
        /// that contains it.  The walk is therefore one level deep from either --
        /// and it follows only "&lt;X&gt;_Data" folders below that, so a library
        /// root holding game folders, or a game root holding a mod loader's own
        /// data directory, is covered without scanning an install of tens of
        /// thousands of files for one name.
        /// </summary>
        private static List<string> ResourceMapFiles(string dir)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Walk(string d, int depth)
            {
                string gg = Path.Combine(d, "globalgamemanagers");
                if (File.Exists(gg) && seen.Add(gg)) found.Add(gg);
                if (depth >= 2) return;

                string[] subs;
                try { subs = Directory.GetDirectories(d); }
                catch { return; }
                foreach (string sub in subs.OrderBy(x => x, StringComparer.Ordinal))
                {
                    string name = Path.GetFileName(sub);
                    bool dataDir = name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase);
                    // At the top, every child is a candidate (it may be the game
                    // root named on the command line, or one game folder below it).
                    // Deeper, only Unity's own data directory is worth opening.
                    if (dataDir || depth == 0) Walk(sub, depth + 1);
                }
            }

            Walk(dir, 0);
            return found;
        }

        /// <summary>
        /// A one-line description of what a failed input actually looks like, for
        /// the diagnostic note.  A reader error almost always means the bytes were
        /// not the structure the reader assumed, and the header is what says which.
        /// </summary>
        private static string Probe(byte[] b)
        {
            if (b == null || b.Length < 20) return " [len=" + (b?.Length ?? 0) + "]";
            uint version = (uint)((b[8] << 24) | (b[9] << 16) | (b[10] << 8) | b[11]);
            string magic = b.Length >= 7 && b[0] == 'U' && b[1] == 'n' && b[2] == 'i' &&
                           b[3] == 't' && b[4] == 'y' && b[5] == 'F' && b[6] == 'S'
                           ? "UnityFS" : "plain";
            return $" [len={b.Length} kind={magic} version={version}]";
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
        /// Everything lands under <c>Assets/</c>, so the tree can be dropped straight
        /// over an ExportedProject.  The Resources map (or the bundle's own container
        /// path) restores the project path the asset had before it was packed;
        /// otherwise the shader name is used under <c>Assets/Shader/</c>.
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

        /// <summary>
        /// The key dedup groups by: the shader's own name, case-insensitive.
        ///
        /// Grouping by output path is not enough: the same shader ships in
        /// <c>resources.assets</c> (where the Resources map names its project path)
        /// and inside a bundle (where only the shader's own name is known), so the two
        /// copies resolve to different files and a path-keyed dedup keeps both.  The
        /// shader name is the identity Unity itself uses.
        /// </summary>
        private static string OutKey(Shader sh, string name,
            Dictionary<(string file, long pathId), string> resMap) =>
            (name ?? "").ToLowerInvariant();

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

            // Directory mode follows AssetRipper's model rather than looking for
            // particular file names: walk the tree and decide what each file is
            // from its header.  A Unity build names its data directory
            // "<Game>_Data" and puts the DLC archives in a sibling "Bundles"
            // folder, so searching only the given directory found the base game
            // and silently dropped every bundle.
            //
            // The walk starts at the given directory *and* at its parent when the
            // given path is a "_Data" directory, which is exactly what a person
            // points at (they have the data directory open, not the game root).
            foreach (var root in ScanRoots(dataDir))
                foreach (var (name, bytes, path) in ScanTree(root))
                    yield return (name, bytes, path);
        }

        /// <summary>
        /// Where the directory walk begins.
        ///
        /// The given directory, plus its parent when the directory is Unity's
        /// "&lt;Game&gt;_Data" folder -- the bundles live next to that folder, not
        /// inside it.  Ordered so the data directory is read first (its shaders win
        /// a --dedup tie) but the parent is still covered.
        /// </summary>
        private static IEnumerable<string> ScanRoots(string dataDir)
        {
            if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            {
                if (!string.IsNullOrEmpty(dataDir)) yield return dataDir;
                yield break;
            }

            string full = Path.GetFullPath(dataDir);
            yield return full;

            // ".../Game_Data" -> "...", where the DLC bundles sit.
            string parent = Path.GetDirectoryName(full);
            if (parent != null && full.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                yield return parent;
        }

        /// <summary>
        /// Every shader-bearing file under <paramref name="root"/>, decided by content.
        ///
        /// The extension is only a hint: a build also writes bare GUID-named serialized
        /// files and packs whole UnityFS containers into bundle nodes.  Resource
        /// payloads and player binaries are skipped by name before the header is read.
        /// </summary>
        private static IEnumerable<(string name, byte[] bytes, string path)> ScanTree(string root)
        {
            if (!Directory.Exists(root)) yield break;

            var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (dir, files) in Walk(root, seenDirs, 0))
            {
                foreach (string f in files)
                {
                    string baseName = Path.GetFileName(f);
                    if (IsPayloadName(baseName)) continue;

                    switch (Sniff(f))
                    {
                        case InputKind.Bundle:
                            foreach (var (path, data) in Bundle.SerializedFiles(f))
                                yield return ($"{baseName}/{path}", data, f);
                            break;

                        case InputKind.Serialized:
                            yield return (baseName, null, f);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Directories under <paramref name="root"/>, depth-first, without following
        /// the same directory twice.
        ///
        /// A crate that happens to contain a symlink loop must not turn the export
        /// into an infinite walk, and a game's install directory can be large
        /// enough that "scan everything" needs a ceiling.
        /// </summary>
        private static IEnumerable<(string dir, string[] files)> Walk(
            string root, HashSet<string> seen, int depth)
        {
            if (depth > 8) yield break;

            string full;
            try { full = Path.GetFullPath(root); }
            catch { yield break; }
            if (!seen.Add(full)) yield break;
            if (!Directory.Exists(full)) yield break;

            string[] files;
            try
            {
                files = Directory.GetFiles(full)
                                 .OrderBy(f => f, StringComparer.Ordinal).ToArray();
            }
            catch { yield break; }
            yield return (full, files);

            string[] subs;
            try
            {
                subs = Directory.GetDirectories(full)
                                .OrderBy(d => d, StringComparer.Ordinal).ToArray();
            }
            catch { yield break; }
            foreach (string sub in subs)
                foreach (var hit in Walk(sub, seen, depth + 1))
                    yield return hit;
        }

        /// <summary>
        /// Names that never hold a shader: resource streams and the binaries a
        /// build ships beside its data.
        ///
        /// Deliberately narrow.  <c>resources.assets</c>, <c>sharedassets*</c> and
        /// <c>level*</c> are *not* excluded: those are exactly the serialized
        /// files shaders live in, and skipping them by name would lose most of the
        /// corpus.  The header check downstream is what rejects a file that turns
        /// out to be something else.
        /// </summary>
        private static bool IsPayloadName(string name) =>
            name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".debug", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("globalgamemanagers", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("boot.config", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("app.info", StringComparison.OrdinalIgnoreCase);

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

        /// <summary>
        /// What an input file is, decided from its first bytes.
        ///
        /// The SerializedFile check cannot be "version is in a plausible range":
        /// a PNG's IHDR happens to put its width where the version lives, so
        /// <c>icon.png</c> reads as version 13 and the reader then runs off the end
        /// of the pixel data (the "arithmetic overflow" and OutOfMemory failures a
        /// directory-wide scan produced).  The header is therefore validated as a
        /// whole: a small metadata size, a file size that matches the file on disk,
        /// and a version only where Unity actually writes one.
        /// </summary>
        private static InputKind Sniff(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                long length = fs.Length;
                // 48 bytes: the v22 header reads a 64-bit dataOffset at 0x20, so a
                // 24-byte buffer would index past its end.
                var head = new byte[48];
                int got = fs.Read(head, 0, head.Length);

                if (got >= 7 && head[0] == 'U' && head[1] == 'n' && head[2] == 'i' &&
                    head[3] == 't' && head[4] == 'y' && head[5] == 'F' && head[6] == 'S')
                    return InputKind.Bundle;

                if (got >= 4 && head[0] == 'P' && head[1] == 'K' &&
                    (head[2] == 3 || head[2] == 5 || head[2] == 7))
                    return InputKind.Zip;

                // A PNG/JPEG/GIF must be rejected before the generic check, since
                // their headers collide with a SerializedFile's at offset 8.
                if (got >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
                    return InputKind.Unknown;   // PNG
                if (got >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
                    return InputKind.Unknown;   // JPEG
                if (got >= 6 && head[0] == 'G' && head[1] == 'I' && head[2] == 'F')
                    return InputKind.Unknown;   // GIF
                if (got >= 4 && head[0] == 'O' && head[1] == 'g' && head[2] == 'g' && head[3] == 'S')
                    return InputKind.Unknown;   // Ogg audio
                if (got >= 4 && head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F')
                    return InputKind.Unknown;   // RIFF (wav/avi)
                if (got >= 3 && head[0] == 'I' && head[1] == 'D' && head[2] == '3')
                    return InputKind.Unknown;   // MP3
                if (got >= 4 && head[0] == 'F' && head[1] == 'S' && head[2] == 'B' && head[3] == '5')
                    return InputKind.Unknown;   // FSB audio bank

                if (got >= 40 && LooksLikeSerializedHeader(head, length))
                    return InputKind.Serialized;

                return InputKind.Unknown;
            }
            catch { return InputKind.Unknown; }
        }

        /// <summary>
        /// Does this look like a real SerializedFile header?
        ///
        /// The fields are big-endian at fixed offsets (metadata size and file size at
        /// 0x00/0x04 before v22 and 0x14/0x18 after, version at 0x08).  A real file's
        /// declared size matches the bytes on disk, which rules out every media format
        /// that happens to put a plausible number at offset 8.
        /// </summary>
        private static bool LooksLikeSerializedHeader(byte[] h, long fileLength)
        {
            uint version = BE32(h, 8);
            if (version < 6 || version > 100) return false;

            uint metadataSize = version >= 22 ? BE32(h, 0x14) : BE32(h, 0);
            long fileSize = version >= 22 ? (long)BE64(h, 0x18) : BE32(h, 4);

            if (metadataSize == 0 && fileSize == 0) return false;   // padding / null page
            if (metadataSize > fileLength) return false;
            // The declared size must match what is on disk (v22 tolerates some
            // slack: Unity writes the uncompressed size for a bundle node).
            if (fileSize != 0 && Math.Abs(fileSize - fileLength) > Math.Max(64, fileLength / 100))
                return false;

            // dataOffset must be inside the file and at or after the metadata.
            long dataOffset = version >= 22 ? (long)BE64(h, 0x20) : BE32(h, 0x0c);
            if (dataOffset < 0 || dataOffset > fileLength) return false;
            return true;
        }

        private static uint BE32(byte[] b, int at) =>
            (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);

        private static ulong BE64(byte[] b, int at) =>
            ((ulong)BE32(b, at) << 32) | BE32(b, at + 4);

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
        private static void ExportShader(Shader sh, string name, string rel, string dirName, string outDir,
                                         Dictionary<(string file, long pathId), string> resMap,
                                         bool shaderLab, bool programs, bool origAsm,
                                         int? platform, Stats s)
        {
            string full = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            string relDir = Path.GetDirectoryName(full) ?? outDir;
            Directory.CreateDirectory(relDir);

            var started = System.Diagnostics.Stopwatch.StartNew();

            // Per-program folders sit beside the .shader, named after it.  Both this
            // name and `rel` are resolved before the shader starts (see Seat) so
            // that parallel exports claim them in the same order a sequential run
            // would -- the claim decides which shader keeps the plain name.
            string dir = programs || origAsm ? Path.Combine(relDir, dirName) : null;

            var props = sh.ParsedForm?.Props ?? new List<Property>();

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
            var (passOf, spOf) = PassMaps(sh);

            // Every platform the game shipped, one at a time: d3d11 drives the
            // DXBC route, the GLES/GL platforms drive the GLSL route, everything
            // else is noted and skipped (no translator for it in this project).
            //
            // A forced platform narrows this to one slot.  That is not only for
            // comparison: a build with both d3d11 and gles3 slots always takes the
            // d3d11 one, so the GLSL route can only be exercised -- and its output
            // only be judged -- when d3d11 is excluded.
            for (int pi = 0; pi < (sh.Platforms?.Length ?? 0); pi++)
            {
                int pid = (int)sh.Platforms[pi];
                if (platform.HasValue && pid != platform.Value) continue;
                var info = Plugins.PlatformCatalog.Get(pid);
                if (info == null || !Plugins.PlatformCatalog.CanRead(pid)) continue;

                byte[] decoded;
                try { decoded = BlobTool.DecodeBlob(sh, pi); }
                catch (Exception e) { s.Skipped++; s.Notes.Add($"{name}/{info.Name}: decode failed: {e.Message}"); continue; }
                if (decoded == null || decoded.Length == 0) { s.Skipped++; continue; }

                if (info.Format == "dxbc") ExportDxbc(sh, name, info, decoded, dir, dirName,
                    passOf, spOf, kwIndex, props, perPass, programs, origAsm, s);
                else if (info.Format == "glsl") ExportGlsl(sh, name, info, decoded, dir, passOf, spOf, kwIndex, perPass, s, origAsm);
                else if (info.Format == "d3d9") ExportD3d9(sh, name, info, decoded, dir, passOf, spOf, perPass, origAsm, s);
                else ExportPlugin(sh, name, info, decoded, dir, passOf, spOf, kwIndex, props, perPass, origAsm, s);
            }

            // A shader whose passes all failed to translate still carries things
            // worth keeping: the Properties block (a material that references the
            // shader keeps its values instead of losing the whole reference) and
            // the `Fallback` line.  Writing the file is what makes the fallback
            // reachable at all -- skipping it dropped the line along with the
            // shader, so Unity answered "missing shader" where the source said
            // "use Diffuse".  The file then consists of properties plus that one
            // line, which is exactly what a build-stripped shader degrades to.
            if (shaderLab)
            {
                var (text, errs) = WrapShader(sh, name, perPass, props);
                if (errs == null)
                {
                    // A bare export overwrites silently, and the file it lands on may
                    // be somebody's own shader at the same path -- the one mistake a
                    // user cannot notice, because the run reported success.  `--overwrite`
                    // has a whole safety story for emptying a directory; the single
                    // file deserves at least a line.
                    WarnIfForeign(full, name);
                    File.WriteAllText(full, text);
                    s.Emitted++;
                    started.Stop();
                    Log.Info(name, rel, ShortSource(sh.SourceFile));
                    Log.Detail(Structure(sh, perPass), started.ElapsedMilliseconds);
                }
                else
                {
                    s.Notes.Add(Localization.Strings.F("ShaderLabNotWellFormed", name, errs));
                    Log.Warn(name, Localization.Strings.F("ShaderLabNotWellFormed", name, errs));
                }
            }
        }

        /// <summary>
        /// Warn when an export is about to replace a file this tool did not write.
        ///
        /// The test is deliberately forgiving: anything containing the tool's name is
        /// taken to be a previous export, because every artifact this program writes
        /// carries that name in its header comment.  Everything else -- a hand-written
        /// shader sitting at the same path in someone's Unity project -- gets a
        /// warning.  Not a refusal: the user may well be re-running into their own
        /// project on purpose, and `--overwrite` is the switch that means "I know".
        /// </summary>
        private static void WarnIfForeign(string path, string name)
        {
            try
            {
                if (!File.Exists(path)) return;
                using var reader = new StreamReader(path);
                var head = new char[8192];
                int read = reader.Read(head, 0, head.Length);
                if (new string(head, 0, read).Contains("ShaderRipper", StringComparison.Ordinal))
                    return;
                Log.Warn(name, Localization.Strings.F("OverwriteForeignFile", path));
            }
            catch (IOException) { }                 // unreadable is not our business
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Wrap a translated pass tree in ShaderLab and check the result.
        /// Returns (text, null) on success or (null, firstError) when the grammar
        /// rejects it.
        /// </summary>
        internal static (string text, string error) WrapShader(
            Shader sh, string name, System.Collections.IDictionary perPass, List<Property> props)
        {
            // The shader's own Properties block is the authority on which names are
            // the game's: a member declared here stays even when its name happens to
            // look like an engine one.
            Emit.ShaderLab.SetPropertyNames(props);
            var typed = (Dictionary<Pass, PassBuild>)perPass;
            // The sub-shaders are the asset's, not the translator's: a shader whose
            // programs could not be decoded still has tags, passes and render state,
            // and dropping them would leave a file that behaves differently from the
            // original.  Passes without a program say so in place.
            var subShaders = PassTree(sh, name, typed);
            string text = Emit.ShaderLab.Wrap(name, sh.FallbackName, subShaders, props,
                                              sh.ParsedForm?.CustomEditorName);
            var errs = Emit.ShaderLab.Check(text);
            return errs.Count == 0 ? (text, null) : (null, errs[0]);
        }

        /// <summary>
        /// Translate one shader to ShaderLab text, without touching the disk.
        ///
        /// This is the same body <see cref="ExportShader"/> runs; it exists so an
        /// embedder that already holds the bytes (AssetRipper) can get the file
        /// content without an output directory or a subprocess.
        /// </summary>
        internal static string TranslateOne(Shader sh, string name, Stats s)
        {
            var props = sh.ParsedForm?.Props ?? new List<Property>();

            var kwIndex = new Dictionary<int, string>();
            for (int ki = 0; ki < (sh.ParsedForm?.KeywordNames.Count ?? 0); ki++)
                if (!string.IsNullOrEmpty(sh.ParsedForm.KeywordNames[ki]))
                    kwIndex[ki] = sh.ParsedForm.KeywordNames[ki];

            var perPass = new Dictionary<Pass, PassBuild>();
            var (passOf, spOf) = PassMaps(sh);

            // A host that cannot supply per-program reflection still has every
            // program in the blob.  When the shader has exactly one pass, every blob
            // belongs to it -- so the lookup is widened instead of the programs being
            // dropped, which is what produced "no SubShader survived".
            Pass onlyPass = null;
            int passCount = sh.ParsedForm?.SubShaders.Sum(x => x.Passes.Count) ?? 0;
            if (passCount == 1)
                onlyPass = sh.ParsedForm.SubShaders.SelectMany(x => x.Passes).First();
            if (onlyPass != null && passOf.Count <= 1)
                passOf = Only(onlyPass);

            // No pass at all, but there are programs that must go somewhere: one
            // unnamed pass is added to hold them, inside an existing sub-shader when
            // the asset describes one.  A shader with no pass *and* no program keeps
            // its structure as-is -- a synthesised block would be a block the
            // original never had.
            if (passCount == 0 && UsedBlobIndices(sh).Count > 0)
            {
                onlyPass = new Pass();
                sh.ParsedForm ??= new ParsedForm();
                if (sh.ParsedForm.SubShaders.Count == 0)
                    sh.ParsedForm.SubShaders.Add(new SubShader());
                sh.ParsedForm.SubShaders[^1].Passes.Add(onlyPass);
                passOf = Only(onlyPass);
                spOf = new Dictionary<int, SubProgram>();
            }

            for (int pi = 0; pi < (sh.Platforms?.Length ?? 0); pi++)
            {
                int pid = (int)sh.Platforms[pi];
                var info = Plugins.PlatformCatalog.Get(pid);
                if (info == null || !Plugins.PlatformCatalog.CanRead(pid)) continue;

                // A raw payload is already decompressed: carve its containers by
                // header rather than running the slot-table decoder over them.
                var raw = pi < sh.RawPayloads.Length ? sh.RawPayloads[pi] : null;
                byte[] decoded;
                List<BlobTool.BlobSlot> pre = null;
                if (raw != null)
                {
                    decoded = raw;
                    pre = CarvePrograms(raw);
                }
                else
                {
                    try { decoded = BlobTool.DecodeBlob(sh, pi); }
                    catch (Exception e) { s.Skipped++; s.Notes.Add($"{name}/{info.Name}: decode failed: {e.Message}"); continue; }
                }
                if (decoded == null || decoded.Length == 0) { s.Skipped++; continue; }
                if (pre != null && pre.Count == 0) { s.Skipped++; continue; }

                if (info.Format == "dxbc") ExportDxbc(sh, name, info, decoded, null, null,
                    passOf, spOf, kwIndex, props, perPass, false, false, s, null, pre);
                else if (info.Format == "glsl") ExportGlsl(sh, name, info, decoded, null, passOf, spOf, kwIndex, perPass, s, false);
                else if (info.Format == "d3d9") ExportD3d9(sh, name, info, decoded, null, passOf, spOf, perPass, false, s);
                else ExportPlugin(sh, name, info, decoded, null, passOf, spOf, kwIndex, props, perPass, false, s, null);
            }

            var (text, error) = WrapShader(sh, name, perPass, props);
            return text;
        }

        /// <summary>Every blob index maps to this one pass.</summary>
        private static Dictionary<int, List<Pass>> Only(Pass pass)
        {
            var one = new List<Pass> { pass };
            var map = new Dictionary<int, List<Pass>>();
            for (int i = 0; i < 256; i++) map[i] = one;
            return map;
        }

        /// <summary>
        /// Carve a stream of already-decompressed program containers.
        ///
        /// Each DXBC container declares its own total length at offset 0x18, and a
        /// GLSL payload is plain text, so the walk needs no slot table.  Used when a
        /// host hands over decompressed programs instead of Unity's blob layout.
        /// </summary>
        internal static List<BlobTool.BlobSlot> CarvePrograms(byte[] data)
        {
            var list = new List<BlobTool.BlobSlot>();
            int at = 0, index = 0;
            while (at + 4 <= data.Length)
            {
                if (at + 0x24 <= data.Length &&
                    BitConverter.ToUInt32(data, at) == 0x43425844)      // "DXBC"
                {
                    uint size = BitConverter.ToUInt32(data, at + 0x18);
                    if (size == 0 || at + size > data.Length) break;
                    list.Add(new BlobTool.BlobSlot(index++, at, (int)size));
                    at += (int)size;
                    continue;
                }
                break;
            }
            return list;
        }

        /// <summary>
        /// Translate every program of a shader to standalone HLSL (no ShaderLab
        /// wrapper), in blob order.  Same body as the <c>--programs</c> artifact.
        /// </summary>
        internal static void TranslateProgramsOf(Shader sh, Stats s,
                                                 List<(int blobIndex, string stage, string hlsl)> into)
        {
            var props = sh.ParsedForm?.Props ?? new List<Property>();
            var kwIndex = new Dictionary<int, string>();
            for (int ki = 0; ki < (sh.ParsedForm?.KeywordNames.Count ?? 0); ki++)
                if (!string.IsNullOrEmpty(sh.ParsedForm.KeywordNames[ki]))
                    kwIndex[ki] = sh.ParsedForm.KeywordNames[ki];

            var perPass = new Dictionary<Pass, PassBuild>();
            var (passOf, spOf) = PassMaps(sh);

            for (int pi = 0; pi < (sh.Platforms?.Length ?? 0); pi++)
            {
                int pid = (int)sh.Platforms[pi];
                var info = Plugins.PlatformCatalog.Get(pid);
                if (info == null || !Plugins.PlatformCatalog.CanRead(pid)) continue;

                byte[] decoded;
                try { decoded = BlobTool.DecodeBlob(sh, pi); }
                catch { s.Skipped++; continue; }
                if (decoded == null || decoded.Length == 0) { s.Skipped++; continue; }

                if (info.Format == "dxbc")
                    ExportDxbc(sh, sh.Name, info, decoded, null, null, passOf, spOf,
                               kwIndex, props, perPass, false, false, s, into);
                else if (info.Format == "glsl")
                    ExportGlsl(sh, sh.Name, info, decoded, null, passOf, spOf, kwIndex,
                               perPass, s, false, into);
                else if (info.Format == "d3d9")
                    ExportD3d9(sh, sh.Name, info, decoded, null, passOf, spOf, perPass, false, s, into);
                else
                    ExportPlugin(sh, sh.Name, info, decoded, null, passOf, spOf, kwIndex,
                                 props, perPass, false, s, into);
            }
        }

        /// <summary>
        /// Which passes and which sub-program each blob slot belongs to.  The name
        /// table and the asset's own reflection hang off those, and a blob index is
        /// the only thing the decoded slot tells us.
        ///
        /// A slot usually belongs to one pass, but Unity stores one compiled copy of
        /// a program that several passes share -- two passes whose vertex and
        /// fragment are byte-identical end up declaring the same blob indices.  The
        /// mapping is therefore a list per slot: keeping only one owner silently
        /// dropped the program from every other pass that declared it.
        /// </summary>
        private static (Dictionary<int, List<Pass>> passOf, Dictionary<int, SubProgram> spOf) PassMaps(Shader sh, int platformIndex = -1)
        {
            var passOf = new Dictionary<int, List<Pass>>();
            var spOf = new Dictionary<int, SubProgram>();
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
                foreach (var p in ss.Passes)
                    foreach (var sp in p.SubPrograms)
                    {
                        if (platformIndex >= 0 && !MatchesPlatform(sp.GpuProgramType, (int)sh.Platforms[platformIndex])) continue;
                        if (!passOf.TryGetValue(sp.BlobIndex, out var owners))
                            passOf[sp.BlobIndex] = owners = new List<Pass>();
                        if (!owners.Contains(p)) owners.Add(p);

                        if (!spOf.TryGetValue(sp.BlobIndex, out var have) ||
                            sp.ConstantBuffers.Count > have.ConstantBuffers.Count)
                            spOf[sp.BlobIndex] = sp;
                    }
            return (passOf, spOf);
        }

        // Blob indices are local to a backend, not unique across the shader.
        private static bool MatchesPlatform(int type, int platform) => type == 0 || (platform switch
        {
            4 => type is >= 13 and <= 22,
            9 => type is 2 or 3 or 4,
            15 => type is 6 or 7 or 8,
            5 => type == 5,
            1 => type == 1,
            _ => true,
        });

        private static string ShortSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            int slash = source.LastIndexOf('/');
            if (slash < 0) return source;

            string bundle = source[..slash];
            string member = source[(slash + 1)..];
            // CAB-<hex> -> CAB-<first 8 hex>…: the id is a content hash, so its
            // prefix identifies it as well as the whole thing for log purposes.
            if (member.StartsWith("CAB-", StringComparison.Ordinal) && member.Length > 12)
                member = member[..12] + "…" + SuffixOf(member);
            return bundle + "/" + member;
        }

        /// <summary>".sharedAssets" and friends: the part of a node name that says what it is.</summary>
        private static string SuffixOf(string member)
        {
            int dot = member.IndexOf('.');
            return dot >= 0 ? member[dot..] : "";
        }

        /// <summary>
        /// A one-line description of what was written: pass / variant / property /
        /// platform counts.
        ///
        /// Printed under the info line at verbose level, because "it wrote a file"
        /// and "it wrote the right file" are different questions and only the
        /// counts answer the second one cheaply.
        /// </summary>
        private static string Structure(Shader sh, Dictionary<Pass, PassBuild> perPass)
        {
            int passes = 0, variants = 0;
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
                passes += ss.Passes.Count;
            foreach (var pb in perPass.Values)
                variants += Math.Max(pb.Keys.Count, 1);

            string targets = string.Join("/", perPass.Values
                .Select(pb => pb.Target)
                .Where(t => t > 0)
                .Distinct()
                .OrderBy(t => t)
                .Select(t => $"{t / 10}.{t % 10}"));

            string line = Localization.Strings.F("LogStructure", passes, variants,
                                                sh.ParsedForm?.Props.Count ?? 0,
                                                sh.Platforms?.Length ?? 0);
            if (targets.Length > 0)
                line = Localization.Strings.F("LogStructureTarget", line, targets);
            return line;
        }

        // ------------------------------------------------------------------
        // DXBC route (d3d11): disassemble, translate, keep every variant
        // ------------------------------------------------------------------

        private static void ExportDxbc(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, string dirName, Dictionary<int, List<Pass>> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<int, string> kwIndex, List<Property> props,
            Dictionary<Pass, PassBuild> perPass, bool programs, bool origAsm, Stats s,
            List<(int blobIndex, string stage, string hlsl)> collected = null,
            List<BlobTool.BlobSlot> preSlots = null)
        {
            int platformIndex = Array.IndexOf(sh.Platforms, (uint)info.Id);
            if (sh.ParsedForm?.SubShaders.SelectMany(ss => ss.Passes).SelectMany(p => p.SubPrograms)
                .Any(sp => sp.GpuProgramType != 0) == true)
                (passOf, spOf) = PassMaps(sh, platformIndex);
            var slots = preSlots ?? BlobTool.DecodeSlots(decoded, spOf.Keys.ToList());
            // The disassembly carries no reflection (Unity strips RDEF), so the
            // only source for `cb1[7]`'s name is the asset's own parameter blob.
            var paramCbs = BlobTool.ParseParamBlobs(decoded);
            var allParamCbs = paramCbs.Values.SelectMany(x => x).ToList();
            var matrixRows = MatrixSpans(sh);
            // Notes are per program but the file shows them per pass, and the pass a
            // slot belongs to is only known once the slot has been decoded.
            var cbNotes = new Dictionary<Pass, List<string>>();

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
                List<Pass>? owners = null;
                passOf.TryGetValue(slot.BlobIndex, out owners);
                var pass = owners is { Count: > 0 } ? owners[0] : null;
                module.Cb = NameConstantBuffers(module, name, pass, sub, props, paramCbs, allParamCbs,
                                                matrixRows);

                // Two consumers, two shapes.  The ShaderLab wrapper merges this text
                // into a file that includes `UnityCG.cginc` -- matrices are
                // `float4x4` and the macro exists -- while `--programs` writes a
                // standalone file whose matrices are row arrays and needs the inlined
                // helper.  Emitting the wrapper-friendly form first keeps the merged
                // file simple; the standalone artifact is produced below only when it
                // is actually written.
                if (pass != null)
                    foreach (string note in module.Cb?.FileNotes ?? new List<string>())
                        if (!cbNotes.TryGetValue(pass, out var notes))
                            cbNotes[pass] = new List<string> { note };
                        else if (!notes.Contains(note)) notes.Add(note);

                var r = HlslEmitter.Emit(module);
                s.Translated++;
                collected?.Add((slot.BlobIndex, module.Stage, r.Hlsl));

                if (programs || origAsm)
                {
                    Directory.CreateDirectory(dir);
                    // The translation is only written with --programs; --orig-asm
                    // alone keeps just the disassembly it compares against.
                    if (programs)
                        File.WriteAllText(Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.hlsl"),
                                          HlslEmitter.Emit(module, standalone: true).Hlsl);

                    // The original disassembly, kept beside the translation so the two
                    // can be compared instruction for instruction.  It is the same text
                    // the translator read.
                    if (origAsm)
                        File.WriteAllText(Path.Combine(dir, $"{slot.BlobIndex}_{module.Stage}.orig.asm"), asm);
                }

                if (pass == null) continue;
                foreach (var owner in owners!)
                {
                    if (!perPass.TryGetValue(owner, out var build)) perPass[owner] = build = new PassBuild();
                    if (cbNotes.TryGetValue(pass, out var passNotes))
                        foreach (string note in passNotes)
                            if (!build.Notes.Contains(note)) build.Notes.Add(note);
                    build.Target = Math.Max(build.Target, ShaderModel(r.Profile));
                    var kw = VariantPlan.KeywordsOf(sub, kwIndex);
                    var key = VariantPlan.Key(kw);
                    if (module.Stage is "ps") build.Add(build.Ps, build.Keys, key, kw, r.Hlsl);
                    else if (module.Stage is "vs") build.Add(build.Vs, build.Keys, key, kw, r.Hlsl);
                }
            }
        }

        // ------------------------------------------------------------------
        // GLSL route (gles20/gles3/glcore): extract text, translate, per variant
        // ------------------------------------------------------------------

        private static void ExportGlsl(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, Dictionary<int, List<Pass>> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<int, string> kwIndex, Dictionary<Pass, PassBuild> perPass, Stats s,
            bool origSrc = false, List<(int blobIndex, string stage, string hlsl)> collected = null)
        {
            int platformIndex = Array.IndexOf(sh.Platforms, (uint)info.Id);
            if (sh.ParsedForm?.SubShaders.SelectMany(ss => ss.Passes).SelectMany(p => p.SubPrograms)
                .Any(sp => sp.GpuProgramType != 0) == true)
                (passOf, spOf) = PassMaps(sh, platformIndex);
            var progs = ProgramSource.Extract(decoded);
            if (Environment.GetEnvironmentVariable("SHADERRIP_PROBE") == "1")
            {
                Console.Error.WriteLine($"[probe] {name} platform={info.Name} offsets={sh.Offsets.Length} slots=[{string.Join(",", spOf.Keys)}] progs={progs.Count}");
                foreach (var pp in sh.ParsedForm.SubShaders.SelectMany(x => x.Passes))
                    Console.Error.WriteLine($"[probe]   pass '{pp.StateName}' sp=" + string.Join(" ", pp.SubPrograms.Select(x => $"{x.BlobIndex}[{string.Join("/", x.KeywordIndices)}]")));
            }

            foreach (var pr in progs)
            {
                if (pr.Format != BlobFormat.Glsl || string.IsNullOrEmpty(pr.Text)) { s.Skipped++; continue; }
                int bi = pr.BlobIndex;

                var r = GlslTranslator.Translate(pr.Text,
                    sh.ParsedForm?.Props.Select(p => p.Name).ToList());
                s.Programs++;
                if (string.IsNullOrWhiteSpace(r.Hlsl)) { s.Skipped++; continue; }
                s.Translated++;
                collected?.Add((bi, "glsl", r.Hlsl));

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
                List<Pass>? owners = null;
                passOf.TryGetValue(bi, out owners);
                if (owners == null || owners.Count == 0) continue;
                var kw = VariantPlan.KeywordsOf(sub, kwIndex);
                foreach (var owner in owners)
                {
                    if (!perPass.TryGetValue(owner, out var build)) perPass[owner] = build = new PassBuild();
                    build.Add(build.Glsl, build.GKeys, VariantPlan.Key(kw), kw, r.Hlsl);
                }
            }
        }

        // ------------------------------------------------------------------
        // d3d9 route (SM2/SM3): disassembly only
        // ------------------------------------------------------------------

        /// <summary>
        /// The d3d9 slot: disassemble every program, translate none.
        ///
        /// Unity's SM2/3 payload is a bare token stream in a different dialect
        /// from DXBC (<c>texld</c>/<c>mova</c>, absolute register numbers), so the
        /// DXBC emitter cannot read it and no back-translation is attempted --
        /// the same position DXIL is in.  The disassembly is still the artifact a
        /// person needs to hand-write the shader, so each program lands beside the
        /// .shader as <c>&lt;blobIndex&gt;_&lt;stage&gt;.orig.asm</c>.
        ///
        /// Nothing reaches the .shader body: a pass with only d3d9 programs keeps
        /// its render state and says so in a note, which is exactly what the
        /// build's data supports.
        /// </summary>
        private static void ExportD3d9(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, Dictionary<int, List<Pass>> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<Pass, PassBuild> perPass, bool origAsm, Stats s,
            List<(int blobIndex, string stage, string hlsl)> collected = null)
        {
            var progs = ProgramSource.Extract(decoded);

            foreach (var pr in progs)
            {
                if (pr.Format != BlobFormat.D3d9) continue;
                s.Programs++;

                string asm = D3D9Asm.Disassemble(decoded, pr.Offset, pr.Length, out string stage);
                if (asm == null) { s.Skipped++; continue; }
                s.Disassembled++;

                if (dir != null && origAsm)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, $"{pr.BlobIndex}_{stage}.orig.asm"), asm);
                }

                // The pass still needs a line saying its program was read but not
                // translated; without it the .shader looks like a build-stripped
                // one, which is a different (and wrong) claim.
                if (!passOf.TryGetValue(pr.BlobIndex, out var owners) || owners == null) continue;
                foreach (var owner in owners)
                {
                    if (!perPass.TryGetValue(owner, out var build))
                        perPass[owner] = build = new PassBuild();
                    string note = Localization.Strings.F("NoteD3d9DisassemblyOnly", info.Name);
                    if (!build.Notes.Contains(note)) build.Notes.Add(note);
                }
            }
        }

        // ------------------------------------------------------------------
        // Plugin route: a backend a plugin brought its own reader and translator for
        // ------------------------------------------------------------------

        /// <summary>
        /// Translate a backend that came from a plugin.
        ///
        /// The plugin supplies both halves -- how to enumerate the programs in the slot
        /// and how to turn one into HLSL -- so this method only does the bookkeeping
        /// the built-in routes also do: count, collect per-pass bodies, note failures.
        /// A slot with no translator still lands on disk as a disassembly-equivalent
        /// artefact when the plugin provides one, which is how d3d9 is handled.
        /// </summary>
        private static void ExportPlugin(Shader sh, string name, PlatformInfo info, byte[] decoded,
            string dir, Dictionary<int, List<Pass>> passOf, Dictionary<int, SubProgram> spOf,
            Dictionary<int, string> kwIndex, List<Property> props,
            Dictionary<Pass, PassBuild> perPass, bool origAsm, Stats s,
            List<(int blobIndex, string stage, string hlsl)> collected = null)
        {
            var registry = Plugins.PluginContext.Registry;
            var entry = registry?.PlatformFor(info.Id);
            if (entry?.SlotReader == null)
            {
                s.Skipped++;
                s.Notes.Add($"{name}/{info.Name}: no slot reader registered");
                return;
            }

            int platformIndex = Array.IndexOf(sh.Platforms, (uint)info.Id);
            if (sh.ParsedForm?.SubShaders.SelectMany(ss => ss.Passes).SelectMany(p => p.SubPrograms)
                .Any(sp => sp.GpuProgramType != 0) == true)
                (passOf, spOf) = PassMaps(sh, platformIndex);

            var context = new Plugins.Platforms.SlotContext
            {
                BlobIndices = spOf.Keys.OrderBy(x => x).ToList(),
                Platform = entry.Descriptor,
            };

            var programs = Plugins.PluginCall.Try($"{entry.Descriptor.Name}.SlotReader",
                () => entry.SlotReader.Read(decoded, context),
                (IReadOnlyList<Plugins.Platforms.ProgramDescriptor>)Array.Empty<Plugins.Platforms.ProgramDescriptor>(),
                s.Notes);
            if (programs == null || programs.Count == 0) { s.Skipped++; return; }

            var propertyNames = props?.Select(p => p.Name).ToList() ?? new List<string>();

            foreach (var program in programs)
            {
                s.Programs++;
                if (program.Offset < 0 || program.Length <= 0 ||
                    program.Offset + program.Length > decoded.Length)
                {
                    s.Skipped++;
                    continue;
                }

                var request = new Plugins.Platforms.TranslationRequest
                {
                    Payload = new ReadOnlyMemory<byte>(decoded, program.Offset, program.Length),
                    Stage = program.Stage,
                    BlobIndex = program.BlobIndex,
                    Entry = program.Entry,
                    PropertyNames = propertyNames,
                    Platform = entry.Descriptor,
                };

                Plugins.Platforms.TranslationResult result = null;
                if (entry.Translator != null)
                {
                    bool can = Plugins.PluginCall.Try($"{entry.Descriptor.Name}.CanTranslate",
                        () => entry.Translator.CanTranslate(request), false, s.Notes);
                    if (!can) { s.Skipped++; continue; }

                    result = Plugins.PluginCall.Try($"{entry.Descriptor.Name}.Translate",
                        () => entry.Translator.Translate(request), null, s.Notes);
                }

                if (result == null)
                {
                    // No translator, or it produced nothing: the program still counts as
                    // read, which is what the d3d9 route does too.
                    s.Disassembled++;
                    continue;
                }
                if (result.Unsupported || string.IsNullOrWhiteSpace(result.Hlsl))
                {
                    s.Skipped++;
                    continue;
                }

                s.Translated++;
                string stage = result.Stage ?? program.Stage ?? "ps";
                collected?.Add((program.BlobIndex, stage, result.Hlsl));

                if (dir != null)
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(Path.Combine(dir, $"{program.BlobIndex}_{stage}.hlsl"),
                                          result.Hlsl);
                        if (origAsm && request.Payload.Length > 0)
                            File.WriteAllBytes(
                                Path.Combine(dir, $"{program.BlobIndex}_{stage}.orig.bin"),
                                request.Payload.ToArray());
                    }
                    catch (IOException e)
                    {
                        s.Notes.Add($"{name}/{info.Name}: cannot write program " +
                                    $"{program.BlobIndex}: {e.Message}");
                    }
                }

                if (!passOf.TryGetValue(program.BlobIndex, out var owners) || owners == null) continue;
                spOf.TryGetValue(program.BlobIndex, out var sub);
                var kw = VariantPlan.KeywordsOf(sub, kwIndex);
                foreach (var owner in owners)
                {
                    if (!perPass.TryGetValue(owner, out var build)) perPass[owner] = build = new PassBuild();
                    foreach (string note in result.Notes ?? Array.Empty<string>())
                        if (!build.Notes.Contains(note)) build.Notes.Add(note);
                    if (result.Target > 0) build.Target = Math.Max(build.Target, result.Target);
                    if (stage == "vs") build.Add(build.Vs, build.Keys, VariantPlan.Key(kw), kw, result.Hlsl);
                    else if (stage == "ps") build.Add(build.Ps, build.Keys, VariantPlan.Key(kw), kw, result.Hlsl);
                }
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
            Shader sh, string shaderName, Dictionary<Pass, PassBuild> programs)        {
            var list = new List<Emit.ShaderLab.SubShaderText>();
            foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
            {
                var text = new Emit.ShaderLab.SubShaderText { LOD = ss.LOD };
                text.Tags.AddRange(ss.Tags);

                for (int pi = 0; pi < ss.Passes.Count; pi++)
                {
                    var p = ss.Passes[pi];
                    var pt = new Emit.ShaderLab.PassText { Name = p.StateName };
                    // A pass with no name of its own is identified by its position;
                    // "pass 0" is how a person would point at it too.
                    string passLabel = string.IsNullOrEmpty(p.StateName)
                        ? $"#{pi}" : $"{pi} \"{p.StateName}\"";
                    pt.Tags.AddRange(p.Tags);
                    // A host that rendered the state itself gets its lines verbatim;
                    // otherwise they are derived from the numeric fields, which is
                    // what the file-based path does.
                    if (p.StateLines.Count > 0)
                        foreach (string block in p.StateLines) pt.States.Add(Dedent(block));
                    else AppendRenderState(pt, p);

                    if (programs.TryGetValue(p, out var pb))
                    {
                        FillPass(pt, pb, sh.ParsedForm);
                        // Each stage of the pass contributes its own copy of a note, and
                        // the lists differ by how many registers that stage happened to
                        // use.  One note per subject, the most complete one.
                        foreach (var g in pb.Notes.GroupBy(n => n.Split(": ")[0]))
                            pt.Notes.Add(g.OrderByDescending(n => n.Length).First());
                        // A pass with no program at all is a render-state-only pass
                        // (Unity ships those deliberately), but one whose fragment
                        // exists and whose vertex does not is worth saying out loud:
                        // the vertex program is then synthesised, not recovered.
                        if (pt.Vertex == null && pt.Fragment == null && pt.Chains == null)
                            Log.Warn(shaderName, Localization.Strings.F("WarnPassNoProgram", passLabel));
                        else if (pt.Vertex == null && pt.Fragment != null && pt.Chains == null)
                            Log.Warn(shaderName, Localization.Strings.F("WarnPassNoVertex", passLabel));
                    }
                    text.Passes.Add(pt);
                }
                list.Add(text);
            }
            return list;
        }

        /// <summary>
        /// A host renders its state lines with its own indentation (tabs, or four
        /// spaces per level).  Removing the block's own margin leaves the relative
        /// depth of a nested <c>Stencil { }</c> intact, so the pass-level pad is the
        /// only one added afterwards.
        /// </summary>
        private static string Dedent(string block)
        {
            if (string.IsNullOrEmpty(block)) return "";
            var raw = block.Replace("\r\n", "\n").Replace('\t', ' ').Split('\n');
            int margin = int.MaxValue;
            foreach (string line in raw)
            {
                if (line.Trim().Length == 0) continue;
                int i = 0;
                while (i < line.Length && line[i] == ' ') i++;
                if (i < margin) margin = i;
            }
            if (margin == int.MaxValue) margin = 0;

            var outLines = new List<string>(raw.Length);
            foreach (string line in raw)
            {
                if (line.Trim().Length == 0) { outLines.Add(""); continue; }
                outLines.Add(line.Length >= margin ? line.Substring(margin).TrimEnd() : line.Trim());
            }
            while (outLines.Count > 0 && outLines[^1].Length == 0) outLines.RemoveAt(outLines.Count - 1);
            return string.Join("\n", outLines);
        }

        /// <summary>
        /// The render-state statements a pass would emit, one entry per statement
        /// (a multi-line entry is the Stencil or Fog block).
        ///
        /// Public so <c>--dump</c> can show the same list without writing a file:
        /// that is how a missing state in the output is told apart from a state the
        /// asset never carried.
        /// </summary>
        internal static List<string> DescribeRenderState(Pass pass)
        {
            var pt = new Emit.ShaderLab.PassText();
            AppendRenderState(pt, pass);
            return pt.States;
        }

        /// <summary>
        /// Every render-state line a pass declares, in ShaderLab order.
        ///
        /// The asset records each state twice: a numeric value and, when the source
        /// drove it from a material property, the property's name
        /// (<c>ZWrite [_ZWrite]</c>).  The name is the one to emit -- the number is
        /// what the property happened to be set to when the asset was compiled, so
        /// writing it would freeze a tunable state.  States at their ShaderLab default
        /// are omitted.
        /// </summary>
        private static void AppendRenderState(Emit.ShaderLab.PassText pt, Pass pass)
        {
            // ---- fixed-function lighting and fog (legacy shaders only) ----
            //
            // ShaderLab reads these before the raster states and they only exist
            // for fixed-function passes.  The asset records them because ShaderLab
            // still accepts the statements, but a modern shader leaves them at
            // their defaults, and emitting the defaults would add two dead lines to
            // every pass.  Fog is written as the whole block, because the
            // mode/density/range only mean anything together.
            if (pass.Lighting) pt.States.Add("Lighting On");
            AppendFog(pt, pass);

            // ---- depth ----
            AppendFloatState(pt, "ZWrite", pass.ZWrite, pass.ZWriteName, 1,
                             v => v == 0 ? "Off" : "On");
            AppendFloatState(pt, "ZTest", pass.ZTest, pass.ZTestName, 4,
                             v => StateName("ZTest", v));
            AppendFloatState(pt, "ZClip", pass.ZClip, pass.ZClipName, 1,
                             v => v == 0 ? "Off" : "On");

            // ---- rasteriser ----
            AppendFloatState(pt, "Cull", pass.Culling, pass.CullingName, 2,
                             v => StateName("Cull", v));

            // ---- blending ----
            AppendBlend(pt, pass);

            // ---- depth offset (polygon offset) ----
            if (pass.OffsetFactor != 0 || pass.OffsetUnits != 0 ||
                pass.OffsetFactorName.Length > 0 || pass.OffsetUnitsName.Length > 0)
            {
                string f = Named(pass.OffsetFactorName, N(pass.OffsetFactor));
                string u = Named(pass.OffsetUnitsName, N(pass.OffsetUnits));
                pt.States.Add($"Offset {f}, {u}");
            }

            // ---- coverage ----
            AppendFloatState(pt, "AlphaToMask", pass.AlphaToMask, pass.AlphaToMaskName, 0,
                             v => v == 0 ? "Off" : "On");

            // ---- stencil ----
            AppendStencil(pt, pass);
        }

        /// <summary>
        /// The legacy <c>Fog { }</c> block, when the pass enables fog.
        ///
        /// <c>m_FogMode</c> is UnityEngine.Rendering.FogMode: 0 = off, 1 = linear,
        /// 2 = exponential, 3 = exponential squared.  A mode of 0 means the shader
        /// does not fog at all, which is ShaderLab's default, so nothing is
        /// written; any other mode needs the block.
        /// </summary>
        private static void AppendFog(Emit.ShaderLab.PassText pt, Pass pass)
        {
            if (pass.FogMode == 0) return;

            string mode = pass.FogMode switch
            {
                1 => "Linear",
                2 => "Exp",
                3 => "Exp2",
                _ => null,
            };
            if (mode == null) return;

            pt.States.Add(string.Join("\n", new[]
            {
                "Fog",
                "{",
                "    Mode " + mode,
                "    Color (0.5, 0.5, 0.5, 0.5)",
                "    Density " + N(pass.FogDensity <= 0 ? 1f : pass.FogDensity),
                "    Range " + N(pass.FogStart) + ", " + N(pass.FogEnd <= 0 ? 300f : pass.FogEnd),
                "}",
            }));
        }

        /// <summary>
        /// One scalar state line, or nothing when it is at its default.
        ///
        /// <paramref name="propertyName"/> wins over <paramref name="value"/>: a
        /// state driven by a material property has to stay driven by it.
        /// </summary>
        private static void AppendFloatState(Emit.ShaderLab.PassText pt, string state,
            float value, string propertyName, float defaultValue, Func<float, string> spell)
        {
            bool hasName = !string.IsNullOrEmpty(propertyName);
            if (!hasName && value == defaultValue) return;
            string arg = hasName ? $"[{propertyName}]" : spell(value);
            pt.States.Add($"{state} {arg}");
        }

        /// <summary>
        /// Blend state: the single <c>Blend A B</c> form when both channels agree,
        /// the separate-alpha form when they do not, and the per-target
        /// <c>Blend N Src Dst</c> form when the shader enables MRT blending.
        ///
        /// <c>m_RtSeparateBlend</c> needs no separate handling: it is exactly the
        /// condition "the RGB and alpha channels are set independently", which is
        /// what <c>alphaSplit</c> below detects from the values themselves.  Trusting
        /// the flag alone would also miss a pass whose channels happen to be equal
        /// while the flag is set, and a pass whose channels differ while it is not.
        /// </summary>
        private static void AppendBlend(Emit.ShaderLab.PassText pt, Pass pass)
        {
            if (pass.Blends.Count == 0) return;

            for (int i = 0; i < pass.Blends.Count; i++)
            {
                var b = pass.Blends[i];
                // Only emit a target line when that target differs from the
                // ShaderLab default (One Zero) -- and always emit target 0, which
                // carries the pass-level Blend line below.
                bool alphaSplit = b.SrcBlendAlpha != b.SrcBlend || b.DestBlendAlpha != b.DestBlend;
                bool plain = b.SrcBlend == 1 && b.DestBlend == 0 &&
                             b.SrcBlendAlpha == 1 && b.DestBlendAlpha == 0 &&
                             b.BlendOp == 0 && b.BlendOpAlpha == 0 && b.ColMask == 15;

                if (i == 0 && plain) break;
                if (i > 0 && plain) continue;

                string src = Named(b.SrcBlendName, StateName("Blend", b.SrcBlend));
                string dst = Named(b.DestBlendName, StateName("Blend", b.DestBlend));
                string prefix = i == 0 ? "Blend" : $"Blend {i}";

                if (alphaSplit)
                {
                    string srcA = Named(b.SrcBlendAlphaName, StateName("Blend", b.SrcBlendAlpha));
                    string dstA = Named(b.DestBlendAlphaName, StateName("Blend", b.DestBlendAlpha));
                    pt.States.Add($"{prefix} {src} {dst}, {srcA} {dstA}");
                }
                else
                {
                    pt.States.Add($"{prefix} {src} {dst}");
                }

                // BlendOp applies to the RGB channel, BlendOpAlpha to alpha; each
                // is omitted at its default (Add).
                if (b.BlendOp != 0 || !string.IsNullOrEmpty(b.BlendOpName))
                {
                    string op = string.IsNullOrEmpty(b.BlendOpName)
                        ? StateName("BlendOp", b.BlendOp) : $"[{b.BlendOpName}]";
                    pt.States.Add($"{prefix}Op {op}");
                }
                if (b.BlendOpAlpha != 0 || !string.IsNullOrEmpty(b.BlendOpAlphaName))
                {
                    string op = string.IsNullOrEmpty(b.BlendOpAlphaName)
                        ? StateName("BlendOp", b.BlendOpAlpha) : $"[{b.BlendOpAlphaName}]";
                    pt.States.Add(i == 0 ? $"BlendOpAlpha {op}" : $"BlendOpAlpha {i} {op}");
                }
            }

            // ColorMask is per-pass: the write mask of target 0 is what ShaderLab's
            // single-string form expresses.
            var first = pass.Blends[0];
            if (first.ColMask != 15 || !string.IsNullOrEmpty(first.ColMaskName))
            {
                string mask = !string.IsNullOrEmpty(first.ColMaskName)
                    ? $"[{first.ColMaskName}]" : ColourMaskLetters(first.ColMask);
                pt.States.Add($"ColorMask {mask}");
            }
        }

        /// <summary>
        /// The stencil block, when the pass uses stencil at all.
        ///
        /// <c>m_StencilOp</c> carries three *independent* sets -- front-facing,
        /// back-facing and the combined one -- and they are emitted as one block
        /// with <c>CompFront</c>/<c>CompBack</c> style entries, not as three
        /// separate <c>Stencil { }</c> blocks: ShaderLab allows exactly one stencil
        /// block per pass, and a second one would be a parse error.
        /// </summary>
        private static void AppendStencil(Emit.ShaderLab.PassText pt, Pass pass)
        {
            if (pass.StencilOps.Count == 0) return;

            bool refSet = pass.StencilRef != 0 || pass.StencilRefName.Length > 0;
            bool readSet = pass.StencilReadMask != 255 || pass.StencilReadMaskName.Length > 0;
            bool writeSet = pass.StencilWriteMask != 255 || pass.StencilWriteMaskName.Length > 0;

            // The op sets are (front, back) or a single combined one; Unity writes
            // "stencilOp" plus optionally "stencilOpFront"/"stencilOpBack".
            var front = pass.StencilOps.Count > 1 ? pass.StencilOps[1] : null;
            var back = pass.StencilOps.Count > 2 ? pass.StencilOps[2] : null;
            var combined = pass.StencilOps[0];

            // ShaderLab's stencil defaults are Comp Always, Pass/Fail/ZFail Keep -
            // a stencil block that says nothing but those is the same as no block
            // at all, and emitting it for every pass would put 400 pointless blocks
            // in the output.
            bool anyOp = StencilDiffers(combined) ||
                         (front != null && StencilDiffers(front)) ||
                         (back != null && StencilDiffers(back));
            if (!anyOp && !refSet && !readSet && !writeSet) return;

            var lines = new List<string> { "Stencil", "{" };
            if (refSet) lines.Add("    Ref " + Named(pass.StencilRefName, N(pass.StencilRef)));
            if (readSet)
                lines.Add("    ReadMask " + Named(pass.StencilReadMaskName, N(pass.StencilReadMask)));
            if (writeSet)
                lines.Add("    WriteMask " + Named(pass.StencilWriteMaskName, N(pass.StencilWriteMask)));

            AppendStencilOps(lines, "", combined);
            if (front != null) AppendStencilOps(lines, "Front", front);
            if (back != null) AppendStencilOps(lines, "Back", back);

            lines.Add("}");
            pt.States.Add(string.Join("\n", lines));
        }

        /// <summary>
        /// Does this stencil operation set differ from ShaderLab's defaults
        /// (Comp Always, Pass/Fail/ZFail Keep)?
        /// </summary>
        private static bool StencilDiffers(StencilOp op) =>
            op.CompName.Length > 0 || op.PassName.Length > 0 ||
            op.FailName.Length > 0 || op.ZFailName.Length > 0 ||
            (op.Comp != 0 && op.Comp != 8) ||      // 8 = Always, the default
            op.PassOp != 0 || op.Fail != 0 || op.ZFail != 0;

        /// <summary>One stencil operation set, with <paramref name="side"/> empty, "Front" or "Back".</summary>
        private static void AppendStencilOps(List<string> lines, string side, StencilOp op)
        {
            if ((op.Comp != 0 && op.Comp != 8) || op.CompName.Length > 0)
                lines.Add("    Comp" + side + " " + Named(op.CompName, StateName("Comp", op.Comp)));
            if (op.PassOp != 0 || op.PassName.Length > 0)
                lines.Add("    Pass" + side + " "
                          + Named(op.PassName, StateName("StencilOp", op.PassOp)));
            if (op.Fail != 0 || op.FailName.Length > 0)
                lines.Add("    Fail" + side + " "
                          + Named(op.FailName, StateName("StencilOp", op.Fail)));
            if (op.ZFail != 0 || op.ZFailName.Length > 0)
                lines.Add("    ZFail" + side + " "
                          + Named(op.ZFailName, StateName("StencilOp", op.ZFail)));
        }

        /// <summary>
        /// A state value the way fxc's own output would spell it: no trailing
        /// zeros, invariant decimal separator.
        /// </summary>
        private static string N(float f) =>
            f.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The property reference when the state is property-driven, else the literal.</summary>
        private static string Named(string propertyName, string literal) =>
            string.IsNullOrEmpty(propertyName) ? literal : $"[{propertyName}]";

        /// <summary><c>ColorMask</c> letters for a four-bit mask (1 = R, 2 = G, 4 = B, 8 = A).</summary>
        private static string ColourMaskLetters(float mask)
        {
            int m = (int)mask;
            if (m == 0) return "0";
            if (m == 15) return "RGBA";
            var sb = new System.Text.StringBuilder(4);
            if ((m & 1) != 0) sb.Append('R');
            if ((m & 2) != 0) sb.Append('G');
            if ((m & 4) != 0) sb.Append('B');
            if ((m & 8) != 0) sb.Append('A');
            return sb.ToString();
        }

        private static string KeywordPragma(VariantPlan.Group group, ParsedForm form, Emit.ShaderLab.PassText pass)
        {
            var names = new[] { group.Name }.Concat(group.Alternatives).ToList();
            bool known = names.All(n => form.KeywordNames.IndexOf(n) is int i && i >= 0 && i < form.KeywordFlags.Count);
            bool local = known && names.All(n => (form.KeywordFlags[form.KeywordNames.IndexOf(n)] & 1) != 0);
            bool global = known && names.All(n => (form.KeywordFlags[form.KeywordNames.IndexOf(n)] & 1) == 0);
            if (!local && !global)
                pass.Notes.Add("Keyword scope unknown or mixed; keeping multi_compile: " + string.Join(" ", names));
            return "#pragma multi_compile" + (local ? "_local" : "") + " __ " + string.Join(" ", names);
        }

        private static void FillPass(Emit.ShaderLab.PassText pt, PassBuild pb, ParsedForm form)
        {
            bool hasKeywords = pb.Keys.Any(k => k.Length > 0) || pb.GKeys.Any(k => k.Length > 0);
            pt.Target = pb.Target;

            bool dxbc = pb.Vs.Count > 0 || pb.Ps.Count > 0;
            bool glsl = !dxbc && pb.Glsl.Count > 0;
            pt.Source = glsl ? "GLSL" : "DXBC";

            // Prefer DXBC when both backends exist; never mix their slot namespaces.
            if (glsl)
            {
                var glslKeys = pb.GKeys.Where(k => pb.Glsl.ContainsKey(k)).ToList();
                var glslSets = glslKeys.Select(k => new HashSet<string>(pb.Glsl[k].kw, StringComparer.Ordinal)).ToList();
                var groups = VariantPlan.Groups(glslKeys.Select(k => pb.Glsl[k].kw));
                pt.Pragmas.AddRange(groups.Select(g => KeywordPragma(g, form, pt)));
                pt.Notes.AddRange(VariantPlan.Analyze(groups, glslSets)
                    .Notes.Select(n => $"{glslKeys.Count} GLSL keyword variant(s), {groups.Count} pragma line(s): " + n));
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
            var sets2 = kwSets.Select(k => new HashSet<string>(k, StringComparer.Ordinal)).ToList();
            pt.Pragmas.AddRange(groups2.Select(g => KeywordPragma(g, form, pt)));
            int total = kwSets.Count();
            pt.Notes.Add($"{total} keyword variant(s), {groups2.Count} pragma line(s)");
            pt.Notes.AddRange(VariantPlan.Analyze(groups2, sets2).Notes);

            // One chain, one branch per keyword combination: the branch body is
            // the vs+ps pair of that combination cross-merged by Merge, so the
            // shared cbuffer/struct declarations exist exactly once per branch.
            // Emitting the stages as two independent guard chains would activate
            // both under the same keyword state and redefine every shared member.
            var chain = new Emit.ShaderLab.VariantChain();
            // A pass often ships its fragment program for fewer keyword
            // combinations than its vertex program (the ps is shared where the vs
            // is specialised).  `#pragma fragment` is written once for the whole
            // block, so a branch with no `frag` in it cannot compile -- and Unity
            // reports that as "did not find shader kernel 'frag' to compile", which
            // points at the pragma instead of at the missing body.
            var anyVs = pb.Vs.Values.FirstOrDefault();
            var anyPs = pb.Ps.Values.FirstOrDefault();
            foreach (var k in pb.Keys)
            {
                bool hadVs = pb.Vs.TryGetValue(k, out var v);
                bool hadPs = pb.Ps.TryGetValue(k, out var pv);
                if (!hadVs && !hadPs) continue;
                if (!hadVs) v = anyVs;
                if (!hadPs) pv = anyPs;
                if (v.hlsl == null && pv.hlsl == null) continue;
                var merged = Emit.ShaderLab.Merge(
                    v.hlsl != null ? new Emit.ShaderLab.Stage { Hlsl = v.hlsl, Entry = "vert" } : null,
                    pv.hlsl != null ? new Emit.ShaderLab.Stage { Hlsl = pv.hlsl, Entry = "frag" } : null);
                if (merged == null) continue;
                chain.Guards.Add(VariantPlan.Guard(groups2, hadVs ? v.kw : pv.kw));
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

        /// <summary>UnityEngine.Rendering.BlendOp.</summary>
        private static readonly Dictionary<float, string> BlendOpNames = new()
        {
            [0] = "Add", [1] = "Sub", [2] = "RevSub", [3] = "Min", [4] = "Max",
            // Logical operations (Unity 2018.1+); the number is the same enum.
            [5] = "LogicalClear", [6] = "LogicalSet",
            [7] = "LogicalCopy", [8] = "LogicalCopyInverted",
            [9] = "LogicalNoop", [10] = "LogicalInvert",
            [11] = "LogicalAnd", [12] = "LogicalNand",
            [13] = "LogicalOr", [14] = "LogicalNor",
            [15] = "LogicalXor", [16] = "LogicalEquiv",
            [17] = "LogicalAndReverse", [18] = "LogicalAndInverted",
            [19] = "LogicalOrReverse", [20] = "LogicalOrInverted",
        };

        /// <summary>UnityEngine.Rendering.CompareFunction, used by ZTest and Stencil Comp.</summary>
        private static readonly Dictionary<float, string> CompNames = new()
        {
            [0] = "Disabled", [1] = "Never", [2] = "Less", [3] = "Equal", [4] = "LessEqual",
            [5] = "Greater", [6] = "NotEqual", [7] = "GreaterEqual", [8] = "Always",
        };

        /// <summary>UnityEngine.Rendering.StencilOp.</summary>
        private static readonly Dictionary<float, string> StencilOpNames = new()
        {
            [0] = "Keep", [1] = "Zero", [2] = "Replace",
            [3] = "IncrSat", [4] = "DecrSat", [5] = "Invert",
            [6] = "IncrWrap", [7] = "DecrWrap",
        };

        /// <summary>ShaderLab's name for a numeric state value, or the number itself.</summary>
        private static string StateName(string kind, float value)
        {
            var table = kind switch
            {
                "ZTest" => ZTestNames,
                "Cull" => CullNames,
                "BlendOp" => BlendOpNames,
                "Comp" => CompNames,
                "StencilOp" => StencilOpNames,
                _ => BlendNames,
            };
            return table.TryGetValue(value, out var n)
                ? n : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Run the constant-buffer analysis for one program.
        ///
        /// Everything it needs comes from the asset, not from the disassembly, which
        /// carries no RDEF: the pass's name table lists the buffers, the parameter blob
        /// gives each buffer's size and members, the Properties block says which names
        /// are material properties, and the program's own reflection is the
        /// authoritative <c>$Globals</c> member list when Unity kept it.
        /// </summary>
        private static Resolve.CbResolver NameConstantBuffers(
            Ast.Module m, string shaderName, Pass pass, SubProgram sub,
            List<Property> props, Dictionary<int, List<BlobTool.CbInfo>> paramCbs,
            List<BlobTool.CbInfo> allParamCbs,
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
                m, nameTable, props, own, -1, BestGlobals(sub), null,
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
