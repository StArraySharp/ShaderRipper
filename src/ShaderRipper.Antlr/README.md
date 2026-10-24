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
| Cb layout | `Emit/CbLayout.cs` | resolver names -> a constant-buffer declaration HLSL accepts |
| Resolver | `Resolve/CbResolver.cs` | which Unity buffer a `cbN` is, and what each row is called |
| Tables | `Resolve/{UnityCbuffers,GlobalsNames,TmpLayouts,MatrixNames}.cs` | the shipped layouts the resolver names from |
| Data layer | `Data/` | SerializedFile, typetree database, BlobTool, Models, Paths |
| Game reader | `Game/GameExport.cs` | `--game <dataDir> <outDir>` |
| CLI | `Program.cs` | `--asm` / `--stats` / `--emit` / `--glsl` / `--game` |

```sh
dotnet build src/ShaderRipper.Antlr/ShaderRipper.Antlr.csproj -c Release

ShaderRipper.Antlr --asm Disassembly/Foo/1_fragment.dxbc.asm --out 1.hlsl
ShaderRipper.Antlr --stats Disassembly
```

The emitted HLSL is deliberately **register-level** — `float4 r0`, one statement
per instruction, `Texture2D` + `SamplerState` — because that is what makes the
slice checkable: `D3DCompile` either accepts it or names the line that is wrong.
Pretty names (`_Color`, `unity_ObjectToWorld`) come from a second layer: the
constant-buffer analysis, which reads the asset rather than the instruction
stream (the disassembly carries no RDEF at all).

## The constant-buffer layer

A `cbN[row]` read has no name in the disassembly. Unity strips the reflection, so
the name can only come from the asset, and the main tool already works out how —
so that whole layer was ported (namespace `Resolve`) rather than re-invented:

* the pass's name table (`m_NameIndices`) says which names the shader uses;
* `UnityCbuffers` knows the row layout of every buffer Unity defines;
* the program's parameter blob and `m_Parameters` give byte sizes and members
  when the build kept them;
* `GlobalsNames` / `TmpLayouts` carry the curated `$Globals` layouts.

`Emit/CbLayout.cs` is the new part, and it exists because the resolver's answer
cannot be copied into a declaration verbatim: a constant buffer is laid out
member after member, so the declaration must cover every row from zero, in
order. It emits one of two shapes per register:

```hlsl
// named: the layout of the buffer Unity defines, rows this program never read included
cbuffer UnityPerDraw : register(b1)
{
    float4 unity_ObjectToWorld[4];
    float4 unity_WorldToObject[4];
}

// array: only when the row index is a runtime value, which no member name can express
cbuffer CB3 : register(b3) { float4 cb3[21]; }
```

A matrix or array row arrives spelled `name[i]`; the consecutive run becomes one
`float4 name[n]`, which is both the correct layout and the spelling the body's
references already use. A member that shares its row with another (a scalar
packed beside something else) keeps a positional `float4` — naming it would move
every member after it.

Measured on a real game build (2,977 programs, 425 shaders):

| Buffer | Blocks | Named rows | Positional rows |
|---|---|---|---|
| UnityPerDraw / PerFrame / Lighting / ReflectionProbes | 3,215 | 18,390 | 0 |
| UnityGlobals (`$Globals`) | 2,084 | 3,100 | 10,399 |
| UnityPerCamera | 632 | 3,394 | 604 |

Every buffer Unity defines is fully named. `$Globals` stays partly positional for
a reason that is in the data, not in the code: this Unity version writes an empty
`m_ConstantBuffers` array, and the parameter blob records a single member for
`$Globals`, so nothing pins the rows. The main tool has the same hole on the same
build; a curated table (`data/globals_names/<shader>.json`) or a TMP layout closes
it per shader.

Compile rate with the layer in place is unchanged: 216/217 on the small test build
and 397/400 on a 400-program sample of the real game (the remainder are singular
syntax cases, not declarations).

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
