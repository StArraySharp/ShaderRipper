using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShaderRipper.Antlr.Data;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// Declares Unity's own globals through includes instead of re-declaring them.
    ///
    /// Unity resolves every identifier inside a program block against its built-in
    /// property table, and a <c>cbuffer</c> member is no exception.  Re-declaring
    /// <c>unity_MatrixVP</c> therefore makes the ShaderLab compiler reject the file:
    ///
    /// <code>
    /// Shader has a float property 'unity_MatrixVP' which clashes with a built-in
    /// non-float property name.  Use some other name!
    /// </code>
    ///
    /// The table is <c>data/unity_engine_names.json</c>: include file -> the names
    /// that file declares.  It is generated from Unity's own CGIncludes, so a name
    /// is only listed when a real file declares it -- removing a name nothing
    /// declares would leave the body referencing an undefined identifier.
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

        private static bool _tried;

        /// <summary>Names Unity declares as a matrix (float4x4 and friends).</summary>
        private static HashSet<string> _matrices;

        /// <summary>Names the table knows, or -1 when it is missing.</summary>
        public static int Count
        {
            get { Ensure(); return _home?.Count ?? -1; }
        }

        private static void Ensure()
        {
            if (_tried) return;
            _tried = true;

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
                foreach (var file in inc.EnumerateObject())
                {
                    if (file.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var e in file.Value.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.String) continue;
                        string n = e.GetString();
                        if (!string.IsNullOrEmpty(n) && !map.ContainsKey(n)) map[n] = file.Name;
                    }
                }
                _home = map;

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
            catch { _home = null; _matrices = null; }
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
        private static readonly Dictionary<string, SortedSet<string>>
            _dropped = new(StringComparer.Ordinal);

        /// <summary>True once something was dropped in this program.</summary>
        public static bool Any => _dropped.Count > 0;

        public static void NoteMember(string name)
        {
            string file = IncludeFor(name);
            if (file == null) return;
            if (!_dropped.TryGetValue(file, out var set))
                _dropped[file] = set = new SortedSet<string>(StringComparer.Ordinal);
            // Record the base name, not the `name[i]` row spelling.
            int br = name.IndexOf('[');
            set.Add(br > 0 ? name[..br] : name);
        }

        public static void Reset() => _dropped.Clear();

        /// <summary>
        /// The include lines for this program, one per file that was drawn on.
        ///
        /// <c>UnityCG.cginc</c> already includes the shader-variable, utility and
        /// instancing headers, so those fold into it rather than costing three
        /// extra lines in every program block.
        /// </summary>
        public static IEnumerable<string> Lines()
        {
            var direct = new List<string>(_dropped.Keys);
            bool viaCg = direct.Contains("UnityShaderVariables.cginc")
                      || direct.Contains("UnityShaderUtilities.cginc")
                      || direct.Contains("UnityInstancing.cginc")
                      || direct.Contains("UnityCG.cginc");

            var lines = new List<string>();
            if (viaCg)
            {
                var names = new List<string>();
                foreach (var pair in _dropped)
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
                lines.Add(Note(f, _dropped[f]));
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
