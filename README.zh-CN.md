# ShaderRipper

[English](README.md) | 简体中文

从 Unity 游戏数据中提取 Shader，重建为可读的 **ShaderLab + CGPROGRAM**。纯 C#，运行资源全部内嵌，下载即用。

> 💡 **本项目代码 100% 由 AI 生成**（Claude / GLM 协作完成），人工负责需求、验收与发布。

**支持平台**：d3d11（DXBC 汇编逐条直译 Cg）、GLES3 / GLES2 / OpenGLCore（GLSL 回译 Cg）、d3d9（SM2/3 自研反汇编）、D3D12（DXIL，经 dxcompiler）；Metal / Vulkan 仅输出参考文件。

## 编译

需要 .NET 10 SDK。Windows 上 NativeAOT 链接需要 MSVC + Windows SDK 库路径。

```bash
dotnet build ShaderRipper.slnx -c Release

# Windows NativeAOT（约 11 MB 单文件）
dotnet publish src/ShaderRipper/ShaderRipper.csproj -c Release -r win-x64 -p:PublishAot=true -o publish/aot-win-x64

# Linux：AOT 不能跨 OS 编译，linux AOT 包需在 Linux 上构建；
# Windows 交叉打包用带 CLR 的裁剪单文件（约 56 MB）
dotnet publish src/ShaderRipper/ShaderRipper.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

发布产物是单个可执行文件，无任何伴生文件：schema 类型树、修复表、TMP 布局、按 RID 的原生库已全部嵌入，首次使用时解压到 `%TEMP%\ShaderRipper\`。

## 渲染器支持

| 后端 | Windows | Linux | macOS | 产物 |
|---|---|---|---|---|
| d3d11 | ✅ | ✅ | ⚠️ 未验证 | ShaderLab + CGPROGRAM（汇编直译） |
| GLES3 / GLES2 / OpenGLCore | ✅ | ✅ | ⚠️ 未验证 | ShaderLab + CGPROGRAM（GLSL 回译） |
| d3d9（SM2/3） | ✅ | ✅ | ⚠️ 未验证 | 反汇编（Disassembly/） |
| SM4/5 DXBC | ✅ | ✅ | ⚠️ 未验证 | 反汇编（Disassembly/） |
| D3D12（DXIL） | ✅ | ✅ | ❌ 无官方 macOS 版 DXC | 反汇编（Disassembly/） |
| Metal / Vulkan | ✅ | ✅ | ✅ | 仅参考文件，不回译 |

> macOS 未实测：托管路径理论上可用（自行 `dotnet publish -r osx-arm64` 打包），DXIL 反汇编因无官方 macOS 版 DXC 而不可用。

## 使用

```bash
# 最简：指定游戏数据目录（含 resources.assets），输出到 ./<游戏名>_ShaderLabs
ShaderRipper.exe --data "游戏目录/游戏名_Data"

# 指定输出目录
ShaderRipper.exe --data "游戏名_Data" --out D:\out

# 只提取名字含 Floor 的 shader，带反汇编注释
ShaderRipper.exe --data "游戏名_Data" --out D:\out --only Floor --asm
```

输出结构：

```
<out>/
├── *.shader                     重建的 ShaderLab（CGPROGRAM 内为汇编直译的 Cg）
└── Disassembly/<shader>/        各平台原始反汇编 / GLSL / Metal 源码
```

注意：CGPROGRAM 里是**编译后汇编的直译**，不是作者源码（源码不在游戏包内）；属性、状态、Pass 结构、变体 `#if` 链均忠实还原。

## ⚠️ 对 IL2CPP + 仅 D3D 后端打包的游戏不友好

对 **IL2CPP 打包、且只带 d3d9 / d3d11 / d3d12 图形后端** 的游戏（Steam 单机大作多为此类），输出会很受限：

