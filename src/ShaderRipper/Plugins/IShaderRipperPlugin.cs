using System;
using System.Collections.Generic;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// Entry point of an external assembly.
    ///
    /// The host instantiates one instance per plugin, calls <see cref="Register"/>
    /// once, and keeps the instance for the life of the process -- so a plugin may
    /// cache per-game state (an index table, a key schedule) between files.  Only
    /// <c>Register</c> is guaranteed to run on a single thread; the capabilities it
    /// registers are called concurrently and must be thread-safe if they carry
    /// mutable state.
    /// </summary>
    public interface IShaderRipperPlugin
    {
        /// <summary>Shown by <c>--plugins</c> and in the log.</summary>
        string Name { get; }

        /// <summary>Optional one-line description; null is allowed.</summary>
        string Description { get; }

        /// <summary>Contract the plugin was compiled against.</summary>
        PluginApiVersion ApiVersion { get; }

        /// <summary>
        /// Names of plugins this one needs loaded first, or null/empty when it stands
        /// alone.
        ///
        /// A plugin that extends another (a testing harness driving a specific
        /// backend, say) has to run after it, or it registers against a registry whose
        /// platform slot is still empty.  The loader sorts by this list before calling
        /// <see cref="Register"/>, and reports a dependency that is not present rather
        /// than silently loading out of order.
        ///
        /// The name is <see cref="Name"/>, not the assembly name: that is what the
        /// user sees in <c>--plugins</c>, and what a plugin author can state without
        /// knowing how the host files assemblies.
        /// </summary>
        IReadOnlyList<string> DependsOn => Array.Empty<string>();

        /// <summary>
        /// Register capabilities.  The registry is closed after this returns, so a
        /// later call throws instead of silently having no effect.
        /// </summary>
        void Register(IPluginRegistry registry);
    }
}
