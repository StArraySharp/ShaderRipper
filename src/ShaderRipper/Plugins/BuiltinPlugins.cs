using System.Collections.Generic;
using ShaderRipper.Data;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// The backends this build ships with, registered through the same path a plugin
    /// uses.
    ///
    /// Doing it this way is what proves the API is sufficient: if the built-ins can be
    /// expressed as slot readers and translators, a third party can express one too.
    /// The descriptors mirror <see cref="Data.Platforms"/> exactly -- that table remains the
    /// single source of truth for ids and names.
    /// </summary>
    internal static class BuiltinPlugins
    {
        /// <summary>
        /// True when this build can enumerate programs of <paramref name="format"/>.
        ///
        /// A format with no slot reader cannot be read at all (Metal, SPIR-V, WebGPU);
        /// one with a reader but no translator can still be disassembled (d3d9, DXIL).
        /// </summary>
        public static bool CanRead(string format) => format is "dxbc" or "glsl" or "d3d9";

        /// <summary>True when <paramref name="format"/> is turned into HLSL.</summary>
        public static bool CanTranslate(string format) => format is "dxbc" or "glsl";

        internal static void Register(PluginRegistry registry)
        {
            string previous = registry.CurrentOwner;
            registry.CurrentOwner = "builtin";
            try
            {
                foreach (var info in Data.Platforms.All)
                {
                    if (!CanRead(info.Format)) continue;

                    // The slot reader and translator are chosen by the existing
                    // code paths; this entry only records that the backend exists and
                    // whether a translator is attached, so lookups (--platform
                    // validation, slot selection) have one place to ask.
                    registry.AddPlatform(
                        new PlatformDescriptor
                        {
                            Id = info.Id,
                            Name = info.Name,
                            Format = info.Format,
                            HasSource = info.HasSource,
                            HasTranslator = CanTranslate(info.Format),
                        },
                        slotReader: null,
                        translator: null,
                        priority: PluginRegistry.BuiltinPriority);
                }

                // The statement-level fold runs last in the built-in pipeline.  It is
                // registered here so the pipeline has one list of passes and a plugin
                // can decide to run before or after it by priority alone.
                registry.AddEmitterPass(new MatrixChainPass(), PluginRegistry.BuiltinPriority);
            }
            finally
            {
                registry.CurrentOwner = previous;
            }
        }
    }
}
