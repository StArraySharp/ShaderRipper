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

`Glsl.g4` does more than check: it reads the GLSL payloads for the translator
itself (see "The GLSL route" below).

## The DXBC route, side by side with the hand parser

`DxbcAsm.g4` also drives a second DXBC asm reader, `DxbcAsmReader.cs`, which
produces the same `AsmParser.Program` model as the hand-written scanner. Both
routes ship; `--asm-parser` picks one:

```
--asm-parser antlr    DxbcAsm.g4 (default)
--asm-parser regex    hand-written scanner
```

Both are grammar-driven now: the DXBC route was flipped to the grammar after the
table below was measured, and a full d3d11 export through either route comes out
byte-identical (3,047 files).

They are kept equal on purpose, and that is measured, not asserted:

| Check | Scale | Result |
|---|---|---|
| Model, field by field | 77,155 files | 77,155 identical, 0 different |
| ... covering | 822,736 declarations, 3,028,214 instructions, 9,276,061 operands, 464,282 signature rows | all fields equal |
| End-to-end export, both routes | 257 shaders, 875 files | 875 identical by hash |

So the switch is a choice of mechanism, not of output. Three places where the
grammar route deliberately reproduces a hand-parser quirk rather than
"improving" it, because diverging would silently change output:

* Register kinds `u`, `a`, `g`, `m` have no case in the hand parser's kind
  table and fall back to `"r"`. They do here too.
* `dcl_uav_*` / `dcl_tgsm_*` come out as kind `"other"`, because the hand
  parser has no branch for them.
* A declaration with a trailing semantic keeps it in the mask:
  `dcl_input_ps_siv linear v0.z, position` gives `"z, position"`, not `"z"`.

Each is a candidate for a single fix applied to both routes at once; the point
of the A/B table above is that such a fix can be made and re-verified.

## The GLSL route: the conversion itself, on a tree

`GlslToCgTree.cs` reads a GLSL payload into the same model the line scanner in
`GlslToCg` produces -- uniforms, attributes, varyings, globals, one translated
string per statement -- and both hand that model to one emitter
(`Cg/GlslEmitter.cs`). `--glsl-parser` picks the reader; `antlr` is the default.

| Check | Scale | Result |
|---|---|---|
| Both readers, same input | 3,164 GLSL files / 6,328 programs | identical text, 0 differences |
| Full GL export through each route | 3,047 files | identical by hash |
| Bodies the grammar could not read | 6,328 | 0 |

The switch is a choice of mechanism, and the mechanism is the point: the scanner
works one physical line at a time, so it cannot see a statement. Three things
follow, and only the first is about output equality:

* A call split across lines -- `u_xlat0 = texture(_MainTex,` then
  `u_xlat0.xyzw);` -- is one node here and one bad line there. The tree rewrites
  the whole call and emits `tex2D(_MainTex, (u_xlat0).xy)`; the scanner emits a
  truncated line that does not compile.
* A comment that mentions `texture(_MainTex, u_xlat0.xyzw)` is rewritten by the
  scanner, because a comment is just text at that level. The grammar drops
  comments in the lexer; the tree never sees one.
* `mix`, `mod`, `lessThan` are rewritten as *calls with arguments* rather than as
  words followed by a parenthesis hunt. `lessThan(a, b)` becomes
  `((a) < (b))` because it has two arguments, not because a bracket counter
  found a comma.

Two defects were found and fixed on both routes while building this, which is
the reason the equality table above is not zero for free:

* The coordinate-narrowing rule built its negative lookahead from the 3D/cube
  sampler names. With no such sampler in the shader the lookahead degenerated to
  `(?!)` -- a pattern that can never match -- so the rule silently did nothing
  for most shaders and left `float4` coordinates in `tex2D`. It now narrows any
  3- or 4-component swizzle and keeps `.xy` as written.
* `LINE_COMMENT` / `BLOCK_COMMENT` were real tokens in `Glsl.g4`, so a body with
  a comment parsed as "extraneous input". They are skipped now, like whitespace.

The tree route deliberately reproduces the scanner where the difference is not a
defect, so the two stay comparable: `int(int(0x1f))` keeps its outer wrapper
(the scanner sees the line once), one-argument vector constructors keep the
`(float4)(x)` cast form, and `glstate_matrix_*[n]` expands to the named columns
for the whole family, not just the four matrices the tree knows by name.

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
