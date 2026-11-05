using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShaderRipper.Plugins;

namespace ShaderRipper.Plugin.AutomatedTesting
{
    /// <summary>
    /// <c>--test-manifest &lt;export-dir&gt;</c>: rebuild the manifest from an export that
    /// already exists.
    ///
    /// Iterating on a harness means re-reading the same export dozens of times, and the
    /// export is the expensive half.  This reconstructs the two things the manifest is
    /// built from -- the shaders written and the material index -- straight out of the
    /// directory, so the harness can be developed without touching the game data.
    ///
    /// What it cannot recover is the reason a shader was skipped: that lives in the
    /// export's own marker file, which records it as prose rather than as data.  The
    /// regenerated manifest therefore reports zero skips, and says so.
    /// </summary>
    internal sealed class RegenerateCommand : ICommandHandler
    {
        public string Command => "--test-manifest";

        public string Summary => "<export-dir> -- rebuild the render-test manifest from an existing export";

        public int PositionalCount => 1;

        public int Run(CommandArgs args)
        {
            string dir = args.Positionals[0];
            if (!Directory.Exists(dir))
            {
                Console.Error.WriteLine($"no such directory: {dir}");
                return 2;
            }

            string materialsPath = Path.Combine(dir, ".shaderripper-materials.json");
            if (!File.Exists(materialsPath))
            {
                Console.Error.WriteLine(
                    $"{dir} holds no .shaderripper-materials.json -- run an export with " +
                    "the material index enabled first");
                return 2;
            }

            List<ExportedShader> shaders = ReadShaders(dir);
            List<ExportedMaterial> materials = ReadMaterials(materialsPath);
            if (materials == null)
            {
                Console.Error.WriteLine($"{materialsPath} is not readable as a material index");
                return 1;
            }

            var context = new ExportCompleted
            {
                OutputDirectory = dir,
                Shaders = shaders,
                Materials = materials,
                // Not recoverable from the directory: the marker records skips as
                // prose.  Reported as zero rather than guessed at.
                Skipped = 0,
                Translated = 0,
                Cancelled = false,
            };

            Manifest manifest = ManifestBuilder.Build(context);
            ManifestWriter.Write(dir, manifest);

            Console.WriteLine($"{manifest.Count} test case(s) from {shaders.Count} shader(s) " +
                              $"and {materials.Count} material(s)");
            int synthetic = manifest.Tests.Count(t => t.Synthetic);
            if (synthetic > 0)
                Console.WriteLine($"  {synthetic} synthetic (Unity defaults -- smoke test only)");
            return 0;
        }

        /// <summary>
        /// The shaders the directory holds, with their Properties blocks read back out
        /// of the emitted text.
        ///
        /// The exporter writes the Properties block verbatim, so it is the source of
        /// truth for a shader whose asset data is no longer at hand -- which is exactly
        /// the situation this command exists for.
        /// </summary>
        private static List<ExportedShader> ReadShaders(string dir)
        {
            var result = new List<ExportedShader>();
            string assets = Path.Combine(dir, "Assets");
            if (!Directory.Exists(assets)) return result;

            foreach (string file in Directory.EnumerateFiles(assets, "*.shader", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }

                string rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                result.Add(new ExportedShader
                {
                    Name = ShaderNameOf(text) ?? Path.GetFileNameWithoutExtension(file),
                    Path = rel,
                    Properties = ParseProperties(text),
                });
            }
            return result;
        }

