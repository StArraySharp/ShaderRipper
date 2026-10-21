# ShaderRipper

[English](README.md) | 简体中文

从 Unity 游戏数据中提取 Shader，重建为可读的 **ShaderLab + CGPROGRAM**。纯 C#，运行资源全部内嵌，下载即用。

> 💡 **本项目代码 100% 由 AI 生成**（DeepSeek / GLM 协作完成），人工负责需求、验收与发布。

**支持平台**：d3d11（DXBC 汇编逐条直译 Cg）、GLES3 / GLES2 / OpenGLCore（GLSL 回译 Cg）、d3d9（SM2/3 反汇编）、D3D12（DXIL，经 dxcompiler）；Metal / Vulkan 仅输出参考文件。

## 编译

需要 .NET 10 SDK。

项目分为两层：

- **`ShaderRipper`（`src/ShaderRipper`，程序集 `ShaderRipper.dll`）**——解析/反编译核心库，可供其它工具引用
- **`ShaderRipper.Application`（`src/ShaderRipper.Application`）**——命令行前端，发布时把 apphost 改名为 `ShaderRipper.exe`

```bash
dotnet build ShaderRipper.slnx -c Release

# Windows NativeAOT（约 11 MB 单文件）；把 apphost 改名为 ShaderRipper.exe
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r win-x64 -p:PublishAot=true -o publish/aot-win-x64
mv publish/aot-win-x64/ShaderRipper.Application.exe publish/aot-win-x64/ShaderRipper.exe

# Linux：AOT 不能跨 OS 编译，linux AOT 包需在 Linux 上构建；
# Windows 交叉打包用带 CLR 的裁剪单文件（约 56 MB）
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

发布产物是单个可执行文件，无任何伴生文件：schema 类型树、修复表、TMP 布局、按 RID 的原生库已全部嵌入，首次使用时解压到 `%TEMP%\ShaderRipper\`。

## 渲染器支持

| 后端                         | Windows | Linux | macOS             | 产物                             |
| -------------------------- | ------- | ----- | ----------------- | ------------------------------ |
| d3d11                      | ✅       | ✅     | ⚠️ 未验证            | ShaderLab + CGPROGRAM（汇编直译）    |
| GLES3 / GLES2 / OpenGLCore | ✅       | ✅     | ⚠️ 未验证            | ShaderLab + CGPROGRAM（GLSL 回译） |
| d3d9（SM2/3）                | ✅       | ✅     | ⚠️ 未验证            | 反汇编（Disassembly/）              |
| SM4/5 DXBC                 | ✅       | ✅     | ⚠️ 未验证            | 反汇编（Disassembly/）              |
| D3D12（DXIL）                | ✅       | ✅     | ❌ 无官方 macOS 版 DXC | 反汇编（Disassembly/）              |
| Metal / Vulkan             | ✅       | ✅     | ✅                 | 仅参考文件，不回译                      |

> macOS 未实测：托管路径理论上可用（自行 `dotnet publish -r osx-arm64` 打包），DXIL 反汇编因无官方 macOS 版 DXC 而不可用。

## 使用

```bash
# 最简：指定游戏数据目录（含 resources.assets），输出到 ./<游戏名>_ShaderLabs
ShaderRipper.exe --data "游戏目录/游戏名_Data"

# 指定输出目录
ShaderRipper.exe --data "游戏名_Data" --out D:\out

# 只提取名字含 Floor 的 shader，带反汇编注释
ShaderRipper.exe --data "游戏名_Data" --out D:\out --only Floor --asm

# 单个 / 多个文件：.assets、.bundle、.unity3d、.apk 均可（按内容识别，扩展名可省）
ShaderRipper.exe --file "游戏名_Data/resources.assets" --out D:\out
ShaderRipper.exe --file "a.apk" --out D:\out
ShaderRipper.exe a.assets b.bundle c.apk --out D:\out      # rm/cp 风格，多个文件空格分隔
ShaderRipper.exe --file "assets*.assets" "sharedassets*.assets" --out D:\out   # 通配符

# 输出固定为 AssetRipper 导出树（Assets/Shader、Assets/Resources），可直接复制覆盖 ExportedProject
ShaderRipper.exe --file "a.apk" --out D:\ExportedProject
```

注意：CGPROGRAM 里是**编译后汇编的直译**，不是作者源码（源码不在游戏包内）；属性、状态、Pass 结构、变体 `#if` 链均忠实还原。

### `multi_compile` 行是怎么还原的

