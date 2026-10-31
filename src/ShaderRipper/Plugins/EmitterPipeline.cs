using System.Collections.Generic;
using ShaderRipper.Plugins.Emit;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// The emitter pipeline: every registered <see cref="IEmitterPass"/> in priority
    /// order.
    ///
    /// This is the single place the translators hand their HLSL to for post-processing,
    /// so a plugin pass runs on the same text as the built-in matrix-chain folder and
    /// in a defined order relative to it.  The pipeline is set once per run; when no
    /// registry is installed (a host calling the library directly, a unit test), it
    /// falls back to the built-in pass alone, which is what the code did before the
    /// registry existed.
    /// </summary>
    public static class EmitterPipeline
    {
        private static IReadOnlyList<IEmitterPass> _passes;

        /// <summary>Install the passes from a registry.  Null restores the default.</summary>
        public static void Install(PluginRegistry registry)
        {
            _passes = registry?.EmitterPasses;
        }

        /// <summary>The passes that will run, for diagnostics and tests.</summary>
        public static IReadOnlyList<IEmitterPass> Passes =>
            _passes ?? new IEmitterPass[] { new MatrixChainPass() };

        /// <summary>
        /// Run every pass over one program body.
        ///
        /// A pass that throws is skipped and the text keeps the state it had before
        /// that pass: a broken plugin must not take the export down, and it must not
        /// leave a half-rewritten body either.
        /// </summary>
        public static string Run(string text, bool macros)
        {
            foreach (IEmitterPass pass in Passes)
            {
                string current = text;
                string next = PluginCall.Try($"{pass.Name}.Apply",
                                             () => pass.Apply(current, macros),
                                             current, null);
                if (!ReferenceEquals(next, current) && next != null) text = next;
            }
            return text;
        }
    }
}
