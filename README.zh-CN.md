# ShaderRipper

[English](README.md) | 简体中文

从 Unity 游戏数据中提取 Shader，重建为可读的 **ShaderLab + HLSLPROGRAM**。纯 C#，运行资源全部内嵌，下载即用。

> 💡 **本项目代码 100% 由 AI 生成**（DeepSeek / GLM 协作完成），人工负责需求、验收与发布。

**支持的载荷**：dxbc —— d3d11、Xbox / GameCore 槽位（DXBC 汇编逐条直译 HLSL），以及 dxbc 容器里的 DXIL chunk（经 dxcompiler 反汇编，回译未验证）；glsl —— GLES3 / GLES2 / OpenGLCore / OpenGL（GLSL 回译 HLSL）。其余后端（d3d9、Metal、Vulkan、主机平台、WebGPU）没有翻译器，会被跳过。

## 1.2.1 更新

- 同一个编译程序被多个 Pass 引用时，为所有引用它的 Pass 保留程序。
- 支持 `v[r2.x + 2]` 等动态索引输入，避免指令解析不完整及索引越界。
- 嵌入 API 支持传递常量缓冲与纹理反射，TMP 内嵌布局按需加载。
- 生成的 ShaderLab 带来源标记，并在程序块上方注明 DXBC/GLSL 翻译来源。

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

# Windows 带 CLR 的裁剪单文件（约 40 MB）。AOT 不可用、或想要一个裁剪过的 CLR 版本时用：
# 产物与框架版逐字节一致，无裁剪警告。
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/win-x64-clr
mv publish/win-x64-clr/ShaderRipper.Application.exe publish/win-x64-clr/ShaderRipper.exe

# Linux：AOT 不能跨 OS 编译，linux AOT 包需在 Linux 上构建；
# Windows 交叉打包用带 CLR 的裁剪单文件（约 56 MB）
dotnet publish src/ShaderRipper.Application/ShaderRipper.Application.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish/linux-x64
```

发布产物是单个可执行文件，无任何伴生文件：schema 类型树、修复表、TMP 布局、按 RID 的原生库已全部嵌入，首次使用时解压到 `%TEMP%\ShaderRipper\`。

`src/ShaderRipper/Grammar/` 下的 ANTLR 词法/语法分析器是提前生成并入库的，因此构建不需要 JVM。改过 `.g4` 之后用 `py -3 scripts/gen_grammar.py` 重新生成（需要 java 和 `antlr-complete.jar`，用 `--antlr <路径>` 指定位置）。

## 验证脚本

`scripts/` 目录只放校验工具——`verify_logic.py`、`verify_glsl.py`、`scan_unwritten.py`、`scan_nan.py`、`scan_props.py`、`usc_check.py`（其协议客户端为 `usc_compile.py`）以及 `gen_grammar.py`。它们是开发期工具，不参与构建；每个脚本的 docstring 说明了它检查什么、怎么跑。它们所校验的不变量见[语义一致性验证](#语义一致性验证scriptsverifylogicpy)一节。

## 渲染器支持

产物按游戏**实际打包的后端载荷**逐平台处理，只有两种载荷有翻译器：

| 载荷（`m_Platforms` 里的后端）                          | Windows | Linux | macOS    | 产物                              |
| ----------------------------------------------- | ------- | ----- | -------- | ------------------------------- |
| dxbc：d3d11、xboxone、xboxone-d3d12、gamecore*     | ✅       | ✅     | ⚠️ 未验证   | ShaderLab + HLSLPROGRAM（汇编直译）   |
| dxbc 容器里的 DXIL chunk（D3D12 / SM6）               | ✅       | ✅     | ❌ 无 macOS DXC | 反汇编；回译未验证                       |
| glsl：gles20 / gles3 / openglcore / opengl      | ✅       | ✅     | ⚠️ 未验证   | ShaderLab + HLSLPROGRAM（GLSL 回译） |
| d3d9 / metal / vulkan / 主机平台 / webgpu         | —       | —     | —        | 跳过（没有对应翻译器）                     |

> macOS 未实测：托管路径理论上可用（自行 `dotnet publish -r osx-arm64` 打包），DXIL 反汇编因无官方 macOS 版 DXC 而不可用。
> 一个 shader 通常带多个后端槽位，工具会逐个处理；只带 d3d9 / metal / vulkan 的游戏不会有任何产出。

## 使用

```bash
# 最简：指向游戏根目录（或它的 <游戏名>_Data），输出到 <输入>_Ripped
ShaderRipper.exe --game "…/steamapps/common/A Dance of Fire and Ice"

