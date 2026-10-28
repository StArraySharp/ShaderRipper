using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Localization;

namespace ShaderRipper.Game
{
    /// <summary>
    /// Ownership of the output directory.
    ///
    /// Re-exporting into the same directory used to leave the previous run's files
    /// behind.  A shader that is no longer in the build keeps its `.shader`, its
    /// `.orig.asm` and its `Disassembly/` subtree, so the tree quietly grows a
    /// second, stale copy of the game next to the current one -- and every check
    /// that walks the directory then measures the union of all runs.
    ///
    /// `--overwrite` empties the directory first, so one path can be reused
    /// instead of a new `.temp/yN` per attempt.
    ///
    /// Emptying a directory is destructive, so it only ever happens to a directory
    /// this tool can prove it owns:
    ///
    ///   * it is empty,
    ///   * it carries the marker written by a previous export, or
    ///   * every entry in it is one of the names an export produces
    ///     (`Assets`, `Disassembly`).
    ///
    /// Anything else -- a typo, a game install, somebody's Documents -- is refused
    /// with an explanation instead of being deleted.  A directory that looks like a
    /// game (it holds a `*_Data` folder) is refused even when it also looks like an
    /// export, because that is the one mistake with no way back.
    /// </summary>
    internal static class ExportDir
    {
        /// <summary>Written after a successful export; proof the directory is ours.</summary>
        internal const string Marker = ".shaderripper-export";

        /// <summary>The only top-level names an export creates.</summary>
        private static readonly string[] Produced = { "Assets", "Disassembly", Marker };

        /// <summary>
        /// Make <paramref name="dir"/> ready to receive an export, emptying it first
        /// when <paramref name="overwrite"/> is set.
        /// </summary>
        internal static void Prepare(string dir, bool overwrite)
        {
            Directory.CreateDirectory(dir);
            if (!overwrite) return;

            if (IsEmpty(dir)) return;      // nothing to overwrite yet
            if (LooksLikeGame(dir))
                throw new InvalidOperationException(Strings.F("OverwriteRefused", dir));

            var foreign = Foreign(dir);
            if (foreign.Count > 0)
                throw new InvalidOperationException(
                    Strings.F("OverwriteForeign", dir, string.Join(", ", foreign.Take(4))));

            int pulled = Clear(dir);
            Log.Note(Strings.F("OverwriteCleared", dir, pulled));
        }

        /// <summary>Record that the directory now holds this tool's output.</summary>
        internal static void Finish(string dir, int shaders)
        {
            try
            {
                File.WriteAllText(Path.Combine(dir, Marker),
                    "ShaderRipper export directory\n" +
                    $"shaders: {shaders}\n" +
                    $"at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    "Re-running with --overwrite empties this directory first.\n");
            }
            catch (IOException) { }        // a read-only target is not worth failing over
            catch (UnauthorizedAccessException) { }
        }

        private static bool IsEmpty(string dir) =>
            !Directory.EnumerateFileSystemEntries(dir).Any();

        /// <summary>Is this a game install rather than an export directory?</summary>
        private static bool LooksLikeGame(string dir) =>
            Directory.EnumerateDirectories(dir)
                     .Select(Path.GetFileName)
                     .Any(n => n.EndsWith("_Data", StringComparison.OrdinalIgnoreCase));

        /// <summary>Entries an export would not have produced.</summary>
        private static List<string> Foreign(string dir)
        {
            var foreign = new List<string>();
            foreach (string entry in Directory.EnumerateFileSystemEntries(dir))
            {
                string name = Path.GetFileName(entry);
                if (Produced.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                foreign.Add(name);
            }
            return foreign;
        }

        /// <summary>Delete the directory's contents (not the directory).</summary>
        private static int Clear(string dir)
        {
            int removed = 0;
            foreach (string entry in Directory.EnumerateFileSystemEntries(dir).ToList())
            {
                try
                {
                    Remove(entry);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // A file still held open by something else (an indexer, a virus
                    // scanner that just saw the previous run write it) cannot be
                    // deleted, and a stack trace is the wrong answer: this is the same
                    // class of message as the other two refusals.
                    throw new InvalidOperationException(
                        Strings.F("OverwriteBusy", Path.GetFileName(entry), dir), e);
                }
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// Delete one entry inside the export directory, read-only bits and all.
        ///
        /// `Directory.Delete(recursive: true)` abandons the whole subtree the moment
        /// one entry inside it is read-only -- and on Windows that includes the
        /// DIRECTORY's own read-only bit, which the file-only sweep never touched.
        /// Deleting bottom-up after clearing both keeps a single stubborn entry from
        /// costing the entire export.  The retry covers the other common case: a file
        /// written a moment ago is still held for a few milliseconds by a scanner.
        /// </summary>
        private static void Remove(string path)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
                        var dirs = Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                                            .OrderByDescending(x => x.Length).ToList();

                        foreach (string f in files)
                        {
                            var fi = new FileInfo(f);
                            if (fi.IsReadOnly) fi.IsReadOnly = false;
                        }
                        foreach (string d in dirs)
                        {
                            var di = new DirectoryInfo(d);
                            if ((di.Attributes & FileAttributes.ReadOnly) != 0)
                                di.Attributes &= ~FileAttributes.ReadOnly;
                        }
                        foreach (string f in files) File.Delete(f);
                        foreach (string d in dirs) Directory.Delete(d, false);
                        Directory.Delete(path, false);
                    }
                    else
                    {
                        // Files an earlier run left read-only (a checked-out artifact, a
                        // copy off a read-only share) would make Delete throw.
                        var file = new FileInfo(path);
                        if (file.IsReadOnly) file.IsReadOnly = false;
                        file.Delete();
                    }
                    return;
                }
                catch (Exception e) when (attempt < 3 && (e is IOException || e is UnauthorizedAccessException))
                {
                    System.Threading.Thread.Sleep(150);
                }
            }
        }
    }
}
