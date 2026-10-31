using System;
using System.Globalization;
using ShaderRipper.Localization;

namespace ShaderRipper.Game
{
    /// <summary>
    /// How much the export prints while it runs.
    ///
    /// Three levels, matching the three questions a person actually asks:
    /// <c>Quiet</c> — "did it work?" (the tally only); <c>Normal</c> — "what did
    /// it make?" (one line per shader); <c>Verbose</c> — "why did it take that
    /// long / why is this missing?" (per-file headings, timings, structure).
    ///
    /// The per-shader line is deliberately uniform: it can be read with
    /// <c>grep</c> and <c>cut</c>, which is how a 400-shader run gets inspected.
    /// </summary>
    internal enum LogLevel
    {
        Quiet = 0,
        Normal = 1,
        Verbose = 2,
    }

    /// <summary>
    /// Writes the progress log shared by the exporter and the front end.
    ///
    /// Every line has a stable, greppable shape (<c>level subject -> detail</c>)
    /// aligned so a column reads as a table, and writes are guarded so a broken
    /// terminal or a closed pipe cannot take an export down mid-way.
    /// </summary>
    internal static class Log
    {
        public static LogLevel Level = LogLevel.Normal;

        /// <summary>
        /// One line as a caller without a console wants it.
        ///
        /// <c>Done</c>/<c>Total</c> come from the same counter the verbose console
        /// line prints, so a front end can draw a real progress bar instead of
        /// guessing from timestamps.
        /// </summary>
        public sealed class Line
        {
            /// <summary>"info", "skip", "dup", "warn", "error", "detail", "file",
            /// "summary" or "note".</summary>
            public string Kind = "";
            public string Subject = "";
            public string Detail;
            public int Done;
            public int Total;
        }

        /// <summary>
        /// Where a front end that draws its own log subscribes.
        ///
        /// Called on whichever thread produced the line, so an implementation must
        /// marshal to its UI thread itself.  Exceptions from the sink are swallowed:
        /// a broken window must not abort an export.
        /// </summary>
        public static Action<Line> Sink;

        /// <summary>Progress counter for the current run (0/0 = not set).</summary>
        private static int _done, _total;

        /// <summary>Width reserved for the shader-name column.</summary>
        private const int NameWidth = 44;

        /// <summary>Set the progress counter the info lines carry while verbose.</summary>
        public static void Expect(int total)
        {
            _done = 0;
            _total = total;
        }

        /// <summary>A shader was written.  The workhorse line of a normal run.</summary>
        public static void Info(string shader, string relativePath, string source)
        {
            // Exports can run in parallel, and the counter is the only piece of
            // shared state on this path.  Console.Out is synchronised already, so
            // the line itself stays whole; only the increment needs to be atomic.
            int done = System.Threading.Interlocked.Increment(ref _done);
            if (Level < LogLevel.Normal) return;
            string counter = Level >= LogLevel.Verbose && _total > 0
                ? $"[{done}/{_total}] " : "";
            Write(Strings.Get("LogInfo"), counter + Quote(shader, NameWidth),
                  "→ " + relativePath, source == null ? null : "(" + source + ")");
        }

        /// <summary>The per-shader structure, printed under the info line when verbose.</summary>
        public static void Detail(string stats, long elapsedMs)
        {
            if (Level < LogLevel.Verbose) return;
            string elapsed = elapsedMs >= 0
                ? Strings.F("LogElapsed", elapsedMs.ToString(CultureInfo.InvariantCulture))
                : null;
            Write("", stats, elapsed, null);
        }

        /// <summary>A heading for one input file, so a long run says where it is.</summary>
        public static void File(string inputName, int shaderCount)
        {
            if (Level < LogLevel.Verbose) return;
            Write(Strings.Get("LogRead"), inputName,
                  Strings.F("LogReadShaders", shaderCount), null);
        }

        /// <summary>A shader left out on purpose (Unity's own).</summary>
        public static void Skip(string shader, string reason)
        {
            if (Level < LogLevel.Normal) return;
            Write(Strings.Get("LogSkip"), Quote(shader, NameWidth), reason, null);
        }

        /// <summary>A duplicate copy dropped by --dedup.</summary>
        public static void Dup(string shader, string keptPath, string keptSource, string thisSource)
        {
            if (Level < LogLevel.Normal) return;
            string reason = keptSource == null
                ? Strings.F("LogKeptFrom", keptPath)
                : Strings.F("LogKeptSource", keptPath, keptSource);
            if (thisSource != null) reason += "  " + Strings.F("LogThisCopy", thisSource);
            Write(Strings.Get("LogDup"), Quote(shader, NameWidth), reason, null);
        }

