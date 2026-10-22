# ShaderRipper — DXBC 汇编 / GLSL 源码 → HLSL 翻译器

在「assets → DXBC → 反汇编 → ShaderLab」链路上，新增**汇编→HLSL 源码翻译**层；
GL 平台（GLES3/GLES2/OpenGLCore）则直接拿 HLSLcc 的 GLSL 源码回译成 HLSL，
两条路径默认都产出同形态的 `HLSLPROGRAM`（见文末「GLSL 输入」）。

**两条路径都是直译**：DXBC 是一句指令映射一句，GLSL 是一行重写一行，
不做优化还原、不重排、不折叠表达式。中间会经过一次 Cg 中间态（见
「为什么不直接上现成反编译器」），那只是采样器写法不同，不是多了一层语义加工。

> **方言开关**：默认 `--syntax hlsl`，输出 `HLSLPROGRAM` + `Texture2D`/`SamplerState` + `.Sample()`；
> 传 `--syntax cg` 则回到历史上文出现过的旧形态 `CGPROGRAM` + `sampler2D` + `tex2D`。
> 两条路径（DXBC / GLSL）都受该开关控制，产物结构一致，只有程序块头、纹理声明和采样调用形式不同。
> 本文档中的示例保留旧 `CGPROGRAM` 形态以便对照；新产物请按 `hlsl` 形态理解。
258 个 Shader 的汇编指令 **100% 翻译完成，零 TODO**。

## 结果

```
unity=2019.3.0f6   shaders=258
done: shaders=258
remaining TODO: 0
```

- 覆盖 **59 种不同 opcode**
- 输出 `game_shaderlab_cg/_cg/*.cg`（每 Shader 一份，含全部 blob）
- 输出 `game_shaderlab_cg/*.shader`（`HLSLPROGRAM` 内嵌翻译后的 HLSL；`--syntax cg` 时为 `CGPROGRAM`）

## 翻译示例

**输入（DXBC 汇编）**
```
ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0.xyzw
dcl_temps 1
sample r0.xyzw, v0.xyxx, t0.xyzw, s0
dp3 o0.xyz, r0.xyzx, l(0.222000, 0.707000, 0.071000, 0.000000)
mov o0.w, l(1.000000)
ret
```

**输出（CG，`--syntax cg`）**
```hlsl
sampler2D t0;
SamplerState s0;

float4 v0;  // input .xy
float4 o0;

float4 r0;

    r0.xyzw = tex2D(t0, float2(v0.x, v0.y));
    o0.xyz = r0.x * 0.222 + r0.y * 0.707 + r0.z * 0.071;
    o0.w = 1.0;
    return;
```

**同一份产物的默认形态（`--syntax hlsl`）**
```hlsl
Texture2D<float4> t0;
SamplerState sampler_t0;

float4 v0;  // input .xy
float4 o0;

float4 r0;

    r0.xyzw = t0.Sample(sampler_t0, float2(v0.x, v0.y));
    o0.xyz = r0.x * 0.222 + r0.y * 0.707 + r0.z * 0.071;
    o0.w = 1.0;
    return;
```

纹理维度决定类型（`Texture2D` / `Texture3D` / `TextureCube` / `Texture2DArray`）；
`.Sample()` 的坐标按维度自动截断 swizzle（2D→`.xy`，3D/cube→`.xyz`），
`sample_l` 译成 `.SampleLevel()`。`sampler<纹理名>` 的命名是 Unity 的绑定约定，
材质上设置的采样状态照常生效。

**控制流（iloop / if / breakc / ilt / iadd）**
```hlsl
for (;;) {
    r1.x = (((int)(2.0) < (int)(r0.w)) ? 1 : 0);
    if (any(r1.x)) break;
    r1.xy = float2(((int)(r0.w) + (int)(2.0)), ((int)(r0.w) + (int)(1.0)));
    ...
}
```

## 支持的指令集

| 类别 | 指令 |
|---|---|
| 算术 | `mov` `add` `sub` `mul` `div` `mad` `min` `max` `frc` `exp` `log` |
| `_sat` 变体 | `mov_sat` `add_sat` `sub_sat` `mul_sat` `div_sat` `mad_sat` `dp2/3/4_sat` |
| 点积 | `dp2` `dp3` `dp4` |
| 超越 | `rsq` `sqrt` `sincos` |
| 舍入 | `round_ni`(floor) `round_pi`(ceil) `round_ne`(round) `round_z`(trunc) |
| 比较 | `lt` `le` `ge` `gt` `eq` `ne` |
| 逻辑/位 | `and` `or` `xor` |
| 整数 | `iadd` `isub` `imul` `imad` `ineg` `ilt` `ige` `ieq` `ine` `ishl` `ushr` `imax` `imin` `udiv` |
| 类型转换 | `itof` `ftoi` |
| 选择 | `movc` |
| 纹理 | `sample` `sample_l` |
| 流程 | `loop` `endloop` `if_nz` `if_z` `else` `endif` `breakc_nz` `breakc_z` `discard_nz` `ret` |
| 声明 | `dcl_constantbuffer` `dcl_sampler` `dcl_resource_texture*` `dcl_input*` `dcl_output*` `dcl_temps` `dcl_indexableTemp` `dcl_immediateConstantBuffer` |

