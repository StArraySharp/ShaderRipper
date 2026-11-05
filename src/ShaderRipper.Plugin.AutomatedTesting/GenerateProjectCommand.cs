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
    /// <c>--gen-test-project &lt;export-dir&gt; [output-dir]</c>: lay down a Unity project
    /// that renders every case in the manifest.
    ///
    /// The point is to close the loop the manifest opens.  A manifest says which
    /// material to build and with what values; something still has to build it, render
    /// it and say whether anything came out.  Doing that by hand does not scale past a
    /// handful of shaders, and doing it in the game's own project means fighting its
    /// scenes and scripts.
    ///
    /// The generated project is deliberately minimal: the emitted shaders, one Editor
    /// script that walks the manifest, and nothing else.  That keeps a run short --
    /// Unity imports only what it is about to render -- and keeps the result
    /// reproducible, because there is no game code to behave differently.
    ///
    /// The manifest is embedded in the generated script rather than read from disk at
    /// run time: the project may be copied elsewhere, and a missing JSON file would
    /// look like "every test passed vacuously".
    /// </summary>
    internal sealed class GenerateProjectCommand : ICommandHandler
    {
        public string Command => "--gen-test-project";

        public string Summary => "<export-dir> [output-dir] --textures <dir> -- write a Unity project that renders every case";

        public int PositionalCount => 1;

        public int Run(CommandArgs args)
        {
            string exportDir = args.Positionals[0];
            if (!Directory.Exists(exportDir))
            {
                Console.Error.WriteLine($"no such directory: {exportDir}");
                return 2;
            }

            string outDir = args.Positionals.Count > 1
                ? args.Positionals[1]
                : Path.Combine(exportDir, "UnityTestProject");

            // Real pictures to sample instead of the generated test pattern.  A
            // screen effect is judged on how it changes an image, so the input has
            // to be an image: a flat colour survives every blur, key and curve
            // unchanged, which is exactly what a broken translation also does.
            string textureDir = args.Value("--textures");
            if (!string.IsNullOrEmpty(textureDir) && !Directory.Exists(textureDir))
            {
                Console.Error.WriteLine($"no such texture directory: {textureDir}");
                return 2;
            }

            Manifest manifest = LoadManifest(exportDir, out ManifestError error);
            if (manifest == null)
            {
                Console.Error.WriteLine(error?.Message ?? "cannot read the manifest");
                return 2;
            }

            try
            {
                Write(exportDir, outDir, manifest, textureDir);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"cannot write {outDir}: {e.Message}");
                return 1;
            }

            Console.WriteLine($"{outDir}");
            Console.WriteLine($"  {manifest.Tests.Count} test case(s), " +
                              $"{manifest.Tests.Count(t => t.Synthetic)} synthetic");
            if (!string.IsNullOrEmpty(textureDir))
                Console.WriteLine($"  sampling textures from {textureDir}");
            Console.WriteLine("  open it with Unity, or run:");
            Console.WriteLine($"    Unity.exe -batchmode -quit -projectPath \"{outDir}\" " +
                              "-executeMethod ShaderRipperTest.Run -logFile -");
            return 0;
        }

        private sealed class ManifestError
        {
            public string Message;
        }

        /// <summary>
        /// How the manifest is read back.
        ///
        /// Case-insensitive because the file is written with the property names as
        /// declared, and named floating-point literals because a material can carry an
        /// infinity or a NaN -- the same values the material index has to write.  A
        /// default reader rejects those and would fail the whole manifest over one
        /// property.
        /// </summary>
        private static readonly JsonSerializerOptions ManifestOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                .AllowNamedFloatingPointLiterals,
        };

        /// <summary>
        /// The manifest to embed: the one beside the export, or one rebuilt from the
        /// directory when the export ran without this plugin.
        ///
        /// Rebuilding is the same path <c>--test-manifest</c> takes, so a project can be
        /// generated from an export made long before this plugin existed.
        /// </summary>
        private static Manifest LoadManifest(string exportDir, out ManifestError error)
        {
            error = null;
            string path = Path.Combine(exportDir, AutomatedTestingPlugin.ManifestName);
            if (File.Exists(path))
            {
                try
                {
                    Manifest loaded = JsonSerializer.Deserialize<Manifest>(
                        File.ReadAllText(path), ManifestOptions);
                    if (loaded?.Tests != null && loaded.Tests.Count > 0) return loaded;
                    error = new ManifestError { Message = $"{path} lists no test cases" };
                    return null;
                }
                catch (JsonException e)
                {
                    error = new ManifestError { Message = $"{path} is not valid JSON: {e.Message}" };
                    return null;
                }
            }

            // No manifest: rebuild it from what the export directory holds.  The
            // regenerate command owns that logic, so it is invoked rather than copied.
            var rebuild = new RegenerateCommand();
            int rc = rebuild.Run(new CommandArgs
            {
                Raw = new[] { exportDir },
                Positionals = new[] { exportDir },
                Value = _ => null,
                Has = _ => false,
            });
            if (rc != 0)
            {
                error = new ManifestError { Message = "no manifest, and rebuilding one from the export failed" };
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Manifest>(
                    File.ReadAllText(path), ManifestOptions);
            }
            catch (JsonException e)
            {
                error = new ManifestError { Message = $"rebuilt manifest is not valid JSON: {e.Message}" };
                return null;
            }
        }

        /// <summary>Lay down the project.</summary>
        private static void Write(string exportDir, string outDir, Manifest manifest, string textureDir)
        {
            Directory.CreateDirectory(outDir);

            // The shaders are copied, not referenced: a Unity project cannot see files
            // outside its own Assets folder, and a symlink would make the result depend
            // on how the checkout was made.
            string assets = Path.Combine(outDir, "Assets");
            CopyShaders(Path.Combine(exportDir, "Assets"), assets);

            // Sample pictures, if the caller supplied any.  They land under
            // Assets/Textures so the editor imports them as assets the script can
            // load by path.
            List<string> samples = CopyTextures(textureDir, assets);

            WriteMinimalProject(outDir);

            string editor = Path.Combine(assets, "Editor");
            Directory.CreateDirectory(editor);
            File.WriteAllText(Path.Combine(editor, "ShaderRipperTest.cs"),
                              TestScript(manifest, samples), new UTF8Encoding(false));

            // The manifest travels with the project as well, so a person can see what
            // the script is going to do without reading the embedded copy.
            File.WriteAllText(Path.Combine(outDir, "manifest.json"),
                              JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                              {
                                  WriteIndented = true,
                                  NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                                      .AllowNamedFloatingPointLiterals,
                              }),
                              new UTF8Encoding(false));
        }

        /// <summary>
        /// Copy the caller's pictures into <c>Assets/Textures</c> and return their asset
        /// paths in a stable order.
        ///
        /// The order matters: a case is assigned a picture by its texture slot index,
        /// so the same export always renders against the same image.  Sorting by file
        /// name gives that, and it also keeps the assignment independent of the order
        /// the file system happens to enumerate a directory in.
        /// </summary>
        private static List<string> CopyTextures(string textureDir, string toAssets)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(textureDir) || !Directory.Exists(textureDir)) return result;

            string dest = Path.Combine(toAssets, "Textures");
            Directory.CreateDirectory(dest);

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".tga", ".bmp",
            };
            var files = Directory.EnumerateFiles(textureDir)
                                 .Where(f => extensions.Contains(Path.GetExtension(f)))
                                 .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                                 .ToList();
            for (int i = 0; i < files.Count; i++)
            {
                // Renamed to a plain index: the caller's names are Chinese and the
                // generated script embeds these as string literals.
                string name = $"sample{i:D2}{Path.GetExtension(files[i]).ToLowerInvariant()}";
                File.Copy(files[i], Path.Combine(dest, name), overwrite: true);
                WriteTextureMeta(Path.Combine(dest, name));
                result.Add("Assets/Textures/" + name);
            }
            return result;
        }

        /// <summary>
        /// Write the import settings for one sample picture.
        ///
        /// Unity's default importer rescales anything larger than 2048 to fit, and a
        /// non-power-of-two image is pushed to the nearest power of two -- which
        /// changes its aspect ratio.  These photographs are 2848x1600, so without a
        /// meta file they arrive as 2048x2048: the frame the shader renders would be
        /// square, and a blur radius or a distortion offset would cover a different
        /// fraction of the picture than it does in the game.  The settings below keep
        /// the pixels as they are and turn mipmaps off, which is also what a screen
        /// effect reads.
        /// </summary>
        private static void WriteTextureMeta(string imagePath)
        {
            string meta = "\nfileFormatVersion: 2\n"
                + "guid: " + Guid.NewGuid().ToString("N") + "\n"
                + "TextureImporter:\n"
                + "  internalIDToNameTable: []\n"
                + "  externalObjects: {}\n"
                + "  serializedVersion: 12\n"
                + "  mipmaps:\n"
                + "    mipMapMode: 0\n"
                + "    enableMipMap: 0\n"
                + "    sRGBTexture: 1\n"
                + "    linearTexture: 0\n"
                + "    fadeOut: 0\n"
                + "  isReadable: 0\n"
                + "  streamingMipmaps: 0\n"
                + "  grayScaleToAlpha: 0\n"
                + "  generateCubemap: 6\n"
                + "  cubemapConvolution: 0\n"
                + "  textureShape: 1\n"
                + "  singleChannelComponent: 0\n"
                + "  textureFormat: 1\n"
                + "  maxTextureSize: 4096\n"
                + "  textureSettings:\n"
                + "    serializedVersion: 2\n"
                + "    filterMode: 1\n"
                + "    aniso: 1\n"
                + "    mipBias: 0\n"
                + "    wrapU: 1\n"
                + "    wrapV: 1\n"
                + "  nPOTScale: 0\n"
                + "  spriteMode: 0\n"
                + "  alphaUsage: 1\n"
                + "  alphaIsTransparency: 0\n"
                + "  spriteMeshType: 0\n"
                + "  spriteExtrude: 1\n"
                + "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n"
                + "  spritePixelsToUnits: 100\n"
                + "  platformSettings: []\n"
                + "  spriteSheet:\n"
                + "    serializedVersion: 2\n"
                + "    sprites: []\n"
                + "  spritePackingTag: \n"
                + "  pSDRemoveMatte: 0\n"
                + "  userData: \n"
                + "  assetBundleName: \n"
                + "  assetBundleVariant: \n";
            File.WriteAllText(imagePath + ".meta", meta, new UTF8Encoding(false));
        }

        private static void CopyShaders(string from, string toAssets)        {
            if (!Directory.Exists(from)) return;
            foreach (string file in Directory.EnumerateFiles(from, "*.shader", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(from, file);
                string dest = Path.Combine(toAssets, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? toAssets);
                File.Copy(file, dest, overwrite: true);
            }
        }

        /// <summary>
        /// Rewrite each case's <c>Textures</c> object into two parallel arrays.
        ///
        /// Unity's <c>JsonUtility</c> has no dictionary support, so the embedded
        /// manifest has to arrive in a shape it can build.  The rewrite is textual
        /// because the input is the serializer's own compact output, which is far
        /// easier to recognise than to rebuild: one case is one object, and its
        /// texture map is a flat object of string-to-string pairs.
        /// </summary>
        private static string FlattenTextures(string json)
        {
            var pattern = new System.Text.RegularExpressions.Regex(
                "\"Textures\":\\{(?<body>[^{}]*)\\}");
            return pattern.Replace(json, m =>
            {
                var names = new List<string>();
                var values = new List<string>();
                foreach (System.Text.RegularExpressions.Match pair in
                         System.Text.RegularExpressions.Regex.Matches(
                             m.Groups["body"].Value, "\"(?<k>[^\"]+)\":\"(?<v>[^\"]*)\""))
                {
                    names.Add(pair.Groups["k"].Value);
                    values.Add(pair.Groups["v"].Value);
                }
                return "\"TextureNames\":[" + string.Join(",", names.Select(Quote)) + "],"
                     + "\"TexturePaths\":[" + string.Join(",", values.Select(Quote)) + "]";
            });
        }

        private static string Quote(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>
        /// The project files Unity needs to open a folder as a project.
        ///
        /// Only three things are required: a <c>ProjectVersion.txt</c> so the editor
        /// knows which version this was made for, a <c>manifest.json</c> listing the
        /// packages (empty is legal), and an <c>Assets</c> folder.  Everything else
        /// Unity generates on first open, and shipping a full set would tie the
        /// project to one editor version's settings.
        /// </summary>
        private static void WriteMinimalProject(string outDir)
        {
            string settings = Path.Combine(outDir, "ProjectSettings");
            Directory.CreateDirectory(settings);
            File.WriteAllText(Path.Combine(settings, "ProjectVersion.txt"),
                              "m_EditorVersion: " + EditorVersion() + "\n",
                              new UTF8Encoding(false));

            string packages = Path.Combine(outDir, "Packages");
            Directory.CreateDirectory(packages);
            // Only the modules a render test touches: the sprite and text packages the
            // game uses are not needed to draw a quad.
            File.WriteAllText(Path.Combine(packages, "manifest.json"),
                "{\n  \"dependencies\": {\n" +
                "    \"com.unity.modules.imageconversion\": \"1.0.0\",\n" +
                "    \"com.unity.modules.jsonserialize\": \"1.0.0\",\n" +
                "    \"com.unity.modules.ui\": \"1.0.0\",\n" +
                "    \"com.unity.modules.uielements\": \"1.0.0\"\n" +
                "  }\n}\n",
                new UTF8Encoding(false));
        }

        /// <summary>
        /// The editor version to record.
        ///
        /// Taken from the environment when the variable Unity itself exports, and
        /// otherwise from the running editor's own directory if this tool happens to sit
        /// inside one.  A wrong value only produces a "project was made with a different
        /// version" prompt, which batch mode answers on its own, so the fallback is
        /// harmless.
        /// </summary>
        private static string EditorVersion()
        {
            string env = Environment.GetEnvironmentVariable("UNITY_VERSION");
            if (!string.IsNullOrEmpty(env)) return env;

            // Look for <...>/Editor/Data next to the running executable's tree.
            string here = AppContext.BaseDirectory;
            for (string dir = here; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, "Editor", "Data", "Resources", "PackageManager");
                if (Directory.Exists(candidate))
                {
                    string version = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(candidate)));
                    if (!string.IsNullOrEmpty(version) && char.IsDigit(version[0])) return version;
                }
                if (Path.GetFileName(dir).Length > 0 && dir.Length < 4) break;
            }
            return "6000.3.10f1";
        }

        /// <summary>
        /// The Editor script the project runs.
        ///
        /// It is one file with the manifest embedded, because that is what makes the
        /// project self-contained.  The structure is a plain sequence -- create, render,
        /// measure, record -- with each step a local function, so a person adapting it
        /// can replace one without reading the whole thing.
        /// </summary>
        private static string TestScript(Manifest manifest, List<string> samples)
        {
            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = false,
                // Unity's JsonUtility rejects the named literals .NET emits for
                // infinity and NaN, so they are written as the largest finite value
                // instead: a harness only needs the material to build, and clamping
                // one unset property is better than failing the whole project.
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                    .AllowNamedFloatingPointLiterals,
            });
            json = json.Replace("\"Infinity\"", "3.4028235E+38")
                       .Replace("-\"Infinity\"", "-3.4028235E+38")
                       .Replace("\"NaN\"", "0");

            // Unity's JsonUtility cannot build a Dictionary, and the texture map is
            // the only field of that shape.  Rewriting it into two parallel arrays
            // here keeps the embedded manifest parseable without changing what the
            // file says.
            json = FlattenTextures(json);

            var sb = new StringBuilder();
            sb.AppendLine("// Generated by ShaderRipper -- renders every case in the embedded manifest.");
            sb.AppendLine("//");
            sb.AppendLine("// Run it from the command line:");
            sb.AppendLine("//   Unity.exe -batchmode -quit -projectPath <this> \\");
            sb.AppendLine("//             -executeMethod ShaderRipperTest.Run -logFile -");
            sb.AppendLine("//");
            sb.AppendLine("// Results land in <project>/test-results.json and one PNG per case.");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("public static class ShaderRipperTest");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>The manifest this project was generated from.</summary>");
            sb.AppendLine("    private const string ManifestJson = @\"" + json.Replace("\"", "\"\"") + "\";");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Width of the rendered frame; the height follows the source's aspect.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// A screen effect samples its input with the same UVs it will be drawn");
            sb.AppendLine("    /// with, so a source squashed into a square changes the pixel pattern a");
            sb.AppendLine("    /// blur radius, a distortion offset or a dither grid produces -- which is");
            sb.AppendLine("    /// exactly what these cases are measuring.  The frame therefore keeps the");
            sb.AppendLine("    /// source's shape at the resolution the game renders at, so the numbers a");
            sb.AppendLine("    /// shader derives from `_ScreenResolution` match what it would see in the");
            sb.AppendLine("    /// game.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private const int OutputWidth = 1920;");
            sb.AppendLine("    private const int OutputHeight = 1080;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Size of the generated pattern, when no photographs were given.</summary>");
            sb.AppendLine("    private const int PatternWidth = OutputWidth;");
            sb.AppendLine("    private const int PatternHeight = OutputHeight;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// The value `_Time.x` is given for every render.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// Not zero: an effect that animates off the clock and is sampled at t=0");
            sb.AppendLine("    /// can legitimately output a flat frame, which would look like a broken");
            sb.AppendLine("    /// shader.  A mid-range value exercises the animated path instead.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private const float TimeSinceStart = 3.7f;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Pictures to sample instead of the generated test pattern.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// A screen effect is judged by how it changes an image, so the input has to");
            sb.AppendLine("    /// be an image.  A flat colour survives every blur, key and curve unchanged,");
            sb.AppendLine("    /// which is exactly what a broken translation also does -- the two cannot be");
            sb.AppendLine("    /// told apart.  Empty when the project was generated without --textures, in");
            sb.AppendLine("    /// which case the built-in pattern is used.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static readonly string[] Samples =");
            sb.AppendLine("    {");
            foreach (string s in samples)
                sb.AppendLine("        \"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",");
            sb.AppendLine("    };");
            sb.AppendLine();
            sb.AppendLine("    private static readonly Dictionary<string, Texture2D> SampleCache =");
            sb.AppendLine("        new Dictionary<string, Texture2D>(StringComparer.Ordinal);");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// The picture a screen effect is given as its source.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// Almost every shader in the game is a full-screen effect: it samples");
            sb.AppendLine("    /// _MainTex and transforms the result.  A manifest only records the");
            sb.AppendLine("    /// texture's asset path, and for a shader the game ships no material for");
            sb.AppendLine("    /// that path is the built-in 'white' texture -- so the effect samples");
            sb.AppendLine("    /// (1,1,1) at every pixel and its output is a constant.  Sepia of white is");
            sb.AppendLine("    /// white, and 382 of 505 cases rendered a flat white frame for exactly");
            sb.AppendLine("    /// that reason: the shader was correct, the input was degenerate.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// When the project was generated with --textures, a real photograph is");
            sb.AppendLine("    /// returned instead, picked by the slot's index so the same export always");
            sb.AppendLine("    /// renders against the same picture.  Eight photographs are enough for the");
            sb.AppendLine("    /// widest shader in this game (five slots), and the index wraps if not.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// Without them, a generated pattern stands in: four quadrants of different");
            sb.AppendLine("    /// brightness, a colour ramp across the middle, and alpha varying from 0 to");
            sb.AppendLine("    /// 1.  It is enough to tell \"changed the image\" from \"wrote a constant\",");
            sb.AppendLine("    /// which is all the pattern can do.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static Texture2D SourceTexture(int slot)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (Samples.Length > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            string path = Samples[slot % Samples.Length];");
            sb.AppendLine("            if (SampleCache.TryGetValue(path, out Texture2D cached) && cached != null)");
            sb.AppendLine("                return cached;");
            sb.AppendLine("            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);");
            sb.AppendLine("            if (loaded != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                SampleCache[path] = loaded;");
            sb.AppendLine("                return loaded;");
            sb.AppendLine("            }");
            sb.AppendLine("            Debug.LogWarning($\"[test] sample not importable: {path}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        var tex = new Texture2D(PatternWidth, PatternHeight, TextureFormat.RGBA32, false);");
            sb.AppendLine("        tex.wrapMode = TextureWrapMode.Clamp;");
            sb.AppendLine("        tex.filterMode = FilterMode.Point;");
            sb.AppendLine("        var px = new Color32[PatternWidth * PatternHeight];");
            sb.AppendLine("        for (int y = 0; y < PatternHeight; y++)");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int x = 0; x < PatternWidth; x++)");
            sb.AppendLine("            {");
            sb.AppendLine("                float u = (x + 0.5f) / PatternWidth;");
            sb.AppendLine("                float v = (y + 0.5f) / PatternHeight;");
            sb.AppendLine("                // Four quadrants at four brightnesses, so a shader that");
            sb.AppendLine("                // reads one corner and ignores the rest is visible as such.");
            sb.AppendLine("                float baseV = 0.15f + 0.25f * ((x < PatternWidth / 2 ? 0 : 1) + (y < PatternHeight / 2 ? 0 : 2));");
            sb.AppendLine("                // A horizontal ramp on top, which survives a blur, a");
            sb.AppendLine("                // posterize or a colour key and makes those effects measurable.");
            sb.AppendLine("                float ramp = 0.55f * u;");
            sb.AppendLine("                byte r = (byte)(255 * Mathf.Clamp01(baseV + ramp));");
            sb.AppendLine("                byte g = (byte)(255 * Mathf.Clamp01(baseV + ramp * 0.7f + 0.1f * v));");
            sb.AppendLine("                byte b = (byte)(255 * Mathf.Clamp01(baseV + ramp * 0.4f + 0.2f * (1 - v)));");
            sb.AppendLine("                // Alpha ramps with v so an alpha-keyed effect changes too.");
            sb.AppendLine("                byte a = (byte)(64 + 191 * v);");
            sb.AppendLine("                px[y * PatternWidth + x] = new Color32(r, g, b, a);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        tex.SetPixels32(px);");
            sb.AppendLine("        tex.Apply();");
            sb.AppendLine("        return tex;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    [Serializable] private sealed class Case");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Id; public string Shader; public string ShaderFile;");
            sb.AppendLine("        public string MaterialPath; public bool Synthetic; public string Strictness;");
            sb.AppendLine("        // Unity's JsonUtility cannot deserialize a dictionary, so the texture");
            sb.AppendLine("        // map arrives as two parallel arrays and is looked up by index.");
            sb.AppendLine("        public string[] TextureNames;");
            sb.AppendLine("        public string[] TexturePaths;");
            sb.AppendLine();
            sb.AppendLine("        public string TexturePath(string slot)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (TextureNames == null) return null;");
            sb.AppendLine("            for (int i = 0; i < TextureNames.Length; i++)");
            sb.AppendLine("                if (TextureNames[i] == slot)");
            sb.AppendLine("                    return TexturePaths != null && i < TexturePaths.Length ? TexturePaths[i] : null;");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// True when the slot holds no real asset, so a substitute picture is what");
            sb.AppendLine("    /// the game would have put there anyway.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// `_MainTex (\"Base\", 2D) = \"white\" {}` is the declaration a screen effect");
            sb.AppendLine("    /// almost always has: the game replaces it with the framebuffer at run time");
            sb.AppendLine("    /// and never ships a texture of its own.  An empty map means the same thing");
            sb.AppendLine("    /// -- a material built from a shader with no recorded value binds nothing.");
            sb.AppendLine("    /// Any other path names a real asset (a font atlas, a gradient ramp), and");
            sb.AppendLine("    /// replacing that would test a shader the game never runs.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static bool IsConstantTexture(string path)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (string.IsNullOrEmpty(path)) return true;");
            sb.AppendLine("        switch (path.ToLowerInvariant())");
            sb.AppendLine("        {");
            sb.AppendLine("            case \"white\": case \"black\": case \"gray\": case \"grey\":");
            sb.AppendLine("            case \"bump\": case \"red\": case \"lineargray\":");
            sb.AppendLine("                return true;");
            sb.AppendLine("            default: return false;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    [Serializable] private sealed class Manifest");
            sb.AppendLine("    {");
            sb.AppendLine("        public List<Case> Tests = new List<Case>();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    [Serializable] private sealed class Result");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Id; public string Shader; public bool Synthetic;");
            sb.AppendLine("        public string Status;      // pass | fail | skip | flat");
            sb.AppendLine("        public string Reason;");
            sb.AppendLine("        public float OpaqueRatio;  // share of pixels with alpha > 0");
            sb.AppendLine("        public float MeanLuma;     // average brightness of the opaque pixels");
            sb.AppendLine("        public float Variance;     // spread of brightness; 0 means one flat colour");
            sb.AppendLine("        public string Image;       // PNG written for this case, or null");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static void Run()");
            sb.AppendLine("    {");
            sb.AppendLine("        Manifest manifest = JsonUtility.FromJson<Manifest>(ManifestJson);");
            sb.AppendLine("        string root = Directory.GetParent(Application.dataPath)!.FullName;");
            sb.AppendLine("        string imageDir = Path.Combine(root, \"test-images\");");
            sb.AppendLine("        Directory.CreateDirectory(imageDir);");
            sb.AppendLine();
            sb.AppendLine("        var results = new List<Result>();");
            sb.AppendLine("        foreach (Case c in manifest.Tests)");
            sb.AppendLine("        {");
            sb.AppendLine("            Result r = RunOne(c, imageDir);");
            sb.AppendLine("            results.Add(r);");
            sb.AppendLine("            Debug.Log($\"[test] {r.Status,-4} {c.Id} {r.Reason}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        int pass = results.Count(r => r.Status == \"pass\");");
            sb.AppendLine("        int fail = results.Count(r => r.Status == \"fail\");");
            sb.AppendLine("        int skip = results.Count(r => r.Status == \"skip\");");
            sb.AppendLine("        int flat = results.Count(r => r.Status == \"flat\");");
            sb.AppendLine();
            sb.AppendLine("        File.WriteAllText(Path.Combine(root, \"test-results.json\"),");
            sb.AppendLine("            JsonUtility.ToJson(new Results { Items = results }, true));");
            sb.AppendLine();
            sb.AppendLine("        Debug.Log($\"[test] done: {pass} pass, {fail} fail, {skip} skip, {flat} flat\");");
            sb.AppendLine("        // A non-zero exit code is what a CI step reads; Unity itself");
            sb.AppendLine("        // always exits 0 from -executeMethod.");
            sb.AppendLine("        if (Application.isBatchMode && fail > 0) EditorApplication.Exit(1);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    [Serializable] private sealed class Results { public List<Result> Items; }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Render one case.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// The shader is looked up by its asset path, so a case whose file did not");
            sb.AppendLine("    /// import is reported rather than silently passing: a missing shader would");
            sb.AppendLine("    /// otherwise render the magenta error material, which is opaque and would");
            sb.AppendLine("    /// look like a success.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static Result RunOne(Case c, string imageDir)");
            sb.AppendLine("    {");
            sb.AppendLine("        var r = new Result { Id = c.Id, Shader = c.Shader, Synthetic = c.Synthetic };");
            sb.AppendLine();
            sb.AppendLine("        string path = \"Assets/\" + c.ShaderFile.Substring(\"Assets/\".Length);");
            sb.AppendLine("        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);");
            sb.AppendLine("        if (shader == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            r.Status = \"fail\";");
            sb.AppendLine("            r.Reason = \"shader did not import: \" + path;");
            sb.AppendLine("            return r;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        Material mat = new Material(shader);");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            // A screen effect's source is a texture the game supplied at run");
            sb.AppendLine("            // time (the framebuffer).  The manifest can only name an asset path,");
            sb.AppendLine("            // and where the shader had no material that path is Unity's built-in");
            sb.AppendLine("            // 'white' -- a constant, which makes the effect render a constant.");
            sb.AppendLine("            // A photograph is substituted so what gets measured is the shader's");
            sb.AppendLine("            // behaviour rather than its input.");
            sb.AppendLine("            // Each replaced slot gets a different picture.  A shader that");
            sb.AppendLine("            // mixes two inputs (a blend, a LUT lookup) then has two");
            sb.AppendLine("            // distinguishable sources, which a single repeated picture");
            sb.AppendLine("            // would hide.");
            sb.AppendLine("            int slotIndex = 0;");
            sb.AppendLine("            // The first picture bound decides the frame's shape: a screen effect");
            sb.AppendLine("            // works in the UV space of the image it reads, so the frame is");
            sb.AppendLine("            // rendered at that picture's aspect ratio rather than at a fixed");
            sb.AppendLine("            // square.");
            sb.AppendLine("            Texture2D frameSource = null;");
            sb.AppendLine("            foreach (string slot in new[] { \"_MainTex\", \"_MainTex2\", \"_GrabTexture\" })");
            sb.AppendLine("            {");
            sb.AppendLine("                if (!mat.HasProperty(slot)) continue;");
            sb.AppendLine("                // Only a built-in constant is replaced.  The manifest also");
            sb.AppendLine("                // names real assets -- font atlases, gradient ramps -- and");
            sb.AppendLine("                // those are not copied into the test project, so a material");
            sb.AppendLine("                // that names one gets a null texture.  Substituting the");
            sb.AppendLine("                // source picture there would test a different shader than");
            sb.AppendLine("                // the one the game runs.");
            sb.AppendLine("                if (!IsConstantTexture(c.TexturePath(slot))) continue;");
            sb.AppendLine("                Texture2D picture = SourceTexture(slotIndex++);");
            sb.AppendLine("                mat.SetTexture(slot, picture);");
            sb.AppendLine("                if (frameSource == null) frameSource = picture;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // A screen effect normalises its UVs against the viewport:");
            sb.AppendLine("            // `uv = (pixel / _ScreenResolution.xy)`.  The material the game");
            sb.AppendLine("            // shipped carries whatever value the runtime pushed in, and a");
            sb.AppendLine("            // shader with no material of its own gets (0,0,0,0) -- which");
            sb.AppendLine("            // divides by zero and collapses every sample to one texel.");
            sb.AppendLine("            // That is a constant again, so the real size is supplied here.");
            sb.AppendLine("            GetFrameSize(frameSource, out int frameW, out int frameH);");
            sb.AppendLine("            foreach (string slot in new[] { \"_ScreenResolution\", \"_ScreenParams\",");
            sb.AppendLine("                                           \"_CustomScreenParams\", \"_Resolution\" })");
            sb.AppendLine("                if (mat.HasProperty(slot))");
            sb.AppendLine("                    mat.SetVector(slot, new Vector4(frameW, frameH, 1f / frameW, 1f / frameH));");
            sb.AppendLine();
            sb.AppendLine("            Texture2D png = Render(mat, frameSource);");
            sb.AppendLine("            Measure(png, out float opaque, out float luma, out float variance);");
            sb.AppendLine("            r.OpaqueRatio = opaque;");
            sb.AppendLine("            r.MeanLuma = luma;");
            sb.AppendLine("            r.Variance = variance;");
            sb.AppendLine();
            sb.AppendLine("            string file = Path.Combine(imageDir, Sanitise(c.Id) + \".png\");");
            sb.AppendLine("            File.WriteAllBytes(file, png.EncodeToPNG());");
            sb.AppendLine("            r.Image = Path.GetFileName(file);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(png);");
            sb.AppendLine();
            sb.AppendLine("            bool draws = opaque > 0.001f;");
            sb.AppendLine("            if (!draws)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Nothing was written at all.  Three causes are told apart,");
            sb.AppendLine("                // because only the last one is about the shader:");
            sb.AppendLine("                //");
            sb.AppendLine("                //   * A uniform the Properties block never declares.  The");
            sb.AppendLine("                //     game fills those from a script, so a rebuilt material");
            sb.AppendLine("                //     cannot supply them.");
            sb.AppendLine("                //   * A vertex-colour stream.  A Quad carries white, and a");
            sb.AppendLine("                //     shader that multiplies by it writes white -- but one");
            sb.AppendLine("                //     that keys on it (an outline mask, a progress fill)");
            sb.AppendLine("                //     writes nothing.  The geometry is what is wrong here.");
            sb.AppendLine("                //   * A shader that genuinely draws nothing for this input.");
            sb.AppendLine("                string missing = UndeclaredUniforms(mat);");
            sb.AppendLine("                if (missing != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"skip\";");
            sb.AppendLine("                    r.Reason = \"needs uniforms the shader never declared: \" + missing;");
            sb.AppendLine("                }");
            sb.AppendLine("                else if (ReadsVertexColor(path))");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"skip\";");
            sb.AppendLine("                    r.Reason = \"keys on vertex colour; a quad supplies none\";");
            sb.AppendLine("                }");
            sb.AppendLine("                else if (MissingTexture(c, mat))");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"skip\";");
            sb.AppendLine("                    r.Reason = \"reads a texture the test project does not carry\";");
            sb.AppendLine("                }");
            sb.AppendLine("                else if (ClearsOnly(path))");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"skip\";");
            sb.AppendLine("                    r.Reason = \"clear pass: writes a constant, draws no geometry\";");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"fail\";");
            sb.AppendLine("                    r.Reason = \"rendered nothing (fully transparent)\";");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (variance < 1e-5f)");
            sb.AppendLine("            {");
            sb.AppendLine("                // One flat colour across the whole frame.  Two causes are");
            sb.AppendLine("                // distinguished, because they mean opposite things:");
            sb.AppendLine("                //");
            sb.AppendLine("                //   * The shader reads a uniform the Properties block never");
            sb.AppendLine("                //     declares.  The game fills those from a script, and a");
            sb.AppendLine("                //     rebuilt material has nothing to fill them with -- so");
            sb.AppendLine("                //     the input is incomplete, not the shader wrong.");
            sb.AppendLine("                //   * The shader ignores its input.  A solid fill is legal,");
            sb.AppendLine("                //     so this is reported rather than failed.");
            sb.AppendLine("                //");
            sb.AppendLine("                // Either way it is not a pass: a frame with no structure");
            sb.AppendLine("                // cannot show that a transform, a blur or a colour key");
            sb.AppendLine("                // works, which is what this harness is for.");
            sb.AppendLine("                string missing = UndeclaredUniforms(mat);");
            sb.AppendLine("                if (missing != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"skip\";");
            sb.AppendLine("                    r.Reason = \"needs uniforms the shader never declared: \" + missing;");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"flat\";");
            sb.AppendLine("                    r.Reason = $\"uniform output: luma {luma:F2}, {opaque:P1} opaque\";");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (c.Synthetic)");
            sb.AppendLine("            {");
            sb.AppendLine("                r.Status = \"pass\";");
            sb.AppendLine("                r.Reason = $\"smoke: {opaque:P1} opaque, luma {luma:F2}\";");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                r.Status = \"pass\";");
            sb.AppendLine("                r.Reason = $\"{opaque:P1} opaque, luma {luma:F2}\";");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        catch (Exception e)");
            sb.AppendLine("        {");
            sb.AppendLine("            r.Status = \"fail\";");
            sb.AppendLine("            r.Reason = e.GetType().Name + \": \" + e.Message;");
            sb.AppendLine("        }");
            sb.AppendLine("        finally");
            sb.AppendLine("        {");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(mat);");
            sb.AppendLine("        }");
            sb.AppendLine("        return r;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// The quad the shader is drawn on, with every UV channel filled.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// Unity's built-in Quad carries UV0 only.  A shader that reads uv1 or uv2");
            sb.AppendLine("    /// then samples its texture at (0,0) -- one texel, so any effect that");
            sb.AppendLine("    /// varies across the surface collapses to a constant and looks broken.  The");
            sb.AppendLine("    /// mesh is built here instead so all four channels exist.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// The channels are not copies of one another: a shader that samples two of");
            sb.AppendLine("    /// them to compare or blend would otherwise get identical values and show");
            sb.AppendLine("    /// nothing.  UV1 is the surface scaled and shifted, UV2 is rotated a quarter");
            sb.AppendLine("    /// turn, UV3 is the surface mirrored -- each still spans the whole 0..1");
            sb.AppendLine("    /// range, so every channel has something to sample.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// Both windings are present.  A pass that culls front faces (a back-face");
            sb.AppendLine("    /// overlay) would draw nothing on a single sheet, and that is a limit of the");
            sb.AppendLine("    /// geometry rather than a defect in the shader.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static Mesh FullScreenQuad(float aspect)");
            sb.AppendLine("    {");
            sb.AppendLine("        float x = aspect * 0.5f, y = 0.5f;");
            sb.AppendLine("        var verts = new Vector3[]");
            sb.AppendLine("        {");
            sb.AppendLine("            new Vector3(-x, -y, 0), new Vector3( x, -y, 0),");
            sb.AppendLine("            new Vector3( x,  y, 0), new Vector3(-x,  y, 0),");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        var uv0 = new Vector2[]");
            sb.AppendLine("        {");
            sb.AppendLine("            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),");
            sb.AppendLine("        };");
            sb.AppendLine("        var uv1 = new Vector2[]");
            sb.AppendLine("        {");
            sb.AppendLine("            new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f),");
            sb.AppendLine("        };");
            sb.AppendLine("        var uv2 = new Vector2[]");
            sb.AppendLine("        {");
            sb.AppendLine("            new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1),");
            sb.AppendLine("        };");
            sb.AppendLine("        var uv3 = new Vector2[]");
            sb.AppendLine("        {");
            sb.AppendLine("            new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1),");
            sb.AppendLine("        };");
            sb.AppendLine("        var colors = new Color[]");
            sb.AppendLine("        {");
            sb.AppendLine("            Color.white, Color.white, Color.white, Color.white,");
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        var mesh = new Mesh { name = \"test-quad\" };");
            sb.AppendLine("        mesh.vertices = verts;");
            sb.AppendLine("        mesh.uv = uv0;");
            sb.AppendLine("        mesh.uv2 = uv1;");
            sb.AppendLine("        mesh.uv3 = uv2;");
            sb.AppendLine("        mesh.uv4 = uv3;");
            sb.AppendLine("        mesh.colors = colors;");
            sb.AppendLine("        // Front winding, then the same triangle reversed: both faces draw,");
            sb.AppendLine("        // so a Cull Front pass is measurable on a single sheet.");
            sb.AppendLine("        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };");
            sb.AppendLine("        mesh.RecalculateNormals();");
            sb.AppendLine("        mesh.RecalculateBounds();");
            sb.AppendLine("        return mesh;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    ///");
            sb.AppendLine("    /// An orthographic camera looking at one unit quad fills the viewport");
            sb.AppendLine("    /// exactly, so the whole image is the shader's output -- there is no");
            sb.AppendLine("    /// background to mistake for content.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// The frame takes the source picture's aspect ratio.  A screen effect");
            sb.AppendLine("    /// works in the same UV space it is drawn in, so squashing a 16:9 source");
            sb.AppendLine("    /// into a square changes the pattern a blur kernel, a radial distortion or");
            sb.AppendLine("    /// a dither matrix lays down -- and those patterns are what the cases are");
            sb.AppendLine("    /// meant to exercise.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static Texture2D Render(Material mat, Texture2D source)");
            sb.AppendLine("    {");
            sb.AppendLine("        GetFrameSize(source, out int width, out int height);");
            sb.AppendLine("        float aspect = (float)width / height;");
            sb.AppendLine("        // Built rather than CreatePrimitive: Unity's Quad carries UV0 only, and");
            sb.AppendLine("        // a shader reading uv1 or uv2 would sample one texel of its input.");
            sb.AppendLine("        var go = new GameObject(\"test-quad\");");
            sb.AppendLine("        var mesh = FullScreenQuad(aspect);");
            sb.AppendLine("        go.AddComponent<MeshFilter>().sharedMesh = mesh;");
            sb.AppendLine("        go.AddComponent<MeshRenderer>().sharedMaterial = mat;");
            sb.AppendLine("        var camGo = new GameObject(\"test-camera\");");
            sb.AppendLine("        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);");
            sb.AppendLine("        RenderTexture previous = RenderTexture.active;");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine();
            sb.AppendLine("            Camera cam = camGo.AddComponent<Camera>();");
            sb.AppendLine("            cam.orthographic = true;");
            sb.AppendLine("            // Half a unit tall means the visible height is one unit; the visible");
            sb.AppendLine("            // width is that times the aspect.  The vertices already span that,");
            sb.AppendLine("            // so the quad covers the frame exactly at any shape.");
            sb.AppendLine("            cam.orthographicSize = 0.5f;");
            sb.AppendLine("            cam.aspect = aspect;");
            sb.AppendLine("            cam.transform.position = new Vector3(0, 0, -1);");
            sb.AppendLine("            cam.clearFlags = CameraClearFlags.SolidColor;");
            sb.AppendLine("            // Opaque black: a shader that draws nothing leaves the image");
            sb.AppendLine("            // black, which is what makes \"fully transparent\" measurable.");
            sb.AppendLine("            cam.backgroundColor = new Color(0, 0, 0, 0);");
            sb.AppendLine("            cam.targetTexture = rt;");
            sb.AppendLine("            // Several effects animate off `_Time`, which is 0 in a batch-mode");
            sb.AppendLine("            // editor that never ticks a frame -- and a shader whose only");
            sb.AppendLine("            // variation is time then renders its t=0 frame, which for many of");
            sb.AppendLine("            // these effects is a flat colour.  Pushing the value in explicitly");
            sb.AppendLine("            // makes the render independent of whether the player loop ran.");
            sb.AppendLine("            Shader.SetGlobalVector(\"_Time\", new Vector4(TimeSinceStart, TimeSinceStart * 0.5f,");
            sb.AppendLine("                                                        TimeSinceStart * 2f, TimeSinceStart * 3f));");
            sb.AppendLine("            cam.Render();");
            sb.AppendLine();
            sb.AppendLine("            RenderTexture.active = rt;");
            sb.AppendLine("            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);");
            sb.AppendLine("            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);");
            sb.AppendLine("            tex.Apply();");
            sb.AppendLine("            return tex;");
            sb.AppendLine("        }");
            sb.AppendLine("        finally");
            sb.AppendLine("        {");
            sb.AppendLine("            RenderTexture.active = previous;");
            sb.AppendLine("            RenderTexture.ReleaseTemporary(rt);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(mesh);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(go);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(camGo);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// The frame size for one case.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// 1920x1080, which is the resolution the game renders at: a shader that");
            sb.AppendLine("    /// converts pixels to UVs against `_ScreenResolution` then works in exactly");
            sb.AppendLine("    /// the numbers it would see there, so a blur radius measured in texels or a");
            sb.AppendLine("    /// dither cell laid down per pixel lands where it does in game.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// A source picture with a different shape keeps its own ratio instead, at");
            sb.AppendLine("    /// the same pixel count.  Squashing it to 16:9 would change the pattern the");
            sb.AppendLine("    /// shader produces, which is the thing being measured.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static void GetFrameSize(Texture2D source, out int width, out int height)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (source == null) { width = OutputWidth; height = OutputHeight; return; }");
            sb.AppendLine();
            sb.AppendLine("        int srcW = source.width, srcH = source.height;");
            sb.AppendLine("        if (srcW <= 0 || srcH <= 0) { width = OutputWidth; height = OutputHeight; return; }");
            sb.AppendLine();
            sb.AppendLine("        // Same pixel budget as 1920x1080, arranged in the source's shape.");
            sb.AppendLine("        long budget = (long)OutputWidth * OutputHeight;");
            sb.AppendLine("        double scale = Math.Sqrt((double)budget / ((long)srcW * srcH));");
            sb.AppendLine("        width = Mathf.Max(1, Mathf.RoundToInt((float)(srcW * scale)));");
            sb.AppendLine("        height = Mathf.Max(1, Mathf.RoundToInt((float)(srcH * scale)));");
            sb.AppendLine();
            sb.AppendLine("        // A source that is already 16:9 lands on 1920x1080 exactly.  Rounding");
            sb.AppendLine("        // a 2848x1600 picture by area comes out one or two pixels wide, and a");
            sb.AppendLine("        // shader that divides by `_ScreenResolution.x` would then see a");
            sb.AppendLine("        // different denominator than it does in the game.");
            sb.AppendLine("        if (Math.Abs((double)srcW / srcH - (double)OutputWidth / OutputHeight) < 0.005)");
            sb.AppendLine("        {");
            sb.AppendLine("            width = OutputWidth;");
            sb.AppendLine("            height = OutputHeight;");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Render targets read back most predictably on even dimensions.");
            sb.AppendLine("        width += width & 1;");
            sb.AppendLine("        height += height & 1;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// The uniforms the shader reads but its Properties block never declares, or");
            sb.AppendLine("    /// null when every uniform it reads is reachable from the material.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// A game sets those from a script (CameraFilterPack does it for its whole");
            sb.AppendLine("    /// family).  A rebuilt material has no way to supply them, so the shader");
            sb.AppendLine("    /// reads zeros -- and an effect that divides by one of those zeros collapses");
            sb.AppendLine("    /// to a single colour no matter what its input texture holds.  Reporting it");
            sb.AppendLine("    /// separately keeps that apart from a shader that is actually wrong.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static string UndeclaredUniforms(Material mat)");
            sb.AppendLine("    {");
            sb.AppendLine("        Shader shader = mat.shader;");
            sb.AppendLine("        if (shader == null) return null;");
            sb.AppendLine("        string path = AssetDatabase.GetAssetPath(shader);");
            sb.AppendLine("        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;");
            sb.AppendLine();
            sb.AppendLine("        // A material only exposes what Properties declares, so a name that is");
            sb.AppendLine("        // declared would have answered HasProperty above.  What is left is the");
            sb.AppendLine("        // body's own reading of an identifier the material cannot reach.");
            sb.AppendLine("        string text = File.ReadAllText(path);");
            sb.AppendLine("        int props = text.IndexOf(\"Properties\", StringComparison.Ordinal);");
            sb.AppendLine("        int program = text.IndexOf(\"HLSLPROGRAM\", StringComparison.Ordinal);");
            sb.AppendLine("        if (props < 0 || program < 0) return null;");
            sb.AppendLine();
            sb.AppendLine("        var declared = new HashSet<string>(StringComparer.Ordinal);");
            sb.AppendLine("        foreach (System.Text.RegularExpressions.Match m in");
            sb.AppendLine("                 System.Text.RegularExpressions.Regex.Matches(");
            sb.AppendLine("                     text.Substring(props, program - props),");
            sb.AppendLine("                     @\"^\\s*(\\w+)\\s*\\(\",");
            sb.AppendLine("                     System.Text.RegularExpressions.RegexOptions.Multiline))");
            sb.AppendLine("            declared.Add(m.Groups[1].Value);");
            sb.AppendLine();
            sb.AppendLine("        // A property the material can reach has a value; anything else is a");
            sb.AppendLine("        // name the game's script used to fill.  Only declarations at file");
            sb.AppendLine("        // scope count: a `float4 in_POSITION0;` inside a function is a local");
            sb.AppendLine("        // the body assigns itself, and treating it as a uniform made almost");
            sb.AppendLine("        // every shader look undriveable.");
            sb.AppendLine("        string body = text.Substring(program);");
            sb.AppendLine("        var missing = new List<string>();");
            sb.AppendLine("        int depth = 0, lineStart = 0;");
            sb.AppendLine("        for (int i = 0; i < body.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            char ch = body[i];");
            sb.AppendLine("            if (ch == '\\n')");
            sb.AppendLine("            {");
            sb.AppendLine("                if (depth == 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    string line = body.Substring(lineStart, i - lineStart);");
            sb.AppendLine("                    System.Text.RegularExpressions.Match m =");
            sb.AppendLine("                        System.Text.RegularExpressions.Regex.Match(line,");
            sb.AppendLine("                            @\"^\\s*(?:float[1-4]?|half[1-4]?|fixed[1-4]?|int|uint|bool)\\s+(\\w+)\\s*;\");");
            sb.AppendLine("                    if (m.Success)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        string name = m.Groups[1].Value;");
            sb.AppendLine("                        if (!declared.Contains(name)");
            sb.AppendLine("                            && !name.StartsWith(\"unity_\", StringComparison.Ordinal)");
            sb.AppendLine("                            && !name.StartsWith(\"glstate_\", StringComparison.Ordinal)");
            sb.AppendLine("                            && !mat.HasProperty(name)");
            sb.AppendLine("                            && !missing.Contains(name))");
            sb.AppendLine("                            missing.Add(name);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                lineStart = i + 1;");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (ch == '{') depth++;");
            sb.AppendLine("            else if (ch == '}') depth--;");
            sb.AppendLine("        }");
            sb.AppendLine("        return missing.Count == 0 ? null : string.Join(\", \", missing);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// True when the shader's fragment stage reads a vertex-colour interpolator.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// The test geometry is a quad, whose vertex colour is opaque white.  A shader");
            sb.AppendLine("    /// that multiplies by it writes white and is fine; one that keys on it -- an");
            sb.AppendLine("    /// outline mask, a progress fill -- writes nothing at all, because the colour");
            sb.AppendLine("    /// it expects is per-vertex data the game supplies.  That is a limit of the");
            sb.AppendLine("    /// harness, not a defect in the shader.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static bool ReadsVertexColor(string shaderPath)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (!File.Exists(shaderPath)) return false;");
            sb.AppendLine("        string text = File.ReadAllText(shaderPath);");
            sb.AppendLine("        int program = text.IndexOf(\"HLSLPROGRAM\", StringComparison.Ordinal);");
            sb.AppendLine("        if (program < 0) return false;");
            sb.AppendLine("        string body = text.Substring(program);");
            sb.AppendLine("        // A COLOR0 interpolator in the interface, or a local filled from one.");
            sb.AppendLine("        return body.Contains(\": COLOR0\", StringComparison.Ordinal)");
            sb.AppendLine("            || body.Contains(\": COLOR1\", StringComparison.Ordinal)");
            sb.AppendLine("            || body.Contains(\": COLOR2\", StringComparison.Ordinal)");
            sb.AppendLine("            || body.Contains(\": COLOR3\", StringComparison.Ordinal);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// True when the case names a texture asset the test project does not have.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// The manifest records what the game bound, which is mostly font atlases and");
            sb.AppendLine("    /// gradient ramps.  Those are not copied into the project -- the shaders are");
            sb.AppendLine("    /// -- so the material ends up sampling nothing and a shader that multiplies");
            sb.AppendLine("    /// by the sample writes black.  Reported as a harness limit.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static bool MissingTexture(Case c, Material mat)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (c.TextureNames == null) return false;");
            sb.AppendLine("        for (int i = 0; i < c.TextureNames.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            string slot = c.TextureNames[i];");
            sb.AppendLine("            string path = c.TexturePaths != null && i < c.TexturePaths.Length ? c.TexturePaths[i] : null;");
            sb.AppendLine("            if (IsConstantTexture(path)) continue;");
            sb.AppendLine("            if (!mat.HasProperty(slot)) continue;");
            sb.AppendLine("            if (mat.GetTexture(slot) == null) return true;");
            sb.AppendLine("        }");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// True when the fragment stage returns a constant, ignoring every input.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// `Hidden/SimpleClear` is the archetype: its whole job is to fill the target");
            sb.AppendLine("    /// with one colour, and the value it uses arrives from the material rather");
            sb.AppendLine("    /// than from the geometry.  A quad drawn with it produces that colour, or");
            sb.AppendLine("    /// nothing when the constant is transparent -- neither outcome says anything");
            sb.AppendLine("    /// about whether the translation is right.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static bool ClearsOnly(string shaderPath)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (!File.Exists(shaderPath)) return false;");
            sb.AppendLine("        string text = File.ReadAllText(shaderPath);");
            sb.AppendLine("        int program = text.IndexOf(\"HLSLPROGRAM\", StringComparison.Ordinal);");
            sb.AppendLine("        if (program < 0) return false;");
            sb.AppendLine("        string body = text.Substring(program);");
            sb.AppendLine();
            sb.AppendLine("        // The fragment entry point must both sample nothing and return a");
            sb.AppendLine("        // literal.  Either alone is common and legitimate.");
            sb.AppendLine("        int frag = body.IndexOf(\"frag(\", StringComparison.Ordinal);");
            sb.AppendLine("        if (frag < 0) return false;");
            sb.AppendLine("        string fn = body.Substring(frag);");
            sb.AppendLine("        if (fn.Contains(\".Sample(\", StringComparison.Ordinal)) return false;");
            sb.AppendLine("        return System.Text.RegularExpressions.Regex.IsMatch(");
            sb.AppendLine("            fn, @\"=\\s*(?:float[1-4]?)?\\s*\\(?\\s*[\\d.]\");");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Share of pixels that drew anything, their mean brightness, and its spread.</summary>");
            sb.AppendLine("    private static void Measure(Texture2D tex, out float opaque, out float luma, out float variance)");
            sb.AppendLine("    {");
            sb.AppendLine("        Color32[] px = tex.GetPixels32();");
            sb.AppendLine("        long drawn = 0;");
            sb.AppendLine("        double sum = 0;");
            sb.AppendLine("        foreach (Color32 p in px)");
            sb.AppendLine("        {");
            sb.AppendLine("            // A pixel counts as drawn when it differs from the clear colour,");
            sb.AppendLine("            // not when its alpha is above zero.  An additive pass (Blend One");
            sb.AppendLine("            // One) writes colour with alpha still 0, and a premultiplied one");
            sb.AppendLine("            // writes alpha 0 over a black clear -- both drew something, and");
            sb.AppendLine("            // keying on alpha reported them as \"rendered nothing\".");
            sb.AppendLine("            if (p.a <= 8 && p.r <= 2 && p.g <= 2 && p.b <= 2) continue;");
            sb.AppendLine("            drawn++;");
            sb.AppendLine("            sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;");
            sb.AppendLine("        }");
            sb.AppendLine("        opaque = px.Length == 0 ? 0 : (float)drawn / px.Length;");
            sb.AppendLine("        luma = drawn == 0 ? 0 : (float)(sum / drawn);");
            sb.AppendLine();
            sb.AppendLine("        // The spread, not the mean: a flat frame and a frame with content can");
            sb.AppendLine("        // share a mean, and only the spread distinguishes them.");
            sb.AppendLine("        if (drawn == 0) { variance = 0; return; }");
            sb.AppendLine("        double mean = sum / drawn, acc = 0;");
            sb.AppendLine("        foreach (Color32 p in px)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (p.a <= 8 && p.r <= 2 && p.g <= 2 && p.b <= 2) continue;");
            sb.AppendLine("            double d = (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0 - mean;");
            sb.AppendLine("            acc += d * d;");
            sb.AppendLine("        }");
            sb.AppendLine("        variance = (float)(acc / drawn);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>A case id turned into a file name.</summary>");
            sb.AppendLine("    private static string Sanitise(string id)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (string.IsNullOrEmpty(id)) return \"case\";");
            sb.AppendLine("        var sb = new System.Text.StringBuilder(id.Length);");
            sb.AppendLine("        foreach (char ch in id)");
            sb.AppendLine("            sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);");
            sb.AppendLine("        return sb.ToString();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
