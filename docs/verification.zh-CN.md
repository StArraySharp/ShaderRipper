# 正确性验证

能编译不等于算得对，所以校验分三层：过一遍真实的 Unity 工程、与原始字节码做语义比对、
用 Unity 自己的着色器编译器编译。

每个 harness 都在 `scripts/` 下、离线可跑。最近一次发布实测的数字见
[README](../README.zh-CN.md#正确性验证)。
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
的计算」调度到中间，顺手覆盖掉其中一个。`HoldShader` 就是这个形状：

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

