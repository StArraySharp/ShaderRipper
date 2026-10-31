using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShaderRipper.Game
{
    /// <summary>
    /// Remembers which .shader files a previous run wrote, so a re-run can leave
    /// the unchanged ones alone.
    ///
    /// The expensive half of an export is the translation (disassembly plus
    /// emitter), not the write, so the unit of reuse is one shader: its output
    /// file is kept when the bytes that produced it are the same bytes as last
    /// time.  "The same bytes" is the input file's identity (path, size, write
    /// time) plus the object's path id, because the shader's own serialized body
    /// is not retained once it has been parsed -- and a changed input file is the
    /// only way any of it can change.
    ///
    /// Two things invalidate the whole cache: a different build of the library
    /// (its module version id is part of the profile) and any option that changes
    /// the output.  A cache that survived a code change would hand back the old
    /// text with no way to notice, which is worse than no cache at all.
    /// </summary>
    internal sealed class ExportCache
    {
        /// <summary>Bumped when the on-disk shape below changes.</summary>
        private const int Format = 1;

        private const string FileName = ".shaderripper-cache.json";

        /// <summary>The cache file's own name, so the export directory recognises it.</summary>
        public static string CacheFileName => FileName;

        private sealed class Entry
        {
            public string Source { get; set; } = "";
            public string Hash { get; set; } = "";
        }

        private sealed class Document
        {
            public int Format { get; set; }
            public string Profile { get; set; } = "";
            public Dictionary<string, Entry> Entries { get; set; } = new();
        }

        private readonly string _path;
        private readonly string _profile;
        private Document _doc = new();

        private ExportCache(string dir, string profile)
        {
            _path = Path.Combine(dir, FileName);
            _profile = profile;
        }

        /// <summary>
        /// Open the cache in <paramref name="dir"/>; a missing, unreadable or
        /// mismatched file simply starts empty.
        /// </summary>
        /// <param name="profile">
        /// Everything besides the input that decides what the output looks like:
        /// the library build, the output switches and the platform filter.
        /// </param>
        public static ExportCache Load(string dir, string profile)
        {
            var c = new ExportCache(dir, profile);
            try
            {
                if (!System.IO.File.Exists(c._path)) return c;
                var doc = JsonSerializer.Deserialize<Document>(System.IO.File.ReadAllText(c._path));
                if (doc == null || doc.Format != Format ||
                    !string.Equals(doc.Profile, profile, StringComparison.Ordinal))
                    return c;
                c._doc = doc;
            }
            catch (Exception e) when (e is IOException || e is JsonException ||
                                      e is UnauthorizedAccessException)
            {
                // A corrupt cache is a reason to translate everything, never to fail.
            }
            return c;
        }

        /// <summary>
        /// The identity of one shader's input.  Null when the file it came from
        /// cannot be inspected, which makes the entry unusable and the shader
        /// re-translated.
        /// </summary>
        public static string SourceKey(string file, long pathId)
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists) return null;
                return $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{pathId}";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// True when this shader's output file is already the one this input would
        /// produce again: the input identity matches and the file on disk is
        /// byte-for-byte what the last run wrote.
        ///
        /// The hash is checked rather than the timestamp because a file the user
        /// edited in place -- the one case where reusing would silently discard
        /// their work -- keeps its identity but not its content.
        /// </summary>
        public bool IsCurrent(string rel, string sourceKey, string outDir)
        {
            if (sourceKey == null) return false;
            if (!_doc.Entries.TryGetValue(rel, out var e) || e.Source != sourceKey) return false;

            string full = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (!System.IO.File.Exists(full)) return false;
                return Hash(File.ReadAllBytes(full)) == e.Hash;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Record what a shader's output file now contains.</summary>
        public void Record(string rel, string sourceKey, string outDir)
        {
            if (sourceKey == null) return;
            string full = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (!System.IO.File.Exists(full)) return;
                _doc.Entries[rel] = new Entry { Source = sourceKey, Hash = Hash(File.ReadAllBytes(full)) };
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Not recording is the safe failure: next run translates this one again.
            }
        }

        /// <summary>Forget entries whose file no longer exists.</summary>
        public void Prune(string outDir, IEnumerable<string> kept)
        {
            var live = new HashSet<string>(kept, StringComparer.Ordinal);
            foreach (string gone in _doc.Entries.Keys.Where(k => !live.Contains(k)).ToList())
                _doc.Entries.Remove(gone);
        }

        /// <summary>
        /// Write the cache back, atomically: a run killed mid-write must not leave
        /// a half-written file that the next run has to guess about.
        /// </summary>
        public void Save()
        {
            if (_doc.Entries.Count == 0)
            {
                try { if (System.IO.File.Exists(_path)) System.IO.File.Delete(_path); } catch { }
                return;
            }
            _doc.Format = Format;
            _doc.Profile = _profile;
            try
            {
                string tmp = _path + ".tmp";
                System.IO.File.WriteAllText(tmp, JsonSerializer.Serialize(_doc,
                    new JsonSerializerOptions { WriteIndented = false }));
                System.IO.File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                try { System.IO.File.Delete(_path + ".tmp"); } catch { }
            }
        }

        /// <summary>
        /// The identity of the build and the switches that produced the output.
        /// The module version id changes on every compile, which is exactly the
        /// granularity needed: a rebuilt library may emit different text for the
        /// same input.
        /// </summary>
        public static string ProfileOf(params string[] switches)
        {
            var sb = new StringBuilder();
            sb.Append(typeof(ExportCache).Assembly.ManifestModule.ModuleVersionId);
            foreach (string s in switches) sb.Append('|').Append(s ?? "");
            return sb.ToString();
        }

        private static string Hash(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes));
    }
}