## 实现要点

1. **DSL 解析**（`Cg/AsmParser.cs`）
   - 按顶层逗号切分操作数（括号/方括号内不切）
   - 解析寄存器种类（r/v/o/cb/x/t/s/icb）、索引（静态或动态表达式）、swizzle、取反
   - 声明按前缀特异度排序匹配（`dcl_indexableTemp` 必须在 `dcl_i*` 之前）

2. **翻译**（`Cg/CgTranslator.cs`）
   - **按分量展开**：每条指令按目标 swizzle 宽度展开成 1–4 个分量表达式
   - **立即数按目标分量索引**：`iadd r2.zw, r1.zzzz, l(0,0,2,1)` → `r1.z+2, r1.z+1`
   - **无 swizzle 时**用目标 swizzle 选取分量（DXBC 语义）
   - 指令语义映射：`dp3`→分量乘积求和、`rsq`→`rsqrt`、`round_ni`→`floor` 等

3. **健壮性**
   - 空字节行、icb 数据行（`{...}`）过滤
   - 所有 `int.Parse` 换 `TryParse`

## 踩过的坑

| 问题 | 根因 | 修复 |
|---|---|---|
| 6 个 Shader 解析崩溃 | `dcl_indexableTemp` 被 `dcl_input` 前缀抢先匹配 | 特异前缀前置 |
| 全部指令变 TODO | 空 opcode 判断用 `StartsWith` 而非精确匹配 | Trim + 精确判断 |
| `v0.xyxx.xy` 冗余 swizzle | 源 swizzle 与目标宽度一致时叠加错误 | 按分量逐个取 |
| `r2.zw` 两分量同值 | 立即数未按目标分量索引 | 传 dst swizzle 进 Comps |
| 863 个空 TODO | DXBC 尾部 0x00 填充被当指令 | `IsBlank()` 检查可打印字符 |
| 最后 6 个 TODO | icb 数据行 `{ 0, 1.0, 0, 0},` 被当指令 | 跳过 `{`/`}` 开头行 |

## 文件

| 路径 | 说明 |
|---|---|
| `src/ShaderRipper/Cg/AsmModel.cs` | 操作数/指令模型 |
| `src/ShaderRipper/Cg/AsmParser.cs` | 汇编文本解析器 |
| `src/ShaderRipper/Cg/CgTranslator.cs` | 汇编→HLSL 翻译器 |
| `src/ShaderRipper/Cg/GlslToCg.cs` | GLSL→Cg 回译器（GLES3/GLES2/OpenGLCore 输入），逐行重写 |
| `src/ShaderRipper/Cg/CgSyntax.cs` | 第二段：Cg→HLSL 语义翻译 + 方言重写（`CGPROGRAM`↔`HLSLPROGRAM`、`tex2D`→`.Sample()`、坐标截断、lod 解包、未用采样器剔除） |
| `src/ShaderRipper/Program.cs` | 编排（解析→翻译→输出），`--syntax` 在此解析 |
| `game_shaderlab_cg/_cg/*.cg` | 258 份翻译后的 HLSL（历史目录名保留） |
| `game_shaderlab_cg/*.shader` | 含翻译结果的 ShaderLab（默认 `HLSLPROGRAM`） |

## GLSL 输入（GLES3 / GLES2 / OpenGLCore）

`--platform gles3`（或包里没有 d3d11 时自动选中）走的是另一条翻译链路：
GL 平台上 Unity 存的就是 HLSLcc 输出的**GLSL 源码**，所以不需要反汇编。
第一段 `Cg/GlslToCg.cs` 逐行重写成 **Cg**（`tex2D`、`sampler2D`、`appdata`/`v2f`），
第二段 `Cg/CgSyntax.ToHlsl` 再把它语义翻译 + 方言重写成 HLSL——与 DXBC 路径
（`CgTranslator` 产出 Cg → `ToHlsl`）汇合于同一个第二段，所以两条路径产物同形态
（`HLSLPROGRAM` + appdata/v2f + 同样的变体 `#if` 链；`--syntax cg` 时跳过第二段，保留 `CGPROGRAM`）。

