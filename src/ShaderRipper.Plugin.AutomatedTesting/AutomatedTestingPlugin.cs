using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ShaderRipper.Plugins;

namespace ShaderRipper.Plugin.AutomatedTesting
{
    /// <summary>
    /// Turns an export into a manifest a render-test harness can drive.
    ///
    /// The problem this solves: a <c>.shader</c> file on its own is not testable.  What
    /// has to be rendered is a <em>material</em> -- a shader plus a set of property
    /// values -- and a large family of shaders ships no material at all (an effect
    /// pack whose script builds one at runtime).  This plugin writes one JSON file
    /// describing, for every testable shader, the material to create and the values to
    /// set, so the harness needs no knowledge of the game's data files.
    ///
    /// It is a plugin rather than part of the exporter because it is one opinion about
    /// how to test: another harness may want a different file, a different grouping, or
    /// no file at all.  The exporter offers the hook; this decides what to do with it.
    ///
    /// Events and delegates, not an interface hierarchy: the work is a pipeline of
    /// small transformations, and each stage is a <c>Func</c> a subclass or a future
    /// plugin can replace without touching the others.
    /// </summary>
    public sealed class AutomatedTestingPlugin : IShaderRipperPlugin
    {
        public string Name => "Automated testing manifest";

        public string Description =>
            "Writes a render-test manifest: which material to build for each shader, and with what values";

        public PluginApiVersion ApiVersion => PluginApiVersion.Current;

        /// <summary>
        /// The SPIR-V backend, when it is installed.
        ///
        /// This manifest is most useful against the Vulkan route -- that is the one
        /// whose output needs the most verification -- so the plugin declares the
        /// dependency and the loader registers the backend first.  It is listed even
        /// though the harness works without it: the loader reports a missing
        /// dependency and loads anyway, which is better than silently changing order.
        /// </summary>
        public IReadOnlyList<string> DependsOn => new[] { "SPIR-V (Vulkan)" };

        /// <summary>The file this plugin produces at the export root.</summary>
        internal const string ManifestName = ".shaderripper-tests.json";

        public void Register(IPluginRegistry registry)
        {
            // Registered first so the export directory accepts the file on the next
            // --overwrite run: the check is a name list, and an unregistered file looks
            // foreign.
            registry.AddExportArtifact(ManifestName);
            registry.AddExportHook(new ManifestHook());

            // `--test-manifest <export-dir>` re-writes the manifest from an existing
            // export without repeating it.  Useful while iterating on the harness: the
            // export is the expensive half.
            registry.AddCommand(new RegenerateCommand());
            registry.AddCommand(new GenerateProjectCommand());
        }
    }

    /// <summary>
    /// The export hook.  Builds the manifest from the finished export's own snapshot.
    /// </summary>
    internal sealed class ManifestHook : IExportHook
    {
        public string Name => "test-manifest";

        public void ExportFinished(ExportCompleted context)
        {
            if (context?.OutputDirectory == null) return;

            var manifest = ManifestBuilder.Build(context);
            ManifestWriter.Write(context.OutputDirectory, manifest);
        }
    }

    /// <summary>One test case: a material to create and the shader to bind.</summary>
    internal sealed class TestCase
    {
        /// <summary>Stable identifier; the harness keys its results by this.</summary>
        public string Id { get; set; }

        /// <summary>Shader name as the asset spells it.</summary>
        public string Shader { get; set; }

        /// <summary>Emitted <c>.shader</c> file, relative to the export root.</summary>
        public string ShaderFile { get; set; }

        /// <summary>Where the harness should create the material.</summary>
        public string MaterialPath { get; set; }

        /// <summary>
        /// True when the values are Unity's declared defaults rather than the game's
        /// own.  A harness that compares pixels against a game screenshot must skip or
        /// loosen these: an effect driven by script-set properties will not match.
        /// </summary>
        public bool Synthetic { get; set; }

        /// <summary>
        /// How strict the comparison can be.  <c>smoke</c> means "must render something
        /// non-transparent"; <c>value</c> means the properties are the game's, so a
        /// pixel comparison is meaningful.
        /// </summary>
        public string Expectation { get; set; }

        /// <summary>Float and Range values to set.</summary>
        public SortedDictionary<string, float> Floats { get; set; }

        /// <summary>Color and Vector values to set.</summary>
        public SortedDictionary<string, float[]> Colors { get; set; }

        /// <summary>
        /// Texture slots.  A built-in name (<c>white</c>, <c>black</c>) is what Unity's
        /// own default is; anything else is an asset path the harness must load, or a
        /// raw <c>fileId:pathId</c> it cannot.
        /// </summary>
        public SortedDictionary<string, string> Textures { get; set; }
    }

    /// <summary>The manifest file's shape.</summary>
    internal sealed class Manifest
    {
        public string Generated { get; set; }
        public string ExportDirectory { get; set; }
        public int Count { get; set; }
        public int ShaderCount { get; set; }
        public int Skipped { get; set; }
        public List<TestCase> Tests { get; set; } = new();
    }

    /// <summary>
    /// Turns an <see cref="ExportCompleted"/> into a <see cref="Manifest"/>.
    ///
    /// Written as a chain of small pure steps so each can be replaced: the default
    /// chain filters to testable cases, sorts them, and assigns ids.
    /// </summary>
    internal static class ManifestBuilder
    {
        /// <summary>One step of the pipeline: cases in, cases out.</summary>
        internal delegate IEnumerable<TestCase> Stage(IEnumerable<TestCase> input);