包里只存了每个变体**编译时带哪些关键字**，行结构本身没有留下。它由「哪些关键字能同时为真」反推：
任何变体里都不同时出现的关键字，是同一行的各个候选（Unity 只会定义其中之一）；能同时出现的就是不同的行。
以 [`docs/verification/`](docs/verification/) 里那个 60 变体的测试着色器为例，还原出的正是它当初手写的四条：
`__ DIST_SWIRL DIST_WAVE`、`__ GLOW_ON`、`__ MODE_ADD MODE_MUL MODE_OVERLAY MODE_SCREEN`、`__ SHAPE_RING`，
覆盖 60/60，即 5 × 3 × 2 × 2 的完整笛卡尔积。若把这 8 个关键字当成独立开关，会主张 2⁸ = 256 个变体；
在真实游戏数据里那些 25 候选的混合模式着色器上，则会主张 2²⁵ 个。每个变体的守卫都是精确匹配
（`defined(MODE_MUL)`，而不是「是某个 mode」），所以函数体里手写的 `#ifdef` 语义与输入一致。

支持的容器与压缩：

- **SerializedFile**（`.assets`、APK 内 GUID 命名的无扩展名文件）——未压缩
- **UnityFS bundle**（`.bundle` / `.unity3d`）——LZ4 / LZ4HC / **LZMA** / 未压缩（块级混用也支持）；LZHAM 会明确报错（极罕见）
- **APK / ZIP**——DEFLATE / Store（LZMA-in-zip 极罕见，不支持）
- 全部按**内容**识别，扩展名可省

导出时会读取 `globalgamemanagers` 里的 ResourceManager 表（`m_Container`），把 `Resources.Load` 的  
资源还原到 `Assets/Resources/原路径.shader`；bundle 来源的 shader 按 bundle 节点路径还原。表里没有的对象  
（如 Always Included Shaders）落到 `Assets/Shader/`。Windows / Linux 构建格式一致，APK 从 zip 内读取。

输出固定为 AssetRipper 导出树，可整体复制覆盖 ExportedProject：

```
<out>/
├── Assets/Shader/*.shader       无容器信息的 shader（bundle 来源按原容器路径还原，如 Assets/Resources/）
└── Disassembly/<shader>/        反汇编留在 Assets 之外，不污染 Unity 工程
```

## ⚠️ 对 IL2CPP + 仅 D3D 后端打包的游戏不友好

对 **IL2CPP 打包、且只带 d3d9 / d3d11 / d3d12 图形后端** 的游戏（Steam 单机大作多为此类），输出会很受限：

- **d3d9 / d3d12**：只输出反汇编，不回译 CGPROGRAM
- **d3d11**：能重建 Cg，但是汇编直译，可读性远不如作者源码
- 连 GLES3 / OpenGLCore 后端都没打包的游戏，基本只能拿到汇编

想获得更好的可读产物，优先找带 **GLES3 / OpenGLCore** 后端的游戏数据（移动版常有）。

## CLI 参数

| 参数                    | 说明                                                                                                                |
| --------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `--data <dir>`        | 游戏数据目录（含 resources.assets），或环境变量 `SHADERRIPPER_DATA`                                                              |
| `--out <dir>`         | 输出目录，默认 `./<游戏名>_ShaderLabs`，或 `SHADERRIPPER_OUT`                                                                 |
| `--root <dir>`        | schema/data 资源根目录（默认从 exe 位置向上查找）                                                                                 |
| `--platform <id\|名>`  | 强制后端：`d3d11` / `d3d9` / `d3d12` / `gles3` / `gles20` / `openglcore` / `metal` / `vulkan`；默认优先 d3d11，无则自动选 GLSL 平台 |
| `--asm`               | 输出逐指令反汇编注释（d3d11=DXBC、glsl=原 GLSL 行、d3d9/12=原生汇编）                                                                 |
| `--only <子串>`         | 只处理名字含该子串的 shader                                                                                                 |
| `--bundles [dir]`     | 同时扫描 asset bundle（默认扫 --data 同级）                                                                                  |
| `--list-shaders <f>`  | 列出 SerializedFile 里的 shader 清单                                                                                    |
| `--dump [子串]`         | 只打印 blob/参数块信息，不写文件                                                                                               |
| `--schema <f\|dir>`   | 指定类型树（默认按文件版本自动选）                                                                                                 |
| `--names <dir>`       | $Globals 修复表目录（默认 `data/globals_names`，存在才加载；发布包可不带）                                                              |
| `--tmp-layouts <dir>` | $Globals 布局表目录（默认 `data/tmp_layouts`）                                                                             |
| `--no-tmp-layout`     | 不用布局表（TMP 行号保持数字）                                                                                                 |
| `--builtin-index <f>` | 内置 shader 名单（默认 `data/unity_builtin_shaders.txt`）                                                                 |
| `--matrix-names <f>`  | float4x4 名单（默认 `data/unity_matrix_names.txt`）                                                                     |
| `--glsl2cg <file>`    | 单个 GLSL 文件回译 Cg（调试用，配 `--asm` 显示原行）                                                                               |
| `--disasm <file>`     | 反汇编单个程序文件（DXBC/DXIL/d3d9 token）                                                                                   |
| `--lang <en\|zh>`     | UI 语言，默认跟随系统                                                                                                      |
| `-h, --help`          | 帮助                                                                                                                |

