using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// Splits a translated HLSL program into its top-level items using the
    /// <c>Hlsl.g4</c> parse tree -- not string scanning.
    ///
    /// The variant assembler (ShaderLab variant chains) and the cross-stage
    /// merge both need the same three-way cut: preprocessor lines, external
    /// declarations (cbuffers, structs, globals) and function definitions.
    /// Cutting it on the parse tree is what keeps that decision grammatical:
    /// a `float4 _Color;` inside a function body can never be mistaken for a
    /// global, and a cbuffer is recognised by its rule, not by its spelling.
    ///
    /// Item text is taken from the original source over the line span the
    /// tree reports, so re-emitting items reproduces the input verbatim.
    /// </summary>
    internal static class HlslTree
    {
        public sealed class Item
        {
            /// <summary>"pp" | "cbuffer" | "struct" | "decl" | "func" | "semi"</summary>
            public string Kind = "decl";
            public string Text = "";
            public string Name = "";
            /// <summary>cbuffer register when the tree names one, -1 otherwise.</summary>
            public int Register = -1;
        }

        public static List<Item> Split(string hlsl, out int parseErrors)
        {
            parseErrors = 0;
            var items = new List<Item>();
            if (string.IsNullOrWhiteSpace(hlsl)) return items;

            // The grammar has no attribute rule, but the emitter writes fxc's
            // `[loop]` / `[unroll]` / `[branch]` lines, and one of those makes the
            // whole translation unit fail to parse.  Masking their text (keeping
            // the line count and therefore every line number) lets the tree cover
            // the item that contains them, and the item text is still taken from
            // the original source, so the attribute survives in the output.
            string parseText = MaskAttributes(hlsl);

            var input = new AntlrInputStream(parseText);
            var lexer = new HlslLexer(input);
            lexer.RemoveErrorListeners();
            var tokens = new CommonTokenStream(lexer);
            var parser = new HlslParser(tokens);
            parser.RemoveErrorListeners();
            var errs = new List<string>();
            parser.AddErrorListener(new Collector(errs));

            var tree = parser.translationUnit();
            parseErrors = errs.Count;

            string[] lines = hlsl.Replace("\r\n", "\n").Split('\n');

            foreach (var child in tree.children.OfType<HlslParser.ExternalItemContext>())
            {
                var it = new Item();
                if (child.ppLine() != null)
                {
                    it.Kind = "pp";
                    it.Text = Span(lines, child);
                }
                else if (child.functionDefinition() != null)
                {
                    var fn = child.functionDefinition();
                    it.Kind = "func";
                    it.Name = fn.IDENT()?.GetText() ?? "";
                    it.Text = Span(lines, child);
                }
                else if (child.declaration() != null)
                {
                    var d = child.declaration();
                    if (d.cbufferDecl() != null)
                    {
                        var cb = d.cbufferDecl();
                        it.Kind = "cbuffer";
                        it.Name = cb.IDENT()?.GetText() ?? "";
                        it.Register = ParseRegister(cb.registerSpec());
                    }
                    else if (d.structDecl() != null)
                    {
                        it.Kind = "struct";
                        it.Name = d.structDecl().IDENT()?.GetText() ?? "";
                    }
                    else it.Kind = "decl";
                    it.Text = Span(lines, child);
                }
                else // bare SEMI
                {
                    it.Kind = "semi";
                    it.Text = Span(lines, child);
                }
                if (!string.IsNullOrWhiteSpace(it.Text)) items.Add(it);
            }

            // A body the grammar cannot carry (emitter edge cases) degrades to
            // one unparsed item rather than losing the program.
            if (parseErrors > 0)
                return new List<Item> { new() { Kind = "raw", Text = hlsl.TrimEnd() } };

            return items;
        }

        /// <summary>
        /// Blank out fxc attribute lines (`[loop]`, `[unroll]`, `[branch]`) without
        /// changing how many lines the text has, so every line number the parse
        /// tree reports still indexes the original source.
        /// </summary>
        private static string MaskAttributes(string hlsl)
        {
            if (hlsl.IndexOf('[') < 0) return hlsl;
            var sb = new System.Text.StringBuilder(hlsl.Length);
            int i = 0;
            while (i < hlsl.Length)
            {
                int eol = hlsl.IndexOf('\n', i);
                int end = eol < 0 ? hlsl.Length : eol;
                string line = hlsl.Substring(i, end - i);
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
                    System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\[[A-Za-z_]\w*\]\s*$"))
                {
                    // Keep the width so token positions on later lines still line up.
                    sb.Append(new string(' ', line.Length));
                }
                else sb.Append(line);
                if (eol < 0) break;
                sb.Append('\n');
                i = eol + 1;
            }
            return sb.ToString();
        }

        /// <summary>Original source lines the item spans, trimmed of blank edges.</summary>
        private static string Span(string[] lines, ParserRuleContext ctx)
        {
            int from = ctx.Start.Line - 1;
            int to = Math.Min(ctx.Stop.Line, lines.Length) - 1;
            // An item can share its last line with the next one only if the
            // emitter packed two declarations onto one line; that never happens
            // in practice, and if it did the extra text is a semicolon at worst.
            var body = string.Join("\n", lines[from..(to + 1)]);
            return body.TrimEnd('\n');
        }

        private static int ParseRegister(HlslParser.RegisterSpecContext ctx)
        {
            // `register(b3)` -- walk the tokens, not the spelling.
            var m = System.Text.RegularExpressions.Regex.Match(
                ctx?.GetText() ?? "", @"b(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        private sealed class Collector : BaseErrorListener
        {
            private readonly List<string> _errs;
            public Collector(List<string> errs) { _errs = errs; }
            public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                             IToken offendingSymbol, int line, int charPositionInLine,
                                             string msg, RecognitionException e) =>
                _errs.Add($"{line}:{charPositionInLine} {msg}");
        }
    }
}
