// Many-variant stress test: 60 compiled programs plus real control flow.
//   4 blend modes + (base)          -> 5
//   2 distortion modes + (base)     -> 3
//   ring shape + (base)             -> 2
//   glow pass + (base)              -> 2
//   5 * 3 * 2 * 2 = 60 variants
// Each variant also carries a uniform-driven loop, if/else chains, a texture
// driven split and two discards, so no variant compiles to a flat program.
Shader "ShaderRipperTest/VariantMonster"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise (RGBA)", 2D) = "gray" {}
        _DetailTex ("Detail (RGBA)", 2D) = "gray" {}
        _MaskTex ("Mask (RGB)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ColorB ("Secondary", Color) = (0.2, 0.55, 0.9, 1)
        _ColorC ("Accent", Color) = (1, 0.45, 0.2, 1)
        _Params ("Params (scale, strength, speed, bias)", Vector) = (5, 0.7, 0.6, 0.15)
        _Params2 ("Params2 (warp, contrast, emission, ringWidth)", Vector) = (0.8, 1.2, 0.9, 0.18)
        _Params3 ("Params3 (cells, threshold, pulse, freq)", Vector) = (10, 0.2, 0.5, 7)
        _Iterations ("Iterations", Range(0, 10)) = 5
        _Steps ("Steps", Range(1, 6)) = 3
        _WarpDepth ("Warp Depth", Range(0, 3)) = 1.2
        _Threshold ("Alpha Threshold", Range(0, 1)) = 0.05
        _Contrast ("Contrast", Range(0, 3)) = 1.1
        _Emission ("Emission", Range(0, 4)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1
        _ToggleDetail ("Detail", Float) = 1
        _ToggleBands ("Bands", Float) = 1
        _ToggleDither ("Dither", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 500

        Pass
        {
            Tags { "LightMode"="Always" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile __ MODE_ADD MODE_MUL MODE_SCREEN MODE_OVERLAY
            #pragma multi_compile __ DIST_WAVE DIST_SWIRL
            #pragma multi_compile __ SHAPE_RING
            #pragma multi_compile __ GLOW_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex, _NoiseTex, _DetailTex, _MaskTex;
            float4 _MainTex_ST;
            float4 _Color, _ColorB, _ColorC;
            float4 _Params, _Params2, _Params3;
            float _Iterations, _Steps, _WarpDepth, _Threshold, _Contrast, _Emission, _Saturation;
            float _ToggleDetail, _ToggleBands, _ToggleDither;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
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

            float fbm5 (float2 p)
            {
                float sum = 0.0, amp = 0.5;
                float2 q = p;
                [unroll] for (int i = 0; i < 5; i++)
                {
                    sum += amp * vnoise(q);
                    q = rot2(q, 0.61) * 2.11 + 7.3;
                    amp *= 0.55;
                }
                return sum;
            }

            float3 hueRotate (float3 c, float a)
            {
                float s = sin(a), co = cos(a);
                float3 k = float3(0.57735, 0.57735, 0.57735);
                return c * co + cross(k, c) * s + k * dot(k, c) * (1.0 - co);
            }

            float3 blendMode (float3 base, float3 top)
            {
            #if defined(MODE_ADD)
                return saturate(base + top);
            #elif defined(MODE_MUL)
                return base * top;
            #elif defined(MODE_SCREEN)
                return 1.0 - (1.0 - base) * (1.0 - top);
            #elif defined(MODE_OVERLAY)
                return lerp(2.0 * base * top, 1.0 - 2.0 * (1.0 - base) * (1.0 - top), step(0.5, base));
            #else
                return top;
            #endif
            }

            // uniform-driven loop shared by all variants
            float layerAccum (float2 p, int steps, float seed)
            {
                float acc = 0.0;
                [loop] for (int i = 0; i < steps; i++)
                {
                    float fi = (float)i + seed;
                    float2 dir = float2(cos(fi * 1.9), sin(fi * 1.9));
                    acc += fbm5(p + dir * (0.06 + fi * 0.02)) * (1.0 / (1.0 + fi));
                    if (acc > 1.1) break;
                }
                return acc;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Params.z;
                float2 uv = i.uv;
                float2 p = uv * _Params.x;

                float4 mask = tex2D(_MaskTex, uv);
                if (mask.a < _Threshold) discard;

                // ---- distortion -------------------------------------------
                float n1 = fbm5(p + t * 0.3);
                float n2 = fbm5(p * 1.9 - t * 0.21);
            #if defined(DIST_WAVE)
                p += float2(sin(p.y * 6.0 + t * 2.0), cos(p.x * 5.0 - t * 1.7)) * _Params.y * 0.25;
                p += (n1 - 0.5) * 0.4;
            #elif defined(DIST_SWIRL)
                float2 d = p - 0.5 * _Params.x;
                float r = length(d);
                float ang = atan2(d.y, d.x) + (1.0 - r) * 3.2 + t * 0.4;
                p = float2(cos(ang), sin(ang)) * r + n1 * 0.35;
            #else
                p += (n2 - 0.5) * _Params.y * 0.5;
            #endif

                // ---- shape mask -------------------------------------------
                float2 c = p / max(_Params.x, 1e-4);
                float2 d2 = c - 0.5;
            #if defined(SHAPE_RING)
                float r2 = length(d2);
                float mask2 = smoothstep(_Params2.w + 0.06, _Params2.w, r2)
                            * smoothstep(_Params2.w * 0.4, _Params2.w * 0.6, r2);
                mask2 *= 0.85 + 0.3 * sin(r2 * 24.0 - t * 3.0);
            #else
                float mask2 = smoothstep(0.55, 0.35, length(d2));
            #endif

                // ---- nested uniform loops with divergence -----------------
                int outer = (int)clamp(_Iterations, 0.0, 10.0);
                int inner = (int)clamp(_Steps, 1.0, 6.0);
                float3 accum = 0;
                [loop] for (int layer = 0; layer < outer; layer++)
                {
                    float fl = (float)layer;
                    float2 q = p * (1.0 + fl * 0.41) + fbm5(p * _WarpDepth + fl) * 0.4;
                    float energy = 0.0;
                    [loop] for (int s = 0; s < inner; s++)
                    {
                        float fs = (float)s;
                        float2 dir = rot2(float2(1, 0), fs * 1.4 + fl);
                        float cell = vnoise(q + dir * 0.27);
                        energy += abs(cell - 0.5) * 2.0;
                        if (energy > 2.2) continue;
                        accum += float3(cell, 1.0 - cell, cell * cell) * (0.6 / (1.0 + fs + fl));
                    }
                    if (energy < 0.6) accum *= 1.04;
                    else if (energy < 1.4) accum += float3(0.01, 0.02, 0.01);
                    else accum = saturate(accum) * 0.97;
                    if (accum.g > 1.5) break;
                }

                // ---- 8 taps of detail noise -------------------------------
                float4 acc = 0;
                if (_ToggleDetail > 0.5)
                {
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = (float)k * 0.7853982 + t * 0.25;
                        float2 tap = p + float2(cos(a), sin(a)) * (0.05 + 0.03 * (float)k);
                        acc += tex2D(_NoiseTex, tap * 0.5 + 0.5);
                    }
                    acc *= 0.125;
                }
                else
                {
                    acc = tex2D(_DetailTex, p * 0.5);
                }

                // ---- interference bands -----------------------------------
                float3 bands = 0;
                if (_ToggleBands > 0.5)
                {
                    [unroll] for (int m = 0; m < 6; m++)
                    {
                        float fm = _Params3.w * (0.3 + (float)m * 0.22);
                        float ph = (float)m * 1.11 + t;
                        bands.r += sin(p.x * fm + ph);
                        bands.g += cos(p.y * fm - ph);
                        bands.b += sin((p.x + p.y) * fm * 0.6 + ph);
                    }
                    bands = saturate(bands * 0.16 + 0.5);
                }
                else
                {
                    bands = frac(p.xyx * _Params3.x);
                }

                float3 col = lerp(_Color.rgb, _ColorB.rgb, saturate(n1 * 1.4));
                col = hueRotate(col, n2 * 1.6 + t * 0.15);
                col = blendMode(col, bands);
                col = lerp(col, col * (0.6 + 0.9 * acc.rgb), 0.55);
                col += _ColorC.rgb * accum.b * 0.35;

                float4 baseTex = tex2D(_MainTex, p * 0.5 + acc.rg * 0.05);
                col = lerp(col, col * baseTex.rgb * 1.5, 0.4);

                // ---- cell pattern -----------------------------------------
                float2 cp = frac(p * _Params3.x) - 0.5;
                float cellDist = length(cp);
                if (cellDist < _Params3.z * 0.5)
                    col += _ColorC.rgb * pow(saturate(1.0 - cellDist * 3.0), 2.0) * _Params3.z;
                else if (cellDist < 0.4)
                    col *= 0.92;

            #if defined(GLOW_ON)
                // extra halo: three more fbm evaluations per pixel
                float g1 = fbm5(p * 2.3 + t * 0.5);
                float g2 = fbm5(p * 4.1 - t * 0.37);
                float halo = pow(saturate(1.0 - length(d2) * 1.6), 2.0);
                col += _ColorB.rgb * halo * (g1 * 0.6 + g2 * 0.4) * 1.2;
                mask2 = saturate(mask2 + halo * 0.25);
                float extra = layerAccum(p * 1.7, (int)clamp(_Steps * 2.0, 1.0, 6.0), 0.3);
                col = lerp(col, col * (0.8 + 0.5 * extra), _Params2.x * 0.5);
            #endif

                // ---- grading ----------------------------------------------
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(lum.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col = saturate(col) * _Color.rgb * _Emission;
                if (_ToggleDither > 0.5)
                {
                    float d3 = hash21(uv * _ScreenParams.xy);
                    col += (d3 - 0.5) * (1.0 / 255.0);
                }

                float alpha = saturate(mask2 * (0.55 + 0.75 * n1)) * i.color.a * mask.g;
                if (alpha < _Threshold) discard;
                col *= i.color.rgb;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