        /// <summary>Something the export had to work around.</summary>
        public static void Warn(string shader, string message)
        {
            if (Level < LogLevel.Normal) return;
            Write(Strings.Get("LogWarn"), Quote(shader, NameWidth), message, null);
        }

        /// <summary>A shader could not be written.</summary>
        public static void Error(string shader, string message)
        {
            if (Level < LogLevel.Quiet) return;
            Write(Strings.Get("LogError"), Quote(shader, NameWidth), message, null);
        }

        /// <summary>The tally at the end of a run.</summary>
        public static void Summary(string line)
        {
            try { Console.WriteLine(line); } catch { }
            Emit("summary", line, null);
        }

        /// <summary>
        /// A diagnostic note under the summary.
        ///
        /// Shown at every level, including <c>--quiet</c>: the tally alone says a
        /// run failed without saying why, and the notes are where the "why" lives.
        /// A caller that truly wants silence can clear the list before printing.
        /// </summary>
        public static void Note(string note)
        {
            try { Console.WriteLine("  " + note); } catch { }
            Emit("note", note, null);
        }

        /// <summary>
        /// A shader name in quotes, padded to a fixed display width.
        ///
        /// Padding counts display columns (a CJK character is two), so a run of
        /// shader names that are half Chinese still lines up.
        /// </summary>
        private static string Quote(string shader, int width)
        {
            string q = "\"" + shader + "\"";
            int cols = 0;
            foreach (char c in q) cols += IsWide(c) ? 2 : 1;
            return cols >= width ? q : q + new string(' ', width - cols);
        }

        /// <summary>
        /// One log line.
        ///
        /// Layout: <c>level subject detail (trailer)</c>, with the level tag padded
        /// to a fixed display width so a column of lines reads as a table.  A
        /// missing part is omitted rather than padded, so a line never ends in a
        /// run of spaces.
        /// </summary>
        private static void Write(string level, string subject, string detail, string trailer,
                                  string kind = null)
        {
            try
            {
                string s = PadTag(level) + " " + subject;
                if (detail != null) s += " " + detail;
                if (trailer != null) s += "  " + trailer;
                Console.WriteLine(s.TrimEnd());
            }
            catch
            {
                // A closed pipe or a redirected handle that refuses writes must not
                // abort an export that is otherwise fine.
            }

            Emit(kind ?? TagKind(level), subject, detail ?? trailer);
        }

        /// <summary>Hand a line to the sink, if one is installed.</summary>
        private static void Emit(string kind, string subject, string detail)
        {
            var sink = Sink;
            if (sink == null) return;
            try
            {
                sink(new Line
                {
                    Kind = kind,
                    Subject = subject ?? "",
                    Detail = detail,
                    Done = _done,
                    Total = _total,
                });
            }
            catch
            {
                // See Sink: the window is not allowed to break the export.
            }
        }

        /// <summary>
        /// The machine-readable name of a level tag.
        ///
        /// The tag itself is localized (it is printed), so it cannot be matched
        /// against a fixed string; this maps the tag the caller used.
        /// </summary>
        private static string TagKind(string level)
        {
            if (level.Length == 0) return "detail";
            if (level == Strings.Get("LogInfo")) return "info";
            if (level == Strings.Get("LogSkip")) return "skip";
            if (level == Strings.Get("LogDup")) return "dup";
            if (level == Strings.Get("LogWarn")) return "warn";
            if (level == Strings.Get("LogError")) return "error";
            if (level == Strings.Get("LogRead")) return "file";
            return "info";
        }

        /// <summary>Pad a level tag to six display columns, counting CJK as two.</summary>
        private static string PadTag(string tag)
        {
            int width = 0;
            foreach (char c in tag) width += IsWide(c) ? 2 : 1;
            return width >= 6 ? tag : tag + new string(' ', 6 - width);
        }

        /// <summary>CJK, fullwidth forms and CJK punctuation occupy two columns.</summary>
        internal static bool IsWide(char c) =>
            (c >= 0x1100 && c <= 0x115F) ||   // Hangul Jamo
            (c >= 0x2E80 && c <= 0xA4CF) ||   // CJK radicals .. Yi
            (c >= 0xAC00 && c <= 0xD7A3) ||   // Hangul syllables
            (c >= 0xF900 && c <= 0xFAFF) ||   // CJK compatibility ideographs
            (c >= 0xFE30 && c <= 0xFE6F) ||   // CJK compatibility forms
            (c >= 0xFF00 && c <= 0xFF60) ||   // fullwidth forms
            (c >= 0xFFE0 && c <= 0xFFE6);
    }
}
