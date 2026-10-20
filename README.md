# ShaderRipper — Unity assets → DXBC / GLSL → ShaderLab（纯 C#）

一个自包含的 C# 工具：读取 Unity `SerializedFile`、提取每个 Shader 的编译产物、
LZ4 解压后按后端分流——**d3d11 的 DXBC** 调原生 `D3DDisassemble` 反汇编再直译 Cg；
**GLES3 / GLES2 / OpenGLCore 的 GLSL 源码**（HLSLcc 输出）由内置翻译器回译成同样的
CGPROGRAM；**Metal** 落 MSL 参考。最后按 Unity `SerializedShader` 字段语义重建
ShaderLab 文本。

## 输入后端一览

| 平台（`--platform`） | 资产里的产物 | 处理 |
|---|---|---|
| `d3d11`（4，默认） | DXBC（SM4/5） | `D3DDisassemble` → 逐条直译 Cg（主路径）；同 id 下出现 **DXIL chunk 即 D3D12/SM6**，走 dxcompiler 反汇编，只落参考 |
| `gles3`(9) / `gles20`(5) / `openglcore`(15) / `opengl`(1) | HLSLcc 的 **GLSL 源码** | `Cg/GlslToCg.cs` 回译成 CGPROGRAM，与 d3d11 产物同形态（含变体 `#if` 链） |
| `d3d9`(2) | SM2/SM3 裸 token 流 | `D3D9Asm.cs` 自研 token 反汇编（fxc 风格文本），只落 `Disassembly/` 参考 |
| `metal`(14) | MSL 归档（`0xF00DCAFE`） | 落 `Disassembly/<shader>/*.metal` 参考 |
| `vulkan`(18) | SMOL-V 压缩的 SPIR-V | 只落 `.smolv.txt` 说明（无魔数，本工具不解压） |

没有 d3d11 时自动退到 GLSL 源码（比汇编直译更接近作者原意），也可用
`--platform gles3` 强制；细节见下文「多平台：GLSL / Vulkan / Metal」。

## 目录结构

```
ShaderRipper/
├── ShaderRipper.slnx          解决方案（Rider / VS / dotnet CLI 均可直接打开）
├── src/ShaderRipper/          唯一项目：全部源码 + 运行时资源
│   ├── ShaderRipper.csproj
│   ├── Cg/                    DXBC 汇编 → Cg、GLSL → Cg 翻译层
│   ├── data/                  运行时数据（名单、$Globals 布局表、sourcetpk.tpk）
│   └── schema/                Unity 类型树 JSON
├── README.md / CG_TRANSLATOR.md / 会话总结.md
└── publish/win-x64/           单文件发布产物（已在 .gitignore 中）
```

## 运行

```
# 在仓库根目录（ShaderRipper.slnx 所在处）
dotnet build ShaderRipper.slnx -c Release

# 或直接从源码跑
dotnet run --project src/ShaderRipper -- --data <游戏>_Data --out D:\out
```

构建产物在 `src/ShaderRipper/bin/Release/net10.0/`，`data/`、`schema/`、
`D3Dcompiler_47.dll` 会随构建自动复制到 exe 同目录（见 `Paths.cs`）。

```
# 推荐：全用命名参数
ShaderRipper.exe --data <游戏>_Data --out D:\out

# 位置参数仍可用（等价写法）
ShaderRipper.exe <游戏>_Data D:\out

# 从 GLSL 出发：Android / GL 包的程序块本来就是 HLSLcc 的 GLSL 源码
ShaderRipper.exe --data <游戏>_Data --out D:\out --platform gles3
```

没有默认游戏目录——`--data` 也可以改用环境变量 `SHADERRIP_DATA`。所有路径都有
回退链，`ShaderRipper.exe --help` 会打印完整清单：

