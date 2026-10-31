# ShaderRipper

[English](README.md) | 简体中文

从 Unity 游戏数据中提取 Shader，重建为可读的 **ShaderLab + HLSLPROGRAM**。纯 C#，运行资源全部内嵌，下载即用。

> 💡 **本项目代码 100% 由 AI 生成**（DeepSeek / GLM 协作完成），人工负责需求、验收与发布。

**支持的载荷**：dxbc —— d3d11、Xbox / GameCore 槽位（DXBC 汇编逐条直译 HLSL），以及 dxbc 容器里的 DXIL chunk（经 dxcompiler 反汇编，回译未验证）；glsl —— GLES3 / GLES2 / OpenGLCore / OpenGL（GLSL 回译 HLSL）；d3d9 —— SM2/SM3（仅反汇编）。其余后端（Metal、Vulkan、主机平台、WebGPU）没有翻译器，会被跳过。

## 编译

需要 .NET 10 SDK。

项目分为两层：

- **`ShaderRipper`（`src/ShaderRipper`，程序集 `ShaderRipper.dll`）**——解析/反编译核心库，可供其它工具引用
- **`ShaderRipper.Application`（`src/ShaderRipper.Application`）**——命令行前端

```bash
dotnet build ShaderRipper.slnx -c Release
```

### 发布打包

```bash
pwsh -File tools/make-release.ps1                      # win-x64 + linux-x64
pwsh -File tools/make-release.ps1 -Rid win-x64         # 只打一个平台
pwsh -File tools/make-release.ps1 -Rid win-x64 -Target gui
```

每个平台发布一份**自包含、多文件**的产物，打成
`publish/<rid>/ShaderRipper-<target>-<version>-<rid>.zip`。

**不裁剪、不单文件**：两者都会让插件失效。裁剪后的自包含发布会把运行时程序集折进原生代码，
插件引用 `System.Runtime` 时没有文件可解析；单文件发布则把同样的程序集藏进 exe，
`Assembly.LoadFrom` 什么也找不到。**多文件自包含是插件唯一能工作的形态**，也是用户能自行
排查的形态。

schema 类型树、修复表、TMP 布局、按 RID 的原生库已全部嵌入库内，首次使用时解压到
`%TEMP%\ShaderRipper\`，因此产物没有任何伴生数据文件。
`src/ShaderRipper/Grammar/` 下的 ANTLR 词法/语法分析器是提前生成并入库的，因此构建不需要 JVM。改过 `.g4` 之后用 `py -3 scripts/gen_grammar.py` 重新生成（需要 java 和 `antlr-complete.jar`，用 `--antlr <路径>` 指定位置）。

## 测试

`src/ShaderRipper.Tests` 是一层 NUnit 单测，覆盖既不需要 Unity 也不需要游戏数据的纯逻辑
——平台表、载荷格式识别、UnityFS 读取、变体规划、ShaderLab 合法性检查、矩阵链折叠、
d3d9 token 反汇编、插件注册表。一秒内跑完：

```bash
dotnet test src/ShaderRipper.Tests/ShaderRipper.Tests.csproj -c Release
```

`scripts/` 下的 harness 负责单测够不到的部分：它们需要真实游戏数据或 Unity 自己的编译器。
见[正确性验证](#正确性验证)。

## 插件

管线有三处可扩展：**容器**（本工具读不了的封装格式）、**后端**（翻不了译的载荷格式）
和**发射后处理**（对生成出的 HLSL 做改写）。插件就是一个类库，实现
`IShaderRipperPlugin` 并注册它需要的那几项。

```bash
# 看加载了什么，以及它们被询问的先后顺序
ShaderRipper.exe --plugins

# 指定 <exe>/plugins 之外的目录
ShaderRipper.exe --game "游戏名_Data" --shaderlab out --plugin-dir ./my-plugins

