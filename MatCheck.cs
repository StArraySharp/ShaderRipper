using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShaderRipper
{
    /// <summary>
    /// --mat-check: reconcile every Material's saved property sheet against the
    /// property table of the Shader it points at.  Three independent value
    /// streams come back from the asset: floats (Float/Range), colors
    /// (Color/Vector) and texture bindings (TexEnvs).  A name the material sets
    /// but the shader lacks is either a version mismatch, a stripped-variant
    /// property, or evidence our property table is wrong — all worth surfacing.
    /// </summary>
    internal static class MatCheck
    {
        private sealed class Row
        {
            public string File, Material, Shader;
            public bool ShaderResolved;
            public int ShaderProps, SetFloats, SetColors, SetTextures;
            public List<string> Missing = new();   // set by material, absent from shader
            public List<string> Touched = new();   // set by material and known to shader
        }

        public static int Run(string dataDir, TypeNode jsonRoot,
                              Func<string, int, TypeNode> tpkClass, string outPath)
        {
            var rows = new List<Row>();
            int filesLoaded = 0, loadErrors = 0;

            // 全局 shader 库：文件标识 → pathId → shader。材质 PPtr 的 m_FileID
            // 指向 externals 表里的路径名，与加载文件的标识对上就能跨文件解析。
            var repo = new List<(string Key, Dictionary<long, Shader> ByPathId)>();

            foreach (var (fileName, loader) in EnumerateSerialized(dataDir, tpkClass))
            {
                SerializedFile sf;
                try { sf = loader(jsonRoot); filesLoaded++; }
                catch (Exception)
                {
                    if (!fileName.EndsWith("resources.assets", StringComparison.OrdinalIgnoreCase))
                        loadErrors++;
                    continue;
                }

                var byPathId = new Dictionary<long, Shader>();
                foreach (var sh in sf.Shaders)
                    byPathId[sh.PathId] = sh;
                repo.Add((KeyOf(fileName), byPathId));
                repo.Add((KeyOf("archive:/" + fileName + "/" + fileName), byPathId));

                foreach (var m in sf.Materials)
                {
                    var row = new Row { File = fileName, Material = m.Name };

                    Shader sh = null;
                    if (m.ShaderFileId == 0)
                        byPathId.TryGetValue(m.ShaderPathId, out sh);
                    else
                    {
                        // externals[i] == m_FileID-1 → 路径名 → 在库里找同名文件
                        int idx = m.ShaderFileId - 1;
                        string ext = idx >= 0 && idx < sf.Externals.Count ? sf.Externals[idx] : null;
                        if (ext != null)
                        {
                            string key = KeyOf(ext);
                            sh = repo.Where(r => r.Key == key)
                                     .Select(r => r.ByPathId)
                                     .FirstOrDefault(d => d.TryGetValue(m.ShaderPathId, out sh))
                                     is Dictionary<long, Shader> d && d.TryGetValue(m.ShaderPathId, out sh)
                                ? sh : null;
                        }
                    }

                    row.ShaderResolved = sh != null;
                    row.Shader = sh != null
                        ? sh.ParsedForm?.Name ?? sh.Name ?? ""
                        : m.ShaderFileId != 0
                            ? $"<external {ShaderName(sf, m.ShaderFileId)}>"
                            : "<shader not found>";
                    Reconcile(m, sh, row);
                    rows.Add(row);
                }
            }

            WriteReport(rows, outPath);

            int resolved = rows.Count(r => r.ShaderResolved);
            int problems = rows.Count(r => r.ShaderResolved && r.Missing.Count > 0);
            Console.WriteLine($"--mat-check: files={filesLoaded} (errors {loadErrors})  "
                              + $"materials={rows.Count}  shader-resolved={resolved}  "
                              + $"with unknown properties={problems}");
            if (outPath != null) Console.WriteLine($"report: {outPath}");
            foreach (var r in rows.Where(r => r.ShaderResolved && r.Missing.Count > 0).Take(12))
                Console.WriteLine($"  [?] {r.Material} -> {r.Shader}: {string.Join(", ", r.Missing.Take(6))}");
            return 0;
        }

        private static void Reconcile(MaterialAsset m, Shader sh, Row row)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (sh?.ParsedForm?.Props != null)
            {
                foreach (var p in sh.ParsedForm.Props) known.Add(p.Name);
                row.ShaderProps = known.Count;
            }

            foreach (var (name, _) in m.Floats)
                (known.Contains(name) ? row.Touched : row.Missing).Add(name);
            foreach (var (name, _, _, _, _) in m.Colors)
                (known.Contains(name) ? row.Touched : row.Missing).Add(name);
            foreach (var (name, fileId, pathId) in m.TexEnvs)
            {
                // 材质保留槽位但没绑纹理（PPtr 全零）是常态，不是 mismatch
                if (!known.Contains(name) && (fileId != 0 || pathId != 0))
                    row.Missing.Add(name);
                if (known.Contains(name))
                    row.Touched.Add(name);
            }

            row.SetFloats = m.Floats.Count;
            row.SetColors = m.Colors.Count;
            row.SetTextures = m.TexEnvs.Count;
        }

        private static string ShaderName(SerializedFile sf, int fileId)
        {
            int idx = fileId - 1;
            return idx >= 0 && idx < sf.Externals.Count ? Path.GetFileName(sf.Externals[idx]) : "?";
        }

        /// <summary>External path names ("sharedassets1.assets",
        /// "archive:/CAB-1234/CAB-1234") reduce to a comparable file key.</summary>
        private static string KeyOf(string path)
        {
            string p = path.Replace("\\", "/");
            if (p.StartsWith("archive:/", StringComparison.Ordinal))
                p = p.Substring("archive:/".Length);
            return Path.GetFileName(p.TrimEnd('/'));
        }

        private static void WriteReport(List<Row> rows, string outPath)
        {
            if (outPath == null) return;
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            using var w = new Utf8JsonWriter(File.Create(outPath), new JsonWriterOptions
            {
                Indented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            w.WriteStartObject();
            w.WriteNumber("materials", rows.Count);
            w.WriteStartArray("items");
            foreach (var r in rows)
            {
                w.WriteStartObject();
                w.WriteString("material", r.Material);
                w.WriteString("file", r.File);
                w.WriteString("shader", r.Shader);
                w.WriteBoolean("resolved", r.ShaderResolved);
                w.WriteNumber("shaderProps", r.ShaderProps);
                w.WriteNumber("floats", r.SetFloats);
                w.WriteNumber("colors", r.SetColors);
                w.WriteNumber("textures", r.SetTextures);
                w.WriteNumber("touched", r.Touched.Count);
                if (r.Missing.Count > 0)
                {
                    w.WriteStartArray("missingInShader");
                    foreach (var s in r.Missing) w.WriteStringValue(s);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        /// <summary>All parseable serialized sources under the game data dir:
        /// resources.assets, every loose *.assets, and every bundle node.</summary>
        private static IEnumerable<(string, Func<TypeNode, SerializedFile>)> EnumerateSerialized(
            string dataDir, Func<string, int, TypeNode> tpkClass)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<string>();
            string res = Path.Combine(dataDir, "resources.assets");
            if (File.Exists(res)) files.Add(res);
            if (Directory.Exists(dataDir))
                files.AddRange(Directory.EnumerateFiles(dataDir, "*.assets")
                    .Where(p => !p.EndsWith("resources.assets", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

            foreach (var f in files)
            {
                if (seen.Add(f))
                {
                    var local = f;
                    yield return (Path.GetFileName(local), root =>
                        SerializedFile.Load(File.ReadAllBytes(local), root, tpkClass));
                }
            }

            string bundles = Path.Combine(dataDir, "StreamingAssets");
            if (!Directory.Exists(bundles)) yield break;
            foreach (var bundle in Directory.EnumerateFiles(bundles, "*.bundle", SearchOption.AllDirectories))
            {
                List<Bundle.Node> nodes;
                try { nodes = Bundle.Read(bundle); }
                catch { continue; }
                foreach (var node in nodes)
                {
                    if (node.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                        node.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                    var data = node.Data;
                    yield return (node.Path, root =>
                        SerializedFile.Load(data, root, tpkClass));
                }
            }
        }
    }
}
