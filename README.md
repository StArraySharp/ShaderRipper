# ShaderRipper

English | [简体中文](README.zh-CN.md)

Extract shaders from Unity game data and rebuild them as readable **ShaderLab + CGPROGRAM**. Pure C#, all runtime resources embedded, ready to run.

> 💡 **This project's code is 100% AI-generated** (DeepSeek / GLM collaboration), with human work limited to requirements, acceptance testing and release.

**Supported platforms**: d3d11 (instruction-by-instruction DXBC → Cg translation), GLES3 / GLES2 / OpenGLCore (GLSL back-translation to Cg), d3d9 (SM2/3 disassembly), D3D12 (DXIL via dxcompiler); Metal / Vulkan are exported as reference files only.

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
| d3d11 | ✅ | ✅ | ⚠️ untested | ShaderLab + CGPROGRAM (assembly transliteration) |
| GLES3 / GLES2 / OpenGLCore | ✅ | ✅ | ⚠️ untested | ShaderLab + CGPROGRAM (GLSL back-translation) |
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

Note: the CGPROGRAM body is a **translation of the compiled assembly**, not the author's source (source code is not shipped in game data); properties, render state, Pass structure, and variant `#if` chains are faithfully reconstructed.

### How the `multi_compile` lines are recovered

The build stores only the **set** of keywords each variant was compiled with — the
line structure is not in the data. It is recovered from which keywords can be on at the
same time: two keywords that never co-occur in any variant are the alternatives of one
line (Unity only ever defines one of them), while keywords that do co-occur are separate
lines. For `Hidden/BlendModes/SpritesDefault/OverlayMasked` that yields three lines — a
25-mode `BLENDMODES_MODE_*` line plus `ETC1_EXTERNAL_ALPHA` and `PIXELSNAP_ON` —
covering 100 of the 104 reachable states; the 4 missing ones are the states the build
stripped, so the emitted `#if / #elif` chain is already the runtime truth. Every variant
keeps its own exact guard (`defined(MODE_MUL)`, not "one of the modes"), so a
hand-written `#ifdef` in the body still means what it did in the original source.

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

- **d3d9 / d3d12**: disassembly only, no CGPROGRAM reconstruction
- **d3d11**: Cg is rebuilt, but as a direct assembly transliteration — far less readable than original source
- Games that do not even ship a GLES3 / OpenGLCore backend: assembly is mostly all you can get

For more readable output, prefer game data that includes **GLES3 / OpenGLCore** backends (mobile builds often have them).

## CLI options

| Option | Description |
|---|---|
| `--data <dir>` | Game data directory (contains resources.assets), or env `SHADERRIPPER_DATA` |
| `--out <dir>` | Output directory, default `./<Game>_ShaderLabs`, or env `SHADERRIPPER_OUT` |
| `--root <dir>` | Root of the schema/data resources (searched upward from the exe by default) |
| `--platform <id\|name>` | Force backend: `d3d11` / `d3d9` / `d3d12` / `gles3` / `gles20` / `openglcore` / `metal` / `vulkan`; d3d11 preferred, otherwise auto-picks a GLSL platform |
| `--asm` | Emit per-instruction disassembly comments (d3d11=DXBC, glsl=original GLSL lines, d3d9/12=native assembly) |
| `--only <substr>` | Only process shaders whose name contains the substring |
| `--bundles [dir]` | Also scan asset bundles (defaults to the same directory as --data) |
| `--list-shaders <f>` | List the shaders inside a SerializedFile |
| `--dump [substr]` | Print blob/parameter block info only, no files written |
| `--schema <f\|dir>` | Type tree to use (auto-selected per file version by default) |
| `--names <dir>` | $Globals fix-up table directory (default `data/globals_names`, loaded only if present; may be omitted from releases) |
| `--tmp-layouts <dir>` | $Globals layout table directory (default `data/tmp_layouts`) |
| `--no-tmp-layout` | Don't use layout tables (TMP row indices stay numeric) |
| `--builtin-index <f>` | Built-in shader name list (default `data/unity_builtin_shaders.txt`) |
| `--matrix-names <f>` | float4x4 name list (default `data/unity_matrix_names.txt`) |
| `--glsl2cg <file>` | Back-translate a single GLSL file to Cg (debug; add `--asm` to show original lines) |
| `--disasm <file>` | Disassemble a single program file (DXBC/DXIL/d3d9 tokens) |
| `--lang <en\|zh>` | UI language; follows the system by default |
| `-h, --help` | Help |

## Third-party libraries

| Library | Purpose | License |
|---|---|---|
| [AssetRipper.Tpk](https://github.com/AssetRipper/Tpk) / [AssetRipper.IO.Endian](https://github.com/AssetRipper/IO.Endian) / [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) | Unity type-tree parsing / serialized file reading | LGPL-3.0 |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) | Unity LZ4 blob decompression | MIT |
| [DXDecompiler-ly](https://github.com/AssetRipper/DXDecompiler) | Fully managed SM4/5 DXBC disassembly (cross-platform fallback) | MIT |
| d3dcompiler_47.dll (Microsoft, shipped with Windows, embedded) | SM4/5 DXBC disassembly on Windows | Windows SDK license |
| dxcompiler / dxil ([Microsoft DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler), Linux builds embedded) | DXIL (D3D12/SM6) disassembly | Apache-2.0 (with LLVM exception) |

## Correctness verification

Verification files live in [`docs/verification/`](docs/verification/) (one sub-directory per shader: the original source (AR export / upstream repo), this tool's rebuilt output (with and without per-instruction comments), and the merged DXBC disassembly; the OverlayMasked directory also holds its upstream cginc includes). Using the largest shader of an unnamed commercial Unity 6 game, `Hidden/BlendModes/SpritesDefault/OverlayMasked` (714 KB / 208 keyword variants), we did a three-way comparison against the blend-modes asset's upstream open-source code (original HLSL → compiled DXBC → CG rebuilt by this tool). The DARKEN mode fragment:

Original source (upstream `BlendFunctions.cginc`):

```csharp
fixed3 Darken (fixed3 a, fixed3 b) { return min(a, b); }
// Macro expansion:
fixed4 overlayColor = tex2D(_BLENDMODES_OverlayTexture, uv) * _BLENDMODES_OverlayColor;
fixed3 blendColor = Darken(color.rgb, overlayColor.rgb);
color.rgb = lerp(color.rgb, blendColor, _BLENDMODES_OverlayColor.a);
```

CG rebuilt by this tool (variable names recovered from cbuffer reflection):

```csharp
r0.xyz = tex2D(t1, ...) * _BLENDMODES_OverlayColor.rgb;   // overlayColor
r2.xyzw = tex2D(_MainTex, ...) * v1;                      // color
r0.xyz = min(r0.xyz, r2.xyz);                             // Darken = min ✓
r0.xyz = mad(-r1 * v1, r0);                               // first half of lerp (compiler rewrite)
r0.xyz = mad(_BLENDMODES_OverlayColor.w, r0, r2);         // lerp(color, blend, a) ✓
```

The compiler rewrote `lerp` into two `mad`s; the instruction-by-instruction transliteration preserves both, semantically equivalent to the source. For the verified sample: all 208 variants / 12000 DXBC instructions translated, 0 untranslated, and every cbuffer field matched its original property name.

## Limitations

- Metal / Vulkan (SMOL-V compressed SPIR-V) are saved as reference files only, no back-translation
- Pure console platforms (PS/Switch) have no readable output
- d3d9 / d3d12 only produce disassembly, not translated Cg
