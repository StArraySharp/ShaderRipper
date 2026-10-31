using System.Collections.Generic;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// Process-wide handle on the installed registry.
    ///
    /// The pipeline is built once, before any translation starts, and then read from
    /// deep inside the export path -- a shader is translated by several layers that
    /// have no reason to carry a plugin reference through their signatures.  This is
    /// the one place that state lives.
    ///
    /// A host that never installs a registry sees <see cref="Registry"/> as null, and
    /// every consumer treats that as "built-in behaviour only", which is what the code
    /// did before plugins existed.
    /// </summary>
    internal static class PluginContext
    {
        /// <summary>The installed registry, or null when plugins were not loaded.</summary>
        public static PluginRegistry Registry { get; private set; }

        /// <summary>
        /// Install a registry and arm the parts of the pipeline that read from it.
        /// Passing null restores built-in-only behaviour, which is what a unit test or
        /// an embedder that wants determinism will do.
        /// </summary>
        public static void Install(PluginRegistry registry)
        {
            Registry = registry;
            PlatformCatalog.Install(registry);
            EmitterPipeline.Install(registry);
            PluginCall.Reset();
        }

        /// <summary>Notes gathered during loading, for the export summary.</summary>
        public static IReadOnlyList<string> Notes => Registry?.Notes ?? new List<string>();
    }
}
