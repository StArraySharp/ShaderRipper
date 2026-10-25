# ShaderRipper

English | [简体中文](README.zh-CN.md)

Extract shaders from Unity game data and rebuild them as readable **ShaderLab + HLSLPROGRAM**. Pure C#, all runtime resources embedded, ready to run.

> 💡 **This project's code is 100% AI-generated** (DeepSeek / GLM collaboration), with human work limited to requirements, acceptance testing and release.

**Supported platforms**: d3d11 (instruction-by-instruction DXBC → HLSL translation), GLES3 / GLES2 / OpenGLCore (GLSL back-translation to HLSL), d3d9 (SM2/3 disassembly), D3D12 (DXIL via dxcompiler); Metal / Vulkan are exported as reference files only.

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

# Linux: AOT cannot cross-compile across OSes — build the Linux AOT on Linux.
# Cross-pack from Windows as a trimmed single-file with CLR host (~56 MB):
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

The published artifact is a single executable with no companion files: schema trees, fix-up tables, TMP layouts and the per-RID native libraries are all embedded and extracted to `%TEMP%\ShaderRipper\` on first use.

## Renderer support

| Backend | Windows | Linux | macOS | Output |
|---|---|---|---|---|
| d3d11 | ✅ | ✅ | ⚠️ untested | ShaderLab + HLSLPROGRAM (assembly transliteration) |
| GLES3 / GLES2 / OpenGLCore | ✅ | ✅ | ⚠️ untested | ShaderLab + HLSLPROGRAM (GLSL back-translation) |
| d3d9 (SM2/3) | ✅ | ✅ | ⚠️ untested | Disassembly (Disassembly/) |
| SM4/5 DXBC | ✅ | ✅ | ⚠️ untested | Disassembly (Disassembly/) |
| D3D12 (DXIL) | ✅ | ✅ | ❌ no official macOS DXC | Disassembly (Disassembly/) |
| Metal / Vulkan | ✅ | ✅ | ✅ | Reference files only, no back-translation |

> macOS is untested: the managed paths should work (publish yourself with `-r osx-arm64`), but DXIL disassembly is unavailable since Microsoft ships no macOS DXC build.

## Usage

```bash
# Simplest: point at the game data directory (contains resources.assets);
# output goes to ./<Game>_ShaderLabs
ShaderRipper.exe --data "GameDir/Game_Data"

# Explicit output directory
ShaderRipper.exe --data "Game_Data" --out D:\out

# Only shaders whose name contains "Floor", with disassembly comments
ShaderRipper.exe --data "Game_Data" --out D:\out --only Floor --asm

# Single / multiple files: .assets, .bundle, .unity3d, .apk all work
# (detected by content — the extension does not matter)
ShaderRipper.exe --file "Game_Data/resources.assets" --out D:\out
ShaderRipper.exe --file "a.apk" --out D:\out
ShaderRipper.exe a.assets b.bundle c.apk --out D:\out      # rm/cp-style: several files, space separated
ShaderRipper.exe --file "assets*.assets" "sharedassets*.assets" --out D:\out   # wildcards

# Output is always an AssetRipper-style tree (Assets/Shader, Assets/Resources) that can be
# copied straight over an ExportedProject
ShaderRipper.exe --file "a.apk" --out D:\ExportedProject
```

Note: the HLSLPROGRAM body is a **translation of the compiled assembly**, not the author's source (source code is not shipped in game data); properties, render state, Pass structure, and variant `#if` chains are faithfully reconstructed.

### Program dialect (`--syntax`)

The emitted block is `HLSLPROGRAM` by default: textures are declared as `Texture2D` / `Texture3D` / `TextureCube` / `Texture2DArray` with a companion `SamplerState`, and sampling goes through `.Sample()` / `.SampleLevel()`. `--syntax cg` restores the legacy `CGPROGRAM` form (`sampler2D` + `tex2D` / `tex2Dlod`). Both are generated from the same translation and compile identically; the difference is only the dialect.

HLSLPROGRAM does **not** pre-include `HLSLSupport.cginc`, so the emitted header includes it explicitly — without it `fixed` / `half` are `unrecognized identifier` (X3004). The sampler is named `sampler<texture>`, which is Unity's own binding convention, so the material texture's importer sampler state still applies.

File names come from the shader name with only the hierarchy separators replaced (`UI/Pause Planets` → `UI_Pause Planets.shader` — spaces, brackets and casing are kept). When a resource path is already claimed by another shader, the file is named after the shader itself rather than getting an opaque path id appended. Shaders are exported in parallel, and every name/path claim is resolved up front, so repeated runs produce identical trees.

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
restores `Resources.Load` assets to `Assets/Resources/<original path>.shader`; bundle-sourced shaders use their
bundle node path. Objects absent from that table (e.g. Always Included Shaders) land in `Assets/Shader/`.
Windows and Linux builds share the same format; for APKs the file is read straight from the zip.

Output is always an AssetRipper-style tree that can be copied over an ExportedProject:

```
<out>/
├── Assets/Shader/*.shader      shaders without container info (bundle-sourced ones keep their
│                               original container path, e.g. Assets/Resources/)
└── Disassembly/<shader>/       disassembly stays outside Assets, so the Unity project stays clean
```

## ⚠️ Unfriendly to IL2CPP games shipping only D3D backends

For games **packed with IL2CPP that only ship d3d9 / d3d11 / d3d12 graphics backends** (typical for Steam titles), output is limited:

- **d3d9 / d3d12**: disassembly only, no HLSL reconstruction
- **d3d11**: HLSL is rebuilt, but as a direct assembly transliteration — far less readable than original source
- Games that do not even ship a GLES3 / OpenGLCore backend: assembly is mostly all you can get