# 完全忽略插件——"与上个版本行为一致"的可复现写法
ShaderRipper.exe --game "游戏名_Data" --shaderlab out --no-plugins
```

| 能力 | 接口 | 调用时机 |
|---|---|---|
| 认领输入字节 | `IContainerProvider` | 每个候选文件一次 |
| 枚举某个后端的程序 | `ISlotReader` | 每个 shader × 槽位 |
| 翻译单个程序 | `IProgramTranslator` | 每个程序一次 |
| 改写生成的 HLSL | `IEmitterPass` | 每个程序体一次 |

每项能力都带优先级：内置为 `0`，插件默认 `100`。查询时数字小的先被执行；同一平台 id
后来注册者胜出——否则一个永远轮不到被询问的条目等于不可用。

插件抛异常会被跳过、导出继续；每种不同的失败只报告一次，避免一个在 400 个 shader 上
都失败的 pass 把真正的问题淹掉。`SHADERRIP_PLUGINDBG=1` 会打印实际生效顺序，并确认某个
pass 真的被调用过。

**[插件 API 参考](docs/api.md)**——每个接口、它拿到什么参数、什么时候被调用，全部由源码
自带的文档注释生成，因此不会与代码脱节：

```bash
py -3 scripts/gen_api_docs.py     # 读 bin/Release/net10.0/ShaderRipper.xml，写出 docs/api.md
```

> 进程内插件依赖 `Assembly.LoadFrom`，所以 **NativeAOT** 发布无法加载：`--plugins` 会
> 说明这一点并只使用内置后端。自包含发布之所以不裁剪、保持多文件，也是同一个原因——
> 见[发布打包](#发布打包)。
>
> [`examples/ShaderRipper.Plugin.Example`](examples/ShaderRipper.Plugin.Example/ExamplePlugin.cs)
> 是三个扩展点的完整示例。它不在解决方案内，按路径单独构建。

## 渲染器支持

产物按游戏**实际打包的后端载荷**逐平台处理，只有两种载荷有翻译器，另有一种只有反汇编器：

| 载荷（`m_Platforms` 里的后端）                          | Windows | Linux | macOS    | 产物                              |
| ----------------------------------------------- | ------- | ----- | -------- | ------------------------------- |
| dxbc：d3d11、xboxone、xboxone-d3d12、gamecore*     | ✅       | ✅     | ⚠️ 未验证   | ShaderLab + HLSLPROGRAM（汇编直译）   |
| dxbc 容器里的 DXIL chunk（D3D12 / SM6）               | ✅       | ✅     | ❌ 无 macOS DXC | 反汇编；回译未验证                       |
| glsl：gles20 / gles3 / openglcore / opengl      | ✅       | ✅     | ⚠️ 未验证   | ShaderLab + HLSLPROGRAM（GLSL 回译） |
| d3d9：SM2/SM3 token 流                             | ✅       | ✅     | ✅        | 仅反汇编（`.orig.asm`），无翻译器            |
| metal / vulkan / 主机平台 / webgpu                 | —       | —     | —        | 跳过（没有对应翻译器）                     |

> macOS 未实测：托管路径理论上可用（自行 `dotnet publish -r osx-arm64` 打包），DXIL 反汇编因无官方 macOS 版 DXC 而不可用。
> 一个 shader 通常带多个后端槽位，工具会逐个处理；只带 metal / vulkan 的游戏不会有任何产出。
>
> `--platform <名字|id>` 只读一个槽位。同时带 d3d11 与 gles3 的构建永远优先 d3d11，所以这是单独看 GLSL 路线产物的唯一方式。指定一个**完全不会有产出**的后端（vulkan、metal）是错误——`--only` 曾踩过"空导出却 exit 0"的坑；`d3d9` 会被接受，因为它的程序仍会作为反汇编落盘。
>
> d3d9 **刻意不做回译**：SM2/SM3 与 DXBC 是不同方言（`texld` / `mova`、绝对寄存器号，而不是 `dcl_` 前缀加相对 `r#` 编号），DXBC 发射器读不了它，为它再写一套翻译器不划算。该 Pass 会保留渲染状态，并附带一条说明：程序已读出但未翻译——这才是构建数据实际能支撑的结论。

## 使用

