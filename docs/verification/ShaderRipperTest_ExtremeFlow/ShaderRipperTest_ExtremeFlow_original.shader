// Maximum-complexity stress test, single variant.
//  - 37 material properties, every one of them feeds the math
//  - real control flow: dynamic (uniform-driven) loops, a [loop] outer pass,
//    break/continue, if/else chains, texture-driven divergence, early discard
//  - plus unrolled fbm layers so the straight-line part is heavy too
Shader "ShaderRipperTest/ExtremeFlow"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise (RGBA)", 2D) = "gray" {}
        _DetailTex ("Detail (RGBA)", 2D) = "gray" {}
        _MaskTex ("Mask (RGB)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _ColorA ("Color A", Color) = (0.10, 0.14, 0.32, 1)
        _ColorB ("Color B", Color) = (0.95, 0.70, 0.30, 1)
        _ColorC ("Color C", Color) = (0.30, 0.85, 0.80, 1)
        _ColorD ("Color D", Color) = (0.85, 0.25, 0.45, 1)
        _ColorE ("Color E", Color) = (0.05, 0.05, 0.08, 1)
        _Scroll ("Scroll", Vector) = (0.13, 0.71, 0.37, 0.91)
        _Params0 ("Params0 (scale, warpAmp, speed, bias)", Vector) = (6, 0.45, 0.6, 0.15)
        _Params1 ("Params1 (sharpness, contrast, gamma, sat)", Vector) = (0.6, 1.2, 2.2, 1.1)
        _Params2 ("Params2 (hue, vignette, emissive, fade)", Vector) = (0.35, 0.6, 0.8, 1)
        _Params3 ("Params3 (loopA, loopB, step, freq)", Vector) = (6, 3, 0.25, 9)
        _Params4 ("Params4 (edge, ring, glow, noiseScale)", Vector) = (0.35, 0.12, 0.7, 2.3)
        _Params5 ("Params5 (cell, threshold, pulse, mirror)", Vector) = (12, 0.25, 0.5, 1)
        _Iterations ("Iterations", Range(0, 12)) = 6
        _Layers ("Layers", Range(0, 4)) = 3
        _Steps ("Steps", Range(1, 8)) = 4
        _WarpDepth ("Warp Depth", Range(0, 3)) = 1.5
        _WaveFreq ("Wave Frequency", Float) = 5.5
        _WaveSpeed ("Wave Speed", Float) = 0.8
        _HueShift ("Hue Shift", Range(0, 6.28)) = 0.9
        _Sharpness ("Sharpness", Range(0.1, 8)) = 1.7
        _Contrast ("Contrast", Range(0, 3)) = 1.15
        _Gamma ("Gamma", Range(0.5, 4)) = 2.2
        _Saturation ("Saturation", Range(0, 3)) = 1.05
        _Vignette ("Vignette", Range(0, 2)) = 0.55
        _Emission ("Emission", Range(0, 4)) = 0.85
        _Threshold ("Alpha Threshold", Range(0, 1)) = 0.08
        _Pulse ("Pulse", Range(0, 1)) = 0.4
        _Mirror ("Mirror", Float) = 1
        _ToggleGlow ("Glow", Float) = 1
        _ToggleCell ("Cells", Float) = 1
        _ToggleNoise ("Noise Pass", Float) = 1
        _ToggleDither ("Dither", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 900

        Pass
        {
            Tags { "LightMode"="Always" }
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #include "UnityCG.cginc"

            sampler2D _MainTex, _NoiseTex, _DetailTex, _MaskTex;
            float4 _MainTex_ST;
            float4 _Tint;
            float4 _ColorA, _ColorB, _ColorC, _ColorD, _ColorE;
            float4 _Scroll, _Params0, _Params1, _Params2, _Params3, _Params4, _Params5;
            float _Iterations, _Layers, _Steps, _WarpDepth, _WaveFreq, _WaveSpeed;
            float _HueShift, _Sharpness, _Contrast, _Gamma, _Saturation, _Vignette;
            float _Emission, _Threshold, _Pulse, _Mirror;
            float _ToggleGlow, _ToggleCell, _ToggleNoise, _ToggleDither;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float4 color: COLOR;
                float3 wpos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float2 rot2 (float2 p, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float vnoise (float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // unrolled: six octaves land directly in the instruction stream
            float fbm6 (float2 p)
            {
                float sum = 0.0, amp = 0.5;
                float2 q = p;
                [unroll] for (int i = 0; i < 6; i++)
                {
                    sum += amp * vnoise(q);
                    q = rot2(q, 0.71) * 2.07 + 9.1;
                    amp *= 0.54;
                }
                return sum;
            }

            float3 hueRotate (float3 c, float a)
            {
                float s = sin(a), co = cos(a);
                float3 k = float3(0.57735, 0.57735, 0.57735);
                return c * co + cross(k, c) * s + k * dot(k, c) * (1.0 - co);
            }

            // uniform-driven loop, deliberately not unrolled
            float radialSum (float2 p, int steps)
            {
                float acc = 0.0;
                [loop] for (int i = 0; i < steps; i++)
                {
                    float fi = (float)i;
                    float2 dir = float2(cos(fi * 1.7), sin(fi * 1.7));
                    acc += fbm6(p + dir * (0.05 + fi * 0.03)) * (1.0 / (1.0 + fi));
                    if (acc > 0.9) break;            // early exit -> real branch
                }
                return acc;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                if (_Mirror > 0.5) uv = abs(uv * 2.0 - 1.0);

                // ---- early rejection (discard on mask + threshold) ---------
                float4 mask = tex2D(_MaskTex, uv);
                if (mask.a < _Threshold) discard;

                float2 p = uv * _Params0.x + _Scroll.xy * t * _Params0.z;

                // ---- nested uniform loops with divergent control flow -----
                int outer = (int)clamp(_Iterations, 0.0, 12.0);
                int inner = (int)clamp(_Steps, 1.0, 8.0);
                float3 accum = 0;
                [loop] for (int layer = 0; layer < outer; layer++)
                {
                    float fl = (float)layer;
                    float2 q = p * (1.0 + fl * 0.37);

                    // domain warp, depth controlled by a property
                    float warp = fbm6(q * _WarpDepth + fl);
                    q += (warp - 0.5) * _Params0.y;

                    // inner loop: different math per iteration, break on energy
                    float energy = 0.0;
                    [loop] for (int s = 0; s < inner; s++)
                    {
                        float fs = (float)s;
                        float2 dir = rot2(float2(1, 0), fs * 1.3 + fl);
                        float cell = vnoise(q * (1.0 + fs * 0.5) + dir * 0.31);
                        energy += abs(cell - 0.5) * 2.0;
                        if (energy > 2.5) continue;      // continue -> branch
                        accum += float3(cell, 1.0 - cell, cell * cell) * (0.5 / (1.0 + fs + fl));
                    }

                    // if/else chain: each arm is a different operator mix
                    if (energy < 0.5)
                        accum *= 1.05;
                    else if (energy < 1.0)
                        accum += float3(0.02, 0.01, 0.03);
                    else if (energy < 1.8)
                        accum = lerp(accum, accum * _ColorC.rgb, 0.25);
                    else
                        accum = saturate(accum) * (0.9 + 0.1 * sin(fl));

                    if (accum.g > 1.4) break;             // second early exit
                }

                // ---- texture-driven divergence ----------------------------
                float4 detail = tex2D(_DetailTex, p * 0.5);
                float3 col;
                if (detail.r > 0.66)
                {
                    col = _ColorA.rgb * (0.6 + accum.r);
                    col += _ColorB.rgb * pow(saturate(accum.g), 2.0);
                }
                else if (detail.r > 0.33)
                {
                    col = lerp(_ColorB.rgb, _ColorC.rgb, saturate(accum.b));
                    col *= 0.8 + 0.4 * accum.r;
                }
                else
                {
                    col = _ColorE.rgb + _ColorD.rgb * saturate(accum.g * 1.3);
                    col = lerp(col, _ColorC.rgb, saturate(length(accum) * 0.2));
                }

                // ---- 10 unrolled taps -------------------------------------
                float4 taps = 0;
                [unroll] for (int k = 0; k < 10; k++)
                {
                    float a = (float)k * 0.6283185 + t * _WaveSpeed;
                    float2 tp = p + float2(cos(a), sin(a)) * (0.04 + 0.02 * (float)k);
                    taps += tex2D(_NoiseTex, tp * 0.5 + 0.5);
                }
                taps *= 0.1;

                // ---- cell pattern toggle ----------------------------------
                if (_ToggleCell > 0.5)
                {
                    float2 cp = frac(p * _Params5.x) - 0.5;
                    float cellDist = length(cp);
                    col *= 0.75 + 0.5 * smoothstep(_Params4.y, _Params4.y + 0.08, cellDist);
                    col += _ColorD.rgb * pow(saturate(1.0 - cellDist * 2.0), 3.0) * _Pulse;
                }

                // ---- interference bands -----------------------------------
                float3 bands = 0;
                [unroll] for (int m = 0; m < 7; m++)
                {
                    float fm = _WaveFreq * (0.5 + (float)m * 0.31);
                    float ph = (float)m * 1.11 + t * _WaveSpeed;
                    bands.r += sin(p.x * fm + ph);
                    bands.g += cos(p.y * fm - ph);
                    bands.b += sin((p.x + p.y) * fm * 0.6 + ph);
                }
                bands = saturate(bands * 0.14 + 0.5);
                col = lerp(col, col * bands, 0.35);

                // ---- optional noise pass (uniform branch) ------------------
                if (_ToggleNoise > 0.5)
                {
                    float n = radialSum(p, (int)clamp(_Layers * 3.0, 1.0, 8.0));
                    col = lerp(col, col * (0.7 + 0.6 * n), _Params4.z);
                    col = max(col, fbm6(p * _Params4.w) * _ColorE.rgb * _Emission);
                }

                // ---- grading ----------------------------------------------
                col = hueRotate(col, _HueShift + (accum.r - 0.5));
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(lum.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col = pow(saturate(col), 1.0 / max(_Gamma, 0.01));
                col *= _Tint.rgb * i.color.rgb;

                float vig = saturate(1.0 - dot(uv - 0.5, uv - 0.5) * _Vignette * 2.0);
                col *= 0.5 + 0.7 * vig;

                if (_ToggleGlow > 0.5)
                {
                    float3 nrm = normalize(float3(ddx(col.r) * 40.0, ddy(col.r) * 40.0, 1.0));
                    float rim = pow(1.0 - saturate(nrm.z), 2.5);
                    col += _ColorB.rgb * rim * _Params4.z;
                }

                if (_ToggleDither > 0.5)
                {
                    float d = hash21(uv * _ScreenParams.xy);
                    col += (d - 0.5) * (1.0 / 255.0);
                }

                // ---- alpha ------------------------------------------------
                float alpha = saturate(mask.g * _Params2.w * (0.4 + 0.9 * taps.r)
                                       + _Sharpness * 0.1 - _Params5.y);
                if (alpha < _Threshold) discard;

                return fixed4(col * _Emission, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
