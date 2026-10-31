# Correctness verification

Compiling is not the same as computing the same thing, so the tool is checked at three
levels: a round trip through a real Unity project, a semantic comparison against the
original bytecode, and a compile with Unity's own shader compiler.

Every harness lives in `scripts/` and runs offline. The measured numbers for the last
release are in the [README](../README.md#correctness-verification).
## Correctness verification

[`docs/verification/`](docs/verification/) holds one round trip through a Unity 6 test project: the source that
went into the project (`*_original.shader`), what this tool extracted back out (`*_ripper.shader`, with
per-instruction comments; `*_ripper_noasm.shader` is the same file without them), and the merged DXBC
disassembly of every compiled variant. Those two `_ripper` files are the artifacts of that run; the
current front end only annotates the file header (and writes `// TODO` for an instruction it cannot
translate), not every instruction. The sources ship with the repo, so the round trip can be repeated by
packing them yourself.

| shader | properties | variants | fragment instructions |
|---|---|---|---|
| `ShaderRipperTest/ExtremeFlow` | 37 | 1 | 1328 |
| `ShaderRipperTest/VariantMonster` | 20 | 60 | 84,425 total, 777–1911 per variant |

Every variant was recompiled with `d3dcompiler_47` at `ps_5_0`: 1/1 and 60/60 pass, 0 untranslated
instructions, 0 `TODO` markers, and control flow survives (ExtremeFlow: 3 loops, 5 exits between them,
2 `discard`s, `ddx`/`ddy` preserved).

### Semantic equivalence (`scripts/verify_logic.py`)

Compiling is not the same as computing the same thing. The check compiles the emitted HLSL back to
DXBC, disassembles it, and compares these invariants against the original disassembly:

1. no constant-buffer register disappears (the *declared* width is not an invariant -- fxc sizes a
   cbuffer from the rows actually read)
2. no constant-buffer row is read that the original never read
3. texture fetch count, from the **generated source** (fxc merges same-coordinate samples, so the
   compiled count is not the promise)
4. `sincos` sin/cos output count -- losing one means a register nothing writes
5. counts of instructions a compiler may not remove (`discard`, `ldms`, `gather`)
6. the immediate-constant set -- where a `1.0`-for-true comparison mask shows up

On a real shipping build (425 shaders, 3,263 programs, 3,195 DXBC programs): **3,195 / 3,195 pass**, and
`scripts/scan_unwritten.py` reports 0 reads of a never-written register. `scripts/usc_check.py`
(Unity's own `UnityShaderCompiler`) compiles them all.

```sh
py -3 scripts/verify_logic.py <export dir> -j 6     # DXBC route: logical equivalence
py -3 scripts/verify_glsl.py <export dir> -j 6      # GLSL route: payload vs emission
py -3 scripts/scan_unwritten.py <export dir>        # reads of unwritten registers
py -3 scripts/scan_nan.py <export dir>              # NaN masks escaping to a target
py -3 scripts/scan_props.py <Assets dir>            # Properties leaked into includes
py -3 scripts/usc_check.py <export dir> -u <Unity>  # Unity's own compiler
py -3 scripts/gen_grammar.py                        # rebuild the parsers from *.g4
```

The GLSL route has no second compiler to compare against, so the reference is the
payload itself: per stage it compares texture fetches, assignment targets and
their counts, comparison operators, and the intrinsics after normalising through
the translator's own rename table. 33/33 pass on that build, and a mutation test
(deleting one `Sample`, dropping one register write) is caught in both cases.

### Readability passes (pattern recognition)

Correct output should also look like the shader it came from. Three pattern
rewrites change the spelling without changing the program:

| Pass | Recognises | Emits |
|---|---|---|
| mask normalisation | per-write data flow: the value is read only by a branch, select, comparison or bitwise op before the lane is rewritten | `? 1.0f : 0.0` instead of `asfloat(0xFFFFFFFFu)` (0xFFFFFFFF is a NaN in IEEE 754, and any later multiply that misses the mask turns a colour into NaN) |
| matrix product | `mul r1,v.y,M[1]; mad r1,M[0],v.x,r1; mad r0,M[2],v.z,r1; add r0,r0,M[3]` | one `mul(M, v)`; the accumulator and the destination may be different registers, the last term may land on an output, and the completing row may sit a few instructions later (fxc schedules the work that feeds the vector in between) |
| `UnityObjectToClipPos` | a `mul(ObjectToWorld, v)` whose result feeds `mul(MatrixVP, ·)` | `o.pos = UnityObjectToClipPos(v.xyz)` — eight instructions become one |

**On `1.0f`**: it is not a blanket replacement. DXBC writes -1 for true, and programs
like `lt → iadd(-t,v) → itof → ge → and` read that mask NUMERICALLY — 1.0 there inverts
the ramp. The test is a per-write def-use walk: from the comparison forward until the
lane is rewritten, every read must be a branch, `movc`, comparison or bitwise op. Only
then are -1 and 1 indistinguishable and `1.0f` is safe (and no longer a NaN).

Per-write, not per-register: in real shaders the same lane holds a mask and then a
number (`if (r0.x) { r0.x = ... }`). A whole-register test rejects all of those — the
first attempt converted 0 of 4,123 masks. Def-use brings it to **144** (−96.5%).

**On re-read components**: a single `mul(M, v)` reads all four components at one
point in time, but fxc is free to overwrite one of them between the rows that read
the others. In `HoldShader` the sequence is

```hlsl
mul  r2,   r0.xxxx, VP[1]        // reads the OLD r0.x
mul  r0.x, v0.y,    O2W[1].w     // r0.x now holds the world-space w
mad  o0,   VP[3],   r0.xxxx, r1  // reads the NEW r0.x
```

Folding that into `mul(VP, float4(x, y, z, w))` would take the wrong `x`. So each
component records the register and lane it was read from, and one that is written
before the completing row is pinned into a local **where it still had its old
value**:

```hlsl
float pin0 = r0.x;
r0.x = mul(unity_ObjectToWorld, float4(v0.x, v0.y, v0.z, v0.w)).w;
o0.xyzw = mul(unity_MatrixVP, float4(r0.y, pin0, r1.z, r0.x));
```

This is also why such a shader does **not** become `UnityObjectToClipPos`: its clip
position is built from object-space `xyz` and the `w` of the object-to-world
product, which is not the same thing as transforming the position. The macro is
only emitted for an exact `mul(VP, mul(O2W, float4(v, 1.0)))` chain.

The matrices are declared differently per output path: a ShaderLab file pulls in
`UnityCG.cginc` and gets the `float4x4` declarations plus the macro, while the
standalone `--programs` file declares `float4 M[4]` rows (the layout the
disassembly reads), which `mul` does not accept — so that file carries its own
`UnityObjectToClipPos`, the same two multiplies written over the rows.

Folding is deliberately narrow: the four rows must come from one array with
indices exactly 0/1/2/3, every destination mask must be full `xyzw`, nothing may
write the intermediate register between the two products, the two products must be
adjacent, and the intermediate must be dead afterwards.

That last condition is measured, not assumed: collapsing
`mul r0, v, ObjectToWorld … mul r1, r0, MatrixVP` into one
`UnityObjectToClipPos` DELETES r0, and a vertex shader that also needs the world
position (`mul r0.y, r0.y, unity_MatrixV[1].z` for view-space depth) then reads an
uninitialised register. `scan_unwritten.py` went from 0 to 80 sites, which is how
the bug surfaced.

ExtremeFlow's if/else chain, before and after:

Input:

```hlsl
if (energy < 0.5)      accum *= 1.05;
else if (energy < 1.0) accum += float3(0.02, 0.01, 0.03);
else if (energy < 1.8) accum = lerp(accum, accum * _ColorC.rgb, 0.25);
else                   accum = saturate(accum) * (0.9 + 0.1 * sin(fl));

if (accum.g > 1.4) break;              // early exit out of the [loop]
```

Output:

```csharp
r3.xyw = float3(r4.x * 1.05, r4.y * 1.05, r4.z * 1.05);                       // arm 1
r6.xyz = float3((r3.z < 0.5) ? -1.0 : 0.0, (r3.z < 1.0) ? -1.0 : 0.0,
                (r3.z < 1.8) ? -1.0 : 0.0);                                   // the three tests, kept
r7.xyz = float3(mad(r4.x, _ColorC.x, -(r4.x)), ...);                          // lerp: accum*color - accum
r7.xyz = float3(mad(r7.x, 0.25, r4.x), ...);                                  //   ... * 0.25 + accum
r3.xyw = float3(r6.x != 0.0 ? r3.x : r5.x, ...);                              // the select tree
r2.w = (1.4 < r3.y) ? 1.0 : 0.0;
if (any(r2.w)) { r2.xyz = float3(r3.x, r3.y, r3.w); break; }                  // early exit rebuilt
```

The compiler flattened the branch chain into predicated selects and rewrote `lerp` into two `mad`s; the
transliteration keeps that shape instead of guessing the original `if/else` back. The `-1.0` is deliberate —
DXBC comparisons write an all-ones mask on true, and the ramp that consumes these values depends on that sign,
so a `1.0` there would silently invert the test. That is also why the output has more instructions than the
input (1328 vs 1097): fusion the compiler performed cannot be recovered, and inventing it would be a guess.

### Names and tags

Two kinds of identifier are re-spelled rather than copied, because the serialized data stores them in a form
that no hand-written shader uses:

- **ShaderLab tag casing.** The asset keeps tag keys upper-cased (`LIGHTMODE`, `FORWARDBASE`). Unity's parser
  is case-insensitive for the *key*, so the file still works, but the value is matched exactly — so
  `"LIGHTMODE" = "FORWARDBASE"` is written `"LightMode" = "ForwardBase"`. Every key in the documented set is
  canonicalised, and `true`/`false` become `True`/`False`.
- **Texture names.** Unity binds a sampler to its texture by name (`SamplerState sampler__MainTex`), and a bare
  `s1` is rejected outright. The name comes from the asset's own texture reflection; the two lists rarely line
  up — a `[PerRendererData]` texture is never in the table, and a variant can sample a built-in texture it has no
  property for — so the reflection names what it can and the remaining registers take the unmatched property
  names in declaration order. A texture the shader declares no property for keeps its positional `tN`.