| 用途 | 参数 | 环境变量 | 省略时的行为 |
|---|---|---|---|
| 游戏数据目录 | `--data <dir>` | `SHADERRIP_DATA` | 取第一个位置参数；都没有则报错退出 |
| 输出目录 | `--out <dir>` | `SHADERRIP_OUT` | 取第二个位置参数；都没有则 `当前目录/game_shaderlab_<6000 或 cg>` |
| 资源根（`schema/`、`data/`） | `--root <dir>` | `SHADERRIP_ROOT` | 从 exe 目录、再从当前目录向上找含 `schema/`/`data/` 的目录 |
| 类型树 | `--schema <file 或 dir>` | — | 按 `SerializedFile` 版本自动选 |
| 内建名名单 | `--builtin-index <f>` | — | `data/unity_builtin_shaders.txt` |
| float4x4 名单 | `--matrix-names <f>` | — | `data/unity_matrix_names.txt` |
| `$Globals` 布局表 | `--tmp-layouts <dir>` | — | `data/tmp_layouts` |
| 修复表 | `--names <dir>` | — | `ShaderLabRef/globals_names`（资源根自动定位） |

`D3Dcompiler_47.dll` 必须与 `ShaderRipper.dll` 同目录（构建时已自动复制）。

## 结果（A Dance of Fire and Ice, Unity 2019.3.0f6）

```
unity=2019.3.0f6  platform=5  shaders=258
done: ok=256  noDxbc=2  failed=0
```

- **258** 个 Shader 全部解析成功
- **256** 个产出完整 ShaderLab（含反汇编程序体）
- 2 个无 DXBC：`WellFired/WipeUp`、`WellFired/WipeRight`
- 输出：`game_shaderlab_cs/*.shader`（258 个文件，约 1.80 MB）

## 组件

| 文件 | 职责 |
|---|---|
| `Program.cs` | 入口：编排解析→解码→按后端分流（DXBC 直译 / GLSL 回译）→生成 |
| `SerializedFile.cs` | Unity SerializedFile (v21) 头/类型表/对象表 + Shader 主体映射 |
| `SchemaReader.cs` | 按类型树（schema）驱动读取，忠实还原二进制布局 |
| `EndianReader.cs` | 小端游标（对齐字符串 / 数组） |
| `BlobTool.cs` | 分块 LZ4 解码 + DXBC 定位 + `D3DDisassemble` P/Invoke；DXIL 走 dxcompiler COM |
| `D3D9Asm.cs` | **D3D9 SM2/SM3 token 反汇编器**（自研，fxc 风格输出，`--selftest-d3d9` 自测） |
| `ProgramSource.cs` | 从 slot 里切出 HLSLcc 的 GLSL / Metal / d3d9 程序（按签名识别） |
| `Cg/GlslToCg.cs` | **GLSL → Cg 回译**（GLES3/GLES2/OpenGLCore 主路径） |
| `Cg/CgTranslator.cs` | DXBC 汇编 → Cg 直译 + 变体组织（d3d11 主路径） |
| `Cg/CbResolver.cs` | 常量缓冲 / `$Globals` 成员命名与矩阵展开 |
| `ShaderLabWriter.cs` | ShaderLab 文本生成（属性/状态/Pass/CGPROGRAM） |
| `Models.cs` | 数据模型 |
| `schema/shader_typetypes.json` | Shader 类型树（由游戏自身的类型定义导出） |
| `schema/shader_typetypes_6000.json` | Unity 6 生成（`SerializedFile >= 22`）的类型树 |

## 关键技术点（踩过的坑）

1. **SerializedFile 头是大端**，元数据段是小端；`version>=22` 另有 32 字节附加头。
2. **类型树必须保留 MetaFlag**：`0x4000` = 读后 4 字节对齐。
   漏掉它会在 `bool`/`SInt8`（如 `m_GpuProgramType`）后错位，
   每进一个 Pass 漂 3 字节，最终彻底失控。
3. **数组自身也带对齐**：`ParserBindChannels.m_Channels`
   （元素 2 字节）读完后必须 4 字节对齐，否则 `m_SourceMap` 读错。
