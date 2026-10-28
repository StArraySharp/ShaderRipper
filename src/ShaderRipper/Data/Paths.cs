using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper.Data
{
    /// <summary>
    /// The single place a data path is resolved.
    ///
    /// Lookup order: an explicit argument (<c>--data</c>, <c>--out</c>,
    /// <c>--root</c>, ...), then the <c>SHADERRIP_DATA</c> / <c>SHADERRIP_OUT</c> /
    /// <c>SHADERRIP_ROOT</c> environment variables, then an upward search from the
    /// executable directory and the working directory for a directory holding
    /// <c>schema/</c> or <c>data/</c> (or a repository containing
    /// <c>ShaderRipper.csproj</c>).
    ///
    /// Returns null when nothing is found, so the caller reports it instead of
    /// guessing a plausible-looking path.
    /// </summary>
    internal static class Paths
    {
        private static string _root;

        /// <summary>Extraction directory for embedded resources, created lazily.</summary>
        private static string EmbedDir =>
            _embedDir ??= TryCreateDir(Path.Combine(Path.GetTempPath(), "ShaderRipper", "embed"));
        private static string _embedDir;

        private static string TryCreateDir(string d)
        {
            try { Directory.CreateDirectory(d); return d; }
            catch { return null; }
        }

        /// <summary>
        /// Extract the embedded resource <c>ShaderRipper.Embed.&lt;relative&gt;</c> to
        /// the temporary directory and return its path, or null when there is no such
        /// resource.  Both the '\' and '/' name separators are tried.
        /// </summary>
        public static string ExtractEmbedded(string relative)
        {
            relative = relative.Replace('\\', '/');
            string dir = EmbedDir;
            if (dir == null) return null;
            var asm = typeof(Paths).Assembly;
            foreach (var sep in new[] { '/', '\\' })
            {
                string name = "ShaderRipper.Embed." + relative.Replace('/', sep);
                using var src = asm.GetManifestResourceStream(name);
                if (src == null) continue;
                try
                {
                    string target = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    if (!File.Exists(target) || new FileInfo(target).Length != src.Length)
                        using (var dst = File.Create(target)) src.CopyTo(dst);
                    return target;
                }
                catch { return null; }
            }
            return null;
        }

        /// <summary>
        /// Extract every embedded resource under a directory (e.g.
        /// <c>data/tmp_layouts</c>) and return the extracted directory, or null when
        /// nothing matched.
        /// </summary>
        public static string ExtractEmbeddedDir(string relative)
        {
            relative = relative.Replace('\\', '/').TrimEnd('/');
            string prefix = "ShaderRipper.Embed." + relative + "/";
            var asm = typeof(Paths).Assembly;
            string dir = EmbedDir;
            if (dir == null) return null;
            int extracted = 0;
            foreach (string full in asm.GetManifestResourceNames())
            {
                int at = full.IndexOf(prefix, System.StringComparison.Ordinal);
                if (at != 0) continue;
                string rel = full.Substring(prefix.Length).Replace('\\', '/');
                if (ExtractEmbedded(relative + "/" + rel) != null) extracted++;
            }
            return extracted > 0 ? Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)) : null;
        }

        /// <summary>The resource root (holding <c>schema/</c> and <c>data/</c>), or null.</summary>
        public static string Root => _root ??= Discover();

        /// <summary>Pin the resource root; null or a missing directory falls back to discovery.</summary>
        public static void SetRoot(string dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                _root = Path.GetFullPath(dir);
        }

        /// <summary>
        /// Find a resource file (relative path such as
        /// <c>data/unity_builtin_shaders.txt</c>).  Order: an embedded resource
        /// (extracted), beside the executable, the resource root, then null.
        /// </summary>
        public static string Find(string relative)
        {
            string emb = ExtractEmbedded(relative);
            if (emb != null) return emb;

            string exeDir = AppContext.BaseDirectory;
            var probes = new List<string> { Path.Combine(exeDir, relative) };
            if (Root != null)
            {
                // A checkout keeps the data files under Resources/data; a
                // published build copies them next to the exe, which is why both
                // spellings are probed.
                probes.Add(Path.Combine(Root, "Resources", relative));
                probes.Add(Path.Combine(Root, relative));
            }
            foreach (var p in probes)
                if (File.Exists(p)) return Path.GetFullPath(p);
            return null;
        }

        /// <summary>Find a resource directory (e.g. <c>data/tmp_layouts</c>).</summary>
        public static string FindDir(string relative)
        {
            string emb = ExtractEmbeddedDir(relative);
            if (emb != null) return emb;

            string exeDir = AppContext.BaseDirectory;
            var probes = new List<string> { Path.Combine(exeDir, relative) };
            if (Root != null)
            {
                probes.Add(Path.Combine(Root, "Resources", relative));
                probes.Add(Path.Combine(Root, relative));
            }
            foreach (var p in probes)
                if (Directory.Exists(p)) return Path.GetFullPath(p);
            return null;
        }

        /// <summary>
        /// Cache directory (<c>.temp</c> under the working directory) for diagnostic
        /// dumps and intermediate decode products.  Created on demand; null when it
        /// cannot be created.
        /// </summary>
        public static string TempDir
        {
            get
            {
                if (_tempDir != null && Directory.Exists(_tempDir)) return _tempDir;
                try
                {
                    string d = Path.GetFullPath(".temp");
                    Directory.CreateDirectory(d);
                    return _tempDir = d;
                }
                catch { return null; }
            }
        }
        private static string _tempDir;


        private static string Discover()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                for (int depth = 0; dir != null && depth < 8; depth++, dir = dir.Parent)
                {
                    // A checkout has Resources/data next to the .csproj; a published
                    // build has the files beside the exe (handled by Find's probes).
                    if (Directory.Exists(Path.Combine(dir.FullName, "Resources", "data")) ||
                        File.Exists(Path.Combine(dir.FullName, "ShaderRipper.csproj")))
                        return dir.FullName;
                }
            }
            return null;
        }
    }
}
