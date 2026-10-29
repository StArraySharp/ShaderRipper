using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Ast;
using ShaderRipper.Emit;
using ShaderRipper.Resolve;
using ShaderRipper.Data;
using ShaderRipper.Localization;

namespace ShaderRipper
{
    /// <summary>
    /// The command-line front end.
    ///
    /// Everything mechanical lives in the ShaderRipper library; this project only
    /// parses argv and prints.  Every message goes through <see cref="Strings"/>, so
    /// the tool speaks the language <c>--lang</c> asks for (or the system UI language
    /// by default).
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // UTF-8 on the console: the localized strings are Chinese in one of
            // the two languages, and a console left on the legacy code page turns
            // every character into '□'.  Failure is not fatal (a redirected stdout
            // on some hosts refuses the change).
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            // One parse for every command below.  Which token is an input, which is
            // an option's value and which is an output directory is the whole job of
            // the parser; reading a positional as `args[2]` instead is what turned
            // `--game <dir> --shaderlab out` into an export into a directory *named*
            // "--shaderlab".
            var opt = Argv.Parse(args);

            // Language is set before anything can print: --lang wins, otherwise
            // the resource manager follows the system UI culture on its own.
            Strings.SetLanguage(opt.Value("--lang"));

            // How much the run says.  Default is one line per shader; --quiet
            // keeps the final tally only, --verbose adds per-file headings,
            // structure counts and timings.
            Game.Log.Level = opt.Has("--quiet") ? Game.LogLevel.Quiet
                           : opt.Has("--verbose") ? Game.LogLevel.Verbose
                           : Game.LogLevel.Normal;

            // Engine declarations come from #include by default, which is what
            // makes the output re-importable.  --self-contained inlines them
            // instead, so the result compiles without Unity's CGIncludes (useful
            // for a standalone d3dcompiler check, at the cost of a file Unity
            // will reject on import).
            if (opt.Has("--self-contained")) Emit.UnityIncludes.Enabled = false;

            // Resolve the command BEFORE the "too few arguments" test, so a
            // mistyped option reports itself instead of printing the usage text and
            // leaving the caller to guess which token it did not recognise.
            string command = Command(args);
            bool knownCommand = Array.IndexOf(Commands, command) >= 0;

            if (args.Length < 2 || args[0] is "-h" or "--help")
            {
                // `--help`/`-h` is a request, not a mistake: print the usage text.
                if (args.Length >= 1 && args[0] is "-h" or "--help")
                {
                    Console.Error.WriteLine(Strings.Get("Usage"));
                    return 2;
                }
                // Otherwise a leading token this tool does not accept is a typo, and
                // saying WHICH token is more useful than five lines of usage.
                if (!knownCommand && args.Length >= 1 &&
                    args[0].StartsWith("-", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(Strings.F("UnknownOption", args[0]));
                    return 2;
                }
                Console.Error.WriteLine(Strings.Get("Usage"));
                return 2;
            }

            // The name tables the constant-buffer analysis reads: the TMP layout
            // rows and the list of Unity's own shaders.  Both are embedded in the
            // binary and unpacked to a temp directory on first use, so a single
            // file is self-contained; a checkout picks up the files directly.
            //
            // $Globals member names are NOT among these: the asset's own parameter
            // blob carries them with byte offsets, which is authoritative in every
            // case a curated table used to cover.
            TmpLayouts.Load(opt.Value("--tmp-layouts"));
            if (opt.Has("--no-tmp-layout")) TmpLayouts.Enabled = false;
            BuiltinIndex.Load(opt.Value("--builtin-index"));

            // The command word is one of the tokens, not necessarily the FIRST one:
            // `--shaderlab <dir> --game <dir>` is a legitimate spelling, and reading
            // args[0] as the command made it fail with "unknown option --shaderlab"
            // even though the parser had already understood it as a flag.

            // How many input arguments this command needs.  `--file` names its
            // inputs through the option itself (variadic), so it needs none; `--game`
            // takes its input from `--game`, so it needs no positional either (an
            // extra one is the output directory).
            int need = command switch
            {
                "--file" or "--file-bare" => 0,
                "--game" => 0,
                "--emit" => 2,
                _ => 1,
            };
            if (!Positionals(opt, need)) return 2;
            switch (command)
            {
                case "--asm": return Asm(opt);
                case "--stats": return Stats(opt.Bare[0]);
                case "--emit": return EmitAll(opt.Bare[0], opt.Bare[1]);
                case "--glsl": return Glsl(opt.Bare[0], opt.Has("--shaderlab"));
                case "--list-shaders": return ListShaders(opt.Bare[0]);
                case "--dump": return Dump(opt.Bare[0], opt.Value("--dump-filter") ?? "");
                case "--file": return ExportFiles(args);
                case "--file-bare": return ExportFiles(args);
                case "--game": return ExportGame(opt);
                default:
                    Console.Error.WriteLine(Strings.F("UnknownOption", command));
                    return 2;
            }
        }