```bash
# 最简：指向游戏根目录（或它的 <游戏名>_Data），输出到 <输入>_Ripped
ShaderRipper.exe --game "…/steamapps/common/某游戏"

# 指定输出目录，并把每个 shader 写成 .shader（交付物）
ShaderRipper.exe --game "…/某游戏" --shaderlab out

# 只导出名字含 Bloom 的 shader，同时把每个程序的原始反汇编也落盘
ShaderRipper.exe --game "游戏名_Data" --out out --only Bloom --orig-asm

# 出图线程数：默认逻辑核数，--jobs 1 为串行（日志顺序可复现）
ShaderRipper.exe --game "游戏名_Data" --shaderlab out --jobs 4

# 只读一个后端：单独看 GLES 路线的产物（d3d11 槽不参与）
ShaderRipper.exe --game "游戏名_Data" --shaderlab out --platform gles3

# 单个 / 多个文件：.assets、.bundle、.unity3d、.apk 均可（按内容识别，扩展名可省）
ShaderRipper.exe --file "游戏名_Data/resources.assets" --out out
ShaderRipper.exe --file "a.apk" --out out
ShaderRipper.exe --file "assets*.assets" "sharedassets*.assets" --out out   # 通配符

# 先看包里有什么，不写文件
ShaderRipper.exe --list-shaders "a.bundle"
ShaderRipper.exe --dump "a.bundle" --dump-filter Bloom

# 调单条链路：一份反汇编 / 一份 GLSL → HLSL
ShaderRipper.exe --asm "1_vs.orig.asm" --out one.hlsl
ShaderRipper.exe --glsl "1_glsl.orig.glsl" --shaderlab > one.shader
ShaderRipper.exe --emit <含 asm/glsl 的目录> <输出目录>
ShaderRipper.exe --stats <含 asm 的目录>
```

`--game` 传游戏根目录即可：工具会自己下探一层找 `<游戏名>_Data/globalgamemanagers`（
`Resources.Load` 路径全靠它还原），不用手动指到 `_Data`。Git Bash 那种 `/e/SteamLibrary/...`
写法在未被 shell 转换时会按盘符重试，不会被当成不存在的路径。

注意：HLSLPROGRAM 里是**编译后汇编的直译**，不是作者源码（源码不在游戏包内）；属性、状态、Pass 结构、变体 `#if` 链均忠实还原。

### 程序块语法

产物固定是 `HLSLPROGRAM`：纹理声明成 `Texture2D` / `Texture3D` / `TextureCube` / `Texture2DArray`，配一个同名的 `SamplerState`，采样走 `.Sample()` / `.SampleLevel()`。旧前端的 `--syntax cg`（`CGPROGRAM` + `sampler2D` + `tex2D`）没有搬过来，现在只有这一种写法。

`HLSLPROGRAM` **不会**自动 include `HLSLSupport.cginc`，所以产物头部显式包含它——缺了它 `fixed` / `half` 会报 `unrecognized identifier`（X3004）。采样器命名为 `sampler<纹理名>`，这是 Unity 自己的绑定约定，材质纹理的导入设置（wrap/filter）因此仍然生效。

产物文件名取自 shader 名，只把层级分隔符换成下划线（`UI/Pause Planets` → `UI_Pause Planets.shader`，空格、括号、大小写都不动）。资源路径已被别的 shader 占用时，改用 shader 名做文件名，而不是追加看不懂的 path id。导出按 shader 并行处理（默认线程数 = 逻辑核数，`--jobs N` 可调，`--jobs 1` 为串行），且所有名字/路径归属都在开工前单线程定好，因此并发与串行、以及重复运行，得到的目录树逐字节相同。

输出路径按三步决定，顺序固定：

1. **有固定路径就用路径。** 资产自己的反射记着它原本在哪——`globalgamemanagers` 里的
   `Resources` 容器条目，或包内 `AssetBundle.m_Container` 的条目——文件就落在那里
   （`Assets/Resources/camerafilterpack_*.shader`）。
2. **否则用 shader 自己的名字**，`/` 换成 `_`
   （`Assets/Shader/CameraFilterPackLegacy_Color_GrayScale.shader`）。
