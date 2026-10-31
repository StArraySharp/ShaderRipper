# ShaderRipper

English | [简体中文](README.zh-CN.md)

Extract shaders from Unity game data and rebuild them as readable **ShaderLab + HLSLPROGRAM**. Pure C#, all runtime resources embedded, ready to run.

> 💡 **This project's code is 100% AI-generated** (DeepSeek / GLM collaboration), with human work limited to requirements, acceptance testing and release.

**Supported payloads**: dxbc — d3d11, Xbox / GameCore slots (instruction-by-instruction DXBC → HLSL translation), and DXIL chunks inside a dxbc container via dxcompiler (disassembly, back-translation unverified); glsl — GLES3 / GLES2 / OpenGLCore / OpenGL (GLSL back-translation to HLSL); d3d9 — SM2/SM3 (disassembly only). Other backends (Metal, Vulkan, console, WebGPU) have no translator and are skipped.

## Build

Requires the .NET 10 SDK.

Two layers:

- **`ShaderRipper` (`src/ShaderRipper`, assembly `ShaderRipper.dll`)** — the parsing/decompilation core library, usable from other tools
- **`ShaderRipper.Application` (`src/ShaderRipper.Application`)** — the CLI front end

```bash
dotnet build ShaderRipper.slnx -c Release
```

### Release packaging

```bash
pwsh -File tools/make-release.ps1                      # win-x64 + linux-x64
pwsh -File tools/make-release.ps1 -Rid win-x64         # one platform
pwsh -File tools/make-release.ps1 -Rid win-x64 -Target gui
```

Each platform publishes a **self-contained, multi-file** build and zips it as
`publish/<rid>/ShaderRipper-<target>-<version>-<rid>.zip`.

The publish is deliberately not trimmed and not single-file: **both of those break
plugins.** A trimmed self-contained publish folds the runtime assemblies into native
code, so a plugin's reference to `System.Runtime` has no file to resolve against; a
single-file publish hides the same assemblies inside the exe, so `Assembly.LoadFrom`
finds nothing. A plain multi-file publish is the only layout in which plugins work --
and the one a user can debug.

Schema trees, fix-up tables, TMP layouts and the per-RID native libraries are embedded
in the library and extracted to `%TEMP%\ShaderRipper\` on first use, so the release
carries no companion data files.

The ANTLR lexers/parsers under `src/ShaderRipper/Grammar/` are generated ahead of time and checked in, so building needs no JVM. After editing a `.g4`, regenerate them with `py -3 scripts/gen_grammar.py` (needs java and `antlr-complete.jar`; `--antlr <path>` overrides the location).

## Testing

`src/ShaderRipper.Tests` is an NUnit suite over the layers that need neither Unity nor
game data — the platform table, blob-format detection, the UnityFS reader, variant
planning, the ShaderLab well-formedness check, the matrix-chain folder, the d3d9 token
decoder and the plugin registry. It runs in a second:

```bash
dotnet test src/ShaderRipper.Tests/ShaderRipper.Tests.csproj -c Release
```

The `scripts/` harnesses check the parts a unit test cannot reach, because they need
real game data or Unity's own compiler. See
[Correctness verification](#correctness-verification).

## Plugins

Three parts of the pipeline are extensible: **containers** (a format this build cannot
read), **backends** (a payload format it cannot translate) and **emitter passes**
(post-processing over the generated HLSL). A plugin is a class library that
implements `IShaderRipperPlugin` and registers whichever of those it needs.

```bash
# what is loaded, in the order it will be consulted
ShaderRipper.exe --plugins

# a plugin directory other than <exe>/plugins
ShaderRipper.exe --game "Game_Data" --shaderlab out --plugin-dir ./my-plugins

