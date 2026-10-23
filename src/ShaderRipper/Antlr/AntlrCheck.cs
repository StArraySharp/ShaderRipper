using System;
using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;

namespace ShaderRipper.Antlr
{
    /// <summary>
    /// Well-formedness gate for what the tool emits, driven by the ANTLR
    /// grammars in this folder.
    ///
    /// This is the production use of the grammars: rather than re-deriving the
    /// translation (which the emitters already do, and do it correctly), the
    /// tool can re-parse its own output and say whether it is actually
    /// well-formed ShaderLab and well-formed HLSL.  That catches the class of
    /// defect the text emitters cannot see about themselves -- a swizzle that
    /// comes out empty (`_FaceTex_ST.`), a missing semicolon, a brace that does
    /// not close -- while the shader is still on the way out.
    ///
    /// Enabled by <c>--validate</c>; off by default so the export stays as fast
    /// as it was.
    ///
    /// Why a lock: ANTLR's generated parsers keep their DFA/ATN decision caches
    /// in process-wide statics, and those are documented as not safe to mutate
    /// from several threads at once.  The exporter runs shaders in a
    /// Parallel.ForEach, so validation is serialised here.  It is not on the hot
    /// path -- one parse per emitted shader -- and correctness beats throughput
    /// for a check.
    /// </summary>
    public static class AntlrCheck
    {
        private static readonly object Gate = new();

        /// <summary>Set by <c>--validate</c>.</summary>
        public static bool Enabled;

        /// <summary>Shaders whose ShaderLab text did not parse.</summary>
        public static int BadShaderLab;

        /// <summary>Program bodies whose HLSL did not parse.</summary>
        public static int BadHlsl;

        /// <summary>Shaders checked (one per emitted file).</summary>
        public static int Checked;

        /// <summary>First few problems, kept for the summary.</summary>
        private static readonly List<string> First = new();

        /// <summary>
        /// Parses one emitted ShaderLab file.  Returns the syntax errors found;
        /// an empty list means the text is well-formed.
        /// </summary>
        public static List<string> CheckShaderLab(string text)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(text)) return errors;

