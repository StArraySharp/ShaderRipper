using System;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// Version of the plugin contract, independent of the library version.
    ///
    /// A plugin compiled against a different <see cref="Major"/> is refused at load
    /// time rather than failing somewhere in the middle of an export: the interface
    /// can only grow within a major version, so a lower minor still works.
    /// </summary>
    public readonly struct PluginApiVersion : IEquatable<PluginApiVersion>
    {
        /// <summary>
        /// Breaking-change counter.  It is compared for equality at load time: a
        /// mismatch rejects the plugin outright, because an interface that gained a
        /// member cannot be implemented by an assembly compiled against the old shape.
        /// </summary>
        public readonly int Major;

        /// <summary>
        /// Additive counter.  A plugin built against a lower minor still loads: every
        /// change within a major version only adds members or defaults, so an old
        /// implementation stays a valid one.
        /// </summary>
        public readonly int Minor;

        /// <summary>Describe a contract version.</summary>
        /// <param name="major">Breaking-change counter; must match the host exactly.</param>
        /// <param name="minor">Additive counter; a lower value is accepted.</param>
        public PluginApiVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        /// <summary>
        /// The contract this build implements.  A plugin should report this value
        /// unless it deliberately targets an older one.
        /// </summary>
        public static PluginApiVersion Current { get; } = new(1, 0);

        /// <summary>
        /// Same major version, so the interface a plugin was compiled against is still
        /// the one the host provides.
        /// </summary>
        /// <param name="host">The version the running host implements.</param>
        public bool IsCompatibleWith(PluginApiVersion host) => Major == host.Major;

        /// <summary>Value equality over both counters.</summary>
        /// <param name="other">The version to compare against.</param>
        public bool Equals(PluginApiVersion other) => Major == other.Major && Minor == other.Minor;

        /// <summary>Value equality over both counters.</summary>
        /// <param name="obj">The value to compare against, if it is a version.</param>
        public override bool Equals(object obj) => obj is PluginApiVersion v && Equals(v);

        /// <summary>A hash over both counters, so the version can key a dictionary.</summary>
        public override int GetHashCode() => HashCode.Combine(Major, Minor);

        /// <summary>Renders as <c>major.minor</c>, the form the loader logs.</summary>
        public override string ToString() => $"{Major}.{Minor}";

        /// <summary>Value equality.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator ==(PluginApiVersion a, PluginApiVersion b) => a.Equals(b);

        /// <summary>Value inequality.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator !=(PluginApiVersion a, PluginApiVersion b) => !a.Equals(b);
    }
}