# ignore every plugin, which is the reproducible spelling of "behave like the release"
ShaderRipper.exe --game "Game_Data" --shaderlab out --no-plugins
```

| Capability | Interface | Called |
|---|---|---|
| Claim input bytes | `IContainerProvider` | once per candidate file |
| Enumerate a backend's programs | `ISlotReader` | once per shader × slot |
| Translate one program | `IProgramTranslator` | once per program |
| Rewrite emitted HLSL | `IEmitterPass` | once per program body |

Every capability takes a priority — built-ins are `0`, plugins default to `100`. Lower
runs first for lookup; a platform registered later wins its id, since an entry that
never gets asked would be unusable.

A plugin that throws is skipped and the run continues; each distinct failure is
reported once, so a pass failing on every program of a 400-shader run cannot bury the
real diagnostics. `SHADERRIP_PLUGINDBG=1` prints the effective order and confirms a
pass actually ran.

**[Plugin API reference](docs/api.md)** — every interface, what it is called with and
when, generated from the source's own doc comments (so it cannot drift):

```bash
py -3 scripts/gen_api_docs.py     # docs/api.md, from bin/Release/net10.0/ShaderRipper.xml
```

> In-process plugins need `Assembly.LoadFrom`, so a **NativeAOT** publish cannot load
> them: `--plugins` says so and the built-in backends are used. A self-contained
> release is untrimmed and multi-file for the same reason — see
> [Release packaging](#release-packaging).
>
> [`examples/ShaderRipper.Plugin.Example`](examples/ShaderRipper.Plugin.Example/ExamplePlugin.cs)
> is a worked example of all three extension points. It is not part of the solution —
> build it by path.

## Renderer support

Output follows the backends the game actually shipped; only two payload formats
have a translator, and one more has a disassembler:

| Payload (`m_Platforms` backend) | Windows | Linux | macOS | Output |
|---|---|---|---|---|
| dxbc: d3d11, xboxone, xboxone-d3d12, gamecore* | ✅ | ✅ | ⚠️ untested | ShaderLab + HLSLPROGRAM (assembly transliteration) |
| DXIL chunk inside a dxbc container (D3D12 / SM6) | ✅ | ✅ | ❌ no macOS DXC | Disassembly; back-translation unverified |
| glsl: gles20 / gles3 / openglcore / opengl | ✅ | ✅ | ⚠️ untested | ShaderLab + HLSLPROGRAM (GLSL back-translation) |
| d3d9: SM2/SM3 token stream | ✅ | ✅ | ✅ | Disassembly only (`.orig.asm`); no translator |
| metal / vulkan / console platforms / webgpu | — | — | — | skipped (no translator) |

> macOS is untested: the managed paths should work (publish yourself with `-r osx-arm64`), but DXIL disassembly is unavailable since Microsoft ships no macOS DXC build.
> One shader usually carries several backend slots and each is processed; a game that only ships metal / vulkan produces nothing.
>
> `--platform <name|id>` reads a single slot. A build that ships both d3d11 and gles3 always takes d3d11, so this is the only way to see what the GLSL route produces on its own. Naming a backend that would produce nothing at all (vulkan, metal) is an error — an empty export that exits 0 is the trap `--only` had; `d3d9` is accepted because its programs still land on disk as disassembly.
>
> d3d9 has no back-translation on purpose. SM2/SM3 is a different dialect from DXBC (`texld` / `mova`, absolute register numbers instead of a `dcl_` prefix plus relative `r#`), so the DXBC emitter cannot read it and a second translator would not pay for itself. The pass keeps its render state and carries a note saying its program was read but not translated, which is what the build's data actually supports.

## Usage

```bash
# Simplest: point at the game root (or its <Game>_Data); output goes to <input>_Ripped
ShaderRipper.exe --game "…/steamapps/common/SomeGame"

# Explicit output directory, and write every shader as a .shader (the deliverable)
ShaderRipper.exe --game "…/SomeGame" --shaderlab out

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
| `--strip-dxbc-registers` | Drop the `: register(bN/tN/sN)` bindings from DXBC-sourced ShaderLab so Unity assigns slots itself (`packoffset` is kept; GLSL passes and standalone programs are untouched) |
| `--quiet` / `--verbose` | Final tally only / per-file headings, structure and timings |
| `--lang <en\|zh>` | UI language; follows the system UI language by default |
| `--tmp-layouts <file\|dir>` / `--no-tmp-layout` / `--builtin-index <file>` | Name-table overrides (the embedded tables are used by default) |
| `--plugins` | List what was loaded — containers, backends and emitter passes, in the order they are consulted |
| `--plugin-dir <dir>` | Plugin directory (default `<exe>/plugins`) |
| `--no-plugins` | Skip loading plugins; built-in backends only |
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

Three checks, all offline, all in `scripts/`:

| Harness | Question it answers |
|---|---|
| `verify_logic.py` | Does the emitted HLSL, compiled back to DXBC, compute the same thing? |
| `verify_glsl.py` | Same, for the GLSL route, against the payload it came from |
| `usc_check.py` | Does Unity's own compiler accept every emitted `.shader`? |

Measured on the corpora used for the last release:

```
verify_logic  428/428 = 100%     scan_unwritten  0 unwritten lanes
verify_glsl    34/34  = 100%     scan_nan        0 escaping NaNs
usc_check       89/89            NUnit suite     144/144
```

A round trip through a real Unity 6 test project — the source, what came back out, and
the merged disassembly of every variant — is in [`docs/verification/`](docs/verification/).
How each check works, what it treats as a real difference rather than a compiler
rewrite, and the readability passes it guards are described in
[`docs/verification.md`](docs/verification.md).

## Limitations

- Metal / Vulkan / WebGPU payloads have no translator: those slots are skipped, not guessed
- d3d9 has a disassembler but no translator: the `.orig.asm` is written, the pass body is not
- Pure console platforms (PS/Switch) have no readable output
- d3d12 shows up as a DXIL chunk in a dxbc slot: disassembly works, HLSL back-translation is unverified
- The HLSL is a per-instruction transliteration: register reuse and compiler fusion are kept as-is, so it is equivalent but verbose rather than idiomatic source