4. **`vector`/`map` 是包装节点**：`m_SubShaders(vector) → Array`，
   必须解包才能拿到元素列表，否则被当成复合体读成 Dictionary。
5. **单次顺序遍历**：对象体必须严格按类型树顺序读一遍；
   用 `ReadChild`「跳读后重读」会让游标错位。
6. **DXBC 定位**：解压后的程序中含多个 `DXBC` 容器（顶点/片元各若干变体），
   按 `+0x18` 处的容器长度与 `+0x1C` 的 chunk 数校验。
7. **blob 索引**：`SerializedSubProgram.m_BlobIndex` 指向该平台程序中的第 N 个容器。

## 校验

解析结果与 UnityPy 1.25.4 的权威输出逐字段吻合：

| 字段 | ShaderRipper | UnityPy |
|---|---|---|
| `m_LOD` | 100 | 100 |
| `m_ProgramMask` | 6 | 6 |
| blobIndex | 0,1,2 | 0,1,2 |
| `m_ShaderHardwareTier` | 3 | 3 |
| `m_GpuProgramType` | 15 / 17 | 15 / 17 |
| 属性 | `_Color`…`_MainTex` (5) | 同 |

Python 侧独立遍历器（`tools/walk_schema.py`）对 13560 字节对象
walk 后 `delta = 0`，确认布局完全一致。

## 产物

- `src/ShaderRipper/` — 工具源码 + `data/`、`schema/`（`ShaderRipper.slnx` 为解决方案）
- `game_shaderlab_cs/*.shader` — 258 个重建的 ShaderLab（d3d11 直译路径）
- `game_shaderlab_cg/*.shader` / `_cg/*.cg` — 带完整 Cg 的 ShaderLab（d3d11 直译路径的产线）
- `tools/walk_schema.py` — 参考遍历器（验证用）
- `tools/typetree_to_schema.py` — 类型树导出

## 验证：直接用 UnityShaderCompiler 编译

`tools/usc_verify_all.py` 起一个 `UnityShaderCompiler.exe`，通过 IPC（协议见
`USC_PROTOCOL.md`）把每个 `.shader` 预处理后按它自己声明的
`#pragma vertex` / `#pragma fragment` 编译成 DXBC，全程一个进程：

```powershell
python tools/usc_verify_all.py game_shaderlab_6000
# total=414 ok=414 fail=0 elapsed=22s platform=4
```

比 Unity batchmode 快两个数量级，且判据相同（同一个编译器二进制）。
报告写入 `_out/usc_verify.txt`，`OK/FAIL + 每个 snippet 数`。

`--only <substr>` 只重生成一个 shader，便于定位单点问题。

### 内建着色器过滤（按引擎清单，不按名字猜）

内建 shader 会被 Unity 复制进 `resources.assets`，所以只跳过来源文件不够，必须
拿引擎自己的清单来判：

| 环节 | 做法 |
|---|---|
| 生成清单 | `_uproj/Assets/Editor/DumpBuiltinShaders.cs` 用 `AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra")` 导出全部 Shader 名 |
| 清单文件 | `data/unity_builtin_shaders.txt`，当前 6000.3.10f1 共 **206** 条 |
| 过滤 | `BuiltinIndex.Contains(name)` 命中即跳过，`--no-builtin-index` 可关，`--builtin-index <file>` 可换 |
| 来源兜底 | `BuiltinSource` 仍跳过 `unity default resources` / `unity_builtin_extra` / `*unitybuiltinassets.bundle` |

### 扫包默认开启

`resources.assets` 不是全部：游戏有 17 个 shader 只存在于 `StreamingAssets/aa/*.bundle`
（`ADOFAI_CustomTextureRect*`、`ScreenScroll`、`FadeBottom`、`Sprites_Tile` …）。所以默认
扫包，`--no-bundles` 退回单文件模式，`--bundles [dir]` 指定别的包目录。

