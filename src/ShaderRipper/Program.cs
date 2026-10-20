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

            // --lang en|zh：覆盖 UI 语言（默认跟随系统；Strings.resx 英文兜底，zh-Hans 中文）。
            // 要放在 --help 之前，`--lang en -h` 才能出英文帮助。
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* 重定向输出时可能失败 */ }

            string lang = cli.Value("--lang");
            if (!string.IsNullOrWhiteSpace(lang))
            {
                try
                {
                    // 常用别名：zh/chinese → zh-Hans（资源用 zh-Hans 命名，"zh" 直查会落回英文）
                    var name = lang.Trim() switch
                    {
                        "zh" or "chinese" or "zh-cn" or "zh_CN" => "zh-Hans",
                        "en" or "english" => "en",
                        var v => v,
                    };
                    Strings.Culture = CultureInfo.GetCultureInfo(name);
                }
                catch { Console.Error.WriteLine($"[!] unknown language: {lang} (en, zh-Hans, ...)"); }
            }

            if (cli.Has("--help") || cli.Has("-h")) { Usage(); return 0; }
            if (cli.Has("--selftest-d3d9")) return D3D9Asm.Selftest();   // 诊断：SM2/3 反汇编自测
            string disasmFile = cli.Value("--disasm");                    // 诊断：反汇编单个程序文件
            if (disasmFile != null) return DisasmFile(disasmFile);
            string glslFile = cli.Value("--glsl2cg");                     // 诊断：单个 GLSL → Cg（--asm 加原行注释）
            if (glslFile != null)
            {
                Cg.GlslToCg.AnnotateSource = cli.Has("--asm");
                Console.Write(Cg.GlslToCg.Translate(File.ReadAllText(glslFile)).Cg);
                return 0;
            }

            // 资源根（schema/、data/）：显式参数 > 环境变量 > 从 exe 目录/工作目录向上找标记。
            string rootArg = cli.Value("--root") ?? Environment.GetEnvironmentVariable("SHADERRIPPER_ROOT") ?? Environment.GetEnvironmentVariable("SHADERRIP_ROOT");
            Paths.SetRoot(rootArg);
            if (!string.IsNullOrWhiteSpace(rootArg) && !Directory.Exists(rootArg))
                Console.Error.WriteLine(Strings.F("RootDirMissing", rootArg));

            // 游戏数据目录：--data > SHADERRIPPER_DATA > 第一个位置参数。
            // 没有默认值——写死本机 Steam 路径换台机器就跑不起来，宁可报错并打印用法。
            string dataDir = FirstNonEmpty(cli.Value("--data"),
                                           Environment.GetEnvironmentVariable("SHADERRIPPER_DATA") ?? Environment.GetEnvironmentVariable("SHADERRIP_DATA"),
                                           cli.Bare.Count > 0 ? cli.Bare[0] : null);
            if (dataDir == null)
            {
                Console.Error.WriteLine(Strings.Get("NoDataDir"));
                Console.Error.WriteLine();
                Usage();
                return 2;
            }

            dataDir = Path.GetFullPath(dataDir);        // 相对路径按当前工作目录展开
            dataDir = Path.GetFullPath(dataDir);        // 相对路径按当前工作目录展开
            string assets = Path.Combine(dataDir, "resources.assets");
            if (!File.Exists(assets)) { Console.Error.WriteLine(Strings.F("ResourcesAssetsMissing", assets)); return 2; }

            // $Globals 修复表：**只在显式 --names <dir> 时启用**，不再默认扫
            // ShaderLabRef/globals_names。绝大多数 shader 的行名已由参数块/资产反射
            // 覆盖，默默加载一个可能有歧义的修复表反而难排查。
            GlobalsNames.TableDir = cli.Value("--names");

            // --only <substring>: regenerate a single shader, for debugging.
            string only = cli.Value("--only");

            // --platform <id|name>: 强制用某个后端（d3d11/gles3/vulkan/metal…）。
            // 默认挑 d3d11，因为只有它能直译成 Cg。
            int? forcedPlatform = Platforms.ByName(cli.Value("--platform"));

            // --dump [filter]: 只打印 blob 表/子程序/参数块，不写产物（诊断用）。
            bool dumpMode = cli.Has("--dump");
            bool matCheck = cli.Has("--mat-check");
            string dumpFilter = cli.Value("--dump") ?? "";

            // 默认扫包：游戏有 17 个 shader 只存在于 StreamingAssets/aa/*.bundle，
            // 只读 resources.assets 会漏掉它们（ADOFAI_CustomTextureRect*、
            // ScreenScroll、FadeBottom、Sprites_Tile …）。
            // Bundle 扫描始终开启：shader 常散落在 assetbundle 里。
            string bundleDir = cli.Value("--bundles") ?? dataDir;

            // --asm: 生成逐指令的原始反汇编注释（默认关，便于游戏内排查；核对直译时打开）。
            // d3d11：DXBC 指令注释；glsl/gles：原始 GLSL 行注释；d3d9/d3d12：汇编落 Disassembly/。
            if (cli.Has("--asm"))
            {
                global::ShaderRipper.Cg.CgProgramWriter.AnnotateAsm = true;
                global::ShaderRipper.Cg.GlslToCg.AnnotateSource = true;
            }

            // 内建着色器索引：名字出现在引擎内建资源清单里的，就是 Unity 自带的，
            // 不做反编译。索引由编辑器导出（_uproj/Assets/Editor/DumpBuiltinShaders.cs）。
            int indexCount = BuiltinIndex.Load(cli.Value("--builtin-index"));

            // Unity 内置 float4x4 名单：$Globals 里这类成员占连续 4 行，参数块只记起始
            // offset，靠这张表把后续行也命名成 name[1..3]。
            int matrixCount = MatrixNames.Load(cli.Value("--matrix-names"));

            // 逐 shader 的 $Globals 布局表（data/tmp_layouts/*.txt）：TMP 系的成员表被
            // 剥空，只能拿 Unity 自己的 CGIncludes 真编一遍，把 RDEF 抄下来。
            // 运行时按 dcl 行数 + name table 交集校验后才用，默认开启。
            int tmpLayoutCount = Cg.TmpLayouts.Load(cli.Value("--tmp-layouts"));
            if (cli.Has("--no-tmp-layout")) Cg.TmpLayouts.Enabled = false;

            // The Shader type tree differs per Unity generation, and the game ships
            // with type trees stripped, so pick the schema that matches the file.
            uint sfVersion = ReadSerializedFileVersion(assets);
            string schemaName = sfVersion >= 22 ? "shader_typetypes_6000.json" : "shader_typetypes.json";
            string schema = Paths.Schema(schemaName, null);
            if (schema == null)
            {
                Console.Error.WriteLine(Strings.F("SchemaNotFound", schemaName));
                return 3;
            }

            // 输出目录：--out > SHADERRIPPER_OUT > 第二个位置参数 > <游戏名>_ShaderLabs。
            // 游戏名取 _Data/app.info 第二行（Unity 标准文件：公司名\n产品名），
            // 没有就退回目录名去掉 _Data 后缀。
            string gameName = ReadProductName(dataDir);
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
            if (!dumpMode && !matCheck)          // --dump / --mat-check 只读，不落盘
            {
                Directory.CreateDirectory(outDir);
            }

            Console.WriteLine(Strings.Get("Banner"));
            Console.WriteLine(Strings.F("SFV_Schema", sfVersion, schemaName));
            var root = TypeNode.LoadSchema(schema);
            // Version-adaptive schemas: hand-transcribed JSON for the generation
            // this tool was built on, AssetRipper's type-tree database otherwise.
            Func<string, int, TypeNode> tpkClass = (ver, classId) =>
                TpkSchema.Available ? TpkSchema.BuildClass(classId, ver) : null;
            var sf = SerializedFile.Load(assets, root, tpkClass);
            Console.WriteLine(Strings.F("Unity_Shaders", sf.UnityVersion, sf.Shaders.Count)
                              + (TpkSchema.Available ? "  tpk=on" : "  tpk=off (data/sourcetpk.tpk missing)"));
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

            // Shaders that live in asset bundles rather than resources.assets.
            var allShaders = new List<Shader>(sf.Shaders);
            {
                int bundleFiles = 0, nodes = 0, found = 0, skippedSources = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var sh in sf.Shaders)
                    seen.Add(!string.IsNullOrEmpty(sh.Name) ? sh.Name : sh.ParsedForm?.Name ?? "");

                foreach (var path in Directory.EnumerateFiles(bundleDir, "*", SearchOption.AllDirectories))
                {
                    if (!path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)) continue;
                    // 内建资源包（unitybuiltinassets.bundle）整体跳过：里面是
                    // Unity 自带的着色器，不是游戏的渲染逻辑。
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
                            continue; // not a serialized file
                        }
                        if (inner.Shaders.Count == 0) continue;
                        nodes++;
                        foreach (var sh in inner.Shaders)
                        {
                            string nm = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                                      : sh.ParsedForm?.Name ?? "";
                            if (!string.IsNullOrEmpty(nm) && !seen.Add(nm)) continue;   // already have it
                            allShaders.Add(sh);
                            found++;
                        }
                    }
                }
                Console.WriteLine(Strings.F("BundlesScanned", bundleFiles, nodes, found));
            }
            Console.WriteLine(Strings.F("Output", outDir));

            // --- --mat-check: 对账每个材质的存档属性 vs shader 属性表，产出报告 ---
            if (matCheck)
            {
                string report = cli.Value("--mat-check");
                if (string.IsNullOrWhiteSpace(report)) report = "mat_check.json";
                return MatCheck.Run(Path.GetDirectoryName(Path.GetFullPath(assets)), root, tpkClass, report);
            }

            // --- diagnostic: --dump <substring> prints the blob table + per-container profile ---
            if (dumpMode)
            {
                string filter = dumpFilter;
                foreach (var dsh in sf.Shaders)
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
                    // ---- blob index table: u32 count, then count * (u32 off, u32 size, u32 pad) ----
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
                // 名字在引擎内建清单里 → Unity 自带，跳过。--only 是显式点名，不受此限。
                if (only == null && BuiltinIndex.Contains(name)) { skippedBuiltin++; continue; }
                string safe = name;
                foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
                safe = safe.Replace(' ', '_');

                try
                {
                    // 一个 shader 可能同时带多个后端的编译结果；m_Offsets 与
                    // m_Platforms 同序，选错下标拿到的就是另一种格式的字节。
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

                    // disassemble + translate each program, keyed by Unity's m_BlobIndex
                    var progs = new Dictionary<int, Cg.AsmParser.Program>();   // blobIndex -> parsed program
                    // blobIndex -> SubProgram（stage + 关键字变体），用于 Disassembly 命名
                    var subByBlob = sh.ParsedForm.SubShaders
                        .SelectMany(ss => ss.Passes)
                        .SelectMany(p => p.SubPrograms)
                        .GroupBy(sp => sp.BlobIndex)
                        .ToDictionary(g => g.Key, g => g.First());
                    var kwIndex = new Dictionary<int, string>();
                    for (int ki = 0; ki < sh.ParsedForm.KeywordNames.Count; ki++)
                        if (!string.IsNullOrEmpty(sh.ParsedForm.KeywordNames[ki]))
                            kwIndex[ki] = sh.ParsedForm.KeywordNames[ki];

                    // 原始反汇编默认落盘：<out>/Disassembly/<shader>/，一个文件
                    // 对应一个变体的一份顶点/片段程序，游戏内排查时可直接对照。
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

                    // d3d9 主平台：SM2/SM3 裸 token 流，自研反汇编（见 D3D9Asm）。
                    // 只产汇编参考，不回译 Cg —— 方言差距太大。
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

                        // DXBC+DXIL = D3D12/SM6：LLVM IR 文本，CgTranslator 吃不了
                        bool dxil = BlobTool.HasDxilChunk(container);
                        if (dxil) anyDxil = true;

                        Directory.CreateDirectory(disDir);
                        File.WriteAllText(Path.Combine(disDir,
                            DisName(slot.BlobIndex) + (dxil ? ".dxil.asm" : ".dxbc.asm")), asm);
                        if (dxil) continue;                              // 只落参考，不进翻译

                        var prog = global::ShaderRipper.Cg.AsmParser.Parse(asm);
                        foreach (var ins in prog.Code)
                        {
                            opStats.TryGetValue(ins.Opcode, out int c);
                            opStats[ins.Opcode] = c + 1;
                        }
                        progs[slot.BlobIndex] = prog;
                    }

                    // Shader names are not unique: several shaders can sanitize to the
                    // same file name, so disambiguate with the path id on collision.
                    string baseName = safe;
                    if (!usedNames.Add(safe))
                    {
                        safe = safe + "_" + sh.PathId;
                        usedNames.Add(safe);
                    }

                    // 其它平台的编译结果：GLSL / Metal 是**源码**，比任何直译都准，
                    // 单独落到 Disassembly/<shader>/ 下供对照；Vulkan 是 SMOL-V 压缩的
                    // SPIR-V，没有魔数，只能报告。
                    ExportOtherPlatforms(sh, mainPlat, safe, outDir, only);

                    // 主平台不是 DXBC 时，把源码按 blobIndex 摆好交给 writer：
                    // GLSL 能直接写成 GLSLPROGRAM 块，Metal 只能旁路参考。
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

                        // 主平台是 GLSL/Metal 时，Unity 存的就是源码 —— 同样落
                        // Disassembly/，一份一个变体，命名与 DXBC 反汇编一致。
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

                    // ShaderLab with CG bodies
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
                    Console.Error.WriteLine(e.StackTrace);   // --trace 已移除：始终打印调用栈
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

                // 非主平台源码统一进 Disassembly/<shader>/，平台名做前缀区分
                string dir = Path.Combine(outDir, "Disassembly", safe);
                string prefix = info.Name + ".";
                var progs = ProgramSource.Extract(payload);

                // d3d9 侧车：SM2/SM3 token 流反汇编（D3D9Asm）
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
                    // GLSL 的一个 slot 就是整个 pass（自带 VERTEX/FRAGMENT 分支），
                    // 不能按 sub-program 的 stage 命名。
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
            // 帮助文本在 Strings.resx / Strings.zh-Hans.resx（--lang 可切）。
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
    }
}