# 指定输出目录，并把每个 shader 写成 .shader（交付物）
ShaderRipper.exe --game "…/A Dance of Fire and Ice" --shaderlab out

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
| `--platform <名\|id>` | 只读这一个后端的槽（d3d11 / gles3 / gles20 / openglcore / opengl …）；同时有 d3d11 和 gles3 的构建永远先取 d3d11，只有指定它才能单独看 GLSL 路线的产物。指定没有翻译器的后端（vulkan / d3d9 / metal）会直接报错，而不是导出 0 个 |
| `--no-dedup` | 保留每个副本（默认同一路径只留一份：优先有固定容器路径的，并列取 path id 最小者） |
| `--builtin` | 连 Unity 自带 shader 一起导出（默认跳过，只统计跳过了多少） |
| `--overwrite`（别名 `--clean`） | 写前清空输出目录，只清本工具自己产出的内容 |
| `--self-contained` | 引擎声明直接内联，不用 `#include`（可独立编译，但 Unity 导入时会拒绝） |
| `--quiet` / `--verbose` | 只输出最后的总账 / 额外输出每个文件的标题、结构与耗时 |
| `--lang <en\|zh>` | 界面语言，默认跟随系统 UI 语言 |
| `--tmp-layouts <文件\|目录>` / `--no-tmp-layout` / `--builtin-index <文件>` | 名称表覆盖项（默认用内嵌的那份） |
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

[`docs/verification/`](docs/verification/) 是一次完整往返：输入是打包进 Unity 6 测试工程的着色器源码（`*_original.shader`），输出是从该工程的构建产物里提取回来的结果（`*_ripper.shader` 带逐指令注释，`*_ripper_noasm.shader` 是去掉注释的同一份），另有所有已编译变体的 DXBC 反汇编合集。这两份 `_ripper` 是当时的产物，现在的前端只往文件头写说明（遇到翻译不了的指令才写 `// TODO`），不再逐条注释。源码随仓库提供，你自己打包一遍就能复现。

| 着色器 | 属性 | 变体 | fragment 指令 |
|---|---|---|---|
| `ShaderRipperTest/ExtremeFlow` | 37 | 1 | 1328 |
| `ShaderRipperTest/VariantMonster` | 20 | 60 | 合计 84,425，单变体 777–1911 |

每个变体都用 `d3dcompiler_47` 以 `ps_5_0` 重新编译验证：1/1 与 60/60 全部通过，0 条未翻译指令，0 处 `TODO`；控制流原样保留（ExtremeFlow：3 个循环、5 个出口、2 处 `discard`，`ddx`/`ddy` 都在）。

### 语义一致性验证（`scripts/verify_logic.py`）

「编译通过」不等于「逻辑一致」。把反编译出的 HLSL 重新编译回 DXBC、再反汇编，与原始反汇编逐项比对以下不变量：

1. cbuffer 寄存器不消失（声明宽度会被 fxc 按实际读取裁剪，不算差异）
2. cbuffer 读取行集合：重建不可多读原始从未读过的行
3. 采样/取值调用数：按**生成源码里的调用数**比对（fxc 会合并同坐标采样，编译产物的数量不可作准）
4. `sincos` 的 sin/cos 输出数：少一个意味着某个寄存器从未被写
5. `discard` / `ldms` / `gather` 等不可被优化的指令数
6. 立即数集合：1.0 与 -1.0 的掩码之争就藏在这里

在一个真实商业构建（425 个 shader / 3,263 个程序，其中 3,195 个 DXBC 程序）上：**3,195 / 3,195 通过**，`scripts/scan_unwritten.py` 报告 0 个「读未写寄存器」。同时 `scripts/usc_check.py`（Unity 官方 `UnityShaderCompiler`）全部编译通过。

