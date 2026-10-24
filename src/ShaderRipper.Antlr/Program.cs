using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Antlr.Ast;
using ShaderRipper.Antlr.Emit;

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
                    "  ShaderRipper.Antlr --stats <dir-with-asm-files>");
                return 2;
            }

            switch (args[0])
            {
                case "--asm": return One(args[1], args.Length > 3 && args[2] == "--out" ? args[3] : null);
                case "--stats": return Stats(args[1]);
                case "--emit": return EmitAll(args[1], args[2]);
                case "--glsl": return Glsl(args[1], args.Contains("--shaderlab"));
                case "--game": return ExportGame(args[1], args.Length > 2 ? args[2] : args[1] + "_Ripped",
                                           Only(args), args.Contains("--shaderlab"));
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
        private static int ExportGame(string dataDir, string outDir, string only, bool shaderLab)
        {
            var s = ShaderRipper.Antlr.Game.GameExport.Run(dataDir, outDir, only, shaderLab);
            Console.WriteLine($"files={s.Files} shaders={s.Shaders} programs={s.Programs} "
                              + $"disassembled={s.Disassembled} translated={s.Translated} "
                              + $"skipped={s.Skipped} failed={s.Failed}");
            foreach (var n in s.Notes.Take(8)) Console.WriteLine("  note: " + n);
            return s.Failed == 0 ? 0 : 1;
        }

        private static string Only(string[] args)
        {
            int at = Array.IndexOf(args, "--only");
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        /// <summary>GLSL payload -> HLSL (optionally wrapped in ShaderLab).</summary>
        private static int Glsl(string path, bool shaderLab)
        {
            var r = GlslTranslator.Translate(File.ReadAllText(path));
            string text = r.Hlsl;
            if (shaderLab)
            {
                text = Emit.ShaderLab.Wrap(Path.GetFileNameWithoutExtension(path), r.Hlsl, true);
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
