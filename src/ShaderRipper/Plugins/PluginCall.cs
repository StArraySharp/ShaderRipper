using System;
using System.Collections.Generic;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// Wraps every call into plugin code.
    ///
    /// The rule this codebase already follows for its own risky paths applies doubly
    /// to third-party code: a plugin that throws must not abort an export.  Each call
    /// is isolated, the failure is recorded once -- a plugin that fails on every
    /// program of a 400-shader run would otherwise bury the real diagnostics -- and
    /// the caller gets the "no opinion" value so the pipeline carries on.
    /// </summary>
    public static class PluginCall
    {
        private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);
        private static readonly object Gate = new();

        /// <summary>
        /// Run <paramref name="body"/>, returning <paramref name="fallback"/> and
        /// recording a note if it throws.
        /// </summary>
        public static T Try<T>(string what, Func<T> body, T fallback, ICollection<string> notes)
        {
            try
            {
                return body();
            }
            catch (Exception e)
            {
                Report(what, e, notes);
                return fallback;
            }
        }

        /// <summary>Run a void action, recording a note if it throws.</summary>
        public static bool Try(string what, Action body, ICollection<string> notes)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception e)
            {
                Report(what, e, notes);
                return false;
            }
        }

        private static void Report(string what, Exception e, ICollection<string> notes)
        {
            string key = what + "|" + e.GetType().Name + "|" + e.Message;
            bool first;
            lock (Gate) first = Reported.Add(key);
            if (!first) return;

            string line = $"{what}: {e.GetType().Name}: {e.Message}";
            notes?.Add(line);
            ShaderRipper.Game.Log.Warn("plugin", line);
        }

        /// <summary>
        /// Clear the deduplication set.  Called at the start of a run so two exports in
        /// one process each report their own failures.
        /// </summary>
        internal static void Reset()
        {
            lock (Gate) Reported.Clear();
        }
    }
}
