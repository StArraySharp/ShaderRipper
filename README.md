# ShaderRipper

[English](README.md) | [简体中文](README.zh-CN.md)

Extract Shaders from Unity game data and rebuild them as readable **ShaderLab + CGPROGRAM**. Pure C#, no external dependencies.

**Supported platforms**: d3d11 (instruction-by-instruction DXBC→Cg translation), GLES3 / GLES2 / OpenGLCore (GLSL source back-translation to Cg), d3d9 (SM2/3, self-contained disassembler), D3D12 (DXIL via dxcompiler COM); Metal / Vulkan are exported as reference files only.

## Build

Requires the .NET 10 SDK. Disassembly on Windows uses d3dcompiler/dxcompiler (NativeAOT linking needs MSVC + Windows SDK lib paths).

```bash
dotnet build ShaderRipper.slnx -c Release

# Windows NativeAOT (~5 MB single file)
dotnet publish src/ShaderRipper/ShaderRipper.csproj -c Release -r win-x64 -p:PublishAot=true -o publish/aot-win-x64

# Linux: AOT cannot cross-compile across OSes — build the Linux AOT on Linux.
# Cross-pack from Windows with self-contained + trimmed (~21 MB single file):
dotnet publish src/ShaderRipper/ShaderRipper.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

Linux / macOS:
- **d3d9 (SM2/3) disassembly is fully managed and works everywhere** with no native libraries
- **DXIL (D3D12/SM6) disassembly** uses `libdxcompiler.so` (official Linux builds of Microsoft's [DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler)); put it in `/usr/lib` or next to the executable. `NativeLibs/linux-x64/` ships v1.8.2407 (glibc ≤ 2.34, compatible with Ubuntu 22.04)
- **SM4/5 DXBC disassembly**: uses the embedded d3dcompiler_47 on Windows; when it is absent or fails (including Linux) it falls back to [DXDecompiler](https://github.com/AssetRipper/DXDecompiler) (fully managed NuGet, cross-platform) — so **Linux can now disassemble SM4/5 DXBC too**
- The GLES3 / GLES2 / OpenGLCore GLSL back-translation path is fully managed and works everywhere

## Usage

```bash
# Simplest: point at the game data directory (contains resources.assets);
# output goes to ./<Game>_ShaderLabs
ShaderRipper.exe --data "GameDir/Game_Data"

# Explicit output directory
ShaderRipper.exe --data "Game_Data" --out D:\out

# Only shaders whose name contains "Floor", with disassembly comments
ShaderRipper.exe --data "Game_Data" --out D:\out --only Floor --asm
```

Output layout:

```
<out>/
├── *.shader                     Rebuilt ShaderLab (CGPROGRAM holds assembly-translated Cg)
└── Disassembly/<shader>/        Per-platform raw disassembly / GLSL / Metal source
```

Note: the CGPROGRAM body is a **translation of the compiled assembly**, not the author's source (source code is not shipped in game data); properties, render state, Pass structure, and variant `#if` chains are faithfully reconstructed.

## CLI options

| Option | Description |
|---|---|
| `--data <dir>` | Game data directory (contains resources.assets), or env `SHADERRIPPER_DATA` |
| `--out <dir>` | Output directory, default `./<Game>_ShaderLabs`, or env `SHADERRIPPER_OUT` |
| `--root <dir>` | Root for schema/data resources (searched upward from the exe by default) |
| `--platform <id\|name>` | Force backend: `d3d11` / `d3d9` / `d3d12` / `gles3` / `gles20` / `openglcore` / `metal` / `vulkan`; defaults to d3d11, otherwise auto-picks a GLSL platform |
| `--asm` | Emit per-instruction disassembly comments (d3d11=DXBC, glsl=original GLSL lines, d3d9/12=native assembly) |
| `--only <substr>` | Only process shaders whose name contains the substring |
| `--bundles [dir]` | Also scan asset bundles (defaults to siblings of --data) |
| `--list-shaders <f>` | List shaders in a SerializedFile |
| `--dump [substr]` | Print blob/parameter block info only, no files written |
| `--schema <f\|dir>` | Type tree to use (auto-selected per file version by default) |
| `--names <dir>` | $Globals fix-up table directory (default `data/globals_names`, loaded only if present; may be omitted from releases) |
| `--tmp-layouts <dir>` | $Globals layout table directory (default `data/tmp_layouts`) |
| `--no-tmp-layout` | Don't use layout tables (TMP row indices stay numeric) |
| `--builtin-index <f>` | Built-in shader name list (default `data/unity_builtin_shaders.txt`) |
| `--matrix-names <f>` | float4x4 name list (default `data/unity_matrix_names.txt`) |
| `--glsl2cg <file>` | Back-translate a single GLSL file to Cg (debug; combine with `--asm` to show original lines) |
| `--disasm <file>` | Disassemble a single program file (DXBC/DXIL/d3d9 tokens) |
| `--selftest-d3d9` | Run the d3d9 disassembler self-test |
| `--lang <en\|zh>` | UI language; follows the system by default |
| `-h, --help` | Help |

## Limitations

- Metal / Vulkan (SMOL-V compressed SPIR-V) are saved as reference files only, no back-translation
- Pure console platforms (PS/Switch) have no readable output
- d3d9 / d3d12 only produce disassembly, not translated Cg