**这里暴露过一个真 bug**：`ADOFAI_CustomTextureRect(Additive)` 报
`invalid type used for 'SV_VERTEXID' input semantics, must be integral`。DXBC 签名只给
寄存器掩码不给 C 类型，`ShaderInterface` 原先把 `SV_VERTEXID` 声明成 `float`。现在
`IntegralSemantics`（`SV_VERTEXID` / `SV_INSTANCEID` / `SV_PRIMITIVEID` / `SV_VIEWID`）
统一发 `uint`。

在 ADOFAI 上实测：**414 → 420**（读包里 17 个游戏 shader，跳过 11 个内建：Standard、
Particles/Standard ×2、Legacy Shaders ×4、Mobile ×2、UI/Unlit/Transparent、
Unlit/Transparent），裸 `cbN[row]` 由 7,220 降到 **1,985**。TextMeshPro 不在引擎清单里
（属于 TMP 包），按预期保留。

`--list-shaders <file>` 打印单个 SerializedFile 里的全部 shader 名，用于核对某个文件到底装了哪些东西。

### 原始汇编注释（默认开）

每条 DXBC 指令前面插入一行 `// <原始反汇编>`，紧跟它生成的那几行 CG：

```hlsl
// ge r2.xy, v3.xyxx, cb0[4].xyxx
r2.xy = float2((v3.x >= _ClipRect.x) ? 1.0 : 0.0, (v3.y >= _ClipRect.y) ? 1.0 : 0.0);
// and r2.xyzw, r2.xyzw, l(0x3f800000, 0x3f800000, 0x3f800000, 0x3f800000)
r2.xyzw = float4((((r2.x) != 0.0) ? 1.0 : 0.0), ...);
```

选择逐行而不是在文件末尾贴整块，理由有三条：

1. **一对多**：一条汇编展开成多行 CG（`float4` 按分量拆开、`and` 掩码折叠），只有行内注释能表达这个映射；
2. **已有整块**：侧车 `_cg/*.cg` 末尾本来就附了完整反汇编，再贴一遍是重复；
3. **可核对**：SV_VERTEXID、十六进制立即数、`and` 掩码这几个 bug 都是逐条对照才定位的。

代价：产物 24M → 28M（+17%），USC 全量 20s → 38s。`--no-asm` 关掉。

### 语义规则：`_ClipRect`

UI 着色器的 `float4 _ClipRect`（`.xy` 最小值、`.zw` 最大值）既不在 Properties 里，也不在资产反射和 name table 里——UI 运行时用 `SetVector` 塞进去。`CbResolver.ApplyClipRectShape` 按指令形态认：同一行出现 `z`/`w` 在比较左侧、`x`/`y` 在右侧的 `ge` 组合，即判为 `_ClipRect`。UI/Wave、UI/Vignette Effect 各 8 处，共 16 处。

### 矩阵成员跨 4 行（`MatrixNames`）

`$Globals` 是 fxc 给「没放进 `CBUFFER` 的全局 uniform」自动开的容器，所以
`unity_WorldToLight` 这类 Unity 全局矩阵会和材质属性挤在同一个缓冲里。参数块只记
成员的**起始 offset**，不记它跨几行，于是矩阵的第 1~3 行就成了没有名字的 `cbN[row]`。

两个条件同时成立才展开，缺一不动：

1. **跨度**：从本成员到下一个成员的 offset 差是 32~64 字节（2~4 行）；
2. **身份**：名字在 `data/unity_matrix_names.txt` 里——该表由 Unity 安装目录
   `CGIncludes/*.cginc` 中所有 `float4x4 NAME;` 声明提取，不是人工枚举。

还有一个前提：同一个 shader 的每个子程序**只记自己读到的成员**，单看一份成员表，
「到下一个成员的距离」会横跨十几行。所以 `Resolve` 现在接收整个 shader 的参数块，
按**同一缓冲尺寸**合并后再算跨度；尺寸由 `dcl_constantbuffer` 的声明行数决定——用
「读到的最远行」会误选另一种布局（272 与 336 字节的成员位置完全不同）。

