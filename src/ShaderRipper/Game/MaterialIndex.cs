using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ShaderRipper.Game
{
    /// <summary>
    /// The materials an export found, written beside the shaders as one hidden JSON
    /// file.
    ///
    /// A <c>.shader</c> file alone cannot be render-tested: the thing a test harness
    /// has to reproduce is the material -- which shader it binds, and every property
    /// value the game saved on it.  Unity's own serialized data keeps that, but only
    /// inside the <c>.assets</c> the material came from, which a test environment
    /// does not have.  This index is that table, flattened and keyed by a stable path,
    /// so a harness can create the materials, assign them and render.
    ///
    /// The file is written next to the shaders and hidden on both platforms: a dot
    /// prefix keeps it out of the way on Unix and out of Unity's importer on Windows.
    /// </summary>
    internal static class MaterialIndex
    {
        /// <summary>Name of the index file; dot-prefixed so an import ignores it.</summary>
        internal const string FileName = ".shaderripper-materials.json";

        /// <summary>One material as the index records it.</summary>
        internal sealed class Entry
        {
            /// <summary>Material asset name (<c>m_Name</c>).</summary>
            public string Name { get; set; }

            /// <summary>
            /// Where the material should be created in a reimported project.  Derived
            /// from the shader's own output path plus the material name, so a harness
            /// can place it without guessing.
            /// </summary>
            public string AssetPath { get; set; }

            /// <summary>Shader name the material binds (<c>m_Shader</c>).</summary>
            public string Shader { get; set; }

            /// <summary>
            /// Path of the emitted <c>.shader</c> file, relative to the export root.
            /// Null when the shader was not exported (a built-in, or a shader that
            /// failed to translate) -- the material still belongs in the index, with
            /// the reason visible to the harness.
            /// </summary>
            public string ShaderFile { get; set; }

            /// <summary>The file the material object was read from.</summary>
            public string SourceFile { get; set; }

            /// <summary>Float and Range properties, name to value.</summary>
            public SortedDictionary<string, float> Floats { get; } = new(StringComparer.Ordinal);

            /// <summary>Color and Vector properties, name to <c>[r,g,b,a]</c>.</summary>
            public SortedDictionary<string, float[]> Colors { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// Texture properties: property name to the texture's asset path when the
            /// resource map knows it, otherwise the PPtr as <c>fileId:pathId</c>.
            /// </summary>
            public SortedDictionary<string, string> Textures { get; } = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// Collect every material the export saw, resolving each one's shader and
        /// texture references through the same tables the exporter already built.
        /// </summary>
        /// <param name="shaderPathOf">Shader object to its emitted path, relative to
        /// the export root; absent when the shader was not written.</param>
        /// <param name="resourceOf">Texture PPtr to asset path.</param>
        public static List<Entry> Collect(
            IEnumerable<(Data.SerializedFile file, string sourceName)> files,
            IReadOnlyDictionary<(long pathId, string source), string> shaderPathOf,
            Func<int, long, string> resourceOf)
        {
            var result = new List<Entry>();
            foreach (var (sf, sourceName) in files)
            {
                foreach (Data.MaterialAsset mat in sf.Materials)
                {
                    var entry = new Entry
                    {
                        Name = mat.Name,
                        SourceFile = sourceName,
                    };

                    // m_Shader is a PPtr: file 0 means "same serialized file", which is
                    // the common case; anything else names an external .assets.  The
                    // shader table is keyed by path id, so a same-file reference
                    // resolves directly.
                    if (mat.ShaderPathId != 0)
                    {
                        if (shaderPathOf.TryGetValue((mat.ShaderPathId, sourceName), out string path))
                        {
                            entry.ShaderFile = path;
                            entry.Shader = Path.GetFileNameWithoutExtension(path);
                        }
                        else if (sf.Shaders.Find(x => x.PathId == mat.ShaderPathId) is { } own)
                        {
                            entry.Shader = own.Name;
                        }
                    }

                    foreach (var (name, value) in mat.Floats)
                        entry.Floats[name] = value;
                    foreach (var (name, r, g, b, a) in mat.Colors)
                        entry.Colors[name] = new[] { r, g, b, a };
                    foreach (var (name, fileId, pathId) in mat.TexEnvs)
                    {
                        string resolved = pathId == 0 ? null : resourceOf(fileId, pathId);
                        entry.Textures[name] = resolved ?? $"{fileId}:{pathId}";
                    }

                    // Placed beside the shader so a harness can compute a project path
                    // without a naming table of its own.
                    string dir = entry.ShaderFile != null
                        ? Path.GetDirectoryName(entry.ShaderFile)?.Replace('\\', '/')
                        : "Assets/Materials";
                    string safe = SafeName(mat.Name);
                    entry.AssetPath = $"{dir}/{safe}.mat";

                    result.Add(entry);
                }
            }
            return result;
        }

        /// <summary>
        /// Write the index, or remove a stale one when the export produced nothing.
        ///
        /// A run with no materials still removes the previous file: leaving it behind
        /// would let a harness render against a shader set that no longer exists.
        /// </summary>
        internal static void Write(string dir, List<Entry> entries)
        {
            string path = Path.Combine(dir, FileName);
            try
            {
                if (entries == null || entries.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // Unity's own JSON writer uses the same shape, so a harness written
                    // against that reads this one unchanged.
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    // A material may hold an infinity or a NaN -- a Range property the
                    // game never clamped, a vector a script wrote.  Rejecting the whole
                    // file over one such value would lose the entire index, so they are
                    // written as the JSON tokens ("Infinity"/"NaN") .NET defines for
                    // them; a harness reads them back through the same option.
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling
                        .AllowNamedFloatingPointLiterals,
                };

                var payload = new
                {
                    generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    count = entries.Count,
                    materials = entries,
                };

                File.WriteAllText(path, JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));

                // Mark it hidden on Windows; the dot prefix already hides it on Unix
                // and keeps Unity's importer from claiming it either way.
                if (OperatingSystem.IsWindows())
                {
                    try { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden); }
                    catch (IOException) { }            // a network share may refuse
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }        // a read-only target is not worth failing over
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// A material name turned into a file name: the characters Unity itself
        /// rejects are replaced, and an empty name gets a stable placeholder so two
        /// unnamed materials still land on distinct paths.
        /// </summary>
        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString().Trim();
        }
    }
}
