# ANTLR grammar set

Five grammars, one per language this tool reads or writes:

| Grammar | Language | Role |
|---|---|---|
| `DxbcAsm.g4` | DXBC assembly (fxc / `D3DDisassemble` text) | source: the d3d11 compilers' only surviving form |
| `Glsl.g4` | GLSL as Unity's HLSLcc emits it | source: the GLES3 / OpenGLCore programs |
| `Dxil.g4` | DXIL (LLVM IR text) | source: D3D12 / Shader Model 6 |
| `Hlsl.g4` | HLSL | target: the language inside `HLSLPROGRAM` |
| `ShaderLab.g4` | ShaderLab | target container: `Shader` / `SubShader` / `Pass` |

The direction of travel is **DXBC / GLSL / DXIL -> ShaderLab + HLSL**, which is
the same direction the emitters already take. These grammars give that pipeline
a definition of each language instead of a pile of regular expressions.

## What is actually wired

`--validate` re-parses every emitted shader with `ShaderLab.g4`, then re-parses
each program block inside it with `Hlsl.g4`, and reports anything that is not
well-formed. It is off by default.

```
ShaderRipper.exe --data "<game>_Data" --out <dir> --validate
...
done: shaders=403, skipped built-in=11
// validate: 403 shader(s) checked, 0 bad ShaderLab, 0 bad HLSL body
```

This is not decoration: it catches defects the text emitters cannot see about
their own output. An earlier build emitted `_FaceTex_ST.` -- a swizzle whose
component name went missing -- in 809 program blocks. The emitters produced it
happily and `UnityShaderCompiler` was never asked; `--validate` rejects it:

```
BAD  tmp_sdf-surface.shader
    hlsl@line 77: line 148:41 no viable alternative at input 'float2(mad(r3.x,_FaceTex_ST.,' [,]
```

`AntlrCheck.cs` is the entry point; the parsers themselves are the generated
files next to each grammar.

## Validation against real corpora

Every grammar is measured against the data in this repository, not against
hand-written samples. The harness that does it lives outside the repo (it is a
throwaway project) because it is a development check, not a shipped feature.

| Grammar | Corpus | Files | Lines | Result |
|---|---|---|---|---|
| `DxbcAsm.g4` | every `Disassembly/**/*.asm` | 75,919 | 5,677,004 | all parse |
| `Glsl.g4` | every `Disassembly/**/*.glsl` | 74,592 | 11,032,414 | all parse |
| `Dxil.g4` | DXIL produced from those DXBC blobs | 300 | 121,395 | all parse |
| `ShaderLab.g4` | every emitted `*.shader` | 20,231 | 12,597,624 | all parse |
| `Hlsl.g4` | program blocks from a current export | 462 | 197,848 | all parse |

Two facts about the corpora that shaped the grammars, both measured rather than
assumed:

* **The stored blobs are text plus padding.** GLSL ends its real text at the
  closing `#endif` of the FRAGMENT branch; the bytes after it are buffer garbage
  -- `00 00 19`, `00 00 11`, and in 242 files a printable `9`. DXBC and DXIL dumps
  are NUL-terminated. Cutting there is a container detail, so the callers do it
  and the grammars do not model padding.
* **There is no DXIL in the game data.** 1,183 containers scanned, zero `DXIL`
  chunks. The DXIL corpus above was produced by converting the DXBC blobs this
  repo already has with `dxbc2dxil /emit-llvm`, which prints the LLVM IR text --
  the same text `dxc -Fc` would give. That is a real corpus, not a synthetic one,
  but it is a converted one.

## Where each grammar draws its line

**The preprocessor is not modelled.** A directive is one opaque line token in
`Glsl.g4` and `Hlsl.g4`. Expanding macros is a different stage: the only macros
that occur are HLSLcc's own (`UNITY_LOCATION`, `UNITY_BINDING`, `UNITY_UNIFORM`)
and include guards, and modelling `#define` bodies inside the grammar would be a
second preprocessor rather than a grammar. `UNITY_UNIFORM` and the
`UNITY_*(...)` invocations are accepted in qualifier position because that is the
only place they appear (161,358 / 106 / 372 occurrences).

**Opcodes are identifiers, not keywords.** In `DxbcAsm.g4` and `Dxil.g4` an
instruction is `opcode operand-list`, and the adapter checks the opcode against
the known set. Assembly dialects are open-world: a new opcode must not require a
grammar change. 101 distinct opcodes occur in the DXBC corpus.

**ShaderLab's program block is one token.** `ShaderLab.g4` swallows
`HLSLPROGRAM ... ENDHLSL` whole, because a `}` inside a shader function would
otherwise close the `Pass`. The HLSL inside is `Hlsl.g4`'s job.

**Comments.** Hidden channel in `Hlsl.g4` and `Glsl.g4` -- they are annotation,
and the emitter writes them anywhere (`// cb0 = $Globals (material properties)`).
Kept as tokens in `DxbcAsm.g4`, because fxc's input/output signature tables are
comments and the adapter reads them.

## Regenerating

The generated C# is checked in, so building this project does **not** need a
JVM. Only `Antlr4.Runtime.Standard` is referenced. After editing a `.g4`,
regenerate with ANTLR 4.13.1:

```sh
JAR=~/.m2/antlr4-4.13.1-complete.jar
for g in DxbcAsm Glsl Hlsl ShaderLab Dxil; do
  java -jar $JAR -Dlanguage=CSharp -no-listener -visitor \
       -package ShaderRipper.Antlr -o src/ShaderRipper/Antlr src/ShaderRipper/Antlr/$g.g4
done
rm -f src/ShaderRipper/Antlr/*.interp src/ShaderRipper/Antlr/*.tokens
```

`-package` is what puts the generated types in `ShaderRipper.Antlr`; the
`@parser::namespace` grammar directive does **not** work for the C# target.

## Two lexer traps worth knowing

Both cost real time and are not obvious from ANTLR's documentation:

1. **A keyword must be declared before `IDENT`.** ANTLR breaks a
   longest-match tie by rule order, so `UNITY_UNIFORM` listed after `IDENT`
   loses and parses as an ordinary name. The same applies to `PROFILE` before
   `IDENT` in `DxbcAsm.g4` and to every keyword in `Dxil.g4`.
2. **A label needs its colon inside the token.** `LABEL_ID : (name | digits) ':'`
   is what keeps a block label called `type` from colliding with the `type`
   keyword. Splitting the colon into its own token makes that collision
   unavoidable.