3. **还是重复才加 `_1`、`_2`……** 实测从不触发：shader 名在工程内唯一，而「路径撞车」
   永远是 `CameraFilterPack` / `CameraFilterPackLegacy` 这一对，两者名字本来就不同。

固定路径优先占位，所以构建时给出显式路径的 shader 一定保留原路径，不会因为另一个只有名字
的 shader 抢先而被迫改名。`--no-dedup` 保留每一个副本；默认只留一份，且**有固定路径的那份
胜出**。

### `multi_compile` 行是怎么还原的

包里只存了每个变体**编译时带哪些关键字**，行结构本身没有留下。它由「哪些关键字能同时为真」反推：  
任何变体里都不同时出现的关键字，是同一行的各个候选（Unity 只会定义其中之一）；能同时出现的就是不同的行。  
以 [`docs/verification/`](docs/verification/) 里那个 60 变体的测试着色器为例，还原出的正是它当初手写的四条：  
`__ DIST_SWIRL DIST_WAVE`、`__ GLOW_ON`、`__ MODE_ADD MODE_MUL MODE_OVERLAY MODE_SCREEN`、`__ SHAPE_RING`，  
覆盖 60/60，即 5 × 3 × 2 × 2 的完整笛卡尔积。若把这 8 个关键字当成独立开关，会主张 2⁸ = 256 个变体。  
每个变体的守卫都是精确匹配  
（`defined(MODE_MUL)`，而不是「是某个 mode」），所以函数体里手写的 `#ifdef` 语义与输入一致。

支持的容器与压缩：

- **SerializedFile**（`.assets`、APK 内 GUID 命名的无扩展名文件）——未压缩
- **UnityFS bundle**（`.bundle` / `.unity3d`）——LZ4 / LZ4HC / **LZMA** / 未压缩（块级混用也支持）；LZHAM 会明确报错（极罕见）
- **APK / ZIP**——DEFLATE / Store（LZMA-in-zip 极罕见，不支持）
- 全部按**内容**识别，扩展名可省

导出时会读取 `globalgamemanagers` 里的 ResourceManager 表（`m_Container`），把 `Resources.Load` 的  
资源还原到 `Assets/Resources/原路径.shader`。包（bundle）内部也有一份同类的按对象清单
（`AssetBundle.m_Container`），同样会读，所以打进包里的 shader 也能回到它原本的工程路径。
两张表都没有命中的对象（如 Always Included Shaders）落到 `Assets/Shader/`。
Windows / Linux 构建格式一致，APK 从 zip 内读取。

输出固定为 AssetRipper 导出树，可整体复制覆盖 ExportedProject：

```
<out>/
└── Assets/
    ├── Resources/…/X.shader   Resources.Load 路径（由 globalgamemanagers 的 m_Container 还原）
    ├── Shader/X.shader        表里没有的 shader（Unity 自带、Always Included 等）
    └── …/X/                   仅 --programs 或 --orig-asm：每个程序的 HLSL / 原始反汇编，就在 X.shader 旁边
```

## ⚠️ 对 IL2CPP + 仅 D3D 后端打包的游戏不友好

对 **IL2CPP 打包、且只带 d3d9 / d3d11 / d3d12 图形后端** 的游戏（Steam 单机大作多为此类），输出会很受限：

- **d3d11**：能重建 HLSL，但是汇编直译，可读性远不如作者源码
- **d3d12（DXIL）**：只保证反汇编；DXIL 回译未验证
- **d3d9**：当前没有翻译器，shader 会被跳过（旧前端那条 d3d9 token 反汇编链路没有搬过来）
- 连 GLES3 / OpenGLCore 后端都没打包的游戏，基本只能拿到汇编

想获得更好的可读产物，优先找带 **GLES3 / OpenGLCore** 后端的游戏数据（移动版常有）：那条路线回译的就是 Unity 当初生成的 GLSL，可读性接近源码。

## CLI 参数