        /// <summary>The command words this tool accepts, in the order to test them.</summary>
        private static readonly string[] Commands =
        {
            "--game", "--file", "--file-bare", "--asm", "--emit", "--glsl",
            "--stats", "--list-shaders", "--dump",
        };

        /// <summary>
        /// The command word on this command line.
        ///
        /// Every token is checked, not just the first: the parser accepts options in
        /// any position, so `--shaderlab out --game &lt;dir&gt;` has to resolve to
        /// <c>--game</c> the same way `--game &lt;dir&gt; --shaderlab out` does.
        /// Returns the FIRST token when none matches, so the "unknown option" message
        /// names what the caller actually typed first.
        /// </summary>
        private static string Command(string[] args)
        {
            foreach (string c in Commands)
                if (args.Contains(c, StringComparer.OrdinalIgnoreCase)) return c;
            return args.Length > 0 ? args[0] : "";
        }

        /// <summary>
        /// Does the command line carry at least <paramref name="count"/> input
        /// arguments, or did it stop after the command word?  Says so once, here,
        /// instead of letting a null path reach a directory enumeration.
        /// </summary>
        private static bool Positionals(Argv opt, int count)
        {
            if (opt.Bare.Count >= count) return true;
            Console.Error.WriteLine(Strings.Get("Usage"));
            return false;
        }

        /// <summary><c>--asm &lt;file&gt; [--out &lt;file&gt;]</c>: one disassembly to HLSL.</summary>
        private static int Asm(Argv opt) => One(opt.Bare[0], opt.Value("--out"));

        private static int One(string path, string outPath)
        {
            var m = Module.Read(File.ReadAllText(path));
            var r = HlslEmitter.Emit(m);
            if (outPath != null) File.WriteAllText(outPath, r.Hlsl);
            else Console.Write(r.Hlsl);

            Console.Error.WriteLine(Strings.F("AsmSummary", m.Profile, r.Instructions,
                                              r.Translated, r.Todo, m.ParseErrors));
            if (r.Todo > 0)
                Console.Error.WriteLine(Strings.F("AsmMissingHandlers", string.Join(", ", r.Missing)));
            return r.Todo == 0 && m.ParseErrors == 0 ? 0 : 1;
        }

        /// <summary>
        /// <c>--game &lt;data dir|game root&gt; [out dir]</c>: rip a whole install.
        ///
        /// The output directory is <c>--out</c>, else the positional after the
        /// input, else "&lt;input&gt;_Ripped".  Every option the parser knows is
        /// skipped, so <c>--game X --shaderlab out</c> writes to <c>out</c> -- reading
        /// the positional as <c>args[2]</c> instead is what made the run create a
        /// directory literally named "--shaderlab".
        /// </summary>
        private static int ExportGame(Argv opt)
        {
            // `--game` names its input through the option, so the input does not
            // depend on where the token sits on the command line.  The output
            // directory is `--out`, else the first positional that is not the input.
            string dataDir = Input(opt.Value("--game") ?? (opt.Bare.Count > 0 ? opt.Bare[0] : ""));
            string positionalOut = opt.Bare
                .FirstOrDefault(b => !string.Equals(b, opt.Value("--game"), StringComparison.Ordinal));
            string outDir = opt.Value("--out")
                            ?? positionalOut
                            ?? dataDir.TrimEnd('/', '\\') + "_Ripped";

            // Every artifact this command can produce is behind a switch: the
            // `.shader` files need `--shaderlab`, the per-program HLSL needs
            // `--programs`, the disassembly needs `--orig-asm`.  Asking for none of
            // them used to mean "translate everything, write nothing" and still exit
            // 0 -- 3263 programs reported as translated, an empty directory, and a
            // marker file claiming `shaders: 425`.
            if (!opt.Has("--shaderlab") && !opt.Has("--programs") && !opt.Has("--orig-asm"))
            {
                Console.Error.WriteLine(Strings.Get("NothingToWrite"));
                return 2;
            }

            // `--platform` narrows the export to a single slot.  A build that ships
            // both d3d11 and gles3 always takes d3d11, so this is the only way to
            // judge the GLSL route's output on its own.
            //
            // A platform with no translator is refused here instead of exported as
            // zero shaders: `--platform vulkan` writing an empty directory and
            // exiting 0 is the same trap `--only` had.
            if (!PlatformArg(opt, out int? platform)) return 2;

            return ExportGame(dataDir, Input(outDir, mustExist: false), opt.Value("--only"),
                              opt.Has("--shaderlab"),
                              opt.Has("--orig-asm"), !opt.Has("--no-dedup"), opt.Has("--builtin"),
                              opt.Has("--programs"), Overwrite(opt),
                              Jobs(opt.Value("--jobs")), platform);
        }