        /// <summary>The name on the <c>Shader "..."</c> line, or null.</summary>
        private static string ShaderNameOf(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, "^\\s*Shader\\s+\"([^\"]+)\"",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Read the Properties block back into property descriptors.
        ///
        /// The emitted line is <c>_Name ("Display", Type) = value</c>, where the type
        /// decides which of the four shapes the value takes.  Only what a harness needs
        /// is recovered: the name, the type id, and the default.
        /// </summary>
        private static List<ExportedProperty> ParseProperties(string text)
        {
            var result = new List<ExportedProperty>();

            var block = System.Text.RegularExpressions.Regex.Match(text,
                @"^\s*Properties\s*\{(.*?)^\s*\}", System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!block.Success) return result;

            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(block.Groups[1].Value,
                @"^\s*(?:\[[^\]]*\]\s*)*(\w+)\s*\(\s*""[^""]*""\s*,\s*(\w+(?:\s*\(\s*\d+\s*\))?)\s*\)\s*=\s*(.+?)\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                string name = m.Groups[1].Value;
                string type = m.Groups[2].Value.Trim();
                string value = m.Groups[3].Value.Trim();

                int id;
                if (type.StartsWith("Color", StringComparison.OrdinalIgnoreCase)) id = 0;
                else if (type.StartsWith("Vector", StringComparison.OrdinalIgnoreCase)) id = 1;
                else if (type.StartsWith("Int", StringComparison.OrdinalIgnoreCase)) id = 2;
                else if (type.StartsWith("Float", StringComparison.OrdinalIgnoreCase)) id = 2;
                else if (type.StartsWith("Range", StringComparison.OrdinalIgnoreCase)) id = 3;
                else id = 4;   // 2D / 3D / Cube

                var prop = new ExportedProperty { Name = name, Type = id };
                if (id == 4)
                {
                    var tex = System.Text.RegularExpressions.Regex.Match(value, "\"([^\"]*)\"");
                    prop = new ExportedProperty
                    {
                        Name = name,
                        Type = id,
                        DefaultTexture = tex.Success ? tex.Groups[1].Value : null,
                    };
                }
                else if (value.StartsWith("(", StringComparison.Ordinal))
                {
                    prop = new ExportedProperty
                    {
                        Name = name,
                        Type = id,
                        DefaultValue = ParseVector(value),
                    };
                }
                else
                {
                    prop = new ExportedProperty
                    {
                        Name = name,
                        Type = id,
                        DefaultValue = new[] { ParseFloat(value), 0f, 0f, 0f },
                    };
                }
                result.Add(prop);
            }
            return result;
        }

        private static float[] ParseVector(string value)
        {
            var parts = value.Trim('(', ')').Split(',');
            var result = new float[4];
            for (int i = 0; i < 4; i++)
                result[i] = i < parts.Length ? ParseFloat(parts[i]) : 0f;
            return result;
        }

        private static float ParseFloat(string s) =>
            float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;

        /// <summary>
        /// Read the material index the exporter wrote.
        ///
        /// Deserialized into the same shape the hook receives, so the regenerated
        /// manifest goes through the identical pipeline as one written during an export.
        /// </summary>
        private static List<ExportedMaterial> ReadMaterials(string path)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                        .AllowNamedFloatingPointLiterals,
                };
                using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                var result = new List<ExportedMaterial>();
                if (!doc.RootElement.TryGetProperty("materials", out var list)) return result;

                foreach (var e in list.EnumerateArray())
                {
                    result.Add(new ExportedMaterial
                    {
                        Name = Str(e, "Name"),
                        AssetPath = Str(e, "AssetPath"),
                        Shader = Str(e, "Shader"),
                        ShaderFile = Str(e, "ShaderFile"),
                        Synthetic = e.TryGetProperty("Synthetic", out var syn) &&
                                    syn.ValueKind == JsonValueKind.True,
                        Floats = ReadFloats(e),
                        Colors = ReadColors(e),
                        Textures = ReadTextures(e),
                    });
                }
                return result;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        private static Dictionary<string, float> ReadFloats(JsonElement e)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (!e.TryGetProperty("Floats", out var f) || f.ValueKind != JsonValueKind.Object) return result;
            foreach (var p in f.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number) result[p.Name] = (float)p.Value.GetDouble();
            return result;
        }

        private static Dictionary<string, float[]> ReadColors(JsonElement e)
        {
            var result = new Dictionary<string, float[]>(StringComparer.Ordinal);
            if (!e.TryGetProperty("Colors", out var c) || c.ValueKind != JsonValueKind.Object) return result;
            foreach (var p in c.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Array) continue;
                var v = new float[4];
                int i = 0;
                foreach (var x in p.Value.EnumerateArray())
                    if (i < 4 && x.ValueKind == JsonValueKind.Number) v[i++] = (float)x.GetDouble();
                result[p.Name] = v;
            }
            return result;
        }

        private static Dictionary<string, string> ReadTextures(JsonElement e)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!e.TryGetProperty("Textures", out var t) || t.ValueKind != JsonValueKind.Object) return result;
            foreach (var p in t.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String) result[p.Name] = p.Value.GetString();
            return result;
        }
    }
}