| | DXBC 路径 | GLSL 路径 |
|---|---|---|
| 输入 | `D3DDisassemble` 的汇编文本 | HLSLcc 的 GLSL 源码 |
| 翻译器 | `CgTranslator.cs`，逐条指令映射 → Cg | `GlslToCg.cs`，逐行重写 → Cg（此后同走 `ToHlsl`） |
| 变量名 | `r0` / `cb0[2].x`，靠常量缓冲反射命名 | `u_xlat0` 这类 HLSLcc 临时量，保留原名 |
| 矩阵 | 按 `MatrixNames` 展开成 `m[row]` 成员 | GLSL 原生 `mat4`，仍存成 4 个 vec4 行 |
| 语义 | 从 DXBC 签名推断 | HLSLcc 命名约定（`in_POSITION0`→POSITION0、`vs_TEXCOORD1`→TEXCOORD1） |
| 变体 | 每个 blob 一个子程序，按关键字拼 `#if` | 一个 slot 就是整个 pass（自带 VERTEX/FRAGMENT 分支），先切分支再翻 |

四个坑：与 CG 前端自动注入的 uniform（`unity_MatrixVP`、`_ScreenParams`…）
撞名时**不能改名**——早期 `xlat_` 前缀版本让矩阵永远不绑定，整只网格在游戏里消失；
正确做法是丢掉自己的声明、直接用 `UnityShaderVariables.cginc` 里的同名变量。
（`CGPROGRAM` 会自动引入 `HLSLSupport.cginc`，`HLSLPROGRAM` 不会——所以 `hlsl`
形态下由 `ShaderLabWriter` 显式 `#include "HLSLSupport.cginc"`，否则 `fixed`/`half` 报 X3004。）
另外 `#version` / `precision` 行不进程序块；GLSL 的单参向量构造（`float4(x)`）
要展开成 `float4(x,x,x,x)`；GLSL 的 `return;` 是「带着已写入的结果返回」，
翻译时不能直接 `return` 掉。


## 为什么不直接上现成反编译器（评估结论）

自研回译器之前，先试过两个现成工具。两者都能跑出可读的 HLSL，**都不能替代**当前路径——
不是因为产物语法不对，而是它们看不到 Unity 的 shader 元数据。

### glslcc（GLSL 路径，v1.7.6 win64，已实测）

GLSL 输入这条路，一个显而易见的替代方案是：不自己写回译器，直接把 HLSLcc 的 GLSL
喂给现成的 GLSL→HLSL 转译器。`glslcc` 是最像样的候选，结论是**不能替代**，
原因不在产物格式而在工具链假设。

`glslcc` 走的是 `glslang → SPIR-V (Vulkan) → SPIRV-Cross`，因此对输入提出 Unity
根本不满足的硬要求：

- 必须 `#version 450`，而 HLSLcc 产出的是 ES2/ES3 风格（`#version 300 es`、`precision` 行）；
- 非 opaque uniform 必须 UBO 化并给出 `layout(binding)`，采样器必须显式 `layout(binding)`，
  varying 必须 `layout(location)`——`asset` 里的源码这三样都没有；
- 顶点着色器在补齐 UBO 与 binding 之前根本无法通过。

就算按上述要求把输入全部改写（等于先做一遍语义重写，再让 glslcc 做第二遍），
产物仍有四处与 Unity 约定冲突：

1. 材质常量被摊平进 `_15_` 这类 cbuffer 前缀，Unity 的 `Properties` 绑定被切断；
2. 语义误映射：`in_COLOR0` 被写成 `TEXCOORD1`；
3. 采样器命名是 `__MainTex_sampler` + `register(s0)`，而 Unity 侧要的是
   `sampler_MainTex` / `UNITY_DECLARE_TEX2D`（见 `HLSLSupport.cginc:461`）；
4. 产物不再受本项目对变体 `#if` 链、`appdata`/`v2f` 结构与常量缓冲注释的控制。

相比之下现有的自研路径（`GlslToCg.cs` 逐行重写 → `CgSyntax.ToHlsl` 语义翻译 +
方言重写）直接在 HLSLcc 的原生方言上工作，不需要 `#version 450`、UBO 化或 binding，
且产物的变体结构与 DXBC 路径完全同形。

**支撑这一取舍的语料事实**：对 520 个 HLSLcc GLSL 文件、221,007 行做精确扫描，
跨行 `texture(...)` 调用 **0 例**、续行 **0 例**——所以「每行一条语句」在真实输入上严格成立，
逐行重写是安全的（这也是 `CgSyntax` 里那条输入假设的依据）。