        /// <summary>
        /// A path argument, as the shell meant it.
        ///
        /// Git Bash and MSYS usually rewrite <c>/e/SteamLibrary/...</c> into
        /// <c>E:\SteamLibrary\...</c> for native programs, but not always, and the
        /// argument as typed is then a directory that does not exist.  A missing
        /// <c>/&lt;letter&gt;/...</c> is retried as the drive it names; anything that
        /// exists, and anything not in that shape, is returned unchanged.
        /// </summary>
        /// <param name="mustExist">
        /// An input has to exist for the retry to be worth taking -- a path missing
        /// both ways is the user's own typo.  An output directory usually does not
        /// exist yet, so it is mapped without that check.
        /// </param>
        private static string Input(string path, bool mustExist = true)
        {
            if (string.IsNullOrEmpty(path) || !OperatingSystem.IsWindows()) return path;
            if (Directory.Exists(path) || File.Exists(path)) return path;
            // "/tmp/x" is a path rooted at the current drive on Windows, not drive
            // T:, so only a one-letter first segment is rewritten.
            if (path.Length < 3 || path[0] != '/' || path[2] != '/' || !char.IsLetter(path[1]))
                return path;
            string mapped = char.ToUpperInvariant(path[1]) + ":\\" + path[3..].Replace('/', '\\');
            return !mustExist || Directory.Exists(mapped) || File.Exists(mapped) ? mapped : path;
        }

        /// <summary>Read a game's data directory and translate every shader in it.</summary>
        private static int ExportGame(string dataDir, string outDir, string only, bool shaderLab,
                                      bool origAsm = false, bool dedup = true, bool builtin = false,
                                      bool programs = false,
                                      bool overwrite = false, int jobs = 1, int? platform = null) =>
            RunExport(() => ShaderRipper.Game.GameExport.Run(
                dataDir, outDir, only, shaderLab, origAsm, dedup, builtin, programs,
                null, overwrite, jobs, default, platform), only);

        /// <summary>
        /// Run an export and report it.
        ///
        /// <c>--overwrite</c> refuses a directory it cannot prove is its own by
        /// throwing, and that refusal is a message for the user, not a crash: a stack
        /// trace says "bug", and the whole point of the check is that the user can
        /// act on it.
        /// </summary>
        private static int RunExport(Func<ShaderRipper.Game.GameExport.Stats> run, string only = null)
        {
            try
            {
                var stats = run();
                int code = Report(stats);
                // A filter that matches nothing is the same trap as a run with no
                // output switch: the summary says "0 shaders, 0 failed" and the exit
                // code says success, so a typo in `--only` looks like a clean export
                // of a game that has no shaders at all.
                if (code == 0 && stats.Shaders == 0 && !string.IsNullOrEmpty(only))
                {
                    Console.Error.WriteLine(Strings.F("NoShaderMatched", only));
                    return 2;
                }
                return code;
            }
            catch (InvalidOperationException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
        }

        /// <summary>
        /// <c>--file a.bundle,b.assets out/</c> -- the same export driven by an
        /// explicit input list instead of a data directory.  Each value may be a
        /// SerializedFile, a bundle, a comma/semicolon list or a wildcard; the
        /// output directory is the last non-option argument.
        /// </summary>
        private static int ExportFiles(string[] args)
        {
            var opt = Argv.Parse(args);

            // `--file`'s values come from the parser (`Valued` + `Variadic`), so the
            // whole "does this token take a value" question is answered once, there.
            // Re-deriving it here is what made the two disagree.
            var values = opt.Values("--file").ToList();
            if (values.Count == 0) values.AddRange(opt.Values("--file-bare"));

            // The output directory is `--out`, else the last positional.  `--file`
            // writes next to the first input when neither is given.
            string explicitOut = opt.Value("--out");
            string outDir = explicitOut;
            if (outDir == null && opt.Bare.Count > 0) outDir = opt.Bare[^1];
            outDir = Input(outDir, mustExist: false);

            if (values.Count == 0)
            {
                Console.Error.WriteLine(Strings.Get("UsageFile"));
                return 2;
            }
            values = values.Select(v => Input(v)).ToList();
            if (outDir == null)
            {
                string first = values[0].Split(',', ';')[0].Trim();
                outDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(first))!, "_Ripped");
            }

