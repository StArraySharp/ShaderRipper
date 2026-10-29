# ShaderRipper

English | [简体中文](README.zh-CN.md)

Extract shaders from Unity game data and rebuild them as readable **ShaderLab + HLSLPROGRAM**. Pure C#, all runtime resources embedded, ready to run.

> 💡 **This project's code is 100% AI-generated** (DeepSeek / GLM collaboration), with human work limited to requirements, acceptance testing and release.

**Supported payloads**: dxbc — d3d11, Xbox / GameCore slots (instruction-by-instruction DXBC → HLSL translation), and DXIL chunks inside a dxbc container via dxcompiler (disassembly, back-translation unverified); glsl — GLES3 / GLES2 / OpenGLCore / OpenGL (GLSL back-translation to HLSL). Other backends (d3d9, Metal, Vulkan, console, WebGPU) have no translator and are skipped.

## 1.2.1 highlights

- Preserve shared compiled programs in every pass that references them.
- Support dynamically indexed inputs such as `v[r2.x + 2]` instead of parsing incomplete instructions.
- Expose constant-buffer and texture reflection through the embedded API; load embedded TMP layouts on demand.
- Mark generated ShaderLab files and identify DXBC/GLSL translation above each program block.

## Build

Requires the .NET 10 SDK.

Two layers:

- **`ShaderRipper` (`src/ShaderRipper`, assembly `ShaderRipper.dll`)** — the parsing/decompilation core library, usable from other tools
- **`ShaderRipper.Application` (`src/ShaderRipper.Application`)** — the CLI front end; rename its apphost to `ShaderRipper.exe` when publishing

```bash
dotnet build ShaderRipper.slnx -c Release

# Windows NativeAOT (~11 MB single file); rename the apphost to ShaderRipper.exe
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r win-x64 -p:PublishAot=true -o publish/aot-win-x64
mv publish/aot-win-x64/ShaderRipper.Application.exe publish/aot-win-x64/ShaderRipper.exe

# Windows, CLR host with trimming (~40 MB single file).  Use this when AOT is not
# available or when a trimmed CLR build is preferred: same output, no trimming
# warnings, and it starts without the AOT compile step.
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/win-x64-clr
mv publish/win-x64-clr/ShaderRipper.Application.exe publish/win-x64-clr/ShaderRipper.exe

# Linux: AOT cannot cross-compile across OSes — build the Linux AOT on Linux.
# Cross-pack from Windows as a trimmed single-file with CLR host (~56 MB):
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

The published artifact is a single executable with no companion files: schema trees, fix-up tables, TMP layouts and the per-RID native libraries are all embedded and extracted to `%TEMP%\ShaderRipper\` on first use.

The ANTLR lexers/parsers under `src/ShaderRipper/Grammar/` are generated ahead of time and checked in, so building needs no JVM. After editing a `.g4`, regenerate them with `py -3 scripts/gen_grammar.py` (needs java and `antlr-complete.jar`; `--antlr <path>` overrides the location).

## Verification scripts

`scripts/` holds the harnesses only — `verify_logic.py`, `verify_glsl.py`, `scan_unwritten.py`, `scan_nan.py`, `scan_props.py`, `usc_check.py` (with `usc_compile.py` as its protocol client) and `gen_grammar.py`. They are development tools, not part of the build; each one's docstring says what it checks and how to run it. The invariants they enforce are described under [Correctness verification](#correctness-verification).

## Renderer support

Output follows the backends the game actually shipped; only two payload formats have a translator:

| Payload (`m_Platforms` backend) | Windows | Linux | macOS | Output |
|---|---|---|---|---|
| dxbc: d3d11, xboxone, xboxone-d3d12, gamecore* | ✅ | ✅ | ⚠️ untested | ShaderLab + HLSLPROGRAM (assembly transliteration) |
| DXIL chunk inside a dxbc container (D3D12 / SM6) | ✅ | ✅ | ❌ no macOS DXC | Disassembly; back-translation unverified |
| glsl: gles20 / gles3 / openglcore / opengl | ✅ | ✅ | ⚠️ untested | ShaderLab + HLSLPROGRAM (GLSL back-translation) |
| d3d9 / metal / vulkan / console platforms / webgpu | — | — | — | skipped (no translator) |

> macOS is untested: the managed paths should work (publish yourself with `-r osx-arm64`), but DXIL disassembly is unavailable since Microsoft ships no macOS DXC build.
> One shader usually carries several backend slots and each is processed; a game that only ships d3d9 / metal / vulkan produces nothing.
>
> `--platform <name|id>` reads a single slot. A build that ships both d3d11 and gles3 always takes d3d11, so this is the only way to see what the GLSL route produces on its own. Naming a backend with no translator (vulkan, d3d9, metal) is an error — an empty export that exits 0 is the trap `--only` had.

## Usage

```bash
# Simplest: point at the game root (or its <Game>_Data); output goes to <input>_Ripped
ShaderRipper.exe --game "…/steamapps/common/A Dance of Fire and Ice"

