using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Antlr.Ast;
using ShaderRipper.Antlr.Emit;
using ShaderRipper.Antlr.Resolve;
using ShaderRipper.Antlr.Data;

namespace ShaderRipper.Antlr
{
    /// <summary>
    /// ShaderRipper.Antlr -- the ANTLR-first rewrite, kept beside the main
    /// project so the two can be compared rather than merged.
    ///
    /// Nothing here reads disassembly text by hand: the grammar
    /// (<c>Grammar/DxbcAsm.g4</c>) is the front end, <c>Ast/</c> is the typed
    /// view of the tree, and <c>Emit/</c> walks it.  The main project stays the
    /// reference implementation (and the shipped tool) while this one grows.
    ///
    ///   ShaderRipper.Antlr --asm &lt;file.dxbc.asm&gt; [--out &lt;file.hlsl&gt;]
    ///   ShaderRipper.Antlr --stats &lt;dir&gt;
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2 || args[0] is "-h" or "--help")
            {
                Console.Error.WriteLine(
                    "usage:\n" +
                    "  ShaderRipper.Antlr --asm <file.dxbc.asm> [--out <file.hlsl>]\n" +
                    "  ShaderRipper.Antlr --stats <dir-with-asm-files>\n" +
                    "  ShaderRipper.Antlr --game <dataDir> <outDir> [options]\n" +
                    "  ShaderRipper.Antlr --file <path[,;path|wildcard]>... <outDir> [options]\n" +
                    "  ShaderRipper.Antlr --list-shaders <file>\n" +
                    "  ShaderRipper.Antlr --dump <file> [--dump-filter <text>]\n" +
                    "\n" +
                    "Export:\n" +
                    "    --shaderlab      write <Name>.shader (the deliverable)\n" +
                    "    --programs       also write per-program HLSL into <Name>/ folders\n" +
                    "    --orig-asm       also dump the original disassembly (implies --programs)\n" +
                    "    --dedup          keep one copy of a shader that ships many times\n" +
                    "    --builtin        include Unity's own shaders as well\n" +
                    "    --only <text>    only shaders whose name contains this\n" +
                    "    --scan-all       take every serialized container, not just shaders\n" +
                    "\n" +
                    "Name tables:\n" +
                    "    --names <dir>          $Globals member list directory\n" +
                    "    --tmp-layouts <dir>    TMP layout rows\n" +
                    "    --builtin-index <file> Unity's own shader list\n" +
                    "    --no-tmp-layout        ignore the TMP layout rows");
                return 2;
            }

            // The name tables the constant-buffer analysis reads: the curated
            // $Globals member lists, the TMP layout rows, and the list of Unity's
            // global matrices.  All three are embedded in the binary and unpacked
            // to a temp directory on first use, so a single file is self-contained;
            // a checkout picks up the files directly.  Each has an override flag,
            // because a shader that names its own members is the only authority on
            // what they are -- a stale table must be overridable.
            GlobalsNames.TableDir = Value(args, "--names")
                                 ?? Paths.FindDir(Path.Combine("data", "globals_names"));
            TmpLayouts.Load(Value(args, "--tmp-layouts"));
            if (args.Contains("--no-tmp-layout")) TmpLayouts.Enabled = false;
            BuiltinIndex.Load(Value(args, "--builtin-index"));

            switch (args[0])
            {
                case "--asm": return One(args[1], args.Length > 3 && args[2] == "--out" ? args[3] : null);
                case "--stats": return Stats(args[1]);
                case "--emit": return EmitAll(args[1], args[2]);
                case "--glsl": return Glsl(args[1], args.Contains("--shaderlab"));
                case "--list-shaders": return ListShaders(args[1]);
                case "--dump": return Dump(args[1], Value(args, "--dump-filter") ?? "");
                case "--file": return ExportFiles(args);
                case "--file-bare": return ExportFiles(args);
                case "--game": return ExportGame(args[1], args.Length > 2 ? args[2] : args[1] + "_Ripped",
                                           Only(args), args.Contains("--shaderlab"),
                                           args.Contains("--orig-asm"),
                                           args.Contains("--dedup"),
                                           args.Contains("--builtin"),
                                           args.Contains("--programs"),
                                           args.Contains("--scan-all"));
                default:
                    Console.Error.WriteLine($"unknown option: {args[0]}");
                    return 2;
            }
        }

        private static int One(string path, string outPath)
        {
            var m = Module.Read(File.ReadAllText(path));
            var r = HlslEmitter.Emit(m);
            if (outPath != null) File.WriteAllText(outPath, r.Hlsl);
            else Console.Write(r.Hlsl);

            Console.Error.WriteLine($"profile={m.Profile} instructions={r.Instructions} "
                                    + $"translated={r.Translated} todo={r.Todo} parseErrors={m.ParseErrors}");
            if (r.Todo > 0) Console.Error.WriteLine("no handler yet: " + string.Join(", ", r.Missing));
            return r.Todo == 0 && m.ParseErrors == 0 ? 0 : 1;
        }

        /// <summary>Read a game's data directory and translate every shader in it.</summary>
        private static int ExportGame(string dataDir, string outDir, string only, bool shaderLab,
                                      bool origAsm = false, bool dedup = false, bool builtin = false,
                                      bool programs = false, bool scanAll = false)
        {
            var s = ShaderRipper.Antlr.Game.GameExport.Run(dataDir, outDir, only, shaderLab,
                                                           origAsm, dedup, builtin, programs,
                                                           null, scanAll);
            return Report(s);
        }

        /// <summary>
        /// <c>--file a.bundle,b.assets out/</c> -- the same export driven by an
        /// explicit input list instead of a data directory.  Each value may be a
        /// SerializedFile, a bundle, a comma/semicolon list or a wildcard; the
        /// output directory is the last non-option argument.
        /// </summary>
        private static int ExportFiles(string[] args)
        {
            var values = new List<string>();
            string outDir = null;
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--", StringComparison.Ordinal))
                {
                    // Options that take a value consume it; flags do not.
                    if (a is "--only") i++;
                    continue;
                }
                // Every non-option argument before the last one is an input; the
                // last is the output directory.  `--file x` (no positional out)
                // therefore writes next to the first input.
                values.Add(a);
            }
            if (values.Count >= 1) outDir = values[^1];
            if (values.Count >= 2) values.RemoveAt(values.Count - 1);
            else if (values.Count == 1) { values.Clear(); }

            if (values.Count == 0)
            {
                Console.Error.WriteLine("usage: ShaderRipper.Antlr --file <path|list|wildcard>... <outDir>");
                return 2;
            }
            if (outDir == null)
            {
                string first = values[0].Split(',', ';')[0].Trim();
                outDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(first))!, "_Ripped");
            }

            var s = ShaderRipper.Antlr.Game.GameExport.Run(
                Path.GetDirectoryName(Path.GetFullPath(outDir)) ?? ".", outDir,
                Only(args), args.Contains("--shaderlab"), args.Contains("--orig-asm"),
                args.Contains("--dedup"), args.Contains("--builtin"),
                args.Contains("--programs"), values, args.Contains("--scan-all"));
            return Report(s);
        }

        private static int Report(ShaderRipper.Antlr.Game.GameExport.Stats s)
        {
            Console.WriteLine($"files={s.Files} shaders={s.Shaders} emitted={s.Emitted} "
                              + $"programs={s.Programs} "
                              + $"disassembled={s.Disassembled} translated={s.Translated} "
                              + $"skipped={s.Skipped} failed={s.Failed}"
                              + (s.BuiltinSkipped > 0 ? $" builtin-skipped={s.BuiltinSkipped}" : ""));
            foreach (var n in s.Notes.Take(8)) Console.WriteLine("  note: " + n);
            return s.Failed == 0 ? 0 : 1;
        }

        private static string Only(string[] args)
        {
            int at = Array.IndexOf(args, "--only");
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        /// <summary>The value of <paramref name="flag"/>, or null when it is absent.</summary>
        private static string Value(string[] args, string flag)
        {
            int at = Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        /// <summary>
        /// <c>--list-shaders &lt;file&gt;</c>: every shader inside an input, one
        /// per line.  Works on a SerializedFile or a bundle, which is how a build
        /// is surveyed before exporting any of it.
        /// </summary>
        private static int ListShaders(string path)
        {
            Func<string, int, TypeNode> tpk = (ver, id) => TpkSchema.BuildClass(id, ver);
            var files = new List<(string name, byte[] bytes, string file)>();
            if (Data.Bundle.IsBundle(path))
                foreach (var (n, d) in Data.Bundle.SerializedFiles(path))
                    files.Add((n, d, path));
            else
                files.Add((Path.GetFileName(path), null, path));

            int total = 0;
            foreach (var (name, bytes, file) in files)
            {
                SerializedFile sf;
                try
                {
                    sf = bytes != null ? SerializedFile.Load(bytes, null, tpk)
                                       : SerializedFile.Load(file, null, tpk);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"  {name}: {e.Message}");
                    continue;
                }
                foreach (var sh in sf.Shaders)
                {
                    string n = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                             : sh.ParsedForm?.Name ?? ("Shader_" + sh.PathId);
                    string container = sh.ContainerPath ?? "";
                    Console.WriteLine(n + "\tpathId=" + sh.PathId + "\tcontainer=" + container);
                    total++;
                }
            }
            Console.Error.WriteLine($"shaders={total} file(s)={files.Count}");
            return 0;
        }

        /// <summary>
        /// <c>--dump &lt;file&gt;</c>: what the exporter sees, without writing anything.
        ///
        /// One line per shader (name, path id, platforms, pass/sub-program count)
        /// plus one line per platform slot saying where the payload came from.  A
        /// translation that comes out wrong is nearly always a slot that was
        /// picked up wrongly or a pass that has no program.
        /// </summary>
        private static int Dump(string path, string filter)
        {
            Func<string, int, TypeNode> tpk = (ver, id) => TpkSchema.BuildClass(id, ver);
            var files = new List<(string name, byte[] bytes, string file)>();
            if (Data.Bundle.IsBundle(path))
                foreach (var (n, d) in Data.Bundle.SerializedFiles(path))
                    files.Add((n, d, path));
            else
                files.Add((Path.GetFileName(path), null, path));

            int total = 0;
            foreach (var (name, bytes, file) in files)
            {
                SerializedFile sf;
                try
                {
                    sf = bytes != null ? SerializedFile.Load(bytes, null, tpk)
                                       : SerializedFile.Load(file, null, tpk);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"  {name}: {e.Message}");
                    continue;
                }
                Console.WriteLine($"# {name}  unity={sf.UnityVersion} objects={sf.Objects.Count}");
                foreach (var sh in sf.Shaders)
                {
                    string n = !string.IsNullOrEmpty(sh.Name) ? sh.Name
                             : sh.ParsedForm?.Name ?? ("Shader_" + sh.PathId);
                    if (filter.Length > 0 && n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    total++;

                    int passes = 0, subs = 0;
                    foreach (var ss in sh.ParsedForm?.SubShaders ?? new List<SubShader>())
                        foreach (var p in ss.Passes)
                        {
                            passes++;
                            subs += p.SubPrograms.Count;
                        }
                    var plats = (sh.Platforms ?? Array.Empty<uint>())
                                .Select(p => Platforms.Get((int)p)?.Name ?? p.ToString());
                    string container2 = sh.ContainerPath ?? "(none)";
                    string source = sh.SourceFile ?? "(none)";
                    Console.WriteLine("  " + n);
                    Console.WriteLine("    pathId=" + sh.PathId + " passes=" + passes
                                      + " subprograms=" + subs
                                      + " props=" + (sh.ParsedForm?.Props.Count ?? 0)
                                      + " keywords=" + (sh.ParsedForm?.KeywordNames.Count ?? 0));
                    Console.WriteLine("    platforms=[" + string.Join(", ", plats) + "]");
                    Console.WriteLine("    container=" + container2 + " source=" + source
                                      + " fallback=" + sh.FallbackName);
                }
            }
            Console.Error.WriteLine($"shaders={total} file(s)={files.Count}");
            return 0;
        }

        /// <summary>GLSL payload -> HLSL (optionally wrapped in ShaderLab).</summary>
        private static int Glsl(string path, bool shaderLab)
        {
            var r = GlslTranslator.Translate(File.ReadAllText(path));
            string text = r.Hlsl;
            if (shaderLab)
            {
                // A standalone GLSL payload has no asset behind it: no properties,
                // no pass tags.  One pass, both entry points it defines.
                var pass = new Emit.ShaderLab.PassText
                {
                    Vertex = r.Hlsl.Contains("float4 vert()", StringComparison.Ordinal)
                        ? new Emit.ShaderLab.Stage { Hlsl = r.Hlsl, Entry = "vert" } : null,
                    Fragment = r.Hlsl.Contains("float4 frag()", StringComparison.Ordinal)
                        ? new Emit.ShaderLab.Stage { Hlsl = r.Hlsl, Entry = "frag" } : null,
                };
                var sub = new Emit.ShaderLab.SubShaderText();
                sub.Passes.Add(pass);
                text = Emit.ShaderLab.Wrap(Path.GetFileNameWithoutExtension(path), null,
                                           new[] { sub }, Array.Empty<ShaderRipper.Antlr.Data.Property>());
                var errs = Emit.ShaderLab.Check(text);
                Console.Error.WriteLine(errs.Count == 0
                    ? "shaderlab: well-formed"
                    : "shaderlab: " + string.Join("; ", errs));
            }
            Console.Write(text);
            Console.Error.WriteLine($"parseErrors={r.ParseErrors}");
            return r.ParseErrors == 0 ? 0 : 1;
        }

        /// <summary>
        /// Write one HLSL file per disassembly, mirroring the input tree.  The
        /// output is what a compiler check runs against: a translation that does
        /// not compile is not a translation.
        /// </summary>
        private static int EmitAll(string dir, string outDir)
        {
            int ok = 0, todo = 0;
            foreach (var f in Directory.EnumerateFiles(dir, "*.asm", SearchOption.AllDirectories))
            {
                var m = Module.Read(File.ReadAllText(f));
                if (m.ParseErrors > 0 || m.Code.Count == 0) continue;
                var r = HlslEmitter.Emit(m);
                string rel = Path.GetRelativePath(dir, f) + ".hlsl";
                string dst = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.WriteAllText(dst, r.Hlsl);
                if (r.Todo == 0) ok++; else todo++;
            }
            // GLSL payloads go through their own translator, same output tree.
            foreach (var f in Directory.EnumerateFiles(dir, "*.glsl", SearchOption.AllDirectories))
            {
                var g = GlslTranslator.Translate(File.ReadAllText(f));
                if (g.ParseErrors > 0) continue;
                string rel = Path.GetRelativePath(dir, f) + ".hlsl";
                string dst = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.WriteAllText(dst, g.Hlsl);
                if (g.Todo == 0) ok++; else todo++;
            }
            Console.WriteLine($"clean={ok} with-todo={todo} out={outDir}");
            return 0;
        }

        /// <summary>
        /// Corpus report: how much of the real disassembly this slice can already
        /// turn into HLSL, counted per instruction rather than per file, because a
        /// file is only as useful as the instructions inside it.
        /// </summary>
        private static int Stats(string dir)
        {
            var files = Directory.EnumerateFiles(dir, "*.asm", SearchOption.AllDirectories)
                                 .OrderBy(f => f, StringComparer.Ordinal).ToList();
            int parsed = 0, failed = 0, instructions = 0, translated = 0, todo = 0;
            var missing = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var f in files)
            {
                var m = Module.Read(File.ReadAllText(f));
                if (m.ParseErrors > 0 || m.Code.Count == 0) { failed++; continue; }
                parsed++;
                var r = HlslEmitter.Emit(m);
                instructions += r.Instructions;
                translated += r.Translated;
                todo += r.Todo;
                foreach (var name in r.Missing) missing[name] = missing.GetValueOrDefault(name) + 1;
            }

            Console.WriteLine($"files={files.Count} parsed={parsed} empty-or-failed={failed}");
            Console.WriteLine($"instructions={instructions} translated={translated} todo={todo} "
                              + $"({(instructions == 0 ? 0 : 100.0 * translated / instructions):F1}%)");
            foreach (var kv in missing.OrderByDescending(k => k.Value).Take(15))
                Console.WriteLine($"  no handler: {kv.Key} ({kv.Value} file(s))");
            return 0;
        }
    }
}
