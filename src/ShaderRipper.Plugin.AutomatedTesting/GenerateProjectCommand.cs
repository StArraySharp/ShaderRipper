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

        public string Summary => "<export-dir> [output-dir] -- write a Unity project that renders every case";

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

            Manifest manifest = LoadManifest(exportDir, out ManifestError error);
            if (manifest == null)
            {
                Console.Error.WriteLine(error?.Message ?? "cannot read the manifest");
                return 2;
            }

            try
            {
                Write(exportDir, outDir, manifest);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"cannot write {outDir}: {e.Message}");
                return 1;
            }

            Console.WriteLine($"{outDir}");
            Console.WriteLine($"  {manifest.Tests.Count} test case(s), " +
                              $"{manifest.Tests.Count(t => t.Synthetic)} synthetic");
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
        private static void Write(string exportDir, string outDir, Manifest manifest)
        {
            Directory.CreateDirectory(outDir);

            // The shaders are copied, not referenced: a Unity project cannot see files
            // outside its own Assets folder, and a symlink would make the result depend
            // on how the checkout was made.
            string assets = Path.Combine(outDir, "Assets");
            CopyShaders(Path.Combine(exportDir, "Assets"), assets);

            WriteMinimalProject(outDir);

            string editor = Path.Combine(assets, "Editor");
            Directory.CreateDirectory(editor);
            File.WriteAllText(Path.Combine(editor, "ShaderRipperTest.cs"),
                              TestScript(manifest), new UTF8Encoding(false));

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

        private static void CopyShaders(string from, string toAssets)
        {
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
        private static string TestScript(Manifest manifest)
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
            sb.AppendLine("    private const int Size = 128;");
            sb.AppendLine();
            sb.AppendLine("    [Serializable] private sealed class Case");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Id; public string Shader; public string ShaderFile;");
            sb.AppendLine("        public string MaterialPath; public bool Synthetic; public string Strictness;");
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
            sb.AppendLine("        public string Status;      // pass | fail | skip");
            sb.AppendLine("        public string Reason;");
            sb.AppendLine("        public float OpaqueRatio;  // share of pixels with alpha > 0");
            sb.AppendLine("        public float MeanLuma;     // average brightness of the opaque pixels");
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
            sb.AppendLine();
            sb.AppendLine("        File.WriteAllText(Path.Combine(root, \"test-results.json\"),");
            sb.AppendLine("            JsonUtility.ToJson(new Results { Items = results }, true));");
            sb.AppendLine();
            sb.AppendLine("        Debug.Log($\"[test] done: {pass} pass, {fail} fail, {skip} skip\");");
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
            sb.AppendLine("            Texture2D png = Render(mat);");
            sb.AppendLine("            Measure(png, out float opaque, out float luma);");
            sb.AppendLine("            r.OpaqueRatio = opaque;");
            sb.AppendLine("            r.MeanLuma = luma;");
            sb.AppendLine();
            sb.AppendLine("            string file = Path.Combine(imageDir, Sanitise(c.Id) + \".png\");");
            sb.AppendLine("            File.WriteAllBytes(file, png.EncodeToPNG());");
            sb.AppendLine("            r.Image = Path.GetFileName(file);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(png);");
            sb.AppendLine();
            sb.AppendLine("            // A synthetic case uses Unity's declared defaults, not the values");
            sb.AppendLine("            // the game chose, so only its ability to draw is meaningful.");
            sb.AppendLine("            bool draws = opaque > 0.001f;");
            sb.AppendLine("            if (!draws)");
            sb.AppendLine("            {");
            sb.AppendLine("                // A screen effect reads the framebuffer through _MainTex and");
            sb.AppendLine("                // expects a camera that already rendered something.  Given a");
            sb.AppendLine("                // blank target it outputs nothing, which is correct behaviour");
            sb.AppendLine("                // rather than a broken shader -- so it is reported as skipped,");
            sb.AppendLine("                // not failed, and the reason says why.");
            sb.AppendLine("                if (mat.HasProperty(\"_MainTex\") &&");
            sb.AppendLine("                    mat.GetTexture(\"_MainTex\") == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"skip\";");
            sb.AppendLine("                    r.Reason = \"screen effect: _MainTex needs a rendered source\";");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine("                    r.Status = \"fail\";");
            sb.AppendLine("                    r.Reason = \"rendered nothing (fully transparent)\";");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (c.Synthetic)");
            sb.AppendLine("            {");
            sb.AppendLine("                r.Status = \"pass\";");
            sb.AppendLine("                r.Reason = $\"smoke: {opaque:P1} opaque\";");
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
            sb.AppendLine("    /// Draw a full-screen quad with the material and read it back.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// An orthographic camera looking at one unit quad fills the viewport");
            sb.AppendLine("    /// exactly, so the whole image is the shader's output -- there is no");
            sb.AppendLine("    /// background to mistake for content.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    private static Texture2D Render(Material mat)");
            sb.AppendLine("    {");
            sb.AppendLine("        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);");
            sb.AppendLine("        var camGo = new GameObject(\"test-camera\");");
            sb.AppendLine("        RenderTexture rt = RenderTexture.GetTemporary(Size, Size, 24);");
            sb.AppendLine("        RenderTexture previous = RenderTexture.active;");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            go.GetComponent<MeshRenderer>().sharedMaterial = mat;");
            sb.AppendLine();
            sb.AppendLine("            Camera cam = camGo.AddComponent<Camera>();");
            sb.AppendLine("            cam.orthographic = true;");
            sb.AppendLine("            cam.orthographicSize = 0.5f;");
            sb.AppendLine("            cam.transform.position = new Vector3(0, 0, -1);");
            sb.AppendLine("            cam.clearFlags = CameraClearFlags.SolidColor;");
            sb.AppendLine("            // Opaque black: a shader that draws nothing leaves the image");
            sb.AppendLine("            // black, which is what makes \"fully transparent\" measurable.");
            sb.AppendLine("            cam.backgroundColor = new Color(0, 0, 0, 0);");
            sb.AppendLine("            cam.targetTexture = rt;");
            sb.AppendLine("            cam.Render();");
            sb.AppendLine();
            sb.AppendLine("            RenderTexture.active = rt;");
            sb.AppendLine("            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);");
            sb.AppendLine("            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);");
            sb.AppendLine("            tex.Apply();");
            sb.AppendLine("            return tex;");
            sb.AppendLine("        }");
            sb.AppendLine("        finally");
            sb.AppendLine("        {");
            sb.AppendLine("            RenderTexture.active = previous;");
            sb.AppendLine("            RenderTexture.ReleaseTemporary(rt);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(go);");
            sb.AppendLine("            UnityEngine.Object.DestroyImmediate(camGo);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Share of pixels that drew anything, and their mean brightness.</summary>");
            sb.AppendLine("    private static void Measure(Texture2D tex, out float opaque, out float luma)");
            sb.AppendLine("    {");
            sb.AppendLine("        Color32[] px = tex.GetPixels32();");
            sb.AppendLine("        long drawn = 0;");
            sb.AppendLine("        double sum = 0;");
            sb.AppendLine("        foreach (Color32 p in px)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (p.a <= 8) continue;");
            sb.AppendLine("            drawn++;");
            sb.AppendLine("            sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;");
            sb.AppendLine("        }");
            sb.AppendLine("        opaque = px.Length == 0 ? 0 : (float)drawn / px.Length;");
            sb.AppendLine("        luma = drawn == 0 ? 0 : (float)(sum / drawn);");
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