# Explicit output directory, and write every shader as a .shader (the deliverable)
ShaderRipper.exe --game "…/A Dance of Fire and Ice" --shaderlab out

# Only shaders whose name contains "Bloom", plus each program's original disassembly
ShaderRipper.exe --game "Game_Data" --out out --only Bloom --orig-asm

# Translation threads: logical cores by default, --jobs 1 for the serial run
ShaderRipper.exe --game "Game_Data" --shaderlab out --jobs 4

# One backend only -- the GLES route's output, with the d3d11 slot kept out of the way
ShaderRipper.exe --game "Game_Data" --shaderlab out --platform gles3

# Single / multiple files: .assets, .bundle, .unity3d, .apk all work
# (detected by content — the extension does not matter)
ShaderRipper.exe --file "Game_Data/resources.assets" --out out
ShaderRipper.exe --file "a.apk" --out out
ShaderRipper.exe --file "assets*.assets" "sharedassets*.assets" --out out   # wildcards

# Look before writing anything
ShaderRipper.exe --list-shaders "a.bundle"
ShaderRipper.exe --dump "a.bundle" --dump-filter Bloom

# One link of the chain at a time: a disassembly / a GLSL payload -> HLSL
ShaderRipper.exe --asm "1_vs.orig.asm" --out one.hlsl
ShaderRipper.exe --glsl "1_glsl.orig.glsl" --shaderlab > one.shader
ShaderRipper.exe --emit <dir-with-asm-and-glsl> <outDir>
ShaderRipper.exe --stats <dir-with-asm>
```

`--game` takes the game root as well: the tool looks one level down for
`<Game>_Data/globalgamemanagers` itself (that table is what restores `Resources.Load` paths), so
pointing at the data directory is optional. A Git Bash style `/e/SteamLibrary/...` argument that the
shell did not translate is retried as the drive it names instead of being taken as a missing path.

Note: the HLSLPROGRAM body is a **translation of the compiled assembly**, not the author's source (source code is not shipped in game data); properties, render state, Pass structure, and variant `#if` chains are faithfully reconstructed.

### Program dialect

The emitted block is always `HLSLPROGRAM`: textures are declared as `Texture2D` / `Texture3D` / `TextureCube` / `Texture2DArray` with a companion `SamplerState`, and sampling goes through `.Sample()` / `.SampleLevel()`. The old front end's `--syntax cg` (`CGPROGRAM` + `sampler2D` + `tex2D`) was not carried over, so this is the only form now.

HLSLPROGRAM does **not** pre-include `HLSLSupport.cginc`, so the emitted header includes it explicitly — without it `fixed` / `half` are `unrecognized identifier` (X3004). The sampler is named `sampler<texture>`, which is Unity's own binding convention, so the material texture's importer sampler state still applies.

