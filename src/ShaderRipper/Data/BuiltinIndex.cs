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

        /// <summary>The index file that was loaded, or null.</summary>
        public static string ResolvedPath { get; private set; }

        /// <summary>Number of names in the index, -1 when not loaded.</summary>
        public static int Count => _names?.Count ?? -1;

        public static int Load(string explicitPath = null)
        {
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

        public static bool Contains(string name) =>
            !string.IsNullOrEmpty(name) && _names != null && _names.Contains(name);
    }
}