## 第三方库

| 库                                                                                                                                                                                               | 用途                        | 许可                      |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------- | ----------------------- |
| [AssetRipper.Tpk](https://github.com/AssetRipper/Tpk) / [AssetRipper.IO.Endian](https://github.com/AssetRipper/IO.Endian) / [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) | Unity 类型树解析 / 序列化文件读取     | LGPL-3.0                |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4)                                                                                                                 | Unity LZ4 压缩 blob 解压      | MIT                     |
| [DXDecompiler-ly](https://github.com/AssetRipper/DXDecompiler)                                                                                                                                  | 纯托管 SM4/5 DXBC 反汇编（跨平台后备） | MIT                     |
| d3dcompiler_47.dll（Microsoft，随 Windows 分发，内嵌）                                                                                                                                                   | Windows 上 SM4/5 DXBC 反汇编  | 随附于 Windows SDK 许可      |
| dxcompiler / dxil（[Microsoft DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler)，内嵌 Linux 版）                                                                             | DXIL（D3D12/SM6）反汇编        | Apache-2.0（含 LLVM 例外条款） |

## 正确性验证

[`docs/verification/`](docs/verification/) 是一次完整往返：这里的两个着色器都是**我们自己写的**，打包进一个 Unity 6 测试工程，再从该工程的构建产物里提取回来——链路两端都是我们自己的东西，下面的数字都能复现。每个子目录里放：我们打包进去的输入（`*_original.shader`）、本工具还原出的输出（`*_ripper.shader`，带逐指令注释；`*_ripper_noasm.shader` 是去掉注释的同一份）、以及所有已编译变体的 DXBC 反汇编合集。

| 着色器 | 属性 | 变体 | fragment 指令 |
|---|---|---|---|
| `ShaderRipperTest/ExtremeFlow` | 37 | 1 | 1286 |
| `ShaderRipperTest/VariantMonster` | 20 | 60 | 合计 82,550，单变体 763–1875 |

每个变体都用 `d3dcompiler_47` 以 `ps_5_0` 重新编译验证：1/1 与 60/60 全部通过，0 条未翻译指令，0 处 `TODO`；控制流原样保留（ExtremeFlow：3 个循环、5 个出口、2 处 `discard`，`ddx`/`ddy` 都在）。60 变体那份只放了去注释版（带注释的单文件就有 6.6 MB），加 `--asm` 跑一次即可生成。

编译器对源码做了什么、直译又还回什么——以 ExtremeFlow 的 if/else 链为例。

输入：

```hlsl
if (energy < 0.5)      accum *= 1.05;
else if (energy < 1.0) accum += float3(0.02, 0.01, 0.03);
else if (energy < 1.8) accum = lerp(accum, accum * _ColorC.rgb, 0.25);
else                   accum = saturate(accum) * (0.9 + 0.1 * sin(fl));

if (accum.g > 1.4) break;              // 提前跳出 [loop]
```

输出：

```csharp
r3.xyw = float3(r4.x * 1.05, r4.y * 1.05, r4.z * 1.05);                       // 第 1 臂
r6.xyz = float3((r3.z < 0.5) ? 1.0 : 0.0, (r3.z < 1.0) ? 1.0 : 0.0,
                (r3.z < 1.8) ? 1.0 : 0.0);                                    // 三个判据还在
r7.xyz = float3(mad(r4.x, _ColorC.x, -(r4.x)), ...);                          // lerp：accum*color - accum
r7.xyz = float3(mad(r7.x, 0.25, r4.x), ...);                                  //   ... * 0.25 + accum
r3.xyw = float3(r6.x != 0.0 ? r3.x : r5.x, ...);                              // 选择树
r2.w = (1.4 < r3.y) ? 1.0 : 0.0;
if (any(r2.w)) { r2.xyz = float3(r3.x, r3.y, r3.w); break; }                  // 提前退出被重建
```

编译器把分支链压成了谓词选择树，并把 `lerp` 改写成两条 `mad`。逐指令直译原样保留这个形态，而不是去猜
原来的 `if/else`——它们本来就是同一个程序。这也解释了为什么输出比输入指令多（ExtremeFlow 1286 vs 1097）：
编译器做过的算子融合拿不回来，硬猜就是编造。

## 限制


- Metal / Vulkan（SMOL-V 压缩 SPIR-V）只存参考文件，不回译
- 纯 console 平台（PS/Switch）无可读产物
- d3d9 / d3d12 只出反汇编，不回译 Cg
