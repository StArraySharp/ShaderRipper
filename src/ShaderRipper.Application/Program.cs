using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShaderRipper.Cg;

namespace ShaderRipper
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var cli = Argv.Parse(args);

            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {  }

            Strings.SetLang(cli.Value("--lang"));

            if (cli.Has("--help") || cli.Has("-h")) { Usage(); return 0; }
            string disasmFile = cli.Value("--disasm");
            if (disasmFile != null) return DisasmFile(disasmFile);
            string glslFile = cli.Value("--glsl2cg");
            if (glslFile != null)
            {
                Cg.GlslToCg.AnnotateSource = cli.Has("--asm");
                Console.Write(Cg.GlslToCg.Translate(File.ReadAllText(glslFile)).Cg);
                return 0;
            }

            string rootArg = cli.Value("--root") ?? Environment.GetEnvironmentVariable("SHADERRIPPER_ROOT") ?? Environment.GetEnvironmentVariable("SHADERRIP_ROOT");
            Paths.SetRoot(rootArg);
            if (!string.IsNullOrWhiteSpace(rootArg) && !Directory.Exists(rootArg))
                Console.Error.WriteLine(Strings.F("RootDirMissing", rootArg));

            // --list-shaders works standalone: it takes a file directly and
            // must not require --data/--file.
            string dumpObjects = cli.Value("--dump-objects");
            if (dumpObjects != null)
            {
                Paths.SetRoot(cli.Value("--root"));
                var dRoot = TypeNode.LoadSchema(Paths.Schema(
                    ReadSerializedFileVersion(dumpObjects) >= 22 ? "shader_typetypes_6000.json" : "shader_typetypes.json",
                    cli.Value("--schema")));
                var dTpk = new Func<string, int, TypeNode>((ver, id) =>
                    TpkSchema.Available ? TpkSchema.BuildClass(id, ver) : null);
                var dsf = SerializedFile.Load(dumpObjects, dRoot, dTpk);
                Console.WriteLine($"version={dsf.UnityVersion} objects={dsf.Objects.Count} externals={dsf.Externals.Count}");
                var seen = new HashSet<int>();
                foreach (var (pid, tid, off, size) in dsf.Objects)
                {
                    int cls = tid >= 0 && tid < dsf.ClassIds.Count ? dsf.ClassIds[tid] : -1;
                    TypeNode tree = null;
                    try { tree = cls > 0 ? dTpk(dsf.UnityVersion, cls) : null; } catch { }
                    string kids = tree == null ? "<no tree>" : string.Join(",", tree.Children.ConvertAll(c => c.Name));
                    if (seen.Add(cls))
                        Console.WriteLine($"  class={cls} size={size} children=[{kids}]  (pathId={pid})");
                }
                foreach (var e in dsf.Externals) Console.WriteLine("  ext: " + e);
                return 0;
            }

            string listPath = cli.Value("--list-shaders");
            if (listPath != null && !File.Exists(listPath) && cli.Bare.Count > 0 && File.Exists(cli.Bare[0]))
                listPath = cli.Bare[0];
            if (listPath != null)
            {
                if (!File.Exists(listPath)) { Console.Error.WriteLine(Strings.F("ResourcesAssetsMissing", listPath)); return 2; }
                Paths.SetRoot(cli.Value("--root"));
                var listRoot = TypeNode.LoadSchema(Paths.Schema(
                    ReadSerializedFileVersion(listPath) >= 22 ? "shader_typetypes_6000.json" : "shader_typetypes.json",
                    cli.Value("--schema")));
                if (listRoot == null) { Console.Error.WriteLine("schema not found"); return 3; }
                Func<string, int, TypeNode> listTpk = (ver, classId) =>
                    TpkSchema.Available ? TpkSchema.BuildClass(classId, ver) : null;
                return ListShaders(listPath, listRoot, listTpk);
            }

            string dataDir = FirstNonEmpty(cli.Value("--data"),
                                           Environment.GetEnvironmentVariable("SHADERRIPPER_DATA") ?? Environment.GetEnvironmentVariable("SHADERRIP_DATA"),
                                           cli.Bare.Count > 0 ? cli.Bare[0] : null);
            if (dataDir == null && cli.Value("--file") == null)
            {
                Console.Error.WriteLine(Strings.Get("NoDataDir"));
                Console.Error.WriteLine();
                Usage();
                return 2;
            }

            // Collect input files: repeatable --file, comma/semicolon separated
            // values, wildcards, and bare args that point at existing files.
            var inputFiles = new List<string>();
            foreach (var rawVal in cli.Values("--file"))
                foreach (var seg in rawVal.Split(',', ';'))
                {
                    string v = seg.Trim();
                    if (v.Length == 0) continue;
                    if (v.Contains('*') || v.Contains('?'))
                    {
                        string dir = Path.GetDirectoryName(Path.GetFullPath(v));
                        string pat = Path.GetFileName(v);
                        if (Directory.Exists(dir))
                            foreach (var hit in Directory.GetFiles(dir, pat)) inputFiles.Add(hit);
                    }
                    else inputFiles.Add(v);
                }
            foreach (var b in cli.Bare)
                if (File.Exists(b)) inputFiles.Add(b);
            // dedupe, keep order
            var seenInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            inputFiles = inputFiles.Select(Path.GetFullPath).Where(f => seenInputs.Add(f)).ToList();
            bool fileMode = inputFiles.Count > 0;
            if (fileMode)
            {
                foreach (var f in inputFiles)
                    if (!File.Exists(f)) { Console.Error.WriteLine(Strings.F("ResourcesAssetsMissing", f)); return 2; }
                if (dataDir == null) dataDir = Path.GetDirectoryName(inputFiles[0]);
            }
            dataDir = Path.GetFullPath(dataDir);
            string assets = fileMode ? inputFiles[0] : null;
            if (!fileMode)
            {
                assets = Path.Combine(dataDir, "resources.assets");
                if (!File.Exists(assets)) { Console.Error.WriteLine(Strings.F("ResourcesAssetsMissing", assets)); return 2; }
            }

            GlobalsNames.TableDir = cli.Value("--names")
                                ?? Paths.FindDir(Path.Combine("data", "globals_names"))
                                ?? Paths.FindDir(Path.Combine("..", "data", "globals_names"));

            string only = cli.Value("--only");

            int? forcedPlatform = Platforms.ByName(cli.Value("--platform"));

            bool dumpMode = cli.Has("--dump");
            bool matCheck = cli.Has("--mat-check");
            string dumpFilter = cli.Value("--dump") ?? "";

            string bundleDir = cli.Value("--bundles") ?? dataDir;

            if (cli.Has("--asm"))
            {
                global::ShaderRipper.Cg.CgProgramWriter.AnnotateAsm = true;
                global::ShaderRipper.Cg.GlslToCg.AnnotateSource = true;
            }

            int indexCount = BuiltinIndex.Load(cli.Value("--builtin-index"));

            int matrixCount = MatrixNames.Load(cli.Value("--matrix-names"));

            int tmpLayoutCount = Cg.TmpLayouts.Load(cli.Value("--tmp-layouts"));
            if (cli.Has("--no-tmp-layout")) Cg.TmpLayouts.Enabled = false;

            // ---- preload inputs: bundle/apk payloads are read once up front ----
            // sources = (bytes, path, container): exactly one of bytes/path is set;
            // container is the bundle-node or apk-entry path used for the output tree.
            var sources = new List<(byte[] bytes, string path, string container, string sourceFile)>();
            uint sfVersion = 0;
            if (fileMode)
            {
                foreach (var f in inputFiles)
                {
                    try
                    {
                        switch (SniffFile(f))
                        {
                            case InputKind.Bundle:
                                foreach (var nd in Bundle.Read(f))
                                {
                                    if (SkipNode(nd.Path)) continue;
                                    sources.Add((nd.Data, null, nd.Path, null));
                                    if (sfVersion == 0) { uint v = ReadVersionFromBytes(nd.Data); if (v > 10 && v < 1000) sfVersion = v; }
                                }
                                break;

                            case InputKind.Zip:
                                using (var zip = System.IO.Compression.ZipFile.OpenRead(f))
                                    foreach (var entry in zip.Entries)
                                    {
                                        string n = entry.FullName;
                                        if (SkipNode(n) || entry.Length < 12) continue;
                                        byte[] head = ReadHead(entry, 12);
                                        switch (SniffHead(head))
                                        {
                                            case InputKind.Bundle:
                                            {
                                                using var es = entry.Open();
                                                var ms = new MemoryStream();
                                                es.CopyTo(ms);
                                                List<Bundle.Node> nodes;
                                                try { nodes = Bundle.Read(ms.ToArray()); }
                                                catch (Exception e) { Console.Error.WriteLine($"    entry {n}: {e.Message}"); continue; }
                                                foreach (var nd in nodes)
                                                {
                                                    if (SkipNode(nd.Path)) continue;
                                                    // node paths are already project-relative; the
                                                    // apk entry name must not leak into them.
                                                    sources.Add((nd.Data, null, nd.Path, null));
                                                    if (sfVersion == 0) { uint v = ReadVersionFromBytes(nd.Data); if (v > 10 && v < 1000) sfVersion = v; }
                                                }
                                                break;
                                            }
                                            case InputKind.Serialized:
                                            {
                                                // Unity data files inside an apk often have no
                                                // extension at all (GUID-named) — sniff by header.
                                                using var es = entry.Open();
                                                var ms = new MemoryStream();
                                                es.CopyTo(ms);
                                                sources.Add((ms.ToArray(), null, null, Path.GetFileName(n)));
                                                if (sfVersion == 0) { uint v = ReadVersionFromBytes(ms.ToArray()); if (v > 10 && v < 1000) sfVersion = v; }
                                                break;
                                            }
                                        }
                                    }
                                break;

                            case InputKind.Serialized:
                                sources.Add((null, f, null, Path.GetFileName(f)));
                                if (sfVersion == 0) sfVersion = ReadSerializedFileVersion(f);
                                break;

                            default:
                                Console.Error.WriteLine($"  [!] unrecognised container format, skipped: {f}");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(Strings.F("BundleSkipped", Path.GetFileName(f), e.Message));
                    }
                }
                if (sources.Count == 0) { Console.Error.WriteLine("no shader-bearing files found in the given inputs"); return 2; }
            }
            else
            {
                sfVersion = ReadSerializedFileVersion(assets);
            }
            string schemaName = sfVersion >= 22 ? "shader_typetypes_6000.json" : "shader_typetypes.json";
            string schema = Paths.Schema(schemaName, cli.Value("--schema"));
            if (schema == null)
            {
                Console.Error.WriteLine(Strings.F("SchemaNotFound", schemaName));
                return 3;
            }

            string gameName = fileMode
                ? SanitizeName(Path.GetFileNameWithoutExtension(inputFiles[0]))
                : ReadProductName(dataDir);
            if (gameName == null)
            {
                string dn = Path.GetFileName(dataDir.TrimEnd('/', '\\'));
                gameName = dn.EndsWith("_Data", StringComparison.OrdinalIgnoreCase) ? dn[..^5] : dn;
            }
            string outDir = FirstNonEmpty(cli.Value("--out"),
                                          Environment.GetEnvironmentVariable("SHADERRIPPER_OUT") ?? Environment.GetEnvironmentVariable("SHADERRIP_OUT"),
                                          cli.Bare.Where((b2, idx) => idx > 0 && !File.Exists(b2)).FirstOrDefault())
                            ?? Path.Combine(Directory.GetCurrentDirectory(), gameName + "_ShaderLabs");
            outDir = Path.GetFullPath(outDir);
            if (!dumpMode && !matCheck)
            {
                Directory.CreateDirectory(outDir);
            }

            Console.WriteLine(Strings.Get("Banner"));
            Console.WriteLine(Strings.F("SFV_Schema", sfVersion, schemaName));
            var root = TypeNode.LoadSchema(schema);
            Func<string, int, TypeNode> tpkClass = (ver, classId) =>
                TpkSchema.Available ? TpkSchema.BuildClass(classId, ver) : null;

            // Resources.Load paths only exist in globalgamemanagers (ResourceManager
            // m_Container); load it so Resources assets land in Assets/Resources/...
            Dictionary<(string file, long pathId), string> resMap = null;
            {
                byte[] gg = FindGlobalGameManagers(inputFiles, dataDir);
                if (gg != null) resMap = ResourceMap.Load(gg, root, tpkClass);
                Console.WriteLine(resMap != null
                    ? $"resources map: {resMap.Count} entrie(s)"
                    : "resources map: not found (globalgamemanagers missing)");
                if (resMap != null && resMap.Count == 0 && ResourceMap.LastError != null)
                    Console.Error.WriteLine("  [resmap] " + ResourceMap.LastError);
                string resDump = Environment.GetEnvironmentVariable("SHADERRIPPER_DEBUG_RESMAP_FILE");
                if (resMap != null && !string.IsNullOrEmpty(resDump))
                    File.WriteAllLines(resDump, resMap.Select(kv => $"{kv.Key.file}\t{kv.Key.pathId}\t{kv.Value}"));
                if (resMap != null && Environment.GetEnvironmentVariable("SHADERRIPPER_DEBUG_RESMAP") != null)
                    foreach (var kv in resMap.Take(5))
                        Console.Error.WriteLine($"  [resmap] {kv.Key.file} :: {kv.Key.pathId} -> {kv.Value}");
            }

            var allShaders = new List<Shader>();
            SerializedFile sf = null;
            string shownVersion = null;
            if (fileMode)
            {
                int nodes = 0, found = 0, failed = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (bytes, path, container, sourceFile) in sources)
                {
                    SerializedFile inner;
                    try { inner = bytes != null ? SerializedFile.Load(bytes, root, tpkClass)
                                               : SerializedFile.Load(path, root, tpkClass); }
                    catch
                    {
                        // apk/zip containers hold hundreds of non-shader assets
                        // (textures, materials) whose type trees are outside our
                        // schema — skip them silently and report a count instead.
                        failed++;
                        continue;
                    }
                    shownVersion ??= inner.UnityVersion;
                    if (inner.Shaders.Count == 0) continue;
                    nodes++;
                    if (container != null)
                        foreach (var sh2 in inner.Shaders) sh2.ContainerPath = container;
                    if (sourceFile != null)
                        foreach (var sh2 in inner.Shaders) sh2.SourceFile = sourceFile;
                    foreach (var sh in inner.Shaders)
                    {
                        string nm = !string.IsNullOrEmpty(sh.Name) ? sh.Name : sh.ParsedForm?.Name ?? "";
                        if (!string.IsNullOrEmpty(nm) && !seen.Add(nm)) continue;
                        allShaders.Add(sh);
                        found++;
                    }
                }
                Console.WriteLine(Strings.F("Unity_Shaders", shownVersion ?? "?", found)
                                  + (TpkSchema.Available ? "  tpk=on" : "  tpk=off (data/sourcetpk.tpk missing)"));
                Console.WriteLine(Strings.F("BundlesScanned", inputFiles.Count, nodes, found)
                                  + (failed > 0 ? $"  ({failed} file(s) skipped)" : ""));
            }
            else
            {
                sf = SerializedFile.Load(assets, root, tpkClass);
                Console.WriteLine(Strings.F("Unity_Shaders", sf.UnityVersion, sf.Shaders.Count)
                                  + (TpkSchema.Available ? "  tpk=on" : "  tpk=off (data/sourcetpk.tpk missing)"));
                // remember which file these came from so the Resources map can key them
                string assetsName = Path.GetFileName(assets);
                foreach (var sh0 in sf.Shaders) sh0.SourceFile = assetsName;
                allShaders.AddRange(sf.Shaders);
            }
            if (indexCount > 0)
                Console.WriteLine(Strings.F("BuiltinIndex", indexCount, BuiltinIndex.ResolvedPath));
            if (matrixCount > 0)
                Console.WriteLine(Strings.F("MatrixNames", matrixCount, MatrixNames.ResolvedPath));
            if (tmpLayoutCount > 0)
                Console.WriteLine(Strings.F("TmpLayouts", tmpLayoutCount, Cg.TmpLayouts.ResolvedDir));
            else
                Console.Error.WriteLine(Strings.Get("TmpLayoutsMissing"));
            if (indexCount <= 0)
                Console.Error.WriteLine(Strings.Get("BuiltinIndexMissing"));

            if (!fileMode)
            {
                int bundleFiles = 0, nodes = 0, found = 0, skippedSources = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var sh in sf.Shaders)
                    seen.Add(!string.IsNullOrEmpty(sh.Name) ? sh.Name : sh.ParsedForm?.Name ?? "");

                var enumOpts = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                };
                foreach (var path in Directory.EnumerateFiles(bundleDir, "*", enumOpts))
                {
                    if (!path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)) continue;
                    if (BuiltinSource.IsBuiltin(path))
                    {
                        skippedSources++;
                        Console.WriteLine(Strings.F("BuiltinSourceSkipped", Path.GetFileName(path)));
                        continue;
                    }
                    bundleFiles++;
                    List<Bundle.Node> list;
                    try { list = Bundle.Read(path); }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(Strings.F("BundleSkipped", Path.GetFileName(path), e.Message));
                        continue;
                    }
                    foreach (var node in list)
                    {
                        if (node.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                            node.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                        SerializedFile inner;
                        try { inner = SerializedFile.Load(node.Data, root, tpkClass); }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine($"    node {node.Path}: {e}");
                            continue;
                        }
                        if (inner.Shaders.Count == 0) continue;
                        nodes++;
                        foreach (var sh2 in inner.Shaders) sh2.ContainerPath = node.Path;
                        foreach (var sh in inner.Shaders)
                        {
                            string nm = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                                      : sh.ParsedForm?.Name ?? "";
                            if (!string.IsNullOrEmpty(nm) && !seen.Add(nm)) continue;
                            allShaders.Add(sh);
                            found++;
                        }
                    }
                }
                Console.WriteLine(Strings.F("BundlesScanned", bundleFiles, nodes, found));
            }
            Console.WriteLine(Strings.F("Output", outDir));

            if (matCheck)
            {
                if (fileMode)
                {
                    Console.Error.WriteLine(Strings.Get("MatCheckNeedsGameDir") ?? "--mat-check requires a game data directory, not a single file.");
                    return 2;
                }
                string report = cli.Value("--mat-check");
                if (string.IsNullOrWhiteSpace(report)) report = "mat_check.json";
                return MatCheck.Run(Path.GetDirectoryName(Path.GetFullPath(assets)), root, tpkClass, report);
            }

            if (dumpMode)
            {
                string filter = dumpFilter;
                foreach (var dsh in allShaders)
                {
                    string dn = dsh.ParsedForm?.Name ?? dsh.Name ?? "";
                    if (!dn.Contains(filter)) continue;
                    Console.WriteLine($"=== {dn} pathID={dsh.PathId}");
                    Console.WriteLine($"    platforms   = [{string.Join(",", dsh.Platforms)}]");
                    for (int p = 0; p < dsh.Offsets.Length; p++)
                    {
                        Console.WriteLine($"    platform[{p}] offsets = [{string.Join(",", dsh.Offsets[p])}]");
                        Console.WriteLine($"                clens   = [{string.Join(",", dsh.CompressedLengths[p])}]");
                        Console.WriteLine($"                dlens   = [{string.Join(",", dsh.DecompressedLengths[p])}]");
                    }
                    Console.WriteLine($"    blob bytes  = {dsh.CompressedBlob.Length}");
                    for (int p = 0; p < dsh.Offsets.Length; p++)
                    {
                        var pb = BlobTool.DecodeBlob(dsh, p);
                        string dumpDir = Environment.GetEnvironmentVariable("SHADERRIPPER_DUMP_BLOBS");
                        if (!string.IsNullOrEmpty(dumpDir))
                        {
                            Directory.CreateDirectory(dumpDir);
                            string safe = string.Concat((dn ?? "shader").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
                            File.WriteAllBytes(Path.Combine(dumpDir, $"{safe}_p{p}_{(p < dsh.Platforms.Length ? dsh.Platforms[p] : 0)}.bin"), pb);
                        }
                        int pid = p < dsh.Platforms.Length ? (int)dsh.Platforms[p] : -1;
                        var fmt = BlobFormatDetect.Detect(pb);
                        string extra = "";
                        if (fmt == BlobFormat.Metal &&
                            BlobFormatDetect.MetalSplit(pb, 0, out var ent, out var so))
                            extra = $" metalEntry={ent} srcOff={so}";
                        Console.WriteLine($"    platform[{p}] id={pid} {pb.Length}B format={fmt}{extra} head={BlobFormatDetect.Hex(pb, 0, 24)}");
                        int cnt = pb.Length >= 4 ? BitConverter.ToInt32(pb, 0) : 0;
                        {
                            int dx = 0, sp = 0, mt = 0, gl = 0, first = -1; BlobFormat ff = BlobFormat.Unknown;
                            for (int q = 0; q + 4 <= pb.Length; q++)
                            {
                                uint u = BitConverter.ToUInt32(pb, q);
                                BlobFormat f2 = u == 0x43425844 ? BlobFormat.Dxbc
                                             : u == 0x07230203 ? BlobFormat.SpirV
                                             : u == 0xF00DCAFE ? BlobFormat.Metal : BlobFormat.Unknown;
                                if (f2 == BlobFormat.Unknown && pb[q] == (byte)'#')
                                {
                                    string h = System.Text.Encoding.ASCII.GetString(pb, q, Math.Min(16, pb.Length - q));
                                    if (h.StartsWith("#version")) f2 = BlobFormat.Glsl;
                                }
                                if (f2 == BlobFormat.Unknown) continue;
                                if (first < 0) { first = q; ff = f2; }
                                switch (f2) { case BlobFormat.Dxbc: dx++; break; case BlobFormat.SpirV: sp++; break;
                                              case BlobFormat.Metal: mt++; break; case BlobFormat.Glsl: gl++; break; }
                            }
                            Console.WriteLine($"      payload scan: dxbc={dx} spirv={sp} metal={mt} glsl={gl} first={ff}@{first}");
                            int tblEnd = 4 + cnt * 12;
                            if (tblEnd > 0 && tblEnd < pb.Length)
                            {
                                int nz = tblEnd;
                                while (nz < pb.Length && pb[nz] == 0) nz++;
                                Console.WriteLine($"      after table({tblEnd}): firstNonZero@{nz} head={BlobFormatDetect.Hex(pb, nz, 48)}");
                            }
                        }
                        if (cnt > 0 && cnt < 4096 && 4 + (long)cnt * 12 <= pb.Length)
                            for (int e = 0; e < Math.Min(cnt, 3); e++)
                            {
                                int so2 = BitConverter.ToInt32(pb, 4 + e * 12);
                                int sz2 = BitConverter.ToInt32(pb, 4 + e * 12 + 4);
                                if (so2 < 0 || sz2 <= 0 || (long)so2 + sz2 > pb.Length) continue;
                                var (pf2, po2) = BlobFormatDetect.FindProgram(pb, so2, so2 + sz2);
                                Console.WriteLine($"      slot[{e}] off={so2} size={sz2} prog={pf2}@{po2 - so2} head={BlobFormatDetect.Hex(pb, so2 + Math.Max(0, po2 - so2), 48)}");
                            }
                    }
                    foreach (var ss in dsh.ParsedForm.SubShaders)
                    {
                        Console.WriteLine($"    subshader: {ss.Passes.Count} pass(es)  tags=[{string.Join(", ", ss.Tags.Select(t => t.k + "=" + t.v))}]");
                        foreach (var ps in ss.Passes)
                        {
                            Console.WriteLine($"      pass '{ps.StateName}': ZTest={ps.ZTest}{ps.ZTestName} ZWrite={ps.ZWrite}{ps.ZWriteName} "
                                              + $"Cull={ps.Culling}{ps.CullingName} ZClip={ps.ZClip}{ps.ZClipName} "
                                              + $"stencil(ref={ps.StencilRef},rd={ps.StencilReadMask},wr={ps.StencilWriteMask}) "
                                              + $"tags=[{string.Join(", ", ps.Tags.Select(t => t.k + "=" + t.v))}]");
                            foreach (var bs in ps.Blends)
                                Console.WriteLine($"        blend: src={bs.SrcBlend}{bs.SrcBlendName} dst={bs.DestBlend}{bs.DestBlendName} "
                                                  + $"op={bs.BlendOp}{bs.BlendOpName} a=({bs.SrcBlendAlpha},{bs.SrcBlendAlphaName},{bs.DestBlendAlpha},{bs.DestBlendAlphaName})");
                        }
                    }
                    foreach (var ss in dsh.ParsedForm.SubShaders)
                        foreach (var ps in ss.Passes)
                            foreach (var sp in ps.SubPrograms)
                            {
                                Console.WriteLine($"    subprogram: stage={sp.Stage,-9} blobIndex={sp.BlobIndex} "
                                                  + $"paramBlob={sp.ParameterBlobIndex} gpuProgramType={sp.GpuProgramType}");
                                foreach (var acb in sp.ConstantBuffers)
                                {
                                    Console.WriteLine($"        cb {acb.Name} ({acb.Size} bytes)"
                                                      + (acb.Partial ? "  partial" : "")
                                                      + $"  members={acb.Members.Count}");
                                    foreach (var am in acb.Members.OrderBy(x => x.Offset))
                                        Console.WriteLine($"            {am.Name,-28} at {am.Offset,-5} "
                                                          + (am.IsMatrix ? $"matrix rows={am.RowCount}" : $"dim={am.Dim}") + $" type={am.Type}"
                                                          + (am.ArraySize > 1 ? $" x{am.ArraySize}" : ""));
                                }
                            }
                    var dd = BlobTool.DecodeBlob(dsh);
                    try { File.WriteAllBytes(Path.Combine(Paths.TempDir,
                        "sr_blob_" + dn.Replace('/', '_') + ".bin"), dd); }
                    catch { }
                    Console.WriteLine($"    decoded     = {dd.Length} bytes");
                    var pbs = BlobTool.ParseParamBlobs(dd);
                    Console.WriteLine($"    paramBlobs  = {pbs.Count} program(s) with cbuffer tables");
                    foreach (var kv in pbs.OrderBy(k => k.Key).Take(64))
                    {
                        Console.WriteLine($"      blobIndex {kv.Key}:");
                        foreach (var cb in kv.Value)
                        {
                            Console.WriteLine($"        {cb.Name,-18} size={cb.Size} members={cb.Members.Count}"
                                              + (cb.Register >= 0 ? $" reg={cb.Register}" : ""));
                            for (int mi = 0; mi < cb.Members.Count; mi++)
                            {
                                var m = cb.Members[mi];
                                var t = cb.MemberTails[mi];
                                Console.WriteLine($"          {m.name,-28} @{m.offset,-4} tail=[{string.Join(", ", t)}]");
                            }
                        }
                    }
                    Console.WriteLine("    --- head hex ---");
                    for (int h = 0; h < Math.Min(0x100, dd.Length); h += 16)
                    {
                        var sbHex = new System.Text.StringBuilder();
                        for (int q = 0; q < 16 && h + q < dd.Length; q++) sbHex.Append(dd[h + q].ToString("x2")).Append(' ');
                        Console.WriteLine($"      {h:x4}  {sbHex}");
                    }
                    var hh = BlobTool.FindDxbc(dd);
                    if (dd.Length >= 8)
                    {
                        int cnt = BitConverter.ToInt32(dd, 0);
                        Console.WriteLine($"    [table] declared count = {cnt}");
                        if (cnt > 0 && cnt < 4096 && 4 + cnt * 12 <= dd.Length)
                        {
                            for (int e = 0; e < cnt; e++)
                            {
                                int eo = BitConverter.ToInt32(dd, 4 + e * 12);
                                int es = BitConverter.ToInt32(dd, 4 + e * 12 + 4);
                                int dx = -1;
                                if (eo >= 0 && es > 0 && eo + es <= dd.Length)
                                {
                                    for (int q = eo; q + 0x20 <= eo + es; q++)
                                        if (dd[q] == 0x44 && dd[q + 1] == 0x58 && dd[q + 2] == 0x42 && dd[q + 3] == 0x43)
                                        { dx = q; break; }
                                }
                                Console.WriteLine($"    [table] blobIndex {e}: off={eo} size={es} spanEnd={eo + es} dxbcAt={dx}"
                                    + (dx >= 0 ? $" (+{dx - eo} into blob)" : ""));
                            }
                        }
                    }
                    for (int k = 0; k < hh.Count; k++)
                    {
                        var (o, t) = hh[k];
                        var cont = new byte[t];
                        Array.Copy(dd, o, cont, 0, t);
                        string asm = BlobTool.Disassemble(cont);
                        string head = asm == null ? "<disasm failed>" : asm.Split('\n')[0].Trim();
                        string prof = "?";
                        if (asm != null)
                            foreach (var ln in asm.Split('\n'))
                            {
                                string t2 = ln.TrimStart('/', ' ', '\t');
                                if (System.Text.RegularExpressions.Regex.IsMatch(t2, @"^(vs|ps|gs|hs|ds|cs)_\d_\d"))
                                { prof = t2.Trim(); break; }
                            }
                        Console.WriteLine($"    hit[{k}] bufOff={o} size={t}  profile={prof}  asmHeader: {head}");
                        if (asm != null)
                        {
                            var lines = asm.Split('\n');
                            for (int q = 0; q < Math.Min(400, lines.Length); q++)
                                Console.WriteLine($"        | {lines[q].TrimEnd('\r')}");
                        }
                    }
                }
                return 0;
            }

            int ok = 0;
            int skippedBuiltin = 0;
            var opStats = new Dictionary<string, int>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            // paths claimed in THIS run only: overwriting last run's output is fine,
            // suffixing it would litter the tree with _<pathID> copies
            var writtenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sh in allShaders)
            {
                string name = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                            : !string.IsNullOrEmpty(sh.ParsedForm?.Name) ? sh.ParsedForm.Name
                            : "Shader_" + sh.PathId;
                if (only != null && !name.Contains(only, StringComparison.OrdinalIgnoreCase)) continue;
                if (only == null && BuiltinIndex.Contains(name)) { skippedBuiltin++; continue; }
                string safe = name;
                foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
                safe = safe.Replace(' ', '_');
                // Deduplicate before anything is written: the Disassembly/<name>/
                // directory is created below, so a late rename would make two
                // same-named shaders overwrite each other's disassembly.
                if (!usedNames.Add(safe))
                {
                    safe = safe + "_" + sh.PathId;
                    usedNames.Add(safe);
                }

                try
                {
                    int mainPlat = Platforms.PickMain(sh.Platforms, forcedPlatform);
                    if (mainPlat < 0)
                    {
                        if (forcedPlatform != null)
                            Console.Error.WriteLine(Strings.F("PlatformNotPresent", forcedPlatform));
                        mainPlat = 0;
                    }
                    int mainPlatId = mainPlat < sh.Platforms.Length ? (int)sh.Platforms[mainPlat] : -1;
                    var mainInfo = Platforms.Get(mainPlatId);
                    byte[] decoded = BlobTool.DecodeBlob(sh, mainPlat);
                    var usedBlobs = sh.ParsedForm.SubShaders
                        .SelectMany(ss => ss.Passes)
                        .SelectMany(p => p.SubPrograms)
                        .Select(sp => sp.BlobIndex).ToHashSet();
                    var slots = mainInfo.Format == "dxbc"
                        ? BlobTool.DecodeSlots(decoded, usedBlobs)
                        : new List<BlobTool.BlobSlot>();
                    var paramBlobs = BlobTool.ParseParamBlobs(decoded);

                    var progs = new Dictionary<int, Cg.AsmParser.Program>();
                    var subByBlob = sh.ParsedForm.SubShaders
                        .SelectMany(ss => ss.Passes)
                        .SelectMany(p => p.SubPrograms)
                        .GroupBy(sp => sp.BlobIndex)
                        .ToDictionary(g => g.Key, g => g.First());
                    var kwIndex = new Dictionary<int, string>();
                    for (int ki = 0; ki < sh.ParsedForm.KeywordNames.Count; ki++)
                        if (!string.IsNullOrEmpty(sh.ParsedForm.KeywordNames[ki]))
                            kwIndex[ki] = sh.ParsedForm.KeywordNames[ki];

                    string disDir = Path.Combine(outDir, "Disassembly", safe);
                    string DisName(int blobIndex)
                    {
                        string stage = "prog";
                        string kw = "";
                        if (subByBlob.TryGetValue(blobIndex, out var sp))
                        {
                            stage = sp.Stage.ToLowerInvariant();
                            var names = Cg.VariantPlan.KeywordsOf(sp, kwIndex);
                            if (names.Count > 0)
                                kw = "_" + Cg.VariantPlan.Key(names).Replace(',', '_');
                        }
                        foreach (char c in Path.GetInvalidFileNameChars()) kw = kw.Replace(c, '_');
                        return $"{blobIndex}_{stage}{kw}";
                    }

                    if (mainInfo.Format == "d3d9")
                    {
                        foreach (var pr in ProgramSource.Extract(decoded))
                        {
                            if (pr.Format != BlobFormat.D3d9 || pr.Length <= 0) continue;
                            string asm9 = D3D9Asm.Disassemble(decoded, pr.Offset, pr.Length);
                            if (asm9 == null)
                                asm9 = $"// d3d9 token stream at 0x{pr.Offset:X} len={pr.Length}: disassembly failed\n";
                            Directory.CreateDirectory(disDir);
                            File.WriteAllText(Path.Combine(disDir, DisName(pr.BlobIndex) + ".d3d9.asm"), asm9);
                        }
                    }

                    bool anyDxil = false;
                    foreach (var slot in slots)
                    {
                        var container = new byte[slot.DxbcSize];
                        Array.Copy(decoded, slot.DxbcOffset, container, 0, slot.DxbcSize);
                        string asm = BlobTool.Disassemble(container);
                        if (asm == null) continue;

                        bool dxil = BlobTool.HasDxilChunk(container);
                        if (dxil) anyDxil = true;

                        Directory.CreateDirectory(disDir);
                        File.WriteAllText(Path.Combine(disDir,
                            DisName(slot.BlobIndex) + (dxil ? ".dxil.asm" : ".dxbc.asm")), asm);
                        if (dxil) continue;

                        var prog = global::ShaderRipper.Cg.AsmParser.Parse(asm);
                        foreach (var ins in prog.Code)
                        {
                            opStats.TryGetValue(ins.Opcode, out int c);
                            opStats[ins.Opcode] = c + 1;
                        }
                        progs[slot.BlobIndex] = prog;
                    }

                    ExportOtherPlatforms(sh, mainPlat, safe, outDir, only);

                    Dictionary<int, ExtractedProgram> srcByBlob = null;
                    if (mainInfo.Format != "dxbc")
                    {
                        var extracted = ProgramSource.Extract(decoded);
                        var order = sh.ParsedForm.SubShaders
                            .SelectMany(ss => ss.Passes)
                            .SelectMany(p2 => p2.SubPrograms)
                            .Select(sp => sp.BlobIndex).Distinct().OrderBy(x => x).ToList();
                        srcByBlob = new Dictionary<int, ExtractedProgram>();
                        for (int q = 0; q < extracted.Count && q < order.Count; q++)
                            srcByBlob[order[q]] = extracted[q];

                        foreach (var kv in srcByBlob)
                        {
                            if (kv.Value.Text.Length == 0) continue;
                            Directory.CreateDirectory(disDir);
                            string ext = kv.Value.Format switch
                            {
                                BlobFormat.Glsl => "glsl",
                                BlobFormat.Metal => "metal",
                                _ => "spv",
                            };
                            File.WriteAllText(Path.Combine(disDir,
                                DisName(kv.Key) + "." + ext), kv.Value.Text);
                        }
                    }

                    string writerFormat = mainInfo.Format == "dxbc" && progs.Count == 0 && anyDxil
                        ? "dxil" : mainInfo.Format;
                    string lab = ShaderLabWriter.Write(sh, name, progs, paramBlobs, safe,
                                                       writerFormat, mainPlatId, srcByBlob);
                    string outPath;
                    {
                        // Prefer the asset's real container path from the bundle
                        // node ("assets/resources/foo.shader" -> Assets/Resources/...);
                        // fall back to AR's default Assets/Shader/<Name>.shader.
                        string rel;
                        string container = sh.ContainerPath;
                        bool fromResources = false;
                        if (container == null && resMap != null && sh.SourceFile != null &&
                            resMap.TryGetValue((sh.SourceFile, sh.PathId), out var mapped))
                        {
                            container = mapped;
                            fromResources = true;
                        }
                        if (fromResources)
                        {
                            // m_Container names are Resources-relative
                            // ("achievements/foo", "camerafilterpack_rainfx")
                            rel = "Resources/" + container.Replace('\\', '/');
                            if (!rel.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)) rel += ".shader";
                            var rparts0 = rel.Split('/');
                            for (int i = 0; i < rparts0.Length; i++)
                                foreach (char c2 in Path.GetInvalidFileNameChars()) rparts0[i] = rparts0[i].Replace(c2, '_');
                            string outPathR = Path.Combine(new[] { outDir, "Assets" }.Concat(rparts0).ToArray());
                            // a resource path can be claimed twice when the same asset ships in
                            // several data files — never overwrite silently (but re-running into
                            // the same directory must simply overwrite)
                            if (!writtenPaths.Add(outPathR))
                                outPathR = outPathR[..^".shader".Length] + "_" + sh.PathId + ".shader";
                            Directory.CreateDirectory(Path.GetDirectoryName(outPathR)!);
                            File.WriteAllText(outPathR, lab);
                            ok++;
                            continue;
                        }
                        if (container == null && resMap != null &&
                            Environment.GetEnvironmentVariable("SHADERRIPPER_DEBUG_RESMAP") != null)
                            Console.Error.WriteLine($"  [resmap] miss {name} :: {sh.SourceFile} :: {sh.PathId}");
                        if (!string.IsNullOrEmpty(container))
                        {
                            rel = container.Replace('\\', '/');
                            if (rel.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) rel = rel["assets/".Length..];
                            // builds store container paths lowercase; restore Unity's canonical
                            // casing for the special folders so the tree drops into a project
                            foreach (var (low, canon) in SpecialFolders)
                                if (rel.StartsWith(low, StringComparison.OrdinalIgnoreCase))
                                {
                                    rel = canon + rel[low.Length..];
                                    break;
                                }
                            if (!rel.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                            {
                                // Unity sometimes stores shader nodes under a data-ish
                                // name; AssetRipper keeps the directory but names the
                                // file after the shader.
                                string dir2 = Path.GetDirectoryName(rel)?.Replace('\\', '/');
                                rel = (string.IsNullOrEmpty(dir2) ? "" : dir2 + "/") + safe + ".shader";
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  [ar] no container path: \"{name}\" -> Assets/Shader/{safe}.shader");
                            rel = "Shader/" + safe + ".shader";
                        }
                        var parts = rel.Split('/');
                        for (int i = 0; i < parts.Length; i++)
                            if (i == parts.Length - 1 || parts[i].Length > 0)
                                foreach (char c in Path.GetInvalidFileNameChars()) parts[i] = parts[i].Replace(c, '_');
                        outPath = Path.Combine(new[] { outDir, "Assets" }.Concat(parts).ToArray());
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    File.WriteAllText(outPath, lab);
                    ok++;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(Strings.F("ShaderFailed", name, e.Message));
                    Console.Error.WriteLine(e.StackTrace);
                }
            }

            Console.WriteLine();
            Console.WriteLine(Strings.F("Done", ok)
                + (skippedBuiltin > 0 ? Strings.F("DoneSkippedSuffix", skippedBuiltin) : ""));
            Console.WriteLine();
            Console.WriteLine(Strings.Get("OpcodeCoverage"));
            foreach (var kv in opStats.OrderByDescending(k => k.Value).Take(40))
                Console.WriteLine($"  {kv.Key,-20} {kv.Value}");

            int unsupported = global::ShaderRipper.Cg.CgTranslator.UnsupportedTotal;
            Console.WriteLine();
            Console.WriteLine(Strings.F("DistinctOpcodes", opStats.Count, unsupported));
            return 0;
        }

        /// <summary>
        /// 把非主平台（GLES3/OpenGLCore/Metal/Vulkan）的编译结果落盘。
        ///
        /// 这些平台里 GLSL 与 Metal 是**源码文本** —— Unity 存的就是 HLSLcc 的输出，
        /// 比从 DXBC 直译回来的 Cg 更接近作者原意；Vulkan 则是 SMOL-V 压缩的 SPIR-V，
        /// 没有 0x07230203 魔数，解压器不在本工具范围内，只报告。
        /// </summary>
        private static void ExportOtherPlatforms(Shader sh, int mainPlat, string safe,
                                                 string outDir, string only)
        {
            if (sh.Platforms.Length <= 1) return;

            var stages = sh.ParsedForm.SubShaders
                .SelectMany(ss => ss.Passes)
                .SelectMany(p => p.SubPrograms)
                .OrderBy(sp => sp.BlobIndex)
                .Select(sp => sp.Stage.ToLowerInvariant())
                .ToList();

            for (int pi = 0; pi < sh.Platforms.Length; pi++)
            {
                int pid = (int)sh.Platforms[pi];
                var info = Platforms.Get(pid);
                if (info.Format is not ("glsl" or "metal" or "spirv" or "d3d9")) continue;

                byte[] payload;
                try { payload = BlobTool.DecodeBlob(sh, pi); }
                catch { continue; }
                if (payload.Length == 0) continue;

                string dir = Path.Combine(outDir, "Disassembly", safe);
                string prefix = info.Name + ".";
                var progs = ProgramSource.Extract(payload);

                if (info.Format == "d3d9")
                {
                    int k9 = 0;
                    bool wrote = false;
                    foreach (var pr in progs)
                    {
                        if (pr.Format != BlobFormat.D3d9 || pr.Length <= 0) continue;
                        string asm9 = D3D9Asm.Disassemble(payload, pr.Offset, pr.Length);
                        if (asm9 == null) continue;
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(Path.Combine(dir, prefix + $"{safe}.{k9}" + ".d3d9.asm"), asm9);
                        wrote = true;
                        k9++;
                    }
                    if (wrote) continue;
                }

                var text = progs.Where(p => p.Text.Length > 0).ToList();

                if (text.Count == 0)
                {
                    if (!ProgramSource.LooksSmolV(payload)) continue;
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, prefix + safe + ".smolv.txt"),
                        $"// {info.Name} ({pid}): SMOL-V compressed SPIR-V\n"
                        + $"// payload {payload.Length} bytes, no plain SPIR-V header.\n"
                        + "// Unity encodes Vulkan programs with smol-v, so the module has no\n"
                        + "// 0x07230203 magic and must be decoded before it can be disassembled.\n");
                    continue;
                }

                Directory.CreateDirectory(dir);
                for (int k = 0; k < text.Count; k++)
                {
                    var prog = text[k];
                    string stage = prog.Format == BlobFormat.Glsl ? "" : (k < stages.Count ? stages[k] : "blob" + k);
                    string ext = prog.Format switch
                    {
                        BlobFormat.Glsl => "glsl",
                        BlobFormat.Metal => "metal",
                        _ => "spv",
                    };
                    string file = Path.Combine(dir, prefix + $"{safe}.{k}" + (stage == "" ? "" : "_" + stage) + "." + ext);
                    File.WriteAllText(file, prog.Text);
                }
            }
        }

        /// <summary>--disasm &lt;file&gt;：反汇编单个程序文件（DXBC/DXIL 或 d3d9 token 流）。</summary>
        private static int DisasmFile(string path)
        {
            byte[] b;
            try { b = File.ReadAllBytes(path); }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
            if (b.Length >= 4 && BitConverter.ToUInt32(b, 0) == 0x43425844)
            {
                bool dxil = BlobTool.HasDxilChunk(b);
                Console.WriteLine(dxil ? Strings.Get("DxbcContainerDxil") : Strings.Get("DxbcContainerSm45"));
                string s = BlobTool.Disassemble(b);
                if (s == null) { Console.Error.WriteLine(Strings.Get("DisasmFailed")); return 1; }
                Console.Write(s);
                return 0;
            }
            for (int i = 0; i + 4 <= b.Length; i++)
            {
                if (BlobFormatDetect.LooksD3d9(b, i, b.Length))
                {
                    int len = BlobFormatDetect.D3d9TokenEnd(b, i + 4, b.Length);
                    string s = D3D9Asm.Disassemble(b, i, len > 0 ? len - i : b.Length - i);
                    if (s == null) { Console.Error.WriteLine(Strings.Get("D3d9DisasmFailed")); return 1; }
                    Console.Write(s);
                    return 0;
                }
            }
            Console.Error.WriteLine(Strings.Get("UnrecognizedFormat"));
            return 1;
        }

        /// <summary>第一个非空字符串。</summary>
        private static string FirstNonEmpty(params string[] xs) =>
            xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        private static void Usage()
        {
            Console.Write(Strings.Get("HelpText"));
            Console.WriteLine();
        }

        /// <summary>
        /// Product name from <c>&lt;data&gt;/app.info</c> (Unity standard: two
        /// UTF-8 lines, company then product).  Falls back to the data directory
        /// name minus its <c>_Data</c> suffix.
        /// </summary>
        private static string ReadProductName(string dataDir)
        {
            try
            {
                string info = Path.Combine(dataDir, "app.info");
                if (File.Exists(info))
                {
                    var lines = File.ReadAllLines(info)
                                    .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                    if (lines.Count >= 2) return SanitizeName(lines[1]);
                    if (lines.Count == 1) return SanitizeName(lines[0]);
                }
            }
            catch { }
            return null;
        }

        private static string SanitizeName(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }

        /// <summary>SerializedFile version from the big-endian header (offset 8).</summary>
        private static uint ReadSerializedFileVersion(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var h = new byte[12];
                int n = fs.Read(h, 0, 12);
                if (n < 12) return 0;
                return (uint)((h[8] << 24) | (h[9] << 16) | (h[10] << 8) | h[11]);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Locate globalgamemanagers for the current input set: next to a loose
        /// .assets, inside the game data dir, or as an entry of an apk/zip.
        /// Windows and Linux builds use the same file (and the same format).
        /// </summary>
        private static byte[] FindGlobalGameManagers(List<string> inputFiles, string dataDir)
        {
            var names = new[] { "globalgamemanagers", "globalgamemanagers.assets" };
            var dirs = new List<string>();
            if (!string.IsNullOrEmpty(dataDir)) dirs.Add(dataDir);
            foreach (var f in inputFiles)
                if (dirs.Count == 0 || !string.Equals(Path.GetDirectoryName(f), dirs[0], StringComparison.OrdinalIgnoreCase))
                    dirs.Add(Path.GetDirectoryName(f) ?? "");

            foreach (var d in dirs)
                foreach (var n in names)
                {
                    string pth = Path.Combine(d, n);
                    if (File.Exists(pth))
                    {
                        try { return File.ReadAllBytes(pth); } catch { }
                    }
                }

            foreach (var f in inputFiles)
            {
                if (SniffFile(f) != InputKind.Zip) continue;
                try
                {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(f);
                    foreach (var n in names)
                        foreach (var entry in zip.Entries)
                            if (string.Equals(Path.GetFileName(entry.FullName), n, StringComparison.OrdinalIgnoreCase))
                            {
                                using var es = entry.Open();
                                var ms = new MemoryStream();
                                es.CopyTo(ms);
                                return ms.ToArray();
                            }
                }
                catch { }
            }
            return null;
        }

        private enum InputKind { Unknown, Serialized, Bundle, Zip }

        /// <summary>Lowercase container prefixes that map back to Unity's canonical folder names.</summary>
        private static readonly (string low, string canon)[] SpecialFolders =
        {
            ("resources/", "Resources/"),
            ("streamingassets/", "StreamingAssets/"),
            ("plugins/", "Plugins/"),
            ("editor/", "Editor/"),
            ("gizmos/", "Gizmos/"),
        };

        private static bool SkipNode(string path) =>
            path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase);

        /// <summary>Content sniffing by file head — extensions are advisory only
        /// (downloaded apks are often renamed to .1, Unity data files inside
        /// apks are GUID-named with no extension).</summary>
        private static InputKind SniffFile(string path)
        {
            using var fs = File.OpenRead(path);
            var h = new byte[12];
            int n = fs.Read(h, 0, 12);
            return SniffHead(h, n);
        }

        private static InputKind SniffHead(byte[] h, int n = 12)
        {
            if (n >= 4 && h[0] == 0x50 && h[1] == 0x4B && h[2] is 3 or 5 or 7) return InputKind.Zip;
            if (n >= 7 && Encoding.ASCII.GetString(h, 0, 7) == "UnityFS") return InputKind.Bundle;
            if (n >= 12)
            {
                uint v = (uint)((h[8] << 24) | (h[9] << 16) | (h[10] << 8) | h[11]);
                if (v >= 6 && v <= 1000) return InputKind.Serialized;
            }
            return InputKind.Unknown;
        }

        private static byte[] ReadHead(System.IO.Compression.ZipArchiveEntry entry, int count)
        {
            using var s = entry.Open();
            var b = new byte[count];
            int n = 0;
            while (n < count)
            {
                int r = s.Read(b, n, count - n);
                if (r <= 0) break;
                n += r;
            }
            return b;
        }

        /// <summary>SerializedFile version from an in-memory header (bundle node data).</summary>
        private static uint ReadVersionFromBytes(byte[] b)        {
            if (b == null || b.Length < 12) return 0;
            return (uint)((b[8] << 24) | (b[9] << 16) | (b[10] << 8) | b[11]);
        }

        /// <summary>--list-shaders: print every shader name in a SerializedFile,
        /// bundle or apk/zip container (detected by content, not extension).</summary>
        private static int ListShaders(string path, TypeNode root, Func<string, int, TypeNode> tpkClass)
        {
            try
            {
                var names = new List<string>();
                void Collect(byte[] data, string path2, string container)
                {
                    SerializedFile inner = data != null ? SerializedFile.Load(data, root, tpkClass)
                                                        : SerializedFile.Load(path2, root, tpkClass);
                    if (container != null)
                        foreach (var sh in inner.Shaders) sh.ContainerPath = container;
                    foreach (var sh in inner.Shaders)
                        names.Add(!string.IsNullOrEmpty(sh.Name) ? sh.Name : sh.ParsedForm?.Name ?? "");
                }

                switch (SniffFile(path))
                {
                    case InputKind.Zip:
                        using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
                            foreach (var entry in zip.Entries)
                            {
                                string n = entry.FullName;
                                if (SkipNode(n) || entry.Length < 12) continue;
                                byte[] head = ReadHead(entry, 12);
                                try
                                {
                                    switch (SniffHead(head))
                                    {
                                        case InputKind.Bundle:
                                            using (var es = entry.Open())
                                            {
                                                var ms = new MemoryStream();
                                                es.CopyTo(ms);
                                                foreach (var nd in Bundle.Read(ms.ToArray()))
                                                {
                                                    if (SkipNode(nd.Path)) continue;
                                                    Collect(nd.Data, null, n + "/" + nd.Path);
                                                }
                                            }
                                            break;
                                        case InputKind.Serialized:
                                            using (var es = entry.Open())
                                            {
                                                var ms = new MemoryStream();
                                                es.CopyTo(ms);
                                                Collect(ms.ToArray(), null, n);
                                            }
                                            break;
                                    }
                                }
                                catch (Exception e) { Console.Error.WriteLine($"    entry {n}: {e.Message}"); }
                            }
                        break;

                    case InputKind.Bundle:
                        foreach (var nd in Bundle.Read(path))
                        {
                            if (SkipNode(nd.Path)) continue;
                            try { Collect(nd.Data, null, nd.Path); }
                            catch (Exception e) { Console.Error.WriteLine($"    node {nd.Path}: {e.Message}"); }
                        }
                        break;

                    case InputKind.Serialized:
                        Collect(null, path, null);
                        break;
                }

                foreach (var n in names.Where(n => !string.IsNullOrEmpty(n)).Distinct().OrderBy(n => n, StringComparer.Ordinal))
                    Console.WriteLine(n);
                Console.Error.WriteLine($"({names.Count} shader object(s))");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[list-shaders] " + e.Message);
                return 2;
            }
        }
    }
}
