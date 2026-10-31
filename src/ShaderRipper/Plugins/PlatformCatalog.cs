using System;
using System.Collections.Generic;
using ShaderRipper.Data;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// The backend table as the pipeline sees it: the build's own table plus whatever
    /// a plugin added or replaced.
    ///
    /// Kept separate from <see cref="Platforms"/> so the static table stays the single
    /// source of truth for ids and names -- this is a view over it, not a replacement.
    /// Until a registry is installed the view answers from the built-in table alone,
    /// which is exactly what the code did before the registry existed, so a host that
    /// calls the library directly is unaffected.
    /// </summary>
    internal static class PlatformCatalog
    {
        private static PluginRegistry _registry;

        /// <summary>Install the registry a plugin added its backends to.</summary>
        public static void Install(PluginRegistry registry) => _registry = registry;

        /// <summary>Drop any plugin registration, restoring built-in behaviour.</summary>
        public static void Reset() => _registry = null;

        /// <summary>
        /// Description of one backend.  Never null: an unknown id yields the same
        /// placeholder <see cref="Platforms.Get"/> produces, so callers that guard on
        /// null keep working.
        /// </summary>
        public static PlatformInfo Get(int id)
        {
            var builtin = Data.Platforms.Get(id);
            if (builtin.Format != "unknown") return builtin;

            PlatformEntry entry = _registry?.PlatformFor(id);
            return entry == null ? builtin : ToInfo(entry.Descriptor);
        }

        /// <summary>
        /// True when the pipeline can enumerate programs of this backend.  A backend a
        /// plugin registered counts only when it brought a slot reader.
        /// </summary>
        public static bool CanRead(int id)
        {
            PlatformEntry entry = _registry?.PlatformFor(id);
            if (entry != null) return entry.SlotReader != null || BuiltinPlugins.CanRead(entry.Descriptor.Format);
            return BuiltinPlugins.CanRead(Data.Platforms.Get(id).Format);
        }

        /// <summary>True when this backend's payload is turned into HLSL.</summary>
        public static bool CanTranslate(int id)
        {
            PlatformEntry entry = _registry?.PlatformFor(id);
            if (entry != null)
                return entry.Translator != null || BuiltinPlugins.CanTranslate(entry.Descriptor.Format);
            return BuiltinPlugins.CanTranslate(Data.Platforms.Get(id).Format);
        }

        /// <summary>Resolve <c>--platform</c>: an id, a built-in name, or a plugin's name.</summary>
        public static int? ByName(string text)
        {
            int? builtin = Data.Platforms.ByName(text);
            if (builtin != null) return builtin;
            if (_registry == null || string.IsNullOrWhiteSpace(text)) return null;

            string want = text.Trim();
            foreach (PlatformEntry entry in _registry.Platforms)
                if (string.Equals(entry.Descriptor.Name, want, StringComparison.OrdinalIgnoreCase))
                    return entry.Descriptor.Id;
            return null;
        }

        /// <summary>Every backend, built-in or plugin, ordered by id.</summary>
        public static IEnumerable<PlatformInfo> All()
        {
            var seen = new HashSet<int>();
            foreach (PlatformInfo info in Data.Platforms.All)
            {
                seen.Add(info.Id);
                yield return info;
            }
            if (_registry == null) yield break;

            foreach (PlatformEntry entry in _registry.Platforms)
                if (seen.Add(entry.Descriptor.Id))
                    yield return ToInfo(entry.Descriptor);
        }

        private static PlatformInfo ToInfo(PlatformDescriptor descriptor) => new()
        {
            Id = descriptor.Id,
            Name = descriptor.Name,
            Format = descriptor.Format,
            HasSource = descriptor.HasSource,
        };
    }
}
