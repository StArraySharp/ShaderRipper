using System;
using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// Wraps translated programs in ShaderLab, and checks the result by parsing
    /// it back with <c>ShaderLab.g4</c>.
    ///
    /// Wrapping needs no grammar — it is a template — but the check does: a
    /// missing brace or an empty tag is invisible to the code that wrote it, and
    /// this is the cheapest place to catch it, before anyone opens the file.
    /// </summary>
    internal static class ShaderLab
    {
        public static string Wrap(string name, string hlsl, bool fragment)
        {
            var sb = new StringBuilder();
            sb.Append("Shader \"Ripped/").Append(name.Replace('"', '_')).Append("\"\n");
            sb.Append("{\n");
            sb.Append("    Properties\n    {\n    }\n");
            sb.Append("    SubShader\n    {\n");
            sb.Append("        Tags { \"RenderType\" = \"Opaque\" }\n");
            sb.Append("        Pass\n        {\n");
            sb.Append("            HLSLPROGRAM\n");
            sb.Append("            #pragma vertex vert\n");
            sb.Append(fragment ? "            #pragma fragment frag\n" : "            #pragma fragment frag\n");
            foreach (var line in hlsl.Replace("\r\n", "\n").Split('\n'))
                sb.Append("            ").Append(line).Append('\n');
            sb.Append("            ENDHLSL\n");
            sb.Append("        }\n    }\n}\n");
            return sb.ToString();
        }

        /// <summary>Re-parse the wrapper; empty when it is well-formed.</summary>
        public static List<string> Check(string shaderLab)
        {
            var errors = new List<string>();
            var lexer = new ShaderLabLexer(new AntlrInputStream(shaderLab));
            lexer.RemoveErrorListeners();
            var parser = new ShaderLabParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new Collector(errors));
            parser.shader();
            return errors;
        }

        private sealed class Collector : BaseErrorListener
        {
            private readonly List<string> _errors;
            public Collector(List<string> errors) { _errors = errors; }
            public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                             IToken offendingSymbol, int line, int charPositionInLine,
                                             string msg, RecognitionException e) =>
                _errors.Add($"line {line}:{charPositionInLine}: {msg}");
        }
    }
}
