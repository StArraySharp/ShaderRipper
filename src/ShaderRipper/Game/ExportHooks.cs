using System;
using System.Collections.Generic;
using ShaderRipper.Plugins;

namespace ShaderRipper.Game
{
    /// <summary>
    /// Runs the registered <see cref="IExportHook"/>s once an export finishes.
    ///
    /// The hooks are driven from here rather than from each export entry point so the
    /// call site is one line and the failure policy is one rule: a hook that throws is
    /// reported and skipped, and the export it was watching stays successful.  A
    /// finished export must not be turned into a failed one by a plugin.
    ///
    /// The event shape is deliberate -- <see cref="Finished"/> lets a host that embeds
    /// the library subscribe without writing a plugin, while the registry path serves
    /// the plugin case.  Both end in <see cref="Raise"/>.
    /// </summary>
    internal static class ExportHooks
    {
        /// <summary>
        /// Raised after an export finishes, for a host that subscribes directly.
        ///
        /// Subscribers run before the registered plugin hooks, so an embedder can
        /// prepare state a plugin then reads.
        /// </summary>
        public static event Action<ExportCompleted> Finished;

        /// <summary>
        /// Run every hook for one finished export.
        ///
        /// <paramref name="context"/> is a snapshot: the exporter is done with it, so a
        /// hook can keep it or hand it to another thread without racing the writer.
        /// </summary>
        internal static void Raise(ExportCompleted context, GameExport.Stats stats)
        {
            if (context == null) return;

            // A cancelled run leaves the directory in an unfinished state; a hook that
            // wrote a manifest would describe output that is not all there.
            if (context.Cancelled) return;

            try { Finished?.Invoke(context); }
            catch (Exception e)
            {
                stats?.Notes.Add($"export hook (host) failed: {e.GetType().Name}: {e.Message}");
            }

            var registry = PluginContext.Registry;
            if (registry == null) return;

            foreach (IExportHook hook in registry.ExportHooks)
            {
                // One hook's failure is that hook's problem: the export is already on
                // disk and a note is what the summary can act on.
                try
                {
                    hook.ExportFinished(context);
                }
                catch (Exception e)
                {
                    stats?.Notes.Add($"export hook {hook.Name} failed: {e.GetType().Name}: {e.Message}");
                }
            }
        }
    }
}
