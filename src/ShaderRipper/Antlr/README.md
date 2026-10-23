# ANTLR expression layer (Cg / HLSL)

A structured alternative to the regex pass in `CgSyntax.RewriteLine`, kept in its
own folder so it does not touch the existing translation path.

## Why

`CgSyntax.RewriteLine` rewrites sampling intrinsics one *line* of text at a time:
it finds `tex2D(` with a regex, then counts parentheses by hand to locate the
argument list.  Three things are invisible to that approach:

| Input | Regex path | This path |
|---|---|---|
| `x = 1; /* tex2D(_MainTex, uv) */` | rewrites **inside the comment** | leaves it alone |
| `s = "tex2D(_MainTex, uv)";` | rewrites **inside the string** | leaves it alone |
| a call whose arguments continue onto the next line | gives up silently, call survives untranslated | handled (body is lexed as a whole) |

The lexer owns the notion of "comment" and "string literal", so the first two
are impossible by construction rather than by careful regex.

## Scope

Deliberately the **expression layer only**.  There is no statement, declaration
or preprocessor grammar.  The rewriter:

1. lexes the whole body once (`CgExprLexer`),
2. scans the default channel for tokens naming a sampling intrinsic,
3. asks `CgExprParser.expression()` for the argument structure of *that* call,
4. splices a replacement into the original text, right to left.

Anything the grammar does not accept is left exactly as emitted — the failure
direction is "no rewrite", never "wrong rewrite".

## Equivalence with the regex path

Measured over the checked-in verification corpus (both `docs/verification`
shaders, every line of the emitted `_ripper_noasm.shader` files):

```
corpus lines      : 82869
identical         : 82869
different         : 0
regex rewrote     : 695
antlr rewrote     : 695
```

So on real emitted shaders the two paths agree line for line; the differences
only appear on the inputs in the table above, where the regex path is wrong.

## Generated code is checked in

`CgExprLexer.cs`, `CgExprParser.cs`, `CgExprVisitor.cs` and
`CgExprBaseVisitor.cs` are **generated ahead of time and committed**, so building
this project needs no JVM.  Only `Antlr4.Runtime.Standard` is referenced.

After editing `CgExpr.g4`, regenerate with ANTLR 4.13.1:

```
java -jar antlr-4.13.1-complete.jar -Dlanguage=CSharp -no-listener -visitor \
     -package ShaderRipper.Antlr -o . CgExpr.g4
rm -f *.interp *.tokens
```

`-package ShaderRipper.Antlr` is what puts the generated types in this
namespace; the `@parser::namespace` grammar directive does **not** work for the
C# target.

## Verification

- Builds and runs under NativeAOT (`PublishAot=true`, `TrimMode=full`,
  `EnableTrimAnalyzer`/`EnableAotAnalyzer` on): zero IL2xxx/IL3xxx diagnostics.
- Throughput on a 29 KB / ~600-statement shader: ~8.3 ms per full parse,
  ~3.3 MB/s.  Fine for per-shader use; do not re-parse the same body in a loop.
