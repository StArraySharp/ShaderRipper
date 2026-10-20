# ShaderRipper — DXBC 汇编 / GLSL 源码 → CG/HLSL 翻译器

在「assets → DXBC → 反汇编 → ShaderLab」链路上，新增**汇编→CG 源码翻译**层；
GL 平台（GLES3/GLES2/OpenGLCore）则直接拿 HLSLcc 的 GLSL 源码回译成 Cg，
两条路径产出同形态的 `CGPROGRAM`（见文末「GLSL 输入」）。
258 个 Shader 的汇编指令 **100% 翻译完成，零 TODO**。

## 结果

```
unity=2019.3.0f6   shaders=258
done: shaders=258
remaining TODO: 0
```

- 覆盖 **59 种不同 opcode**
- 输出 `game_shaderlab_cg/_cg/*.cg`（每 Shader 一份，含全部 blob）
- 输出 `game_shaderlab_cg/*.shader`（CGPROGRAM 内嵌翻译后的 CG）

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

**输出（CG）**
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
| `src/ShaderRipper/Cg/CgTranslator.cs` | 汇编→CG 翻译器 |
| `src/ShaderRipper/Cg/GlslToCg.cs` | GLSL→CG 回译器（GLES3/GLES2/OpenGLCore 输入） |
| `src/ShaderRipper/Program.cs` | 编排（解析→翻译→输出） |
| `game_shaderlab_cg/_cg/*.cg` | 258 份翻译后的 CG |
| `game_shaderlab_cg/*.shader` | 含 CG 的 ShaderLab |

## GLSL 输入（GLES3 / GLES2 / OpenGLCore）

`--platform gles3`（或包里没有 d3d11 时自动选中）走的是另一条翻译链路：
GL 平台上 Unity 存的就是 HLSLcc 输出的**GLSL 源码**，所以不需要反汇编，
`Cg/GlslToCg.cs` 逐行重写回 Cg 即可，产物与 DXBC 路径同形态
（CGPROGRAM + appdata/v2f + 同样的变体 `#if` 链）。

| | DXBC 路径 | GLSL 路径 |
|---|---|---|
| 输入 | `D3DDisassemble` 的汇编文本 | HLSLcc 的 GLSL 源码 |
| 翻译器 | `CgTranslator.cs`，逐条指令映射 | `GlslToCg.cs`，逐行重写 |
| 变量名 | `r0` / `cb0[2].x`，靠常量缓冲反射命名 | `u_xlat0` 这类 HLSLcc 临时量，保留原名 |
| 矩阵 | 按 `MatrixNames` 展开成 `m[row]` 成员 | GLSL 原生 `mat4`，仍存成 4 个 vec4 行 |
| 语义 | 从 DXBC 签名推断 | HLSLcc 命名约定（`in_POSITION0`→POSITION0、`vs_TEXCOORD1`→TEXCOORD1） |
| 变体 | 每个 blob 一个子程序，按关键字拼 `#if` | 一个 slot 就是整个 pass（自带 VERTEX/FRAGMENT 分支），先切分支再翻 |

四个坑：与 CGPROGRAM 前端自动注入的 uniform（`unity_MatrixVP`、`_ScreenParams`…）
撞名时**不能改名**——早期 `xlat_` 前缀版本让矩阵永远不绑定，整只网格在游戏里消失；
正确做法是丢掉自己的声明、直接用 `UnityShaderVariables.cginc` 里的同名变量。
另外 `#version` / `precision` 行不进 CG；GLSL 的单参向量构造（`float4(x)`）
要展开成 `float4(x,x,x,x)`；GLSL 的 `return;` 是「带着已写入的结果返回」，
翻译时不能直接 `return` 掉。


## 已知限制（如实说明）

1. **翻译是逐指令直译，不是优化还原** —— 结果等价但冗长：
   寄存器复用、常量折叠、共同子表达式都保留原样。若要接近人手写的 HLSL，
   需要额外的 SSA/表达式树优化（未做）。
2. **`cb#[]` 常量缓冲未命名** —— Unity 的 CBuffer 成员名（如 `unity_ObjectToWorld`）
   在 DXBC 里只是个索引，需结合 Shader 的 `m_ConstantBuffers` 元数据才能映射；
   当前保留了 `cb3[9]` 这种原始形式。
3. **类型系统是弱化的** —— 一律用 `float4` 临时寄存器 + `(int)` 强制转换，
   没有区分 int/uint/half。
4. **未在 Unity 里编译验证** —— 生成的 CG 语法自洽，但没有实际 Import 测试。
5. 本游戏是 d3d11；GL 平台走 `Cg/GlslToCg.cs` 的源码回译（见上），
   Metal / SPIR-V 没有对应翻译路径。
