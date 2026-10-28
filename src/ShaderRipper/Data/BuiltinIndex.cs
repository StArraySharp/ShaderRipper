using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Index of Unity's built-in shaders.
    ///
    /// Whether a shader is the engine's or the game's cannot be guessed from a name
    /// prefix, and the source file does not settle it either -- a build copies its
    /// built-ins into resources.assets.  The only evidence is the engine's own
    /// resource listing, dumped once from <c>Resources/unity_builtin_extra</c> into
    /// <c>data/unity_builtin_shaders.txt</c>.
    ///
    /// Re-run that dump after an editor upgrade; the file is plain text and can also
    /// be appended to by hand.
    /// </summary>
    internal static class BuiltinIndex
    {
        private const string FileName = "unity_builtin_shaders.txt";

        private static HashSet<string> _names;
        /// <summary>One look-up attempt is enough: a missing file must not cost a stat per shader.</summary>
        private static bool _tried;

        /// <summary>The index file that was loaded, or null.</summary>
        public static string ResolvedPath { get; private set; }

        /// <summary>Number of names in the index, -1 when not loaded.</summary>
        public static int Count => _names?.Count ?? -1;

        public static int Load(string explicitPath = null)
        {
            _tried = true;
            string path = explicitPath ?? Paths.Find(Path.Combine("data", FileName));
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return -1;

            _names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path))
            {
                string n = line.Trim();
                if (n.Length > 0 && !n.StartsWith("#", StringComparison.Ordinal)) _names.Add(n);
            }
            ResolvedPath = path;
            return _names.Count;
        }

        /// <summary>
        /// Is this one of Unity's own shaders?
        ///
        /// Loads the index on first use.  The command line loads it at start-up so
        /// it can report a missing file, but a library host (a GUI, an importer) has
        /// no such start-up hook -- and without the index <c>Contains</c> answers
        /// false for everything, quietly turning "skip Unity's shaders" into "rip
        /// them too".
        /// </summary>
        public static bool Contains(string name)
        {
            if (_names == null && !_tried) Load();
            return !string.IsNullOrEmpty(name) && _names != null && _names.Contains(name);
        }
    }
}