            lock (Gate)
            {
                var lexer = new ShaderLabLexer(new AntlrInputStream(text));
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new CollectTokens(errors));
                var parser = new ShaderLabParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new Collect(errors));
                parser.shader();
            }
            return errors;
        }

        /// <summary>Parses one HLSL body (the text between HLSLPROGRAM/ENDHLSL).</summary>
        public static List<string> CheckHlsl(string body)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(body)) return errors;

            lock (Gate)
            {
                var lexer = new HlslLexer(new AntlrInputStream(body));
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new CollectTokens(errors));
                var parser = new HlslParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new Collect(errors));
                parser.translationUnit();
            }
            return errors;
        }

        /// <summary>Parses a GLSL source blob (HLSLcc output).</summary>
        public static List<string> CheckGlsl(string text)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(text)) return errors;
            text = CutAtLast(text, "#endif", '\0');

            lock (Gate)
            {
                var lexer = new GlslLexer(new AntlrInputStream(text));
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new CollectTokens(errors));
                var parser = new GlslParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new Collect(errors));
                parser.translationUnit();
            }
            return errors;
        }

        /// <summary>Parses a DXBC disassembly.</summary>
        public static List<string> CheckDxbcAsm(string asm)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(asm)) return errors;
            asm = CutAtLast(asm, null, '\0');

            lock (Gate)
            {
                var lexer = new DxbcAsmLexer(new AntlrInputStream(asm));
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new CollectTokens(errors));
                var parser = new DxbcAsmParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new Collect(errors));
                parser.program();
            }
            return errors;
        }

        /// <summary>Parses a DXIL (LLVM IR) module.</summary>
        public static List<string> CheckDxil(string ir)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(ir)) return errors;
            ir = CutAtLast(ir, null, '\0');

            lock (Gate)
            {
                var lexer = new DxilLexer(new AntlrInputStream(ir));
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new CollectTokens(errors));
                var parser = new DxilParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new Collect(errors));
                parser.module();
            }
            return errors;
        }

        /// <summary>
        /// Runs the whole gate over one emitted shader: the ShaderLab file, then
        /// every program block inside it.  Records counters and returns true when
        /// everything parsed.
        /// </summary>
        public static bool CheckEmitted(string name, string shaderLab)
        {
            int labErrors = 0, hlslErrors = 0;
            var detail = new StringBuilder();

            var lab = CheckShaderLab(shaderLab);
            labErrors = lab.Count;
            if (lab.Count > 0)
                detail.Append("    shaderlab: ").AppendLine(lab[0]);

            foreach (var (body, line) in ProgramBlocks(shaderLab))
            {
                var errs = CheckHlsl(body);
                if (errs.Count == 0) continue;
                hlslErrors += errs.Count;
                if (detail.Length < 400)
                    detail.Append("    hlsl@line ").Append(line).Append(": ").AppendLine(errs[0]);
            }

            System.Threading.Interlocked.Increment(ref Checked);
            if (labErrors == 0 && hlslErrors == 0) return true;

            if (labErrors > 0) System.Threading.Interlocked.Increment(ref BadShaderLab);
            if (hlslErrors > 0) System.Threading.Interlocked.Increment(ref BadHlsl);

            lock (Gate)
            {
                if (First.Count < 15) First.Add($"{name}\n{detail}");
            }
            return false;
        }

        /// <summary>The text of each HLSLPROGRAM/CGPROGRAM block, with its start line.</summary>
        private static IEnumerable<(string body, int line)> ProgramBlocks(string shaderLab)
        {
            var lines = shaderLab.Replace("\r\n", "\n").Split('\n');
            List<string> buf = null;
            int start = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (buf == null &&
                    (t.Equals("HLSLPROGRAM", StringComparison.OrdinalIgnoreCase) ||
                     t.Equals("CGPROGRAM", StringComparison.OrdinalIgnoreCase)))
                {
                    buf = new List<string>();
                    start = i + 1;
                    continue;
                }
                if (buf != null &&
                    (t.Equals("ENDHLSL", StringComparison.OrdinalIgnoreCase) ||
                     t.Equals("ENDCG", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return (string.Join("\n", buf), start);
                    buf = null;
                    continue;
                }
                buf?.Add(lines[i]);
            }
        }

        /// <summary>
        /// Cuts a stored blob at its container terminator: the last <paramref name="marker"/>
        /// when there is one, else the first <paramref name="terminator"/>.
        /// </summary>
        private static string CutAtLast(string text, string marker, char terminator)
        {
            if (marker != null)
            {
                int i = text.LastIndexOf(marker, StringComparison.Ordinal);
                if (i >= 0) return text.Substring(0, i + marker.Length);
            }
            int nul = text.IndexOf(terminator);
            return nul < 0 ? text : text.Substring(0, nul);
        }

        /// <summary>Human-readable summary, or null when nothing was checked.</summary>
        public static string Summary()
        {
            if (Checked == 0) return null;
            var sb = new StringBuilder();
            sb.Append($"// validate: {Checked} shader(s) checked, ")
              .Append($"{BadShaderLab} bad ShaderLab, {BadHlsl} bad HLSL body");
            if (First.Count == 0) return sb.ToString();

            sb.AppendLine();
            foreach (var f in First) sb.AppendLine(f);
            return sb.ToString();
        }

        private sealed class Collect : BaseErrorListener
        {
            private readonly List<string> _into;
            public Collect(List<string> into) => _into = into;

            public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
                                             int line, int charPositionInLine, string msg, RecognitionException e)
            {
                if (_into.Count >= 8) return;
                _into.Add($"line {line}:{charPositionInLine} {msg} [{offendingSymbol?.Text ?? "<eof>"}]");
            }
        }

        // A lexer reports `int` token types rather than `IToken`s, so it needs its
        // own listener interface; same collection, different signature.
        private sealed class CollectTokens : IAntlrErrorListener<int>
        {
            private readonly List<string> _into;
            public CollectTokens(List<string> into) => _into = into;

            public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
                                    int line, int charPositionInLine, string msg, RecognitionException e)
            {
                if (_into.Count >= 8) return;
                _into.Add($"lex {line}:{charPositionInLine} {msg}");
            }
        }
    }
}
