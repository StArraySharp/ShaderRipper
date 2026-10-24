# ShaderRipper.Antlr

The ANTLR-first rewrite, living beside the shipped tool instead of inside it.

The main project grew a hand-written disassembly scanner first and a grammar
route second. That order shows: the scanner is still the reference the grammar
is measured against, and the translator takes a flattened instruction list no
matter who parsed it. This project starts from the other end — **the parse tree
is the program**, and everything is derived from it.

Nothing is shared with `src/ShaderRipper`: same repository, separate assembly,
its own copy of the grammar, no project reference. That way both can be built,
run and compared without either one constraining the other.

## What exists today

| Piece | Path | Role |
|---|---|---|
| Grammar | `Grammar/DxbcAsm.g4` | DXBC disassembly -> tree (copy of the main project's grammar) |
| Generated parser | `Grammar/DxbcAsm{Lexer,Parser,Visitor,BaseVisitor}.cs` | ANTLR 4.13.1 output, checked in (no JVM needed to build) |
| AST | `Ast/DxbcModule.cs` | typed view of the tree: profile, declarations, instructions, operands |
| Emitter | `Emit/HlslEmitter.cs` | tree -> HLSL, register-level |
| CLI | `Program.cs` | `--asm <file>` / `--stats <dir>` |

```sh
dotnet build src/ShaderRipper.Antlr/ShaderRipper.Antlr.csproj -c Release

ShaderRipper.Antlr --asm Disassembly/Foo/1_fragment.dxbc.asm --out 1.hlsl
ShaderRipper.Antlr --stats Disassembly
```

The emitted HLSL is deliberately **register-level** — `float4 r0`, one statement
per instruction, `Texture2D` + `SamplerState` — because that is what makes the
slice checkable: `D3DCompile` either accepts it or names the line that is wrong.
Pretty names (`_Color`, `unity_ObjectToWorld`) are a later layer; they need the
constant-buffer reflection, not the instruction stream.

## Where it stands, measured

Against the 2,645-program disassembly corpus produced from a real Unity build:

```
files=2645 parsed=2645 empty-or-failed=0
instructions=65525 translated=63891 todo=1634 (97.5%)
```

Everything the emitter produces for a straight-line program compiles with
`D3DCompile` (checked on the vertex sampler above with `vs_5_0`).

What is still missing, in the order the corpus asks for it:

| Gap | Occurrences | Why it is not trivial |
|---|---|---|
| `sincos` | 168 files | two outputs (`sincos dst.x, dst.y, src`) |
| `if_z` / `if_nz` / `else` / `endif` | 335 files | block structure: the emitter is flat today |
| `loop` / `endloop` / `breakc_nz` | 96 files | same, plus loop counters |
| `iadd` / `ige` / `ilt` / `itof` / `ftoi` | 159 files | integer register semantics (`asint`/`asfloat` around every op) |
| `discard_nz` | 42 files | one-line, just not written yet |

## Deliberate differences from the main project

* **SLL first, LL on retry.** The disassembly is machine-written; a bail
  strategy turns a mis-guess into an exception and the same token stream is
  parsed again with full LL. Same approach as the main project's grammar reader,
  measured there at 11.0 s against 13.2-15.0 s for plain LL.
* **Operands are read off the tree.** `kind / index / row / swizzle / sign /
  abs` come from the parse, not from re-scanning the operand text; `Raw` exists
  only for diagnostics and `// TODO` comments.
* **No fallback scanner.** If this project cannot parse a line it reports the
  error and keeps going; there is no second parser to hide behind. The main
  project is the fallback, by being a separate program.

## Regenerating the parser

The generated C# is checked in, so building this project needs no JVM:

```sh
JAR=~/.m2/antlr4-4.13.1-complete.jar
java -jar $JAR -Dlanguage=CSharp -no-listener -visitor \
     -package ShaderRipper.Antlr -o src/ShaderRipper.Antlr/Grammar \
     src/ShaderRipper.Antlr/Grammar/DxbcAsm.g4
rm -f src/ShaderRipper.Antlr/Grammar/*.interp src/ShaderRipper.Antlr/Grammar/*.tokens
```

Keep `-package ShaderRipper.Antlr`: the `@parser::namespace` directive has no
effect for the C# target.