```hlsl
// 展开前
r1.xyz = float3(r0.y * cb0[5].x, r0.y * cb0[5].y, r0.y * cb0[5].z);
r0.xyz = float3(mad(cb0[6].x, r0.z, r1.x), ...);

// 展开后：float4x4 unity_WorldToLight; —— 就是 mul(float4(pos,1), unity_WorldToLight)
r1.xyz = float3(r0.y * unity_WorldToLight[1].x, r0.y * unity_WorldToLight[1].y, ...);
r0.xyz = float3(mad(unity_WorldToLight[2].x, r0.z, r1.x), ...);
```


### TMP `$Globals`：把布局从编译器里抄下来（默认开启）

TextMeshPro 的 uniform 全是裸声明（`uniform float _FaceDilate;`，没有 `CBUFFER`），
所以 fxc 把它们连同少数没被 Unity 包进 `CBUFFER` 的全局变量一起塞进 `$Globals`。
这些 shader 的参数块里成员数是 **0**、资产反射也是空的，行号因此全裸。

布局不需要推导——它就是 fxc 对「声明顺序 + HLSL 打包规则」的结果，而声明顺序在
Unity 自己的 CGIncludes 里。`tools/gen_tmp_layout.py` 直接编译源码、读 **RDEF** 抄表：

```
源码（含 #include 链） --fxc -D SHADER_API_D3D11--> DXBC --RDEF--> data/tmp_layouts/*.txt
```

**踩了半天的坑**：不加 `-D SHADER_API_D3D11` 时 `CBUFFER_START` 是空的
（`HLSLSupport.cginc` 里它只在 D3D11/PSSL/Metal 等条件下才展开成 `cbuffer`），
`UnityPerCamera`/`UnityPerDraw` 那些本该进独立 cbuffer 的全局会全部掉进
`$Globals`，尺寸变成 3072 字节，和游戏里对不上。加上之后：

| shader | 表里的 `$Globals` | 游戏里实测 |
|---|---|---|
| `TMP_SDF` / `TMP_SDF Overlay` | 528 B / 33 行 / 66 成员 | 33 行 ✓ |
| `TMP_SDF-Mobile` / `-2 Pass` | 480 B / 30 行 | 30 行 ✓ |
| `TMP_SDF-Mobile Masking` | 512 B / 32 行 | 32 行 ✓ |
| `TMP_SDF-Surface`（ForwardBase） | 560 B / 35 行 | 35 行 ✓ |
| `TMP_SDF-Surface`（ForwardAdd/POINT） | 624 B / 39 行 | 39 行 ✓ |

逐成员偏移也与资产自带的常量缓冲反射逐个吻合（`_FaceColor@48`、`_EnvMatrix@176`、
`_FaceTex_ST@480`…）。Surface 着色器没有可编译的入口，表是模拟代码生成器的前置
声明顺序得到的：`UnityCG` + `UnityLightingCommon`（`_LightColor0`/`_SpecColor`）+
`AutoLight`（ForwardAdd 的 `unity_WorldToLight`）+ 正文 include 与全局 + 属性里
每个"用自己 uv"的纹理自动生成的 `_Xxx_ST`（`_MainTex` 排最前，
`tex2D(_BumpMap, input.uv2_FaceTex)` 这种借别人 uv 的不生成）。

运行时不推导，只校验（`Cg/TmpLayouts.cs`）：

1. `dcl_constantbuffer CBn[k]` 的行数只是**下界**（同一个 528 字节的块在不同变体里
   分别声明成 CB0[23]、CB0[29]…），所以表可以更大；
2. 本程序读到的**每一行**都必须被表覆盖；
3. 表的成员名与 name table 交集 ≥4，或与参数块/资产反射的「名字 @ 偏移」锚点
   吻合 ≥3 个；