File names come from the shader name with only the hierarchy separators replaced (`UI/Pause Planets` → `UI_Pause Planets.shader` — spaces, brackets and casing are kept). When a resource path is already claimed by another shader, the file is named after the shader itself rather than getting an opaque path id appended. Shaders are exported in parallel (threads = logical cores by default, `--jobs N` to change it, `--jobs 1` for serial), and every name/path claim is resolved up front, so a parallel run, a serial run and a repeated run all produce byte-identical trees.

The output path is chosen in three steps, in this order:

1. **A fixed path wins.** If the asset's own reflection names where it used to live — a
   `Resources` container entry in `globalgamemanagers`, or an `AssetBundle.m_Container`
   entry inside the bundle — the file lands there (`Assets/Resources/camerafilterpack_*.shader`).
2. **Otherwise the shader's own name**, with `/` → `_`
   (`Assets/Shader/CameraFilterPackLegacy_Color_GrayScale.shader`).
3. **Only if that is taken too**, a `_1`, `_2`, … suffix. In practice this never fires:
   a shader name is unique per project, and colliding *paths* are always a
   `CameraFilterPack` / `CameraFilterPackLegacy` pair whose names differ.

Fixed paths claim first, so a shader the build put at an explicit path keeps it even when a
name-only shader would otherwise win the same file. `--no-dedup` keeps every copy; the default
keeps one, and the copy with a fixed path wins.

### How the `multi_compile` lines are recovered

The build stores only the **set** of keywords each variant was compiled with — the line structure is not in the
data. It is recovered from which keywords can be on at the same time: two keywords that never co-occur in any
variant are the alternatives of one line (Unity only ever defines one of them), while keywords that do co-occur
are separate lines. For the 60-variant test shader in [`docs/verification/`](docs/verification/) that gives back
exactly the four lines it was written with — `__ DIST_SWIRL DIST_WAVE`, `__ GLOW_ON`,
`__ MODE_ADD MODE_MUL MODE_OVERLAY MODE_SCREEN`, `__ SHAPE_RING` — and 60/60 coverage, the full cartesian
product 5 × 3 × 2 × 2. Treating those eight keywords as independent toggles would have claimed 2⁸ = 256 variants.
Every variant keeps its own exact guard (`defined(MODE_MUL)`, not "one of the modes"), so a hand-written `#ifdef`
in the body still means what it did in the input.

Supported containers and compression:

- **SerializedFile** (`.assets`, and the extension-less GUID-named files inside APKs) — uncompressed
- **UnityFS bundles** (`.bundle` / `.unity3d`) — LZ4 / LZ4HC / **LZMA** / uncompressed, mixed per block; LZHAM fails with a clear message (very rare)
- **APK / ZIP** — DEFLATE / stored (LZMA-in-zip is not supported; extremely rare)
- Everything is detected by **content**, so the extension can be omitted

During export the tool also reads the ResourceManager table (`m_Container`) inside `globalgamemanagers` and
restores `Resources.Load` assets to `Assets/Resources/<original path>.shader`. Bundles carry the same kind of
table per object (`AssetBundle.m_Container`), which is read too, so a shader packed into a bundle still lands on
the project path it came from. Objects named by neither land in `Assets/Shader/`.
Windows and Linux builds share the same format; for APKs the file is read straight from the zip.

Output is always an AssetRipper-style tree that can be copied over an ExportedProject:

```
<out>/
└── Assets/
    ├── Resources/…/X.shader  Resources.Load paths (restored from m_Container in globalgamemanagers)
    ├── Shader/X.shader       shaders the table does not list (Unity's own, Always Included, …)
    └── …/X/                  --programs or --orig-asm only: per-program HLSL / original disassembly,
                              beside X.shader
```

## ⚠️ Unfriendly to IL2CPP games shipping only D3D backends

For games **packed with IL2CPP that only ship d3d9 / d3d11 / d3d12 graphics backends** (typical for Steam titles), output is limited:

