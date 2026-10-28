using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShaderRipper.Data;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// Declares Unity's own globals through includes instead of re-declaring them.
    ///
    /// Unity resolves every identifier inside a program block against its built-in
    /// property table, so re-declaring <c>unity_MatrixVP</c> makes the ShaderLab
    /// compiler reject the file.
    ///
    /// The table (<c>data/unity_engine_names.json</c>, include file -> the names it
    /// declares) comes from Unity's own CGIncludes, so a name is only listed when a
    /// real file declares it.
    /// </summary>
    internal static class UnityIncludes
    {
        private const string FileName = "unity_engine_names.json";

        /// <summary>
        /// Replace engine-owned declarations with <c>#include</c>.  Off emits the
        /// file self-contained, which compiles but cannot be re-imported by the
        /// editor.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>name -> the include file that declares it.</summary>
        private static Dictionary<string, string> _home;

        /// <summary>
        /// Names an include declares only inside a preprocessor guard.
        ///
        /// <c>unity_ProbeVolumeParams</c> lives in <c>UnityShaderVariables.cginc</c>
        /// but only below <c>#if UNITY_LIGHT_PROBE_PROXY_VOLUME</c>, and
        /// <c>unity_MatrixVP</c> only below <c>#if !defined(USING_STEREO_MATRICES)</c>.
        /// Handing such a name to the include would leave the body referencing an
        /// identifier the preprocessor deleted, so the emitter keeps declaring it
        /// itself — that is the one case where a clash cannot happen, because the
        /// include said nothing.
        /// </summary>
        private static HashSet<string> _conditional;

        /// <summary>
        /// True once the tables are published.  Volatile: <see cref="Ensure"/> reads
        /// it outside the lock, so a worker that sees it set must also see the
        /// dictionaries written before it.
        /// </summary>
        private static volatile bool _tried;

        /// <summary>Names Unity declares as a matrix (float4x4 and friends).</summary>
        private static HashSet<string> _matrices;

        /// <summary>Names the table knows, or -1 when it is missing.</summary>
        public static int Count
        {
            get { Ensure(); return _home?.Count ?? -1; }
        }

        private static readonly object _gate = new();

        /// <summary>
        /// Load the include index once, under a lock.
        ///
        /// Lazy and lock-free let a <c>--jobs</c> worker see <c>_tried</c> set by
        /// another that was still filling the tables, and answer "no include declares
        /// this name" for a name the table does declare -- which silently moves the
        /// declaration into the shader's own buffers.
        ///
        /// The flag is tested before and after: this is on the hot path (once per
        /// declaration of every program), so after the first load the volatile read
        /// returns without touching the monitor.
        /// </summary>
        private static void Ensure()
        {
            if (_tried) return;
            lock (_gate)
            {
                if (_tried) return;
                Load();
                _tried = true;      // published last: the tables are complete here
            }
        }

        private static void Load()
        {
            string path = Paths.Find(Path.Combine("data", FileName));
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path),
                    new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    });
                if (!doc.RootElement.TryGetProperty("includes", out var inc) ||
                    inc.ValueKind != JsonValueKind.Object) return;

                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var files = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var file in inc.EnumerateObject())
                {
                    if (file.Value.ValueKind != JsonValueKind.Array) continue;
                    var declared = new List<string>(file.Value.GetArrayLength());
                    foreach (var e in file.Value.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.String) continue;
                        string n = e.GetString();
                        if (string.IsNullOrEmpty(n)) continue;
                        declared.Add(n);
                        if (!map.ContainsKey(n)) map[n] = file.Name;
                        if (!byName.TryGetValue(n, out var homes))
                            byName[n] = homes = new List<string>(2);
                        homes.Add(file.Name);
                    }
                    files[file.Name] = declared;
                }
                _home = map;
                _providers = byName;
                _includes = files;

                // `conditional` lists the names an include declares only under a
                // preprocessor guard.  Those keep their own declaration: the
                // guard would delete the include's copy for a shader that does
                // not carry the matching pragma.
                if (doc.RootElement.TryGetProperty("conditional", out var cond))
                {
                    var cset = new HashSet<string>(StringComparer.Ordinal);
                    if (cond.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in cond.EnumerateArray())
                            if (e.ValueKind == JsonValueKind.String)
                            {
                                string n = e.GetString();
                                if (!string.IsNullOrEmpty(n)) cset.Add(n);
                            }
                    }
                    else if (cond.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var e in cond.EnumerateObject())
                            if (!string.IsNullOrEmpty(e.Name)) cset.Add(e.Name);
                    }
                    _conditional = cset;
                }

                // `matrix` lists the names Unity declares as a matrix.  The
                // resolver consults it where the asset reflection cannot answer --
                // a stage reading one column of a global float4x4 records only that
                // column, so nothing in the data says the member spans four rows.
                if (doc.RootElement.TryGetProperty("matrix", out var mats) &&
                    mats.ValueKind == JsonValueKind.Array)
                {
                    var set = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var e in mats.EnumerateArray())
                        if (e.ValueKind == JsonValueKind.String)
                        {
                            string n = e.GetString();
                            if (!string.IsNullOrEmpty(n)) set.Add(n);
                        }
                    _matrices = set;
                }
            }
            catch { _home = null; _matrices = null; _conditional = null; }
        }

        /// <summary>
        /// The include that declares <paramref name="name"/>, or null when no
        /// CGInclude does.  A matrix row is spelled <c>name[i]</c>, so the index and
        /// a trailing swizzle are stripped first.
        /// </summary>
        public static string IncludeFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Ensure();
            if (_home == null) return null;

            int dot = name.IndexOf('.');
            string bas = dot > 0 ? name[..dot] : name;
            int br = bas.IndexOf('[');
            if (br > 0) bas = bas[..br];
            return _home.TryGetValue(bas, out var file) ? file : null;
        }

        /// <summary>Is this a name some CGInclude declares (and we can therefore drop)?</summary>
        public static bool IsEngineMember(string name) => IncludeFor(name) != null;

        /// <summary>
        /// Does <paramref name="name"/> only reach the include behind a
        /// preprocessor guard?  Such a name must keep its own declaration, since
        /// the include's copy disappears for any shader without the matching
        /// pragma.
        /// </summary>
        public static bool IsConditional(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            Ensure();
            if (_conditional == null) return false;
            int cut = name.IndexOfAny(new[] { '[', '.' });
            string bas = cut > 0 ? name[..cut] : name;
            return _conditional.Contains(bas);
        }

        /// <summary>
        /// Does some include declare <paramref name="name"/> outside every guard?
        ///
        /// This is the complement of <see cref="IsConditional"/> among the names
        /// the table knows, and it is the weaker of the two answers: the same name
        /// can be declared unconditionally in one file and under a guard in
        /// another, in which case the unconditional declaration is the one that
        /// always exists.
        /// </summary>
        public static bool IsUnconditional(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            Ensure();
            if (_home == null) return false;
            int cut = name.IndexOfAny(new[] { '[', '.' });
            string bas = cut > 0 ? name[..cut] : name;
            return _home.ContainsKey(bas) && !(_conditional?.Contains(bas) ?? false);
        }

        /// <summary>
        /// Does Unity declare this name as a matrix (<c>float4x4</c>)?
        ///
        /// The resolver needs this for a global that a stage reads only one column
        /// of: the parameter blob records that column and nothing else, so the
        /// asset reflection cannot tell a <c>float4x4</c> from a <c>float4</c>, and
        /// the remaining rows would stay unnamed.
        /// </summary>
        public static bool IsMatrixMember(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            Ensure();
            if (_matrices == null) return false;
            int br = name.IndexOf('[');
            if (br > 0) name = name[..br];
            return _matrices.Contains(name);
        }

        /// <summary>Include file -> names dropped from this program.</summary>
        ///
        /// Per-thread, like the two fields below it: this is state of the program
        /// being emitted, reset at the start of every <c>ShaderLab.Merge</c>.  As
        /// process-wide statics, two shaders exported with `--jobs` shared one set
        /// and each added the other's includes -- which changes which declarations
        /// are handed to the cginc and therefore the generated HLSL.
        [ThreadStatic]
        private static Dictionary<string, SortedSet<string>> _dropped;

        /// <summary>
        /// name -> every include that declares it, cheapest file first.
        ///
        /// One name is usually declared by several cgincs.  Picking the file with the
        /// fewest declarations minimises the chance of dragging in something the
        /// program also declares -- <c>SpeedTreeCommon.cginc</c> declares
        /// <c>sampler2D _MainTex</c>, which clashes with a body that declares
        /// <c>Texture2D _MainTex</c>.
        /// </summary>
        private static Dictionary<string, List<string>> _providers;

        /// <summary>Names the program declares itself, so an include must not.</summary>
        [ThreadStatic]
        private static HashSet<string> _programNames;

        /// <summary>Names already supplied by an include chosen for this program.</summary>
        [ThreadStatic]
        private static HashSet<string> _covered;

        /// <summary>The current thread's dropped-include set, created on first use.</summary>
        private static Dictionary<string, SortedSet<string>> Dropped =>
            _dropped ??= new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        private static HashSet<string> Covered =>
            _covered ??= new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Tell the resolver which names the program is about to declare on its
        /// own.  An include that declares any of them cannot be added: Unity
        /// rejects a second declaration of a texture or a global.
        /// </summary>
        public static void SetProgramNames(IEnumerable<string> names)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            _programNames = set;
            if (names == null) return;
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                int cut = n.IndexOfAny(new[] { '[', '.' });
                set.Add(cut > 0 ? n[..cut] : n);
            }
        }

        /// <summary>True once something was dropped in this program.</summary>
        public static bool Any => _dropped is { Count: > 0 };

        public static void Reset()
        {
            Dropped.Clear();
            Covered.Clear();
            _programNames = null;
        }

        /// <summary>
        /// Pick the include that supplies <paramref name="name"/> without colliding
        /// with anything the program or a previously chosen include declares, or null
        /// when no file can carry it -- in which case the caller keeps its own
        /// declaration.
        /// </summary>
        private static string ChooseProvider(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Ensure();
            if (_providers == null) return null;

            int cut = name.IndexOfAny(new[] { '[', '.' });
            string bas = cut > 0 ? name[..cut] : name;
            if (!_providers.TryGetValue(bas, out var candidates)) return null;

            foreach (string file in candidates.OrderBy(f => SafeCount(f)).ThenBy(f => f, StringComparer.Ordinal))
            {
                if (Dropped.ContainsKey(file)) return file;    // already added
                if (Conflicts(file, bas)) continue;
                return file;
            }
            return null;
        }

        private static int SafeCount(string file)
        {
            Ensure();
            return _includes != null && _includes.TryGetValue(file, out var names)
                ? names.Count : int.MaxValue;
        }

        /// <summary>
        /// Would adding <paramref name="file"/> declare a name that is already
        /// declared by the program body or by a chosen include?
        ///
        /// <paramref name="requested"/> is exempt: it is the very name the include
        /// is being added for, so the program will not keep its own copy.
        /// </summary>
        private static bool Conflicts(string file, string requested)
        {
            Ensure();
            if (_includes == null || !_includes.TryGetValue(file, out var names)) return false;
            foreach (string n in names)
            {
                if (string.Equals(n, requested, StringComparison.Ordinal)) continue;
                if (_programNames != null && _programNames.Contains(n)) return true;
                if (Covered.Contains(n)) return true;
            }
            return false;
        }

        /// <summary>
        /// File -> the names it declares unconditionally.  Needed to answer
        /// <see cref="Conflicts"/> without re-reading the JSON.
        /// </summary>
        private static Dictionary<string, List<string>> _includes;

        public static bool NoteMember(string name)
        {
            string file = ChooseProvider(name);
            if (file == null) return false;
            if (!Dropped.TryGetValue(file, out var set))
                Dropped[file] = set = new SortedSet<string>(StringComparer.Ordinal);
            // Record the base name, not the `name[i]` row spelling.
            int br = name.IndexOf('[');
            string bas = br > 0 ? name[..br] : name;
            set.Add(bas);
            if (_includes != null && _includes.TryGetValue(file, out var declared))
                foreach (string n in declared) Covered.Add(n);
            return true;
        }

        /// <summary>
        /// The include lines for this program, one per file that was drawn on.
        ///
        /// <c>UnityCG.cginc</c> already includes the shader-variable, utility and
        /// instancing headers, so those fold into it rather than costing three
        /// extra lines in every program block.
        /// </summary>
        public static IEnumerable<string> Lines()
        {
            var dropped = Dropped;
            var direct = new List<string>(dropped.Keys);
            bool viaCg = direct.Contains("UnityShaderVariables.cginc")
                      || direct.Contains("UnityShaderUtilities.cginc")
                      || direct.Contains("UnityInstancing.cginc")
                      || direct.Contains("UnityCG.cginc");

            var lines = new List<string>();
            if (viaCg)
            {
                var names = new List<string>();
                foreach (var pair in dropped)
                    if (pair.Key is "UnityCG.cginc" or "UnityShaderVariables.cginc"
                        or "UnityShaderUtilities.cginc" or "UnityInstancing.cginc")
                        names.AddRange(pair.Value);
                lines.Add(Note("UnityCG.cginc", names));
            }

            foreach (string f in direct.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (f is "UnityCG.cginc" or "UnityShaderVariables.cginc"
                     or "UnityShaderUtilities.cginc" or "UnityInstancing.cginc")
                    continue;
                lines.Add(Note(f, dropped[f]));
            }
            return lines;
        }

        private static string Note(string file, IEnumerable<string> names)
        {
            var list = names.Distinct(StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal).ToList();
            string what = list.Count == 0 ? "" : "   // " + string.Join(", ", list);
            return $"#include \"{file}\"{what}";
        }
    }
}