| 参数 | 说明 |
| --- | --- |
| `--game <游戏根\|<游戏名>_Data> [输出目录]` | 整目录导出。两层都会找 `globalgamemanagers`（游戏根本身没有，它在 `<游戏名>_Data/` 里） |
| `--file <路径[,;路径\|通配符]>... [输出目录]` | 按显式文件清单导出，可多个、可用通配符 |
| `--out <目录>` | 输出目录；缺省 `<输入>_Ripped`（`--file` 时是首个输入所在目录下的 `_Ripped`） |
| `--shaderlab` | 写出 `<名字>.shader`（交付物）；不加就只统计、不落 `.shader` |
| `--programs` | 额外把每个程序的 HLSL 写进 `<名字>/` 子目录 |
| `--orig-asm` | 额外把原始反汇编（`.orig.asm`）或原 GLSL（`.orig.glsl`）写进 `<名字>/` 子目录；与 `--programs` 互不隐含 |
| `--jobs <N>` | 并发线程数，默认逻辑核数；`--jobs 1` 为串行 |
| `--only <文本>` | 只处理名字含该文本的 shader |
| `--platform <名\|id>` | 只读这一个后端的槽（d3d11 / gles3 / gles20 / openglcore / opengl / d3d9 …）；同时有 d3d11 和 gles3 的构建永远先取 d3d11，只有指定它才能单独看 GLSL 路线的产物。指定完全没有产出的后端（vulkan / metal）会直接报错，而不是导出 0 个；d3d9 可指定，其程序会作为反汇编落盘 |
| `--no-dedup` | 保留每个副本（默认同一路径只留一份：优先有固定容器路径的，并列取 path id 最小者） |
| `--builtin` | 连 Unity 自带 shader 一起导出（默认跳过，只统计跳过了多少） |
| `--overwrite`（别名 `--clean`） | 写前清空输出目录，只清本工具自己产出的内容 |
| `--self-contained` | 引擎声明直接内联，不用 `#include`（可独立编译，但 Unity 导入时会拒绝） |
| `--strip-dxbc-registers` | 移除 DXBC 来源 ShaderLab 的 `: register(bN/tN/sN)` 绑定，让 Unity 自己分配槽位（保留 `packoffset`；GLSL 通道与独立程序不受影响） |
| `--quiet` / `--verbose` | 只输出最后的总账 / 额外输出每个文件的标题、结构与耗时 |
| `--lang <en\|zh>` | 界面语言，默认跟随系统 UI 语言 |
| `--tmp-layouts <文件\|目录>` / `--no-tmp-layout` / `--builtin-index <文件>` | 名称表覆盖项（默认用内嵌的那份） |
| `--plugins` | 列出已加载的容器、后端与发射后处理，按被询问的顺序 |
| `--plugin-dir <目录>` | 插件目录（默认 `<exe>/plugins`） |
| `--no-plugins` | 不加载插件，只用内置后端 |
| `--list-shaders <文件>` | 列出 SerializedFile / bundle 里的 shader（名字、pathId、容器路径），不写文件 |
| `--dump <文件> [--dump-filter <文本>]` | 打印每个 shader 的结构：平台槽位、pass/子程序数、属性、非默认渲染状态 |
| `--asm <文件> [--out <文件>]` | 一份反汇编 → HLSL（默认打到 stdout） |
| `--glsl <文件> [--shaderlab]` | 一份 GLSL 载荷 → HLSL（配 `--shaderlab` 再套一层 ShaderLab 并自检） |
| `--emit <目录> <输出目录>` | 把目录下所有 `*.asm` / `*.glsl` 逐份翻成 HLSL，镜像目录树 |
| `--stats <目录>` | 语料统计：可解析文件数、逐指令的翻译覆盖率、缺 handler 的指令 TOP15 |
| `-h, --help` | 帮助（与 `--lang` 同步） |

每个 `.shader` 落盘前都会用 ShaderLab 文法复核一遍，不合格的不写文件、只记一条提示；导出带失败项时进程退出码为 1，用法错误为 2。