4. 与已有命名冲突就整块放弃（`_EnvMatrix` 与 `_EnvMatrix[0]` 这种同一成员的不同
   写法不算冲突，并以表里的矩阵写法为准）；
5. 表里的类型覆盖前面路径的类型（修复表/资产反射常把矩阵整行记成 `float4`，
   那样 `_EnvMatrix[1].y` 编不过）。

Surface 的分片程序常常连参数块都没有、name table 只剩 `_LightColor0`/`_SpecColor`，
这时代码会用同一 shader 顶点程序已确认的布局（同尺寸必然同布局）。另外 `$Globals`
的寄存器归属也修了：它原本恒得 0 分，会在多个"都解释不了"的寄存器里随便挑一个
（实测 Surface 分片把 `$Globals` 挂到了 cb3，真正 33 行的 cb0 留成数字）；现在
"行数明显超过任何 Unity 内建缓冲（≤23 行）"的寄存器优先归 `$Globals`。

效果：裸 `cbN[row]` 从 **1,886 → 12**（仅剩 `ADOFAI_MergeSprite`，非 TMP，与本次
无关）；USC 全量校验 420/420 通过。

另外两条这次踩到的教训（都已写进代码注释）：

- 参数块的尺寸匹配只能用 `Size >= 声明行数×16`，**不能要求相等**：`dcl_constantbuffer`
  的行数只是下界（MergeSprite 声明 21 行、成员表却是 336 字节那份），要求相等会让
  整块找不到表、行名全丢。
- `_ClipRect` 的形态规则是"补空缺"，必须无条件执行；一旦挂在其他路径后面做短路，
  UI 着色器就会丢掉这一行。


## 2026-10-18 修复

| 问题 | 根因 | 修法 |
|---|---|---|
| `and` 掩码恒 0 | `float.TryParse` 读不了 `l(0x3f800000)`，六成以上 `and` 的掩码被抹成 0.0（65/414 个 shader 受影响） | `AsmParser.ParseImmediate` 解十六进制位模式；`and/or` 遇常量掩码发 `(x != 0) ? mask : 0`，双常量直接折叠 |
| `$Globals` 张冠李戴 | 回退分配里 `$Globals` 恒得 0 分，会和 46 行的 UnityPerFrame 抢寄存器 | 用参数块/资产反射的字节大小做行数上界，越界记 -100 分 |
| 覆盖率统计失真 | `Program.Supported` 是手写白名单，早已落后 | 改由 `CgTranslator.UnsupportedTotal` 实测计数 |
| **属性驱动的渲染状态被写成常量** | `SerializedShaderFloatValue.name` 一直没读，表面着色器的 `ZWrite [_ZWrite]` / `Blend [_SrcBlend] [_DstBlend]` 退化成 `ZWrite Off` / `Blend Zero Zero`；`Queue` 还按混合猜成 `Transparent` | 读属性名并输出引用；`Queue`/`RenderType` 回归 ShaderLab 默认（Geometry/Opaque）；`BlendOp` 不支持属性引用，退回数值 |
| **`Stencil` / `ColorMask` 的属性引用同样丢了** | 上一条只修了 `ZWrite`/`Blend`：`stencilRef`、`stencilOp.comp`、`rtBlendN.colMask` 从没读 `name`。而 UI/TextMeshPro 恰好全用属性驱动（`Ref [_Stencil]`、`Comp [_StencilComp]`、`ColorMask [_ColorMask]`），序列化值又等于 Unity 默认值，于是整块被误判成 no-op 直接丢弃 —— 28 个着色器的 RectMask2D 裁剪全部失效 | 三个字段补齐 `name`；只要有一个 `name` 就强制输出整块（值不参与 no-op 判定）；`Front`/`Back` 的默认槽不再输出（Unity 6 拒绝 `Front Comp Always`） |
| **纹理维度全被写成 `2D`** | 纹理属性的 `m_Type` 恒为 4（"texture"），真正的维度在 `m_DefTexture.m_TexDim`（2=2D 3=3D 4=Cube 5=2DArray 6=CubeArray）。该字段早就读了却没人用 | 属性按 `m_TexDim` 输出；`sampler2D` 同步换成 `samplerCUBE`/`sampler3D`/`sampler2DArray`（`_Cube` 之前是 `2D` + `sampler2D`，`texCUBE` 语义是错的） |
| **Pass 名是编出来的** | `m_StateName` 为空时按序号生成 `Pass0`/`Pass1`，让 `UsePass` 引用和 Frame Debugger 出现不存在的名字 | 只输出资产里真有的名字 |
| `CustomEditor` 丢失 | `m_CustomEditorName` 落在 `Skip` 分支 | 读取并输出（TMP 的 8 个着色器找回 `TMP_SDFShaderGUI`，材质 Inspector 才能正常显示） |