For more readable output, prefer game data that includes **GLES3 / OpenGLCore** backends (mobile builds often have them).

## CLI options

| Option | Description |
|---|---|
| `--data <dir>` | Game data directory (contains resources.assets), or env `SHADERRIPPER_DATA` |
| `--out <dir>` | Output directory, default `./<Game>_ShaderLabs`, or env `SHADERRIPPER_OUT` |
| `--root <dir>` | Root of the schema/data resources (searched upward from the exe by default) |
| `--platform <id\|name>` | Force backend: `d3d11` / `d3d9` / `d3d12` / `gles3` / `gles20` / `openglcore` / `metal` / `vulkan`; d3d11 preferred, otherwise auto-picks a GLSL platform |
| `--syntax <hlsl\|cg>` | Program-block dialect: `hlsl` (default, `HLSLPROGRAM` + `Texture2D`/`SamplerState` + `.Sample()`) or `cg` (legacy `CGPROGRAM` + `sampler2D` + `tex2D`) |
| `--asm` | Emit per-instruction disassembly comments (d3d11=DXBC, glsl=original GLSL lines, d3d9/12=native assembly) |
| `--validate` | Re-parse each emitted shader with the bundled ANTLR grammars (ShaderLab + HLSL) and report any that is not well-formed |
| `--asm-parser <antlr\|regex>` | Which route reads the DXBC disassembly: `DxbcAsm.g4` (default) or the hand-written scanner. Both produce identical output (verified field by field on 77,155 files, and byte-identical on a full export) |
| `--glsl-parser <antlr\|regex>` | Which route reads the GLSL payload: `Glsl.g4` (default) or the line scanner. Both produce identical output (verified on 3,164 files, and byte-identical on a full GL export) |
| `--only <substr>` | Only process shaders whose name contains the substring |
| `--dedup` | Keep one copy per shader when several data files ship the same shader; the copy with a fixed container path wins, ties break on the lowest path id |
| `--bundles [dir]` | Also scan asset bundles (defaults to the same directory as --data) |
| `--list-shaders <f>` | List the shaders inside a SerializedFile |
| `--dump [substr]` | Print blob/parameter block info only, no files written |
| `--schema <f\|dir>` | Type tree to use (auto-selected per file version by default) |
| `--names <dir>` | $Globals fix-up table directory (default `data/globals_names`, loaded only if present; may be omitted from releases) |
| `--tmp-layouts <dir>` | $Globals layout table directory (default `data/tmp_layouts`) |
| `--no-tmp-layout` | Don't use layout tables (TMP row indices stay numeric) |
| `--builtin-index <f>` | Built-in shader name list (default `data/unity_builtin_shaders.txt`) |
| `--matrix-names <f>` | float4x4 name list (default `data/unity_matrix_names.txt`) |
| `--glsl2cg <file>` | Back-translate a single GLSL file to HLSL (debug; add `--asm` to show original lines) |
| `--disasm <file>` | Disassemble a single program file (DXBC/DXIL/d3d9 tokens) |
| `--lang <en\|zh>` | UI language; follows the system by default |
| `-h, --help` | Help |

## Third-party libraries

| Library | Purpose | License |
|---|---|---|
| [AssetRipper.Tpk](https://github.com/AssetRipper/Tpk) / [AssetRipper.IO.Endian](https://github.com/AssetRipper/IO.Endian) / [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) | Unity type-tree parsing / serialized file reading | LGPL-3.0 |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) | Unity LZ4 blob decompression | MIT |
| [DXDecompiler-ly](https://github.com/AssetRipper/DXDecompiler) | Fully managed SM4/5 DXBC disassembly (cross-platform fallback) | MIT |
| [Antlr4.Runtime.Standard](https://github.com/antlr/antlr4) | Runtime for the grammars in [`src/ShaderRipper/Antlr/`](src/ShaderRipper/Antlr/) (`--validate`) | BSD-3-Clause |
| d3dcompiler_47.dll (Microsoft, shipped with Windows, embedded) | SM4/5 DXBC disassembly on Windows | Windows SDK license |
| dxcompiler / dxil ([Microsoft DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler), Linux builds embedded) | DXIL (D3D12/SM6) disassembly | Apache-2.0 (with LLVM exception) |

## Correctness verification

[`docs/verification/`](docs/verification/) holds one round trip through a Unity 6 test project: the source that
went into the project (`*_original.shader`), what this tool extracted back out (`*_ripper.shader`, with
per-instruction comments; `*_ripper_noasm.shader` is the same file without them), and the merged DXBC
disassembly of every compiled variant. The sources ship with the repo, so the round trip can be repeated by
packing them yourself.

| shader | properties | variants | fragment instructions |
|---|---|---|---|
| `ShaderRipperTest/ExtremeFlow` | 37 | 1 | 1328 |
| `ShaderRipperTest/VariantMonster` | 20 | 60 | 84,425 total, 777–1911 per variant |

Every variant was recompiled with `d3dcompiler_47` at `ps_5_0`: 1/1 and 60/60 pass, 0 untranslated
instructions, 0 `TODO` markers, and control flow survives (ExtremeFlow: 3 loops, 5 exits between them,
2 `discard`s, `ddx`/`ddy` preserved).

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

## Limitations

- Metal / Vulkan (SMOL-V compressed SPIR-V) are saved as reference files only, no back-translation
- Pure console platforms (PS/Switch) have no readable output
- d3d9 / d3d12 only produce disassembly, not translated HLSL
- The HLSL is a per-instruction transliteration: register reuse and compiler fusion are kept as-is, so it is equivalent but verbose rather than idiomatic source
