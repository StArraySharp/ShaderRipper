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

            string singleFile = cli.Value("--file");
            if (singleFile == null && dataDir != null && File.Exists(dataDir))
                singleFile = dataDir;          // a bare arg pointing at a file
            bool fileMode = singleFile != null;
            bool singleBundle = false;

            if (fileMode)
            {
                singleFile = Path.GetFullPath(singleFile);
                if (!File.Exists(singleFile)) { Console.Error.WriteLine(Strings.F("ResourcesAssetsMissing", singleFile)); return 2; }
                string ext = Path.GetExtension(singleFile).ToLowerInvariant();
                singleBundle = ext is ".bundle" or ".assetbundle" or ".unity3d";
                if (dataDir == null) dataDir = Path.GetDirectoryName(singleFile);
            }
            dataDir = Path.GetFullPath(dataDir);
            string assets = singleFile;
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

            List<Bundle.Node> preNodes = null;
            uint sfVersion;
            if (fileMode && singleBundle)
            {
                // Bundle layout: read once up front and probe the first node's
                // SerializedFile header to pick the right schema.
                preNodes = Bundle.Read(singleFile);
                sfVersion = 0;
                foreach (var nd in preNodes)
                {
                    uint v = ReadVersionFromBytes(nd.Data);
                    if (v > 10 && v < 1000) { sfVersion = v; break; }
                }
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
                ? SanitizeName(Path.GetFileNameWithoutExtension(singleFile))
                : ReadProductName(dataDir);
            if (gameName == null)
            {
                string dn = Path.GetFileName(dataDir.TrimEnd('/', '\\'));
                gameName = dn.EndsWith("_Data", StringComparison.OrdinalIgnoreCase) ? dn[..^5] : dn;
            }
            string outDir = FirstNonEmpty(cli.Value("--out"),
                                          Environment.GetEnvironmentVariable("SHADERRIPPER_OUT") ?? Environment.GetEnvironmentVariable("SHADERRIP_OUT"),
                                          cli.Bare.Count > 1 ? cli.Bare[1] : null)
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

            var allShaders = new List<Shader>();
            SerializedFile sf = null;
            string shownVersion = null;
            if (fileMode && singleBundle)
            {
                int nodes = 0, found = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var nd in preNodes)
                {
                    if (nd.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                        nd.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                    SerializedFile inner;
                    try { inner = SerializedFile.Load(nd.Data, root, tpkClass); }
                    catch (Exception e) { Console.Error.WriteLine($"    node {nd.Path}: {e.Message}"); continue; }
                    shownVersion ??= inner.UnityVersion;
                    if (inner.Shaders.Count == 0) continue;
                    nodes++;
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
                Console.WriteLine(Strings.F("BundlesScanned", 1, nodes, found));
            }
            else
            {
                sf = SerializedFile.Load(assets, root, tpkClass);
                Console.WriteLine(Strings.F("Unity_Shaders", sf.UnityVersion, sf.Shaders.Count)
                                  + (TpkSchema.Available ? "  tpk=on" : "  tpk=off (data/sourcetpk.tpk missing)"));
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
                    File.WriteAllText(Path.Combine(outDir, safe + ".shader"), lab);
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

        /// <summary>SerializedFile version from an in-memory header (bundle node data).</summary>
        private static uint ReadVersionFromBytes(byte[] b)
        {
            if (b == null || b.Length < 12) return 0;
            return (uint)((b[8] << 24) | (b[9] << 16) | (b[10] << 8) | b[11]);
        }

        /// <summary>--list-shaders: print every shader name in a SerializedFile or bundle.</summary>
        private static int ListShaders(string path, TypeNode root, Func<string, int, TypeNode> tpkClass)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                var names = new List<string>();
                if (ext is ".bundle" or ".assetbundle" or ".unity3d")
                {
                    foreach (var nd in Bundle.Read(path))
                    {
                        if (nd.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                            nd.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                            {
                                foreach (var sh in SerializedFile.Load(nd.Data, root, tpkClass).Shaders)
                                    names.Add(!string.IsNullOrEmpty(sh.Name) ? sh.Name : sh.ParsedForm?.Name ?? "");
                            }
                        catch (Exception e) { Console.Error.WriteLine($"    node {nd.Path}: {e.Message}"); }
                    }
                }
                else
                {
                    foreach (var sh in SerializedFile.Load(path, root, tpkClass).Shaders)
                        names.Add(!string.IsNullOrEmpty(sh.Name) ? sh.Name : sh.ParsedForm?.Name ?? "");
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