            if (!PlatformArg(opt, out int? platform)) return 2;

            var s = RunExport(() => ShaderRipper.Game.GameExport.Run(
                Path.GetDirectoryName(Path.GetFullPath(outDir)) ?? ".", outDir,
                opt.Value("--only"), opt.Has("--shaderlab"), opt.Has("--orig-asm"),
                !opt.Has("--no-dedup"), opt.Has("--builtin"),
                opt.Has("--programs"), values,
                Overwrite(opt), Jobs(opt.Value("--jobs")), default, platform));
            return s;
        }

        /// <summary>
        /// <c>--platform &lt;id|name&gt;</c>: read one slot only.
        ///
        /// Both input paths take it, so the option means the same thing whether the
        /// run was pointed at a data directory or at a list of files.  A platform
        /// this tool has no translator for is an error here, not an empty export.
        /// </summary>
        private static bool PlatformArg(Argv opt, out int? platform)
        {
            platform = null;
            string text = opt.Value("--platform");
            if (string.IsNullOrWhiteSpace(text)) return true;

            platform = Data.Platforms.ByName(text);
            if (platform == null)
            {
                Console.Error.WriteLine(Strings.F("PlatformUnknown", text));
                return false;
            }
            var info = Data.Platforms.Get(platform.Value);
            if (info.Format is not ("dxbc" or "glsl"))
            {
                Console.Error.WriteLine(Strings.F("PlatformNoTranslator", info.Name, info.Format));
                return false;
            }
            return true;
        }

        /// <summary>
        /// <c>--jobs N</c>: how many shaders to translate at once.
        ///
        /// Translation is the whole cost of an export and each shader is independent
        /// once its output paths are claimed, so this is close to a linear speedup up
        /// to the core count.  The default is the core count; <c>--jobs 1</c> forces
        /// the serial run, whose log lines do not interleave.
        ///
        /// The output is identical either way: every contested path is decided before
        /// the first worker starts (see <c>GameExport.Seat</c>), and all per-shader
        /// emitter state is per-thread.
        /// </summary>
        private static int Jobs(string value)
        {
            int fallback = Math.Max(1, Environment.ProcessorCount);
            if (string.IsNullOrEmpty(value)) return fallback;
            return int.TryParse(value, out int n) && n > 0 ? n : fallback;
        }

        /// <summary>
        /// <c>--overwrite</c> (alias <c>--clean</c>): empty the output directory
        /// before writing, so one path can be reused instead of a new one per run.
        /// Off by default: an export normally adds to what is there.
        /// </summary>
        private static bool Overwrite(Argv opt) =>
            opt.Has("--overwrite") || opt.Has("--clean");

        private static int Report(ShaderRipper.Game.GameExport.Stats s)
        {
            string summary = Strings.F("ExportSummary", s.Files, s.Shaders, s.Emitted,
                                       s.Programs, s.Disassembled, s.Translated,
                                       s.Skipped, s.Failed);
            if (s.BuiltinSkipped > 0)
                summary += Strings.F("ExportSummaryBuiltinSkipped", s.BuiltinSkipped);
            Console.WriteLine(summary);
            foreach (var n in s.Notes.Take(NoteLimit()))
                Game.Log.Note(Strings.F("Note", n));
            return s.Failed == 0 ? 0 : 1;
        }

        /// <summary>
        /// How many diagnostics to print.  The default is small because a clean
        /// run has nothing to say; a run with many failures is exactly when the
        /// full list is wanted, and <c>SHADERRIP_NOTES=all</c> prints all of it.
        /// </summary>
        private static int NoteLimit() =>
            string.Equals(Environment.GetEnvironmentVariable("SHADERRIP_NOTES"), "all",
                          StringComparison.OrdinalIgnoreCase)
                ? int.MaxValue
                : 8;

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
                    sf = bytes != null ? SerializedFile.Load(bytes, tpk)
                                       : SerializedFile.Load(file, tpk);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(Strings.F("DumpEntryError", name, e.Message));
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
            Console.Error.WriteLine(Strings.F("ListShadersSummary", total, files.Count));
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
                    sf = bytes != null ? SerializedFile.Load(bytes, tpk)
                                       : SerializedFile.Load(file, tpk);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(Strings.F("DumpEntryError", name, e.Message));
                    continue;
                }
                Console.WriteLine(Strings.F("DumpFileHeader", name, sf.UnityVersion,
                                            sf.Objects.Count));
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
                    string none = Strings.Get("DumpNone");
                    string container2 = sh.ContainerPath ?? none;

                    // Two passes out of three inherit every state from the
                    // ShaderLab default, so only the ones that differ are listed;
                    // this is the same information AppendRenderState writes into
                    // the output file, shown before anything is written.
                    var states = RenderStateLines(sh);
                    string source = sh.SourceFile ?? none;
                    Console.WriteLine("  " + n);
                    Console.WriteLine(Strings.F("DumpShaderSummary", sh.PathId, passes, subs,
                                                sh.ParsedForm?.Props.Count ?? 0,
                                                sh.ParsedForm?.KeywordNames.Count ?? 0));
                    Console.WriteLine(Strings.F("DumpPlatforms", string.Join(", ", plats)));
                    Console.WriteLine(Strings.F("DumpOrigin", container2, source, sh.FallbackName));
                    foreach (string line in states) Console.WriteLine("      " + line);
                }
            }
            Console.Error.WriteLine(Strings.F("ListShadersSummary", total, files.Count));
            return 0;
        }

        /// <summary>
        /// Every non-default render-state statement of a shader's passes, one per
        /// line, prefixed with the pass index when there is more than one pass.
        ///
        /// This mirrors what <c>GameExport.AppendRenderState</c> writes into the
        /// .shader, which is the only way to tell "the exporter dropped this
        /// state" from "the asset never carried it".
        /// </summary>
        private static List<string> RenderStateLines(Shader sh)
        {
            var lines = new List<string>();
            var subs = sh.ParsedForm?.SubShaders ?? new List<SubShader>();
            int passIndex = 0;
            foreach (var ss in subs)
                foreach (var p in ss.Passes)
                {
                    var states = ShaderRipper.Game.GameExport.DescribeRenderState(p);
                    if (states.Count == 0) { passIndex++; continue; }
                    string prefix = subs.Sum(s2 => s2.Passes.Count) > 1 ? $"pass {passIndex}: " : "";
                    foreach (string s in states)
                        foreach (string one in s.Split('\n'))
                            lines.Add(prefix + one.Trim());
                    passIndex++;
                }
            return lines;
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
                                           new[] { sub }, Array.Empty<ShaderRipper.Data.Property>());
                var errs = Emit.ShaderLab.Check(text);
                Console.Error.WriteLine(errs.Count == 0
                    ? Strings.Get("ShaderLabWellFormed")
                    : Strings.F("ShaderLabErrors", string.Join("; ", errs)));
            }
            Console.Write(text);
            Console.Error.WriteLine(Strings.F("GlslParseErrors", r.ParseErrors));
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
            Console.WriteLine(Strings.F("EmitAllSummary", ok, todo, outDir));
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

            Console.WriteLine(Strings.F("StatsSummary", files.Count, parsed, failed));
            double pct = instructions == 0 ? 0 : 100.0 * translated / instructions;
            Console.WriteLine(Strings.F("StatsInstructions", instructions, translated, todo,
                                        pct.ToString("F1", Strings.Culture)));
            foreach (var kv in missing.OrderByDescending(k => k.Value).Take(15))
                Console.WriteLine(Strings.F("StatsNoHandler", kv.Key, kv.Value));
            return 0;
        }
    }
}