- **d3d11**: HLSL is rebuilt, but as a direct assembly transliteration — far less readable than original source
- **d3d12 (DXIL)**: disassembly only is guaranteed; DXIL back-translation is unverified
- **d3d9**: no translator at the moment — those shaders are skipped (the old front end's d3d9 token disassembler was not carried over)
- Games that do not even ship a GLES3 / OpenGLCore backend: assembly is mostly all you can get

For more readable output, prefer game data that includes **GLES3 / OpenGLCore** backends (mobile builds often have them): that route back-translates the GLSL Unity itself generated, which is close to the original source.

## CLI options

| Option | Description |
|---|---|
| `--game <gameRoot\|<Game>_Data> [outDir]` | Export a whole install. Both levels are searched for `globalgamemanagers` (it lives in `<Game>_Data/`, not in the root) |
| `--file <path[,;path\|wildcard]>... [outDir]` | Export from an explicit file list; several values, wildcards allowed |
| `--out <dir>` | Output directory; default `<input>_Ripped` (for `--file`, `_Ripped` beside the first input) |
| `--shaderlab` | Write `<Name>.shader` (the deliverable); without it the run only counts, no `.shader` is written |
| `--programs` | Also write every program's HLSL into a `<Name>/` folder |
| `--orig-asm` | Also write the original disassembly (`.orig.asm`) or GLSL (`.orig.glsl`) into that folder; independent of `--programs` |
| `--jobs <N>` | Translation threads, default logical cores; `--jobs 1` is the serial run |
| `--only <text>` | Only shaders whose name contains the text |
| `--no-dedup` | Keep every copy (default: one copy per path — a fixed container path wins, ties break on the lowest path id) |
| `--builtin` | Include Unity's own shaders too (skipped by default, the tally says how many) |
| `--overwrite` (alias `--clean`) | Empty the output directory first; only content this tool produced is removed |
| `--self-contained` | Inline the engine declarations instead of `#include` (compiles standalone; Unity rejects it on import) |
| `--quiet` / `--verbose` | Final tally only / per-file headings, structure and timings |
| `--lang <en\|zh>` | UI language; follows the system UI language by default |
| `--tmp-layouts <file\|dir>` / `--no-tmp-layout` / `--builtin-index <file>` | Name-table overrides (the embedded tables are used by default) |
| `--list-shaders <file>` | List the shaders inside a SerializedFile / bundle (name, pathId, container path); writes nothing |
| `--dump <file> [--dump-filter <text>]` | Print each shader's structure: platform slots, pass/sub-program counts, properties, non-default render state |
| `--asm <file> [--out <file>]` | One disassembly → HLSL (stdout by default) |
| `--glsl <file> [--shaderlab]` | One GLSL payload → HLSL (with `--shaderlab`, wrap it and self-check the result) |
| `--emit <dir> <outDir>` | Translate every `*.asm` / `*.glsl` under a directory into HLSL, mirroring the tree |
| `--stats <dir>` | Corpus report: files parsed, per-instruction translation coverage, top-15 missing handlers |
| `-h, --help` | Help (localized by `--lang`) |

Every `.shader` is re-parsed with the ShaderLab grammar before it is written; one that is not well-formed is counted but not emitted (with a note). An export with failures exits 1, a usage error exits 2.

Options may appear in any order: `--shaderlab out --game <dir>` and `--game <dir> --shaderlab out` are the same run. Only one command word (`--game`, `--file`, `--asm`, …) may be given; its own option carries the input, so no positional is required.

The old front end's options (`--data`, `--root`, `--platform`, `--syntax`, `--dedup`, `--bundles`, `--names`, `--schema`, `--matrix-names`, `--asm-parser`, `--glsl-parser`, `--glsl2cg`, `--disasm`, `--validate`, `--scan-all`, and `--asm` used as a flag) are gone with the rewrite and are ignored if present — the directory scan already takes every container by content, so there is nothing to switch on.

Debug environment variables: `SHADERRIP_NOTES=all` prints every diagnostic (8 by default), `SHADERRIP_STACK=1` prints exception stacks, `SHADERRIP_FORCE_MANAGED=1` skips d3dcompiler and uses the managed disassembler, `SHADERRIP_TPK=<file>` points at the type tree, and `SHADERRIP_CBDBG` / `SHADERRIP_TMPDBG` / `SHADERRIP_TYPEDBG` / `SHADERRIP_BUNDLEDBG` trace their stage.

## Third-party libraries

| Library | Purpose | License |
|---|---|---|
| [AssetRipper.Tpk](https://github.com/AssetRipper/Tpk) / [AssetRipper.IO.Endian](https://github.com/AssetRipper/IO.Endian) / [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) | Unity type-tree parsing / serialized file reading | LGPL-3.0 |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) | Unity LZ4 blob decompression | MIT |
| [DXDecompiler-ly](https://github.com/AssetRipper/DXDecompiler) | Fully managed SM4/5 DXBC disassembly (cross-platform fallback) | MIT |
| [Antlr4.Runtime.Standard](https://github.com/antlr/antlr4) | Runtime for the grammars in [`src/ShaderRipper/Grammar/`](src/ShaderRipper/Grammar/) (DXBC asm, GLSL, HLSL, ShaderLab) | BSD-3-Clause |
| d3dcompiler_47.dll (Microsoft, shipped with Windows, embedded) | SM4/5 DXBC disassembly on Windows | Windows SDK license |
| dxcompiler / dxil ([Microsoft DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler), Linux builds embedded) | DXIL (D3D12/SM6) disassembly | Apache-2.0 (with LLVM exception) |

## Correctness verification

[`docs/verification/`](docs/verification/) holds one round trip through a Unity 6 test project: the source that
went into the project (`*_original.shader`), what this tool extracted back out (`*_ripper.shader`, with
per-instruction comments; `*_ripper_noasm.shader` is the same file without them), and the merged DXBC
disassembly of every compiled variant. Those two `_ripper` files are the artifacts of that run; the
current front end only annotates the file header (and writes `// TODO` for an instruction it cannot
translate), not every instruction. The sources ship with the repo, so the round trip can be repeated by
packing them yourself.

| shader | properties | variants | fragment instructions |
|---|---|---|---|
| `ShaderRipperTest/ExtremeFlow` | 37 | 1 | 1328 |
| `ShaderRipperTest/VariantMonster` | 20 | 60 | 84,425 total, 777–1911 per variant |

Every variant was recompiled with `d3dcompiler_47` at `ps_5_0`: 1/1 and 60/60 pass, 0 untranslated
instructions, 0 `TODO` markers, and control flow survives (ExtremeFlow: 3 loops, 5 exits between them,
2 `discard`s, `ddx`/`ddy` preserved).

### Semantic equivalence (`scripts/verify_logic.py`)

Compiling is not the same as computing the same thing. The check compiles the emitted HLSL back to
DXBC, disassembles it, and compares these invariants against the original disassembly:

1. no constant-buffer register disappears (the *declared* width is not an invariant -- fxc sizes a
   cbuffer from the rows actually read)
2. no constant-buffer row is read that the original never read
3. texture fetch count, from the **generated source** (fxc merges same-coordinate samples, so the
   compiled count is not the promise)
4. `sincos` sin/cos output count -- losing one means a register nothing writes
5. counts of instructions a compiler may not remove (`discard`, `ldms`, `gather`)
6. the immediate-constant set -- where a `1.0`-for-true comparison mask shows up

On a real shipping build (425 shaders, 3,263 programs, 3,195 DXBC programs): **3,195 / 3,195 pass**, and
`scripts/scan_unwritten.py` reports 0 reads of a never-written register. `scripts/usc_check.py`
(Unity's own `UnityShaderCompiler`) compiles them all.

```sh
py -3 scripts/verify_logic.py <export dir> -j 6     # DXBC route: logical equivalence
py -3 scripts/verify_glsl.py <export dir> -j 6      # GLSL route: payload vs emission
py -3 scripts/scan_unwritten.py <export dir>        # reads of unwritten registers
py -3 scripts/scan_nan.py <export dir>              # NaN masks escaping to a target
py -3 scripts/scan_props.py <Assets dir>            # Properties leaked into includes
py -3 scripts/usc_check.py <export dir> -u <Unity>  # Unity's own compiler
py -3 scripts/gen_grammar.py                        # rebuild the parsers from *.g4
```

The GLSL route has no second compiler to compare against, so the reference is the
payload itself: per stage it compares texture fetches, assignment targets and
their counts, comparison operators, and the intrinsics after normalising through
the translator's own rename table. 33/33 pass on that build, and a mutation test
(deleting one `Sample`, dropping one register write) is caught in both cases.

### Readability passes (pattern recognition)

Correct output should also look like the shader it came from. Three pattern
rewrites change the spelling without changing the program:

| Pass | Recognises | Emits |
|---|---|---|
| mask normalisation | per-write data flow: the value is read only by a branch, select, comparison or bitwise op before the lane is rewritten | `? 1.0f : 0.0` instead of `asfloat(0xFFFFFFFFu)` (0xFFFFFFFF is a NaN in IEEE 754, and any later multiply that misses the mask turns a colour into NaN) |
| matrix product | `mul r1,v.y,M[1]; mad r1,M[0],v.x,r1; mad r0,M[2],v.z,r1; add r0,r0,M[3]` | one `mul(M, v)`; the accumulator and the destination may be different registers, the last term may land on an output, and the completing row may sit a few instructions later (fxc schedules the work that feeds the vector in between) |
| `UnityObjectToClipPos` | a `mul(ObjectToWorld, v)` whose result feeds `mul(MatrixVP, ·)` | `o.pos = UnityObjectToClipPos(v.xyz)` — eight instructions become one |

**On `1.0f`**: it is not a blanket replacement. DXBC writes -1 for true, and programs
like `lt → iadd(-t,v) → itof → ge → and` read that mask NUMERICALLY — 1.0 there inverts
the ramp. The test is a per-write def-use walk: from the comparison forward until the
lane is rewritten, every read must be a branch, `movc`, comparison or bitwise op. Only
then are -1 and 1 indistinguishable and `1.0f` is safe (and no longer a NaN).

Per-write, not per-register: in real shaders the same lane holds a mask and then a
number (`if (r0.x) { r0.x = ... }`). A whole-register test rejects all of those — the
first attempt converted 0 of 4,123 masks. Def-use brings it to **144** (−96.5%).

**On re-read components**: a single `mul(M, v)` reads all four components at one
point in time, but fxc is free to overwrite one of them between the rows that read
the others. In `ADOFAI_HoldShader` the sequence is

```hlsl
mul  r2,   r0.xxxx, VP[1]        // reads the OLD r0.x
mul  r0.x, v0.y,    O2W[1].w     // r0.x now holds the world-space w
mad  o0,   VP[3],   r0.xxxx, r1  // reads the NEW r0.x
```

Folding that into `mul(VP, float4(x, y, z, w))` would take the wrong `x`. So each
component records the register and lane it was read from, and one that is written
before the completing row is pinned into a local **where it still had its old
value**:

```hlsl
float pin0 = r0.x;
r0.x = mul(unity_ObjectToWorld, float4(v0.x, v0.y, v0.z, v0.w)).w;
o0.xyzw = mul(unity_MatrixVP, float4(r0.y, pin0, r1.z, r0.x));
```

This is also why such a shader does **not** become `UnityObjectToClipPos`: its clip
position is built from object-space `xyz` and the `w` of the object-to-world
product, which is not the same thing as transforming the position. The macro is
only emitted for an exact `mul(VP, mul(O2W, float4(v, 1.0)))` chain.

The matrices are declared differently per output path: a ShaderLab file pulls in
`UnityCG.cginc` and gets the `float4x4` declarations plus the macro, while the
standalone `--programs` file declares `float4 M[4]` rows (the layout the
disassembly reads), which `mul` does not accept — so that file carries its own
`UnityObjectToClipPos`, the same two multiplies written over the rows.

Folding is deliberately narrow: the four rows must come from one array with
indices exactly 0/1/2/3, every destination mask must be full `xyzw`, nothing may
write the intermediate register between the two products, the two products must be
adjacent, and the intermediate must be dead afterwards.

That last condition is measured, not assumed: collapsing
`mul r0, v, ObjectToWorld … mul r1, r0, MatrixVP` into one
`UnityObjectToClipPos` DELETES r0, and a vertex shader that also needs the world
position (`mul r0.y, r0.y, unity_MatrixV[1].z` for view-space depth) then reads an
uninitialised register. `scan_unwritten.py` went from 0 to 80 sites, which is how
the bug surfaced.

ExtremeFlow's if/else chain, before and after:

Input:

```hlsl
if (energy < 0.5)      accum *= 1.05;
else if (energy < 1.0) accum += float3(0.02, 0.01, 0.03);
else if (energy < 1.8) accum = lerp(accum, accum * _ColorC.rgb, 0.25);
else                   accum = saturate(accum) * (0.9 + 0.1 * sin(fl));

if (accum.g > 1.4) break;              // early exit out of the [loop]
```

Output:

```csharp
r3.xyw = float3(r4.x * 1.05, r4.y * 1.05, r4.z * 1.05);                       // arm 1
r6.xyz = float3((r3.z < 0.5) ? -1.0 : 0.0, (r3.z < 1.0) ? -1.0 : 0.0,
                (r3.z < 1.8) ? -1.0 : 0.0);                                   // the three tests, kept
r7.xyz = float3(mad(r4.x, _ColorC.x, -(r4.x)), ...);                          // lerp: accum*color - accum
r7.xyz = float3(mad(r7.x, 0.25, r4.x), ...);                                  //   ... * 0.25 + accum
r3.xyw = float3(r6.x != 0.0 ? r3.x : r5.x, ...);                              // the select tree
r2.w = (1.4 < r3.y) ? 1.0 : 0.0;
if (any(r2.w)) { r2.xyz = float3(r3.x, r3.y, r3.w); break; }                  // early exit rebuilt
```

The compiler flattened the branch chain into predicated selects and rewrote `lerp` into two `mad`s; the
transliteration keeps that shape instead of guessing the original `if/else` back. The `-1.0` is deliberate —
DXBC comparisons write an all-ones mask on true, and the ramp that consumes these values depends on that sign,
so a `1.0` there would silently invert the test. That is also why the output has more instructions than the
input (1328 vs 1097): fusion the compiler performed cannot be recovered, and inventing it would be a guess.

### Names and tags

Two kinds of identifier are re-spelled rather than copied, because the serialized data stores them in a form
that no hand-written shader uses:

- **ShaderLab tag casing.** The asset keeps tag keys upper-cased (`LIGHTMODE`, `FORWARDBASE`). Unity's parser
  is case-insensitive for the *key*, so the file still works, but the value is matched exactly — so
  `"LIGHTMODE" = "FORWARDBASE"` is written `"LightMode" = "ForwardBase"`. Every key in the documented set is
  canonicalised, and `true`/`false` become `True`/`False`.
- **Texture names.** Unity binds a sampler to its texture by name (`SamplerState sampler__MainTex`), and a bare
  `s1` is rejected outright. The name comes from the asset's own texture reflection; the two lists rarely line
  up — a `[PerRendererData]` texture is never in the table, and a variant can sample a built-in texture it has no
  property for — so the reflection names what it can and the remaining registers take the unmatched property
  names in declaration order. A texture the shader declares no property for keeps its positional `tN`.

## Limitations

- Metal / Vulkan / WebGPU / d3d9 payloads have no translator: those slots are skipped, not guessed
- Pure console platforms (PS/Switch) have no readable output
- d3d12 shows up as a DXIL chunk in a dxbc slot: disassembly works, HLSL back-translation is unverified
- The HLSL is a per-instruction transliteration: register reuse and compiler fusion are kept as-is, so it is equivalent but verbose rather than idiomatic source
