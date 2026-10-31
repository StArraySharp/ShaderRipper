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
        /// Register capabilities.  The registry is closed after this returns, so a
        /// later call throws instead of silently having no effect.
        /// </summary>
        void Register(IPluginRegistry registry);
    }
}