### HLSL-Decompiler（DXBC 路径，v0.6，已实测）

[YYadorigi/HLSL-Decompiler](https://github.com/YYadorigi/HLSL-Decompiler) 直接支持
DXBC → HLSL，是 DXBC 路径最直接的替代候选。拿本项目的真实 DXBC 实测：`exit=0`，
产物 44 行、格式干净，`Texture2D` / `SamplerState` / `.Sample()` 都齐备。

它走 `DXBC → DXIL → SPIR-V → SPIRV-Cross(HLSL)`，因此产物带着 SPIRV-Cross 的印记，
而它只读得到 DXBC 字节码、读不到 Shader 资产里的元数据，于是：

| | 它的产物 | Unity 需要的 |
|---|---|---|
| 采样器 | `Texture2D<float4> T0 : register(t0); SamplerState S0 : register(s0);` | `sampler2D t0` → 按名绑定的 `_MainTex`，采样器为 `sampler_MainTex` |
| 语义 | 硬编码 `TEXCOORD1` / `TEXCOORD2` | 从 DXBC 签名 + 资产元数据推断的真实语义 |
| 入口结构 | `SPIRV_Cross_Input` / `frag_main()` | `appdata` / `v2f` / `vert` / `frag` |
| 常量 | 不解析 `cb#` | 结合 `m_ConstantBuffers` 命名的 `$Globals` 成员 |

关键差别在最后一条：现有 `CgTranslator` 能从 Shader 资产的 `m_ConstantBuffers` 把
`cb0[3]` 还原成 `_MainTex_ST` 这样的真实属性名，而 HLSL-Decompiler 只看得到寄存器编号，
无从得知材质属性叫什么。`T0` / `S0` 在我们的产物里是 `sampler2D t0` 加按名绑定的
`_MainTex`；`S0` 在 Unity 里根本绑不到材质采样器状态。

另外两个工程事实：仓库**没有 LICENSE 文件**（再分发有法律风险），且依赖一整包预编译
二进制（`dxcompiler.dll`、`dxil-spirv.exe`、`spirv-cross.exe`，约 12.8 MB）。

它与 glslcc 的定位一致：**面向图形调试的反编译器**，目标是「人能读懂单条 shader」；
本项目的目标是「产物能作为 Unity 工程资产编译运行」，必须保住属性名、语义与常量缓冲
绑定。目标不同，不能互换。

## 已知限制（如实说明）

1. **第二段假设「每行一条语句」** —— `CgSyntax.ToHlsl` 按行匹配调用并按行配平括号。
   两个上游都满足这个假设（`CgTranslator` 逐语句 `AppendLine`；`GlslToCg` 一行进一行出，
   其唯一的跨行改写 `CollapseMvp` 只合并不拆分），真实语料 520 文件 / 221,007 行里
   续行为 0。但它是**对输入的假设而非代码保证**：若某行括号不闭合，`RewriteCode`
   会直接退让（`close < 0` 时原样返回），该行剩余调用**不会被翻译且不会报错**。
2. **翻译是逐指令直译，不是优化还原** —— 结果等价但冗长：
   寄存器复用、常量折叠、共同子表达式都保留原样。若要接近人手写的 HLSL，
   需要额外的 SSA/表达式树优化（未做）。
3. **`cb#[]` 常量缓冲未命名** —— Unity 的 CBuffer 成员名（如 `unity_ObjectToWorld`）
   在 DXBC 里只是个索引，需结合 Shader 的 `m_ConstantBuffers` 元数据才能映射；
   当前保留了 `cb3[9]` 这种原始形式。
4. **类型系统是弱化的** —— 一律用 `float4` 临时寄存器 + `(int)` 强制转换，
   没有区分 int/uint/half。
5. **验证范围限于单文件编译** —— 产物已用真实 Unity 的 `UnityShaderCompiler`
   逐文件编译过（ADOFAI Windows 数据 63 个 Shader：d3d11 63/63、gles3 63/63 通过；
   Linux depot 398 个：14 → 7 个失败，无回归，且修好 8 个 3D 纹理被当 2D 采样的错译），
   但没有做整包 Import/实机渲染回归。GPU 驱动级行为（采样状态、纹理绑定）未实测。
6. **仍有少量 Shader 编译失败** —— 属历史遗留而非本特性引入：
   `unity_4LightPosX0` 等 Unity 内建 uniform 被重复声明（`CGPROGRAM` 下同样报错），
   以及个别 `u_xlat0` 未初始化就被读取的 GLSL 回译路径。
7. 本游戏是 d3d11；GL 平台走 `Cg/GlslToCg.cs` 的源码回译（见上），
   Metal / SPIR-V 没有对应翻译路径。