- **d3d9 / d3d12**：只输出反汇编，不回译 CGPROGRAM
- **d3d11**：能重建 Cg，但是汇编直译，可读性远不如作者源码
- 连 GLES3 / OpenGLCore 后端都没打包的游戏，基本只能拿到汇编

想获得更好的可读产物，优先找带 **GLES3 / OpenGLCore** 后端的游戏数据（移动版常有）。

## CLI 参数

| 参数 | 说明 |
|---|---|
| `--data <dir>` | 游戏数据目录（含 resources.assets），或环境变量 `SHADERRIPPER_DATA` |
| `--out <dir>` | 输出目录，默认 `./<游戏名>_ShaderLabs`，或 `SHADERRIPPER_OUT` |
| `--root <dir>` | schema/data 资源根目录（默认从 exe 位置向上查找） |
| `--platform <id\|名>` | 强制后端：`d3d11` / `d3d9` / `d3d12` / `gles3` / `gles20` / `openglcore` / `metal` / `vulkan`；默认优先 d3d11，无则自动选 GLSL 平台 |
| `--asm` | 输出逐指令反汇编注释（d3d11=DXBC、glsl=原 GLSL 行、d3d9/12=原生汇编） |
| `--only <子串>` | 只处理名字含该子串的 shader |
| `--bundles [dir]` | 同时扫描 asset bundle（默认扫 --data 同级） |
| `--list-shaders <f>` | 列出 SerializedFile 里的 shader 清单 |
| `--dump [子串]` | 只打印 blob/参数块信息，不写文件 |
| `--schema <f\|dir>` | 指定类型树（默认按文件版本自动选） |
| `--names <dir>` | $Globals 修复表目录（默认 `data/globals_names`，存在才加载；发布包可不带） |
| `--tmp-layouts <dir>` | $Globals 布局表目录（默认 `data/tmp_layouts`） |
| `--no-tmp-layout` | 不用布局表（TMP 行号保持数字） |
| `--builtin-index <f>` | 内置 shader 名单（默认 `data/unity_builtin_shaders.txt`） |
| `--matrix-names <f>` | float4x4 名单（默认 `data/unity_matrix_names.txt`） |
| `--glsl2cg <file>` | 单个 GLSL 文件回译 Cg（调试用，配 `--asm` 显示原行） |
| `--disasm <file>` | 反汇编单个程序文件（DXBC/DXIL/d3d9 token） |
| `--lang <en\|zh>` | UI 语言，默认跟随系统 |
| `-h, --help` | 帮助 |

## 第三方库

| 库 | 用途 | 许可 |
|---|---|---|
| [AssetRipper.Tpk](https://github.com/AssetRipper/Tpk) / [AssetRipper.IO.Endian](https://github.com/AssetRipper/IO.Endian) / [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) | Unity 类型树解析 / 序列化文件读取 | LGPL-3.0 |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) | Unity LZ4 压缩 blob 解压 | MIT |
| [DXDecompiler-ly](https://github.com/AssetRipper/DXDecompiler) | 纯托管 SM4/5 DXBC 反汇编（跨平台后备） | MIT |
| d3dcompiler_47.dll（Microsoft，随 Windows 分发，内嵌） | Windows 上 SM4/5 DXBC 反汇编 | 随附于 Windows SDK 许可 |
| dxcompiler / dxil（[Microsoft DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler)，内嵌 Linux 版） | DXIL（D3D12/SM6）反汇编 | Apache-2.0（含 LLVM 例外条款） |

## 计划

- **支持 APK**：直接扫描 Android 安装包（解包 APK 后走现有的 bundles 扫描路径），GLES3 / GLES2 后端的主阵地就在这里——这也是对 IL2CPP + 仅 D3D 游戏可读性问题的正解

## 限制

- Metal / Vulkan（SMOL-V 压缩 SPIR-V）只存参考文件，不回译
- 纯 console 平台（PS/Switch）无可读产物
- d3d9 / d3d12 只出反汇编，不回译 Cg