        /// <summary>
        /// The stages a manifest goes through, in order.
        ///
        /// A future variant of this plugin -- or a test that wants a different shape --
        /// can replace any one of them without reimplementing the rest, which is the
        /// reason the work is decomposed this way rather than written inline.
        /// </summary>
        internal static readonly List<Stage> Pipeline = new()
        {
            DropUnrenderable,
            SortById,
            AssignIds,
        };

        internal static Manifest Build(ExportCompleted context)
        {
            var manifest = new Manifest
            {
                Generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ExportDirectory = context.OutputDirectory,
                ShaderCount = context.Shaders?.Count ?? 0,
                Skipped = context.Skipped,
            };

            IEnumerable<TestCase> cases = ToCases(context);
            foreach (Stage stage in Pipeline) cases = stage(cases);

            manifest.Tests = cases.ToList();
            manifest.Count = manifest.Tests.Count;
            return manifest;
        }

        /// <summary>
        /// One case per material that names an exported shader.
        ///
        /// The shader file is what has to exist: a material whose shader was built-in
        /// or failed to translate cannot be instantiated against this export, so it is
        /// not a test case.  The manifest records how many were dropped so a falling
        /// count is visible instead of silent.
        /// </summary>
        private static IEnumerable<TestCase> ToCases(ExportCompleted context)
        {
            var exported = new HashSet<string>(
                (context.Shaders ?? Array.Empty<ExportedShader>())
                    .Select(s => s.Path)
                    .Where(p => p != null),
                StringComparer.OrdinalIgnoreCase);

            foreach (ExportedMaterial m in context.Materials ?? Array.Empty<ExportedMaterial>())
            {
                if (m.ShaderFile == null || !exported.Contains(m.ShaderFile)) continue;

                yield return new TestCase
                {
                    Shader = m.Shader,
                    ShaderFile = m.ShaderFile,
                    MaterialPath = m.AssetPath,
                    Synthetic = m.Synthetic,
                    // A synthetic material carries Unity's defaults, so the only honest
                    // assertion is "this renders something"; a material the game shipped
                    // carries the values the game used, so a pixel comparison means
                    // something.
                    Expectation = m.Synthetic ? "smoke" : "value",
                    Floats = Sorted(m.Floats, v => v),
                    Colors = Sorted(m.Colors, v => v),
                    Textures = Sorted(m.Textures, v => v),
                };
            }
        }

        /// <summary>
        /// Copy a dictionary into a name-sorted one.
        ///
        /// Sorted so two runs of one export produce the same file: the manifest is
        /// diffed between builds to spot a shader that changed, and a hash order would
        /// make every run look different.
        /// </summary>
        private static SortedDictionary<string, T> Sorted<T>(
            IReadOnlyDictionary<string, T> source, Func<T, T> _)
        {
            var result = new SortedDictionary<string, T>(StringComparer.Ordinal);
            if (source != null)
                foreach (var kv in source) result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>Drop a case that cannot produce a picture at all.</summary>
        private static IEnumerable<TestCase> DropUnrenderable(IEnumerable<TestCase> input) =>
            input.Where(c => c.ShaderFile != null && c.MaterialPath != null);

        /// <summary>Deterministic order, so two runs of one export agree byte for byte.</summary>
        private static IEnumerable<TestCase> SortById(IEnumerable<TestCase> input) =>
            input.OrderBy(c => c.MaterialPath, StringComparer.Ordinal)
                 .ThenBy(c => c.Shader, StringComparer.Ordinal);

        /// <summary>
        /// Assign each case a stable id.
        ///
        /// Derived from the material path rather than a running index: a harness keeps
        /// results between runs, and an index would renumber everything the moment one
        /// case appears or disappears.
        /// </summary>
        private static IEnumerable<TestCase> AssignIds(IEnumerable<TestCase> input)
        {
            foreach (TestCase c in input)
            {
                c.Id = IdOf(c.MaterialPath);
                yield return c;
            }
        }

        /// <summary>
        /// A path turned into an id: lower case, with everything that is not a letter
        /// or digit collapsed to one underscore.  Two different materials always differ
        /// in more than punctuation, so the collision risk is a naming accident rather
        /// than a systematic one.
        /// </summary>
        internal static string IdOf(string materialPath)
        {
            if (string.IsNullOrEmpty(materialPath)) return "unnamed";
            var sb = new StringBuilder(materialPath.Length);
            bool gap = false;
            foreach (char ch in materialPath)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (gap && sb.Length > 0) sb.Append('_');
                    gap = false;
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else gap = true;
            }
            return sb.Length == 0 ? "unnamed" : sb.ToString();
        }
    }

    /// <summary>Writes the manifest, or removes a stale one.</summary>
    internal static class ManifestWriter
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // camelCase, matching the material index the exporter writes: a harness
            // reads both files and should not have to remember two spellings.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // A material may hold an infinity or a NaN; rejecting the whole file over
            // one such value would lose the manifest, so they are written as the JSON
            // tokens .NET defines and read back through the same option.
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                .AllowNamedFloatingPointLiterals,
        };

        internal static void Write(string dir, Manifest manifest)
        {
            string path = Path.Combine(dir, AutomatedTestingPlugin.ManifestName);
            try
            {
                if (manifest == null || manifest.Count == 0)
                {
                    // Leaving a stale manifest behind would have the harness render a
                    // shader set that no longer exists.
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                File.WriteAllText(path, JsonSerializer.Serialize(manifest, Options),
                                  new UTF8Encoding(false));

                if (OperatingSystem.IsWindows())
                {
                    try { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }        // a read-only target is not worth failing over
            catch (UnauthorizedAccessException) { }
        }
    }
}