```sh
py -3 scripts/verify_logic.py <导出目录> -j 6      # DXBC 路线：逻辑一致性
py -3 scripts/verify_glsl.py <导出目录> -j 6       # GLSL 路线：载荷 vs 生成
py -3 scripts/scan_unwritten.py <导出目录>          # 读到未写的寄存器
py -3 scripts/scan_nan.py <导出目录>                # NaN 掩码逃逸到渲染目标
py -3 scripts/scan_props.py <Assets 目录>          # Properties 泄漏进 include 集合
py -3 scripts/usc_check.py <导出目录> -u <Unity>    # Unity 官方编译器
py -3 scripts/gen_grammar.py                       # 从 *.g4 重新生成解析器
```

### 可读性 Pass（模式识别）

输出除了正确，还要看得出原来的意图。三处模式重写在**同一份语义**上换一种写法：

| Pass | 识别 | 输出 |
|---|---|---|
| 布尔掩码归一 | 逐次写入做 def-use：该值在**下一次重写前**只被分支/选择/比较/位运算读取 | `? 1.0f : 0.0`（而不是 `asfloat(0xFFFFFFFFu)`，那是 IEEE NaN） |
| 矩阵乘折叠 | `mul r1,v.y,M[1]; mad r1,M[0],v.x,r1; mad r0,M[2],v.z,r1; add r0,r0,M[3]` | 一条 `mul(M, v)`；累加器与目标可以不同寄存器，末项可以是输出寄存器；收尾行可以晚几条指令（fxc 会把喂给向量的计算调度在中间） |
| `UnityObjectToClipPos` | `mul(ObjectToWorld, v)` 的结果直接喂给 `mul(MatrixVP, ·)` | `o.pos = UnityObjectToClipPos(v.xyz)` —— 8 条指令变 1 条 |

**关于 `1.0f` 的判据**：不能无条件替换。DXBC 的比较写真值 -1（全 1 位模式），像
`lt → iadd(-t,v) → itof → ge → and` 这种区间选择器会**把掩码当数值算**，换成 1.0
会让整段符号翻转。所以判据是**逐次写入的数据流**：从这条比较往后扫，直到该 lane 被重写，
期间每次读取都必须是分支/`movc`/比较/位运算——全是布尔消费才能用 `1.0f`（真值等价，且不再产生 NaN）。

判据必须是 per-write 而不是 per-register：真实 shader 里同一个 lane 常常先存掩码、
`` `if (r0.x) { r0.x = ... }` `` 随后又存数字。整寄存器判定会把这类全部拒掉——
第一版正是如此，4123 处掩码一个都没改。改成 def-use 后降到 **144**（−96.5%）。

**关于「分量被重读」**：一条 `mul(M, v)` 只在同一时刻读四个分量，但 fxc 会把「喂其他分量
的计算」调度到中间，顺手覆盖掉其中一个。`ADOFAI_HoldShader` 就是这个形状：

```hlsl
mul  r2,   r0.xxxx, VP[1]        // 读的是「旧的」 r0.x
mul  r0.x, v0.y,    O2W[1].w     // r0.x 变成了世界坐标的 w
mad  o0,   VP[3],   r0.xxxx, r1  // 这里读的是「新的」 r0.x
```

直接折成 `mul(VP, float4(x, y, z, w))` 会取到错的 `x`。所以每个分量都记下它读自哪个
寄存器的哪个 lane；若它在收尾行之前被写过，就在**它还有旧值的那一刻** pin 成局部变量：

```hlsl
float pin0 = r0.x;
r0.x = mul(unity_ObjectToWorld, float4(v0.x, v0.y, v0.z, v0.w)).w;
o0.xyzw = mul(unity_MatrixVP, float4(r0.y, pin0, r1.z, r0.x));
```

这也正是这类 shader **不该**变成 `UnityObjectToClipPos` 的原因：它的 clip 坐标由
物体空间 `xyz` 与 object-to-world 乘积的 `w` 拼成，和「变换顶点位置」不是一回事。
只有严格的 `mul(VP, mul(O2W, float4(v, 1.0)))` 链才会换成宏。

矩阵声明在两条输出路径上不同：ShaderLab 文件经 `UnityCG.cginc` 拿到 `float4x4`，可用宏；`--programs` 的独立文件按反汇编的布局声明为 `float4 M[4]`，`mul` 不接受，所以该文件自带一份按行点积实现的 `UnityObjectToClipPos`（同样两次乘法，只是展开）。