状态改动由 Unity batchmode 把关（`_uproj`，420/420 通过）——USC 只验程序，
不验 ShaderLab 状态语法。

## 多平台：GLSL / Vulkan / Metal

`m_Platforms` 与 `m_Offsets` 同序（下标即平台），id 是
`UnityEditor.Rendering.ShaderCompilerPlatform`：D3D=4、GLES3=9、Metal=14、
OpenGLCore=15、Vulkan=18（用编辑器反射枚举实测导出，见
`_uproj/Assets/Editor/DumpPlatforms.cs`）。选错下标拿到的就是另一种后端的字节，
所以平台选择是显式的：默认挑 d3d11（唯一能直译成 Cg 的汇编后端），包里没有 d3d11 时
自动退到 GLSL 源码，`--platform` 可强制指定。

每个 slot = `0x0C0A75BA` 头 + 常量缓冲名表 + 编译产物；产物的格式只有看签名才知道：

| 后端 | 产物 | 处理 |
|---|---|---|
| d3d11 | DXBC | `D3DDisassemble` → 直译 Cg（主路径） |
| GLES3 / OpenGLCore | HLSLcc 的 **GLSL 源码** | `Cg/GlslToCg.cs` 反向翻译回 Cg，与 d3d11 产物同形态（CGPROGRAM + appdata/v2f + 变体 #if 链） |
| Metal | `0xF00DCAFE` 归档（入口名 + MSL 文本） | 落 `Disassembly/<shader>/metal.*` 参考 |
| Vulkan | **SMOL-V 压缩的 SPIR-V** | 无魔数，落 `.smolv.txt` 说明（解压器不在工具范围） |

GLSL → Cg 翻译要点：HLSLcc 的 GLSL 本身就是寄存器风格直译（`u_xlatN` 临时量），
逐行重写即可保真；矩阵保持「4 个 vec4 行」的存储（`unity_ObjectToWorld[4]`），
代数一字不改；与 CGPROGRAM 前端自动注入的 uniform（`unity_MatrixVP`、
`_ScreenParams`…）撞名时**不能改名**（早期 `xlat_` 前缀版本会让矩阵永远不绑定，
整只网格消失），正确做法是丢掉自己那份声明、直接用 cginc 里的同名变量；
语义全部来自 HLSLcc 的命名约定（`in_POSITION0` → POSITION0，
`vs_TEXCOORD1` → TEXCOORD1，`SV_Target0` 原样）。`#version` / `precision` 行不进 CG；
GLSL 允许的单参向量构造（`float4(x)`）要展开、GLSL 的 `return;` 带着半成品结果返回。

用真实样本端到端验证过：把 TMP 着色器分别打成 Windows(Vulkan+D3D11) 和
Android(GLES3+Vulkan) 的 Player（`_uproj/Assets/Editor/BuildPlayerProbe.cs`），
Android 产物 32 个变体全部翻译成 CGPROGRAM，Unity batchmode Import **OK 0 警告**；
d3d11 主路径产物逐字节不变。