选项可以写在任意位置：`--shaderlab out --game <目录>` 与 `--game <目录> --shaderlab out` 是同一次运行。命令词（`--game`、`--file`、`--asm`……）只能给一个，且输入由它自己的选项携带，不需要额外位置参数。

旧前端的参数（`--data`、`--root`、`--platform`、`--syntax`、`--dedup`、`--bundles`、`--names`、`--schema`、`--matrix-names`、`--asm-parser`、`--glsl-parser`、`--glsl2cg`、`--disasm`、`--validate`、`--scan-all`，以及当作开关用的 `--asm`）已随前端重写移除，命令行里出现会被忽略——目录扫描本来就按内容识别所有容器，不需要再打开什么。

调试用环境变量：`SHADERRIP_NOTES=all` 打印全部诊断（默认 8 条）、`SHADERRIP_STACK=1` 打印异常栈、`SHADERRIP_FORCE_MANAGED=1` 跳过 d3dcompiler 走纯托管反汇编、`SHADERRIP_TPK=<文件>` 指定类型树，`SHADERRIP_CBDBG` / `SHADERRIP_TMPDBG` / `SHADERRIP_TYPEDBG` / `SHADERRIP_BUNDLEDBG` 输出对应环节的细节追踪。

## 第三方库

| 库                                                                                                                                                                                               | 用途                        | 许可                      |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------- | ----------------------- |
| [AssetRipper.Tpk](https://github.com/AssetRipper/Tpk) / [AssetRipper.IO.Endian](https://github.com/AssetRipper/IO.Endian) / [AssetRipper.Primitives](https://github.com/AssetRipper/Primitives) | Unity 类型树解析 / 序列化文件读取     | LGPL-3.0                |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4)                                                                                                                 | Unity LZ4 压缩 blob 解压      | MIT                     |
| [DXDecompiler-ly](https://github.com/AssetRipper/DXDecompiler)                                                                                                                                  | 纯托管 SM4/5 DXBC 反汇编（跨平台后备） | MIT                     |
| d3dcompiler_47.dll（Microsoft，随 Windows 分发，内嵌）                                                                                                                                                   | Windows 上 SM4/5 DXBC 反汇编  | 随附于 Windows SDK 许可      |
| dxcompiler / dxil（[Microsoft DirectXShaderCompiler](https://github.com/microsoft/DirectXShaderCompiler)，内嵌 Linux 版）                                                                             | DXIL（D3D12/SM6）反汇编        | Apache-2.0（含 LLVM 例外条款） |

## 正确性验证

三项检查，全部离线，都在 `scripts/` 下：

| Harness | 它回答什么 |
|---|---|
| `verify_logic.py` | 生成出的 HLSL 编译回 DXBC 后，算的是不是同一件事 |
| `verify_glsl.py` | 同上，针对 GLSL 路线，参照物是它自己的载荷 |
| `usc_check.py` | Unity 自己的编译器接受每一个写出的 `.shader` 吗 |

最近一次发布所用语料的实测结果：

```
verify_logic  428/428 = 100%     scan_unwritten  0 处未写入的 lane
verify_glsl    34/34  = 100%     scan_nan        0 处逃逸的 NaN
usc_check       89/89            NUnit 单测      144/144
```

过一遍真实 Unity 6 测试工程的往返记录——原始源码、反解回来的产物、每个变体的合并反汇编
——在 [`docs/verification/`](docs/verification/) 下。每项检查具体怎么做、什么算真差异而不是
编译器的合法改写、以及它守护的可读性 pass，见
[`docs/verification.zh-CN.md`](docs/verification.zh-CN.md)。

## 限制

- Metal / Vulkan / WebGPU 载荷没有翻译器：这些槽位直接跳过，不做猜测
- d3d9 有反汇编器但没有翻译器：写出 `.orig.asm`，不生成 Pass 主体
- 纯 console 平台（PS/Switch）无可读产物
- d3d12 在数据里表现为 dxbc 槽位中的一个 DXIL chunk：反汇编可用，HLSL 回译未验证
- HLSL 是逐指令直译：寄存器复现和编译器的运算合并都原样保留，等价但啰嗦，不是地道的作者源码