折叠的边界是严格的：四行必须来自同一个数组、索引正好 0/1/2/3、目标掩码都是完整 `xyzw`、中间寄存器在两次乘积之间没有被别的指令写过，**并且**两次乘积必须紧邻、中间寄存器在第二次乘积之后已经不再被读。收尾行可以晚几条指令（fxc 会把喂给向量的计算调度在中间），但扫描在遇到写累加器或流控指令时立即停止。

最后一条是实测出来的：`mul r0, v, ObjectToWorld … mul r1, r0, MatrixVP` 合成一次 `UnityObjectToClipPos` 会**删掉 r0**，而需要世界坐标的顶点着色器（`mul r0.y, r0.y, unity_MatrixV[1].z` 求视空间深度）随后会读到未初始化的寄存器。`scan_unwritten.py` 正是靠这个从 0 变成 80 才发现它。

GLSL 路线没有可对拍的第二编译器，所以参照物是 GLSL 载荷本身：逐 stage 比纹理取值数、
赋值目标及其次数、比较运算符、内在函数（按翻译器自己的重命名表归一）。
在该构建上 33/33 通过；变异测试（删一条 `Sample`、少写一次寄存器）两类都能被抓。

以 ExtremeFlow 的 if/else 链为例，输入：

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
r6.xyz = float3((r3.z < 0.5) ? -1.0 : 0.0, (r3.z < 1.0) ? -1.0 : 0.0,
                (r3.z < 1.8) ? -1.0 : 0.0);                                   // 三个判据还在
r7.xyz = float3(mad(r4.x, _ColorC.x, -(r4.x)), ...);                          // lerp：accum*color - accum
r7.xyz = float3(mad(r7.x, 0.25, r4.x), ...);                                  //   ... * 0.25 + accum
r3.xyw = float3(r6.x != 0.0 ? r3.x : r5.x, ...);                              // 选择树
r2.w = (1.4 < r3.y) ? 1.0 : 0.0;
if (any(r2.w)) { r2.xyz = float3(r3.x, r3.y, r3.w); break; }                  // 提前退出被重建
```

编译器把分支链压成了谓词选择树，并把 `lerp` 改写成两条 `mad`；直译原样保留这个形态，而不是去猜原来的 `if/else`。这里的 `-1.0` 不是笔误——DXBC 的比较指令为真时写的是全 1 掩码，后面消费这些值的斜坡运算依赖这个符号，改成 `1.0` 会让判断整个反过来。这也解释了为什么输出比输入指令多（ExtremeFlow 1328 vs 1097）：编译器做过的算子融合拿不回来，硬猜就是编造。

### 名称与标签

有两类标识符不是照抄而是重新拼写，因为序列化数据里存的形态根本不是手写 shader 会用的：

- **ShaderLab 标签大小写。** 资产里标签键是大写（`LIGHTMODE`、`FORWARDBASE`）。Unity 的解析器对**键**不区分
  大小写，所以文件仍然能用，但**值**是精确匹配的——因此 `"LIGHTMODE" = "FORWARDBASE"` 会写成
  `"LightMode" = "ForwardBase"`。文档里列出的键全部规范化，`true`/`false` 转成 `True`/`False`。
- **纹理名。** Unity 按名字把采样器绑到纹理（`SamplerState sampler__MainTex`），裸 `s1` 会被直接拒绝。
  名字来自资产自己的纹理反射；但两边数目很少对得上——`[PerRendererData]` 的纹理从不出现在表里，而某个
  变体可能采样了它没有属性的内置纹理——所以反射能命名的先命名，剩下的寄存器按声明顺序取未用过的属性名。
  shader 没有声明属性的纹理保留位置名 `tN`。

## 限制

- Metal / Vulkan / WebGPU / d3d9 载荷没有翻译器：这些槽位直接跳过，不做猜测
- 纯 console 平台（PS/Switch）无可读产物
- d3d12 在数据里表现为 dxbc 槽位中的一个 DXIL chunk：反汇编可用，HLSL 回译未验证
- HLSL 是逐指令直译：寄存器复现和编译器的运算合并都原样保留，等价但啰嗦，不是地道的作者源码
