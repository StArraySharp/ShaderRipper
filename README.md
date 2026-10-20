# ShaderRipper

从 Unity 游戏数据中提取 Shader，重建为可读的 **ShaderLab + CGPROGRAM**。纯 C#，无外部依赖。

**支持平台**：d3d11（DXBC 汇编逐条直译 Cg）、GLES3 / GLES2 / OpenGLCore（GLSL 源码回译 Cg）、d3d9（SM2/3 自研反汇编）、D3D12（DXIL，经 dxcompiler COM 反汇编）；Metal / Vulkan 仅输出参考文件。

## 编译

需要 .NET 10 SDK。Windows 上反汇编用到 d3dcompiler/dxcompiler（AOT 链接需要 MSVC + Windows SDK 库路径）。

```bash
dotnet build ShaderRipper.slnx -c Release

# Windows NativeAOT（约 5 MB 单文件）
dotnet publish src/ShaderRipper/ShaderRipper.csproj -c Release -r win-x64 -p:PublishAot=true -o publish/aot-win-x64

# Linux：AOT 不能跨 OS 编译，linux 包需在 Linux 上构建；
# Windows 交叉打包用 self-contained（约 16 MB）
dotnet publish src/ShaderRipper/ShaderRipper.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

Linux / macOS：
- **d3d9（SM2/3）反汇编是纯托管实现，全平台开箱即用**，无需任何原生库
- **DXIL（D3D12/SM6）反汇编**走 `libdxcompiler.so`（微软 [DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler) 官方 Linux 发行版），放到 `/usr/lib` 或 exe 同目录即可；`NativeLibs/linux-x64/` 已随仓库携带 v1.8.2407（glibc ≤2.34，兼容 Ubuntu 22.04）
- **SM4/5 DXBC 反汇编仅 Windows 可用**：只有 d3dcompiler_47 认这个格式（dxc 拒收非 DXIL 容器，且无任何 Linux 原生替代）；d3dcompiler 已内嵌进主程序集，仅 Windows 加载时解压
- GLES3 / GLES2 / OpenGLCore 的 GLSL 回译路径纯托管，全平台可用

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
| `--selftest-d3d9` | d3d9 反汇编器自测 |
| `--lang <en\|zh>` | UI 语言，默认跟随系统 |
| `-h, --help` | 帮助 |

## 限制

- Metal / Vulkan（SMOL-V 压缩 SPIR-V）只存参考文件，不回译
- 纯 console 平台（PS/Switch）无可读产物
- d3d9 / d3d12 只出反汇编，不回译 Cg
