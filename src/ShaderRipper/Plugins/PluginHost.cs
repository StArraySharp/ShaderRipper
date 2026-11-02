using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// Loads plugin assemblies and hands back a closed registry.
    ///
    /// Discovery is deliberately plain: every <c>*.dll</c> under the plugin directory
    /// is opened, each type implementing <see cref="IShaderRipperPlugin"/> is
    /// instantiated, and anything that fails -- a missing dependency, a mismatched
    /// contract version, a throwing constructor -- is reported and skipped.  One bad
    /// plugin must not stop an export.
    ///
    /// In a NativeAOT build there is no <c>Assembly.LoadFrom</c>, so in-process plugins
    /// are unavailable; <see cref="InProcessSupported"/> is false there and the loader
    /// returns an empty registry rather than pretending.
    /// </summary>
    internal static class PluginHost
    {
        /// <summary>
        /// The load contexts of every plugin loaded so far.
        ///
        /// A collectible context that nothing references gets collected, and
        /// collecting it unloads it: the plugin's own types survive, but every
        /// dependency it asks for afterwards fails with "AssemblyLoadContext is
        /// unloading or was already unloaded".  This list is what keeps a loaded
        /// plugin usable for the whole run.
        /// </summary>
        private static readonly List<AssemblyLoadContext> Contexts = new();

        /// <summary>
        /// False in a NativeAOT publish.  The constant is compiled in, so the AOT
        /// build carries no reflection-based loader at all.
        /// </summary>
        public static bool InProcessSupported =>
#if NATIVEAOT
            false;
#else
            true;
#endif

        /// <summary>Default directory: <c>plugins</c> beside the executable.</summary>
        public static string DefaultDirectory
        {
            get
            {
                try
                {
                    string exe = Assembly.GetEntryAssembly()?.Location;
                    if (!string.IsNullOrEmpty(exe))
                        return Path.Combine(Path.GetDirectoryName(exe) ?? ".", "plugins");
                }
                catch { /* single-file publish has no Location */ }
                return Path.Combine(AppContext.BaseDirectory, "plugins");
            }
        }

        /// <summary>
        /// Build the registry: built-ins first, then every plugin found.
        ///
        /// <paramref name="directory"/> null or missing means "no plugins", which is
        /// the normal case; the built-ins are registered either way so lookups have
        /// one code path.
        /// </summary>
        public static PluginRegistry Load(string directory, ICollection<string> notes)
        {
            var registry = new PluginRegistry();
            BuiltinPlugins.Register(registry);

            if (!InProcessSupported)
            {
                notes?.Add("this build cannot load plugins (NativeAOT); built-in backends only");
                registry.Close();
                return registry;
            }

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                registry.Close();
                return registry;
            }

            foreach (string path in EnumerateAssemblies(directory))
            {
                try
                {
                    LoadAssembly(path, registry, notes);
                }
                catch (Exception e)
                {
                    notes?.Add($"{Path.GetFileName(path)}: {e.GetType().Name}: {e.Message}");
                }
            }

            registry.Close();
            foreach (string note in registry.Notes) notes?.Add(note);
            return registry;
        }

        /// <summary>
        /// A plugin directory may hold either loose <c>*.dll</c> files or one
        /// directory per plugin; both are walked, in a fixed order so the load
        /// sequence is reproducible.
        /// </summary>
        private static IEnumerable<string> EnumerateAssemblies(string directory)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
            }
            catch { yield break; }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                string name = Path.GetFileName(f);
                // Skip the host's own assemblies and the symbol/XML sidecars.  The test
                // must be for the exact host assembly names, not a prefix: a plugin is
                // very likely to be called `ShaderRipper.Plugin.Something`, and a
                // prefix check silently refused to load it.
                if (IsHostAssembly(name)) continue;
                if (name.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)) continue;
                // A package carries its native libraries under `runtimes/<rid>/native`,
                // and those are not assemblies: opening one throws BadImageFormat and
                // buries the real diagnostics.  They are found by name at call time
                // instead, through the load context's unmanaged resolver.
                if (IsNativePath(f)) continue;
                yield return f;
            }
        }

        /// <summary>True for a path under a package's <c>runtimes</c> directory.</summary>
        private static bool IsNativePath(string path)
        {
            string directory = Path.GetDirectoryName(path) ?? "";
            foreach (string part in directory.Split(Path.DirectorySeparatorChar,
                                                    Path.AltDirectorySeparatorChar))
                if (part.Equals("runtimes", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Assemblies the host itself ships, which are never plugins.</summary>
        private static bool IsHostAssembly(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return stem.Equals("ShaderRipper", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("ShaderRipper.Application", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("ShaderRipper.Gui", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadAssembly(string path, PluginRegistry registry, ICollection<string> notes)
        {
            // A collectible context keeps a failed load from pinning the assembly for
            // the life of the process.  Unloading only happens at exit; there is no
            // hot reload.
            //
            // The context is held in a static list because a collectible context that
            // nothing references is collected -- and collecting it unloads it, so the
            // plugin's types survive but every dependency it asks for afterwards fails
            // with "AssemblyLoadContext is unloading or was already unloaded".  The
            // list is what keeps a loaded plugin usable for the whole run.
            //
            // The critical part is Resolving: a plugin references this library, and if
            // that reference resolved to a second copy on disk, `IShaderRipperPlugin`
            // would be a *different* type there and nothing the plugin registered would
            // ever be seen by the host.  Resolving binds every host-provided assembly
            // to the instance already loaded here.
            var context = new HostBoundLoadContext(Path.GetFileNameWithoutExtension(path),
                                                   Path.GetDirectoryName(path) ?? ".");
            Contexts.Add(context);
            Assembly assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
                notes?.Add($"{Path.GetFileName(path)}: some types failed to load " +
                           $"({e.LoaderExceptions?.FirstOrDefault()?.Message})");
            }

            foreach (Type type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IShaderRipperPlugin).IsAssignableFrom(type)) continue;

                IShaderRipperPlugin plugin;
                try
                {
                    plugin = (IShaderRipperPlugin)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    notes?.Add($"{type.FullName}: cannot instantiate ({e.GetType().Name})");
                    continue;
                }

                if (plugin == null) continue;

                // Version gate before Register: a plugin written against a different
                // major contract would otherwise half-register and fail at call time.
                if (!plugin.ApiVersion.IsCompatibleWith(PluginApiVersion.Current))
                {
                    notes?.Add($"{plugin.Name}: compiled against plugin API {plugin.ApiVersion}, " +
                               $"this build provides {PluginApiVersion.Current} -- not loaded");
                    continue;
                }

                registry.CurrentOwner = string.IsNullOrEmpty(plugin.Name)
                    ? type.Name : plugin.Name;
                try
                {
                    plugin.Register(registry);
                }
                catch (Exception e)
                {
                    notes?.Add($"{registry.CurrentOwner}: Register() threw " +
                               $"{e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    registry.CurrentOwner = "builtin";
                }
            }
        }
    }

    /// <summary>
    /// A load context that resolves anything the host already has to the host's copy,
    /// and everything else from the plugin's own directory.
    ///
    /// Without the first rule, a plugin that references this library gets its own
    /// instance of every type in it, so the interface it implements is not the
    /// interface the host checks for -- and the plugin loads, registers, and is then
    /// silently ignored.  Binding by simple name is right here: a plugin is expected
    /// to compile against the host it runs in, not to bring its own dependency
    /// versions.
    ///
    /// Without the second rule a plugin cannot have dependencies of its own.  A
    /// plugin directory is not the application base directory, so the default probing
    /// path does not include it and a package reference resolves to nothing at call
    /// time -- the plugin loads, registers, and then fails on its first real request
    /// with FileNotFoundException.  A plugin's own managed and native dependencies are
    /// looked up next to the assembly that brought them.
    /// </summary>
    internal sealed class HostBoundLoadContext : AssemblyLoadContext
    {
        private readonly string _directory;

        public HostBoundLoadContext(string name, string directory) : base(name, isCollectible: true)
        {
            // Made absolute here because `LoadFromAssemblyPath` rejects a relative
            // path, and the plugin directory comes from the command line: a user who
            // types `--plugin-dir plugins/foo` would otherwise lose every dependency
            // the plugin brought, with an ArgumentException buried in the notes.
            _directory = Path.GetFullPath(directory);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == null) return null;

            // Returning the host's instance keeps one copy of every shared type alive.
            // Returning null would let the runtime probe the plugin directory first,
            // which is exactly the situation this exists to prevent.
            try
            {
                Assembly host = Default.Assemblies
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name,
                                                       StringComparison.OrdinalIgnoreCase));
                if (host != null) return host;
            }
            catch { }

            // Not a host assembly: it is the plugin's own dependency, so it comes from
            // the plugin directory.  `LoadFromAssemblyPath` is used rather than
            // `LoadFromAssemblyName` so the file is bound to this context and shares
            // the plugin's lifetime.
            string candidate = Path.Combine(_directory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                try { return LoadFromAssemblyPath(candidate); }
                catch (Exception e)
                {
                    if (Environment.GetEnvironmentVariable("SHADERRIP_PLUGINDBG") == "1")
                        Console.Error.WriteLine($"[plugin] {Name}: {candidate} failed: " +
                                                $"{e.GetType().Name}: {e.Message}");
                }
            }
            if (Environment.GetEnvironmentVariable("SHADERRIP_PLUGINDBG") == "1")
                Console.Error.WriteLine($"[plugin] {Name}: cannot resolve {assemblyName.Name} " +
                                        $"from {_directory}");
            return null;
        }

        /// <summary>
        /// Resolve a native library a plugin brought.
        ///
        /// NuGet puts native binaries under <c>runtimes/&lt;rid&gt;/native</c>, which the
        /// default resolver does not look at for a directory that is not the
        /// application base.  The current platform's own subdirectory is searched, and
        /// nothing else: loading a foreign architecture's binary fails with
        /// BadImageFormat at first use rather than here.
        /// </summary>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            string[] names = unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                             unmanagedDllName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                             unmanagedDllName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
                ? new[] { unmanagedDllName }
                : new[] { unmanagedDllName + ".dll", unmanagedDllName + ".so", unmanagedDllName + ".dylib" };

            foreach (string name in names)
            {
                string candidate = Path.Combine(_directory, "runtimes", rid, "native", name);
                if (File.Exists(candidate))
                {
                    try { return LoadUnmanagedDllFromPath(candidate); }
                    catch { }
                }
            }
            return IntPtr.Zero;
        }
    }
}