## 限制（如实说明）

- **d3d11 路径**的 `CGPROGRAM` 内是汇编逐条直译出的 Cg（每条指令上方带原始反汇编
  注释），不是作者手写的 HLSL —— 原始源码不在包内。
- **GLSL 路径**拿到的是 Unity 自己的 HLSLcc 输出（`--platform gles3/gles20/openglcore`，
  或包里没有 d3d11 时自动选中），逐行回译保真度更高；但它是**编译后**的形态：
  宏展开、结构体名、注释都无法还原，仍是 `u_xlatN` 这类寄存器风格命名。
- **d3d9 / D3D12(DXIL) 路径只做反汇编**（`Disassembly/<shader>/*.d3d9.asm` /
  `*.dxil.asm`），不回译 Cg —— SM2/3 与 DXBC、LLVM IR 与 Cg 的方言差距太大；
  ShaderLab 结构（属性/状态/Pass）照常输出，程序体位置有指引注释。
  DXIL 反汇编需要 dxcompiler.dll（自动从注册表定位的 Unity 安装目录加载）。
- Metal 只有 MSL 文本、ShaderLab 没有对应块，落 `Disassembly/` 参考；
  Vulkan 是 SMOL-V 压缩的 SPIR-V（无 `0x07230203` 魔数），本工具不解压。
- 只有 console（PS4/PS5/Switch）程序的包没有可读产物，`--platform` 指定到这些
  平台时不会产出可用的 `CGPROGRAM`。
- ShaderLab 的状态枚举映射按 Unity 通行取值推断；`Stencil` 默认值抑制按
  「Comp=Always / ops=Keep 且无属性名」判定，已由 Unity batchmode Import 验证
  （420/420，仅剩 20 条编译器 hint，全是 `rsqrt` 常量传播与整数除法）。
- 属性上的高级 drawer（`[Enum(...)]`、`[PowerSlider]`、`[IntRange]`）和
  `m_KeywordFlags` 不在序列化数据里，还原不出来 —— 材质 Inspector 的控件样式
  会和原版不同，但取值正确。
- ~~`ADOFAI_MergeSprite` 还剩 4 处裸 `cb0[11].z/.w`~~ 已解决，且结论和第一版推断
  完全不同。这个 shader 的 keyword 变体编译出**两种** `$Globals` 打包：
  **272/288 B** 的布局与资产反射完全一致（`r11 = _Texture5Rot | _DisableTexture5 |
  pad`，代码印证：`rot = cb0[11].x * -0.017453`）；**336 B** 的程序则把整个
  T1..T5 组后移 64 B，裸的 `cb0[11]` 是 **T3 的 scale.xy | pos.xy**（每个片元变体
  都以 `div cb0[11].zzwz, cb0[11].xxyx` + `div 0.5, cb0[11].xyxx` +
  `rot = cb0[12].x * -0.017453` 的形状读它，纹理 id 是 t2=_Texture3）。
  `ADOFAI_MergeSprite.json` 用尺寸专用键 `"$Globals@336"` 只对 336 B 程序覆盖
  `r11 = _Texture3Scale.xy | _Texture3Pos.xy`（成员名取自反射的同一批属性，
  位置按 +64 B 移位）。中途一版曾根据 Assembly-CSharp.dll 里的
  `_StartTileSize`/`_EndTileSize` 字符串把整行推成 `_StartTileSize` —— 那是错的
  （那些字符串属于 floorIcon 图集系统，且 272 B 程序的 `r11.x` 是旋转角，
  scale 不可能乘 π/180），note 里保留了这段弯路作警示。
  全量裸 `cbN[row]` 现在**为 0**。
- 顶点输出结构体在少数分支里没写满，Unity 会给
  `Output value 'vert' is not completely initialized` —— DXBC 里本来就没写，
  忠实照抄的结果，原版编译同样如此。
