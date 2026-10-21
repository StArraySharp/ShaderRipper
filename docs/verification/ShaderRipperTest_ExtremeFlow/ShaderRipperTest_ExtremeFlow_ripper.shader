// Reconstructed ShaderLab by ShaderRipper
// pathID=13  platforms=[4,15]
Shader "ShaderRipperTest/ExtremeFlow"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_NoiseTex ("Noise (RGBA)", 2D) = "gray" {}
		_DetailTex ("Detail (RGBA)", 2D) = "gray" {}
		_MaskTex ("Mask (RGB)", 2D) = "white" {}
		_Tint ("Tint", Color) = (1,1,1,1)
		_ColorA ("Color A", Color) = (0.1,0.14,0.32,1)
		_ColorB ("Color B", Color) = (0.95,0.7,0.3,1)
		_ColorC ("Color C", Color) = (0.3,0.85,0.8,1)
		_ColorD ("Color D", Color) = (0.85,0.25,0.45,1)
		_ColorE ("Color E", Color) = (0.05,0.05,0.08,1)
		_Scroll ("Scroll", Vector) = (0.13,0.71,0.37,0.91)
		_Params0 ("Params0 (scale, warpAmp, speed, bias)", Vector) = (6,0.45,0.6,0.15)
		_Params1 ("Params1 (sharpness, contrast, gamma, sat)", Vector) = (0.6,1.2,2.2,1.1)
		_Params2 ("Params2 (hue, vignette, emissive, fade)", Vector) = (0.35,0.6,0.8,1)
		_Params3 ("Params3 (loopA, loopB, step, freq)", Vector) = (6,3,0.25,9)
		_Params4 ("Params4 (edge, ring, glow, noiseScale)", Vector) = (0.35,0.12,0.7,2.3)
		_Params5 ("Params5 (cell, threshold, pulse, mirror)", Vector) = (12,0.25,0.5,1)
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
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
		LOD 900

		Pass
		{
			Tags { "LightMode"="Always" }
			Cull Off
			Stencil
			{
				Ref 0
				ReadMask 255
				WriteMask 255
				Comp Always
			}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#pragma target 4.0
			// 1 keyword variant(s), 0 multi_compile line(s)
			// variants: no keywords: single variant

			// ---- variant 0 (base) ----
			#if 1
			struct appdata
			{
				float4  vertex       : POSITION;
				float2  uv0          : TEXCOORD0;
				float4  color        : COLOR0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float2  uv0          : TEXCOORD0;
				float4  color0       : COLOR0;
				float3  uv1          : TEXCOORD1;
			};

			sampler2D _MaskTex;
			sampler2D _DetailTex;
			sampler2D _NoiseTex;
			// cb0 = $Globals (material properties); 32 of them are named below
			float4 _MainTex_ST;
			float4 _Tint;
			float4 _ColorA;
			float4 _ColorB;
			float4 _ColorC;
			float4 _ColorD;
			float4 _ColorE;
			float4 _Scroll;
			float4 _Params0;
			float4 _Params2;
			float4 _Params4;
			float4 _Params5;
			float _Iterations;
			float _Layers;
			float _Steps;
			float _WarpDepth;
			float _WaveFreq;
			float _WaveSpeed;
			float _HueShift;
			float _Sharpness;
			float _Contrast;
			float _Gamma;
			float _Saturation;
			float _Vignette;
			float _Emission;
			float _Threshold;
			float _Pulse;
			float _Mirror;
			float _ToggleGlow;
			float _ToggleCell;
			float _ToggleNoise;
			float _ToggleDither;
			// $Globals cb0[21] = 21 row(s) / 336 bytes; 17 row(s) read, 17 named.
			//   read whole    : rows 2, 16, 17, 18, 19, 20
			//   read partial  : row 3.xyz row 4.xyz row 5.xyz row 6.xyz row 7.xyz row 8.xyz row 9.xy row 10.xyz row 12.w row 14.yzw row 15.xy
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			float4 _NoiseTex_ST;
			float4 _NoiseTex_TexelSize;
			float4 _DetailTex_ST;
			float4 _DetailTex_TexelSize;
			float4 _MaskTex_ST;
			float4 _MaskTex_TexelSize;
			float4 _Params1;
			float4 _Params3;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 slot(s) named from the compiled constant-buffer member table
			// $Globals cb0: 31 member(s) named from the asset's own constant-buffer reflection (336 bytes)

			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float2 v1 = v.uv0;
				float4 v2 = v.color;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.uv0
				float4 o2;  // -> o.color0
				float4 o3;  // -> o.uv1
				float4 r0 = 0, r1 = 0;

				// mul r0.xyzw, v0.yyyy, cb1[1].xyzw
				o0 = UnityObjectToClipPos(v0.xyz);
				// mad o1.xy, v1.xyxx, cb0[2].xyxx, cb0[2].zwzz
				o1.xy = float2(mad(v1.x, _MainTex_ST.x, _MainTex_ST.z), mad(v1.y, _MainTex_ST.y, _MainTex_ST.w));
				// mov o2.xyzw, v2.xyzw
				o2.xyzw = float4(v2.x, v2.y, v2.z, v2.w);

				v2f o;
				o.pos = o0;
				o.uv0 = o1.xy;
				o.color0 = o2;
				o.uv1 = o3.xyz;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float2 v1 = i.uv0;
				float4 v2 = i.color0;
				float3 v3 = i.uv1;
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0, r8 = 0;

				// lt r0.x, l(0.500000), cb0[19].w
				r0.x = (0.5 < _Mirror) ? 1.0 : 0.0;
				// mad r0.yz, v1.xxyx, l(0.000000, 2.000000, 2.000000, 0.000000), l(0.000000, -1.000000, -1.000000, 0.000000)
				r0.yz = float2(mad(v1.x, 2.0, -1.0), mad(v1.y, 2.0, -1.0));
				// movc r0.xy, r0.xxxx, |r0.yzyy|, v1.xyxx
				r0.xy = float2(r0.x != 0.0 ? abs(r0.y) : v1.x, r0.x != 0.0 ? abs(r0.z) : v1.y);
				// sample r1.xyzw, r0.xyxx, t0.xyzw, s2
				r1.xyzw = tex2D(_MaskTex, float2(r0.x, r0.y));
				// lt r0.z, r1.w, cb0[19].y
				r0.z = (r1.w < _Threshold) ? 1.0 : 0.0;
				// discard_nz r0.z
				if (any(r0.z)) discard;
				// mul r0.zw, cb0[9].xxxy, cb1[0].yyyy
				r0.zw = float2(_Scroll.x * _Time.y, _Scroll.y * _Time.y);
				// mul r0.zw, r0.zzzw, cb0[10].zzzz
				r0.zw = float2(r0.z * _Params0.z, r0.w * _Params0.z);
				// mad r0.zw, r0.xxxy, cb0[10].xxxx, r0.zzzw
				r0.zw = float2(mad(r0.x, _Params0.x, r0.z), mad(r0.y, _Params0.x, r0.w));
				// max r1.xz, cb0[16].xxzx, l(0.000000, 0.000000, 1.000000, 0.000000)
				r1.xz = float2(max(_Iterations, 0.0), max(_Steps, 1.0));
				// min r1.xz, r1.xxzx, l(12.000000, 0.000000, 8.000000, 0.000000)
				r1.xz = float2(min(r1.x, 12.0), min(r1.z, 8.0));
				// ftoi r1.xz, r1.xxzx
				r1.xz = float2(int(r1.x), int(r1.z));
				// mov r2.xyz, l(0,0,0,0)
				r2.xyz = float3(0.0, 0.0, 0.0);
				// mov r1.w, l(0)
				r1.w = 0.0;
					// loop
				for (;;) {
					// ige r2.w, r1.w, r1.x
					r2.w = (((int)(r1.w) >= (int)(r1.x)) ? 1 : 0);
					// breakc_nz r2.w
					if (any(r2.w)) break;
					// itof r2.w, r1.w
					r2.w = float(r1.w);
					// mad r3.x, r2.w, l(0.370000), l(1.000000)
					r3.x = mad(r2.w, 0.37, 1.0);
					// mul r3.xy, r0.zwzz, r3.xxxx
					r3.xy = float2(r0.z * r3.x, r0.w * r3.x);
					// mul r3.zw, r3.xxxy, cb0[16].wwww
					r3.zw = float2(r3.x * _WarpDepth, r3.y * _WarpDepth);
					// mad r4.xy, r3.xyxx, cb0[16].wwww, r2.wwww
					r4.xy = float2(mad(r3.x, _WarpDepth, r2.w), mad(r3.y, _WarpDepth, r2.w));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r3.zw, r3.zzzw
					r3.zw = float2(frac(r3.z), frac(r3.w));
					// mul r5.xy, r3.zwzz, r3.zwzz
					r5.xy = float2(r3.z * r3.z, r3.w * r3.w);
					// mad r3.zw, -r3.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r3.zw = float2(mad(-(r3.z), 2.0, 3.0), mad(-(r3.w), 2.0, 3.0));
					// mul r3.zw, r3.zzzw, r5.xxxy
					r3.zw = float2(r3.z * r5.x, r3.w * r5.y);
					// mul r5.xy, r4.zwzz, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r5.zw, r5.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
					r5.zw = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r5.z, r5.xyxx, r5.zwzz
					r5.z = r5.x * r5.z + r5.y * r5.w;
					// add r5.xy, r5.zzzz, r5.xyxx
					r5.xy = float2(r5.z + r5.x, r5.z + r5.y);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.y, r6.xyxx, r7.xyxx
					r5.y = r6.x * r7.x + r6.y * r7.y;
					// add r5.yz, r5.yyyy, r6.xxyx
					r5.yz = float2(r5.y + r6.x, r5.y + r6.y);
					// mul r5.y, r5.z, r5.y
					r5.y = r5.z * r5.y;
					// dp2 r5.z, r6.zwzz, r7.zwzz
					r5.z = r6.z * r7.z + r6.w * r7.w;
					// add r5.zw, r5.zzzz, r6.zzzw
					r5.zw = float2(r5.z + r6.z, r5.z + r6.w);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// frc r5.xyz, r5.xyzx
					r5.xyz = float3(frac(r5.x), frac(r5.y), frac(r5.z));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r5.w, r4.zwzz, r6.xyxx
					r5.w = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r5.wwww
					r4.zw = float2(r4.z + r5.w, r4.w + r5.w);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r5.x, r5.y
					r4.w = -(r5.x) + r5.y;
					// mad r4.w, r3.z, r4.w, r5.x
					r4.w = mad(r3.z, r4.w, r5.x);
					// add r4.z, -r5.z, r4.z
					r4.z = -(r5.z) + r4.z;
					// mad r3.z, r3.z, r4.z, r5.z
					r3.z = mad(r3.z, r4.z, r5.z);
					// add r3.z, -r4.w, r3.z
					r3.z = -(r4.w) + r3.z;
					// mad r3.z, r3.w, r3.z, r4.w
					r3.z = mad(r3.w, r3.z, r4.w);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mul r3.w, r3.w, l(0.270000)
					r3.w = r3.w * 0.27;
					// mad r3.z, r3.z, l(0.500000), r3.w
					r3.z = mad(r3.z, 0.5, r3.w);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mad r3.z, r3.w, l(0.145800), r3.z
					r3.z = mad(r3.w, 0.1458, r3.z);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mad r3.z, r3.w, l(0.078732), r3.z
					r3.z = mad(r3.w, 0.078732, r3.z);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mad r3.z, r3.w, l(0.042515), r3.z
					r3.z = mad(r3.w, 0.042515, r3.z);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.xy, r4.xyxx, r4.xyxx
					r5.xy = float2(r4.x * r4.x, r4.y * r4.y);
					// mad r4.xy, -r4.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r4.xy = float2(mad(-(r4.x), 2.0, 3.0), mad(-(r4.y), 2.0, 3.0));
					// mul r4.xy, r4.xyxx, r5.xyxx
					r4.xy = float2(r4.x * r5.x, r4.y * r5.y);
					// mul r5.xy, r4.zwzz, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r5.zw, r5.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
					r5.zw = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r3.w, r5.xyxx, r5.zwzz
					r3.w = r5.x * r5.z + r5.y * r5.w;
					// add r5.xy, r3.wwww, r5.xyxx
					r5.xy = float2(r3.w + r5.x, r3.w + r5.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r5.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r5.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r5.xyzw, r5.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r5.xyzw = float4(r5.x * 127.1, r5.y * 311.7, r5.z * 127.1, r5.w * 311.7);
					// frc r5.xyzw, r5.xyzw
					r5.xyzw = float4(frac(r5.x), frac(r5.y), frac(r5.z), frac(r5.w));
					// add r6.xyzw, r5.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r6.xyzw = float4(r5.x + 34.23, r5.y + 34.23, r5.z + 34.23, r5.w + 34.23);
					// dp2 r6.x, r5.xyxx, r6.xyxx
					r6.x = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r5.xyxx, r6.xxxx
					r5.xy = float2(r5.x + r6.x, r5.y + r6.x);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// dp2 r5.y, r5.zwzz, r6.zwzz
					r5.y = r5.z * r6.z + r5.w * r6.w;
					// add r5.yz, r5.yyyy, r5.zzwz
					r5.yz = float2(r5.y + r5.z, r5.y + r5.w);
					// mul r5.y, r5.z, r5.y
					r5.y = r5.z * r5.y;
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r5.zw, r4.zzzw, l(0.000000, 0.000000, 34.230000, 34.230000)
					r5.zw = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r5.z, r4.zwzz, r5.zwzz
					r5.z = r4.z * r5.z + r4.w * r5.w;
					// add r4.zw, r4.zzzw, r5.zzzz
					r4.zw = float2(r4.z + r5.z, r4.w + r5.z);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.x
					r4.w = -(r3.w) + r5.x;
					// mad r3.w, r4.x, r4.w, r3.w
					r3.w = mad(r4.x, r4.w, r3.w);
					// add r4.z, -r5.y, r4.z
					r4.z = -(r5.y) + r4.z;
					// mad r4.x, r4.x, r4.z, r5.y
					r4.x = mad(r4.x, r4.z, r5.y);
					// add r4.x, -r3.w, r4.x
					r4.x = -(r3.w) + r4.x;
					// mad r3.w, r4.y, r4.x, r3.w
					r3.w = mad(r4.y, r4.x, r3.w);
					// mad r3.z, r3.w, l(0.022958), r3.z
					r3.z = mad(r3.w, 0.022958, r3.z);
					// add r3.z, r3.z, l(-0.500000)
					r3.z = r3.z + -0.5;
					// mad r3.xy, r3.zzzz, cb0[10].yyyy, r3.xyxx
					r3.xy = float2(mad(r3.z, _Params0.y, r3.x), mad(r3.z, _Params0.y, r3.y));
					// mov r4.xyz, r2.xyzx
					r4.xyz = float3(r2.x, r2.y, r2.z);
					// mov r3.zw, l(0,0,0,0)
					r3.zw = float2(0.0, 0.0);
						// loop
					for (;;) {
						// ige r4.w, r3.w, r1.z
						r4.w = (((int)(r3.w) >= (int)(r1.z)) ? 1 : 0);
						// breakc_nz r4.w
						if (any(r4.w)) break;
						// itof r4.w, r3.w
						r4.w = float(r3.w);
						// mad r5.x, r4.w, l(1.300000), r2.w
						r5.x = mad(r4.w, 1.3, r2.w);
						// sincos r5.x, r6.x, r5.x
						r5.x = sin(r5.x);
						r6.x = cos(r5.x);
						// mad r5.y, r4.w, l(0.500000), l(1.000000)
						r5.y = mad(r4.w, 0.5, 1.0);
						// mov r6.y, r5.x
						r6.y = r5.x;
						// mul r5.xz, r6.xxyx, l(0.310000, 0.000000, 0.310000, 0.000000)
						r5.xz = float2(r6.x * 0.31, r6.y * 0.31);
						// mad r5.xy, r3.xyxx, r5.yyyy, r5.xzxx
						r5.xy = float2(mad(r3.x, r5.y, r5.x), mad(r3.y, r5.y, r5.z));
						// round_ni r5.zw, r5.xxxy
						r5.zw = float2(floor(r5.x), floor(r5.y));
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r5.zwzz, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r5.z * 127.1, r5.w * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r6.z, r6.xyxx, r6.zwzz
						r6.z = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r6.zzzz, r6.xyxx
						r6.xy = float2(r6.z + r6.x, r6.z + r6.y);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// add r7.xyzw, r5.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
						r7.xyzw = float4(r5.z + 1.0, r5.w + 0.0, r5.z + 0.0, r5.w + 1.0);
						// mul r7.xyzw, r7.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r7.xyzw = float4(r7.x * 127.1, r7.y * 311.7, r7.z * 127.1, r7.w * 311.7);
						// frc r7.xyzw, r7.xyzw
						r7.xyzw = float4(frac(r7.x), frac(r7.y), frac(r7.z), frac(r7.w));
						// add r8.xyzw, r7.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r8.xyzw = float4(r7.x + 34.23, r7.y + 34.23, r7.z + 34.23, r7.w + 34.23);
						// dp2 r6.y, r7.xyxx, r8.xyxx
						r6.y = r7.x * r8.x + r7.y * r8.y;
						// add r6.yz, r6.yyyy, r7.xxyx
						r6.yz = float2(r6.y + r7.x, r6.y + r7.y);
						// mul r6.y, r6.z, r6.y
						r6.y = r6.z * r6.y;
						// dp2 r6.z, r7.zwzz, r8.zwzz
						r6.z = r7.z * r8.z + r7.w * r8.w;
						// add r6.zw, r6.zzzz, r7.zzzw
						r6.zw = float2(r6.z + r7.z, r6.z + r7.w);
						// mul r6.z, r6.w, r6.z
						r6.z = r6.w * r6.z;
						// frc r6.xyz, r6.xyzx
						r6.xyz = float3(frac(r6.x), frac(r6.y), frac(r6.z));
						// add r5.zw, r5.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
						r5.zw = float2(r5.z + 1.0, r5.w + 1.0);
						// mul r5.zw, r5.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
						r5.zw = float2(r5.z * 127.1, r5.w * 311.7);
						// frc r5.zw, r5.zzzw
						r5.zw = float2(frac(r5.z), frac(r5.w));
						// add r7.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
						r7.xy = float2(r5.z + 34.23, r5.w + 34.23);
						// dp2 r6.w, r5.zwzz, r7.xyxx
						r6.w = r5.z * r7.x + r5.w * r7.y;
						// add r5.zw, r5.zzzw, r6.wwww
						r5.zw = float2(r5.z + r6.w, r5.w + r6.w);
						// mul r5.z, r5.w, r5.z
						r5.z = r5.w * r5.z;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// add r5.w, -r6.x, r6.y
						r5.w = -(r6.x) + r6.y;
						// mad r5.w, r5.x, r5.w, r6.x
						r5.w = mad(r5.x, r5.w, r6.x);
						// add r5.z, -r6.z, r5.z
						r5.z = -(r6.z) + r5.z;
						// mad r5.x, r5.x, r5.z, r6.z
						r5.x = mad(r5.x, r5.z, r6.z);
						// add r5.x, -r5.w, r5.x
						r5.x = -(r5.w) + r5.x;
						// mad r5.x, r5.y, r5.x, r5.w
						r5.x = mad(r5.y, r5.x, r5.w);
						// add r5.w, r5.x, l(-0.500000)
						r5.w = r5.x + -0.5;
						// mad r5.w, |r5.w|, l(2.000000), r3.z
						r5.w = mad(abs(r5.w), 2.0, r3.z);
						// lt r6.x, l(2.500000), r5.w
						r6.x = (2.5 < r5.w) ? 1.0 : 0.0;
							// if_nz r6.x
						if (any(r6.x)) {
							// iadd r6.x, r3.w, l(1)
							r6.x = ((int)(r3.w) + (int)(1.0));
							// mov r3.z, r5.w
							r3.z = r5.w;
							// mov r3.w, r6.x
							r3.w = r6.x;
							// continue
							continue;
						// endif
						}
						// mul r5.z, r5.x, r5.x
						r5.z = r5.x * r5.x;
						// add r4.w, r2.w, r4.w
						r4.w = r2.w + r4.w;
						// add r4.w, r4.w, l(1.000000)
						r4.w = r4.w + 1.0;
						// div r4.w, l(0.500000), r4.w
						r4.w = 0.5 / r4.w;
						// add r5.y, -r5.x, l(1.000000)
						r5.y = -(r5.x) + 1.0;
						// mad r4.xyz, r5.xyzx, r4.wwww, r4.xyzx
						r4.xyz = float3(mad(r5.x, r4.w, r4.x), mad(r5.y, r4.w, r4.y), mad(r5.z, r4.w, r4.z));
						// iadd r3.w, r3.w, l(1)
						r3.w = ((int)(r3.w) + (int)(1.0));
						// mov r3.z, r5.w
						r3.z = r5.w;
					// endloop
					}
					// mul r3.xyw, r4.xyxz, l(1.050000, 1.050000, 0.000000, 1.050000)
					r3.xyw = float3(r4.x * 1.05, r4.y * 1.05, r4.z * 1.05);
					// add r5.xyz, r4.xyzx, l(0.020000, 0.010000, 0.030000, 0.000000)
					r5.xyz = float3(r4.x + 0.02, r4.y + 0.01, r4.z + 0.03);
					// lt r6.xyz, r3.zzzz, l(0.500000, 1.000000, 1.800000, 0.000000)
					r6.xyz = float3((r3.z < 0.5) ? 1.0 : 0.0, (r3.z < 1.0) ? 1.0 : 0.0, (r3.z < 1.8) ? 1.0 : 0.0);
					// mad r7.xyz, r4.xyzx, cb0[6].xyzx, -r4.xyzx
					r7.xyz = float3(mad(r4.x, _ColorC.x, -(r4.x)), mad(r4.y, _ColorC.y, -(r4.y)), mad(r4.z, _ColorC.z, -(r4.z)));
					// mad r7.xyz, r7.xyzx, l(0.250000, 0.250000, 0.250000, 0.000000), r4.xyzx
					r7.xyz = float3(mad(r7.x, 0.25, r4.x), mad(r7.y, 0.25, r4.y), mad(r7.z, 0.25, r4.z));
					// mov_sat r4.xyz, r4.xyzx
					r4.xyz = float3(saturate(r4.x), saturate(r4.y), saturate(r4.z));
					// sincos r2.w, null, r2.w
					r2.w = sin(r2.w);
					// mad r2.w, r2.w, l(0.100000), l(0.900000)
					r2.w = mad(r2.w, 0.1, 0.9);
					// mul r8.xyz, r2.wwww, r4.xyzx
					r8.xyz = float3(r2.w * r4.x, r2.w * r4.y, r2.w * r4.z);
					// movc r7.xyz, r6.zzzz, r7.xyzx, r8.xyzx
					r7.xyz = float3(r6.z != 0.0 ? r7.x : r8.x, r6.z != 0.0 ? r7.y : r8.y, r6.z != 0.0 ? r7.z : r8.z);
					// movc r5.xyz, r6.yyyy, r5.xyzx, r7.xyzx
					r5.xyz = float3(r6.y != 0.0 ? r5.x : r7.x, r6.y != 0.0 ? r5.y : r7.y, r6.y != 0.0 ? r5.z : r7.z);
					// movc r3.xyw, r6.xxxx, r3.xyxw, r5.xyxz
					r3.xyw = float3(r6.x != 0.0 ? r3.x : r5.x, r6.x != 0.0 ? r3.y : r5.y, r6.x != 0.0 ? r3.w : r5.z);
					// lt r2.w, l(1.400000), r3.y
					r2.w = (1.4 < r3.y) ? 1.0 : 0.0;
						// if_nz r2.w
					if (any(r2.w)) {
						// mov r2.xyz, r3.xywx
						r2.xyz = float3(r3.x, r3.y, r3.w);
						// break
						break;
					// endif
					}
					// iadd r1.w, r1.w, l(1)
					r1.w = ((int)(r1.w) + (int)(1.0));
					// mov r2.xyz, r3.xywx
					r2.xyz = float3(r3.x, r3.y, r3.w);
				// endloop
				}
				// mul r1.xz, r0.zzwz, l(0.500000, 0.000000, 0.500000, 0.000000)
				r1.xz = float2(r0.z * 0.5, r0.w * 0.5);
				// sample r3.xyzw, r1.xzxx, t1.xyzw, s1
				r3.xyzw = tex2D(_DetailTex, float2(r1.x, r1.z));
				// add r1.x, r2.x, l(0.600000)
				r1.x = r2.x + 0.6;
				// mov_sat r1.zw, r2.yyyz
				r1.zw = float2(saturate(r2.y), saturate(r2.z));
				// mul r1.z, r1.z, r1.z
				r1.z = r1.z * r1.z;
				// mul r3.yzw, r1.zzzz, cb0[5].xxyz
				r3.yzw = float3(r1.z * _ColorB.x, r1.z * _ColorB.y, r1.z * _ColorB.z);
				// mad r3.yzw, cb0[4].xxyz, r1.xxxx, r3.yyzw
				r3.yzw = float3(mad(_ColorA.x, r1.x, r3.y), mad(_ColorA.y, r1.x, r3.z), mad(_ColorA.z, r1.x, r3.w));
				// lt r1.xz, l(0.660000, 0.000000, 0.330000, 0.000000), r3.xxxx
				r1.xz = float2((0.66 < r3.x) ? 1.0 : 0.0, (0.33 < r3.x) ? 1.0 : 0.0);
				// add r4.xyz, -cb0[5].xyzx, cb0[6].xyzx
				r4.xyz = float3(-(_ColorB.x) + _ColorC.x, -(_ColorB.y) + _ColorC.y, -(_ColorB.z) + _ColorC.z);
				// mad r4.xyz, r1.wwww, r4.xyzx, cb0[5].xyzx
				r4.xyz = float3(mad(r1.w, r4.x, _ColorB.x), mad(r1.w, r4.y, _ColorB.y), mad(r1.w, r4.z, _ColorB.z));
				// mul r3.x, r2.y, l(1.300000)
				r3.x = r2.y * 1.3;
				// mad r1.w, r2.x, l(0.400000), l(0.800000)
				r1.w = mad(r2.x, 0.4, 0.8);
				// mul r4.xyz, r1.wwww, r4.xyzx
				r4.xyz = float3(r1.w * r4.x, r1.w * r4.y, r1.w * r4.z);
				// mov_sat r3.x, r3.x
				r3.x = saturate(r3.x);
				// mad r5.xyz, cb0[7].xyzx, r3.xxxx, cb0[8].xyzx
				r5.xyz = float3(mad(_ColorD.x, r3.x, _ColorE.x), mad(_ColorD.y, r3.x, _ColorE.y), mad(_ColorD.z, r3.x, _ColorE.z));
				// dp3 r1.w, r2.xyzx, r2.xyzx
				r1.w = r2.x * r2.x + r2.y * r2.y + r2.z * r2.z;
				// sqrt r1.w, r1.w
				r1.w = sqrt(r1.w);
				// mul r1.w, r1.w, l(0.200000)
				r1.w = r1.w * 0.2;
				// min r1.w, r1.w, l(1.000000)
				r1.w = min(r1.w, 1.0);
				// add r2.yzw, -r5.xxyz, cb0[6].xxyz
				r2.yzw = float3(-(r5.x) + _ColorC.x, -(r5.y) + _ColorC.y, -(r5.z) + _ColorC.z);
				// mad r2.yzw, r1.wwww, r2.yyzw, r5.xxyz
				r2.yzw = float3(mad(r1.w, r2.y, r5.x), mad(r1.w, r2.z, r5.y), mad(r1.w, r2.w, r5.z));
				// movc r2.yzw, r1.zzzz, r4.xxyz, r2.yyzw
				r2.yzw = float3(r1.z != 0.0 ? r4.x : r2.y, r1.z != 0.0 ? r4.y : r2.z, r1.z != 0.0 ? r4.z : r2.w);
				// movc r1.xzw, r1.xxxx, r3.yyzw, r2.yyzw
				r1.xzw = float3(r1.x != 0.0 ? r3.y : r2.y, r1.x != 0.0 ? r3.z : r2.z, r1.x != 0.0 ? r3.w : r2.w);
				// mul r2.y, cb0[17].y, cb1[0].y
				r2.y = _WaveSpeed * _Time.y;
				// sincos r3.x, r4.x, r2.y
				r3.x = sin(r2.y);
				r4.x = cos(r2.y);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r2.zw, r4.xxxy, l(0.000000, 0.000000, 0.040000, 0.040000), r0.zzzw
				r2.zw = float2(mad(r4.x, 0.04, r0.z), mad(r4.y, 0.04, r0.w));
				// mad r2.zw, r2.zzzw, l(0.000000, 0.000000, 0.500000, 0.500000), l(0.000000, 0.000000, 0.500000, 0.500000)
				r2.zw = float2(mad(r2.z, 0.5, 0.5), mad(r2.w, 0.5, 0.5));
				// sample r3.xyzw, r2.zwzz, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r2.z, r2.w));
				// mad r4.xyzw, cb1[0].yyyy, cb0[17].yyyy, l(0.628318, 1.256637, 1.884955, 2.513274)
				r4.xyzw = float4(mad(_Time.y, _WaveSpeed, 0.628318), mad(_Time.y, _WaveSpeed, 1.256637), mad(_Time.y, _WaveSpeed, 1.884955), mad(_Time.y, _WaveSpeed, 2.513274));
				// sincos r4.x, r5.x, r4.x
				r4.x = sin(r4.x);
				r5.x = cos(r4.x);
				// mov r5.y, r4.x
				r5.y = r4.x;
				// mad r2.zw, r5.xxxy, l(0.000000, 0.000000, 0.060000, 0.060000), r0.zzzw
				r2.zw = float2(mad(r5.x, 0.06, r0.z), mad(r5.y, 0.06, r0.w));
				// mad r2.zw, r2.zzzw, l(0.000000, 0.000000, 0.500000, 0.500000), l(0.000000, 0.000000, 0.500000, 0.500000)
				r2.zw = float2(mad(r2.z, 0.5, 0.5), mad(r2.w, 0.5, 0.5));
				// sample r5.xyzw, r2.zwzz, t2.xyzw, s0
				r5.xyzw = tex2D(_NoiseTex, float2(r2.z, r2.w));
				// add r2.z, r3.x, r5.x
				r2.z = r3.x + r5.x;
				// sincos r3.x, r4.x, r4.y
				r3.x = sin(r4.y);
				r4.x = cos(r4.y);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.080000, 0.080000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.08, r0.z), mad(r4.y, 0.08, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// sincos r3.x, r4.x, r4.z
				r3.x = sin(r4.z);
				r4.x = cos(r4.z);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.100000, 0.100000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.1, r0.z), mad(r4.y, 0.1, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// sincos r3.x, r4.x, r4.w
				r3.x = sin(r4.w);
				r4.x = cos(r4.w);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.120000, 0.120000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.12, r0.z), mad(r4.y, 0.12, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// mad r3.xyzw, cb1[0].yyyy, cb0[17].yyyy, l(3.141593, 3.769911, 4.398230, 5.026548)
				r3.xyzw = float4(mad(_Time.y, _WaveSpeed, 3.141593), mad(_Time.y, _WaveSpeed, 3.769911), mad(_Time.y, _WaveSpeed, 4.39823), mad(_Time.y, _WaveSpeed, 5.026548));
				// sincos r3.x, r4.x, r3.x
				r3.x = sin(r3.x);
				r4.x = cos(r3.x);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r4.xy, r4.xyxx, l(0.140000, 0.140000, 0.000000, 0.000000), r0.zwzz
				r4.xy = float2(mad(r4.x, 0.14, r0.z), mad(r4.y, 0.14, r0.w));
				// mad r4.xy, r4.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r4.xy = float2(mad(r4.x, 0.5, 0.5), mad(r4.y, 0.5, 0.5));
				// sample r4.xyzw, r4.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r4.x, r4.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// sincos r3.x, r4.x, r3.y
				r3.x = sin(r3.y);
				r4.x = cos(r3.y);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.160000, 0.160000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.16, r0.z), mad(r4.y, 0.16, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r4.xyzw, r3.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// sincos r3.x, r4.x, r3.z
				r3.x = sin(r3.z);
				r4.x = cos(r3.z);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.180000, 0.180000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.18, r0.z), mad(r4.y, 0.18, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r4.xyzw, r3.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// sincos r3.x, r4.x, r3.w
				r3.x = sin(r3.w);
				r4.x = cos(r3.w);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.200000, 0.200000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.2, r0.z), mad(r4.y, 0.2, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// mad r3.xyzw, cb1[0].yyyy, cb0[17].yyyy, l(5.654866, 1.110000, 2.220000, 3.330000)
				r3.xyzw = float4(mad(_Time.y, _WaveSpeed, 5.654866), mad(_Time.y, _WaveSpeed, 1.11), mad(_Time.y, _WaveSpeed, 2.22), mad(_Time.y, _WaveSpeed, 3.33));
				// sincos r3.x, r4.x, r3.x
				r3.x = sin(r3.x);
				r4.x = cos(r3.x);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r4.xy, r4.xyxx, l(0.220000, 0.220000, 0.000000, 0.000000), r0.zwzz
				r4.xy = float2(mad(r4.x, 0.22, r0.z), mad(r4.y, 0.22, r0.w));
				// mad r4.xy, r4.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r4.xy = float2(mad(r4.x, 0.5, 0.5), mad(r4.y, 0.5, 0.5));
				// sample r4.xyzw, r4.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r4.x, r4.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// lt r4.xy, l(0.500000, 0.500000, 0.000000, 0.000000), cb0[20].yzyy
				r4.xy = float2((0.5 < _ToggleCell) ? 1.0 : 0.0, (0.5 < _ToggleNoise) ? 1.0 : 0.0);
				// mul r4.zw, r0.zzzw, cb0[15].xxxx
				r4.zw = float2(r0.z * _Params5.x, r0.w * _Params5.x);
				// frc r4.zw, r4.zzzw
				r4.zw = float2(frac(r4.z), frac(r4.w));
				// add r4.zw, r4.zzzw, l(0.000000, 0.000000, -0.500000, -0.500000)
				r4.zw = float2(r4.z + -0.5, r4.w + -0.5);
				// dp2 r2.w, r4.zwzz, r4.zwzz
				r2.w = r4.z * r4.z + r4.w * r4.w;
				// sqrt r2.w, r2.w
				r2.w = sqrt(r2.w);
				// add r3.x, r2.w, -cb0[14].y
				r3.x = r2.w + -(_Params4.y);
				// mul_sat r3.x, r3.x, l(12.500000)
				r3.x = saturate(r3.x * 12.5);
				// mad r4.z, r3.x, l(-2.000000), l(3.000000)
				r4.z = mad(r3.x, -2.0, 3.0);
				// mul r3.x, r3.x, r3.x
				r3.x = r3.x * r3.x;
				// mul r3.x, r3.x, r4.z
				r3.x = r3.x * r4.z;
				// mad r3.x, r3.x, l(0.500000), l(0.750000)
				r3.x = mad(r3.x, 0.5, 0.75);
				// mad r2.w, -r2.w, l(2.000000), l(1.000000)
				r2.w = mad(-(r2.w), 2.0, 1.0);
				// max r2.w, r2.w, l(0.000000)
				r2.w = max(r2.w, 0.0);
				// mul r4.z, r2.w, r2.w
				r4.z = r2.w * r2.w;
				// mul r2.w, r2.w, r4.z
				r2.w = r2.w * r4.z;
				// mul r5.xyz, r2.wwww, cb0[7].xyzx
				r5.xyz = float3(r2.w * _ColorD.x, r2.w * _ColorD.y, r2.w * _ColorD.z);
				// mul r5.xyz, r5.xyzx, cb0[19].zzzz
				r5.xyz = float3(r5.x * _Pulse, r5.y * _Pulse, r5.z * _Pulse);
				// mad r5.xyz, r1.xzwx, r3.xxxx, r5.xyzx
				r5.xyz = float3(mad(r1.x, r3.x, r5.x), mad(r1.z, r3.x, r5.y), mad(r1.w, r3.x, r5.z));
				// movc r1.xzw, r4.xxxx, r5.xxyz, r1.xxzw
				r1.xzw = float3(r4.x != 0.0 ? r5.x : r1.x, r4.x != 0.0 ? r5.y : r1.z, r4.x != 0.0 ? r5.z : r1.w);
				// mul r5.xyzw, cb0[17].xxxx, l(0.500000, 0.810000, 1.120000, 1.430000)
				r5.xyzw = float4(_WaveFreq * 0.5, _WaveFreq * 0.81, _WaveFreq * 1.12, _WaveFreq * 1.43);
				// mad r2.w, r0.z, r5.x, r2.y
				r2.w = mad(r0.z, r5.x, r2.y);
				// mad r3.x, r0.w, r5.x, -r2.y
				r3.x = mad(r0.w, r5.x, -(r2.y));
				// add r4.x, r0.w, r0.z
				r4.x = r0.w + r0.z;
				// mul r6.xyzw, r5.xyzw, r4.xxxx
				r6.xyzw = float4(r5.x * r4.x, r5.y * r4.x, r5.z * r4.x, r5.w * r4.x);
				// mad r2.y, r6.x, l(0.600000), r2.y
				r2.y = mad(r6.x, 0.6, r2.y);
				// sincos r2.yw, null, r2.yyyw
				r2.yw = sin(r2.yyyw);
				// mad r4.z, r0.z, r5.y, r3.y
				r4.z = mad(r0.z, r5.y, r3.y);
				// sincos r4.z, null, r4.z
				r4.z = sin(r4.z);
				// add r2.w, r2.w, r4.z
				r2.w = r2.w + r4.z;
				// mad r4.z, r0.w, r5.y, -r3.y
				r4.z = mad(r0.w, r5.y, -(r3.y));
				// add r3.x, r3.x, r4.z
				r3.x = r3.x + r4.z;
				// mad r6.xyz, r6.yzwy, l(0.600000, 0.600000, 0.600000, 0.000000), r3.yzwy
				r6.xyz = float3(mad(r6.y, 0.6, r3.y), mad(r6.z, 0.6, r3.z), mad(r6.w, 0.6, r3.w));
				// sincos r6.xyz, null, r6.xyzx
				r6.xyz = sin(r6.xyzx);
				// add r2.y, r2.y, r6.x
				r2.y = r2.y + r6.x;
				// mad r4.zw, r0.zzzz, r5.zzzw, r3.zzzw
				r4.zw = float2(mad(r0.z, r5.z, r3.z), mad(r0.z, r5.w, r3.w));
				// sincos r4.zw, null, r4.zzzw
				r4.zw = sin(r4.zzzw);
				// add r2.w, r2.w, r4.z
				r2.w = r2.w + r4.z;
				// mad r3.yz, r0.wwww, r5.zzwz, -r3.zzwz
				r3.yz = float2(mad(r0.w, r5.z, -(r3.z)), mad(r0.w, r5.w, -(r3.w)));
				// add r3.x, r3.y, r3.x
				r3.x = r3.y + r3.x;
				// add r2.y, r6.y, r2.y
				r2.y = r6.y + r2.y;
				// add r2.w, r4.w, r2.w
				r2.w = r4.w + r2.w;
				// add r3.x, r3.z, r3.x
				r3.x = r3.z + r3.x;
				// add r2.y, r6.z, r2.y
				r2.y = r6.z + r2.y;
				// mul r5.xyzw, cb0[17].xxxw, l(1.740000, 2.050000, 2.360000, 0.100000)
				r5.xyzw = float4(_WaveFreq * 1.74, _WaveFreq * 2.05, _WaveFreq * 2.36, _Sharpness * 0.1);
				// mad r3.yzw, cb1[0].yyyy, cb0[17].yyyy, l(0.000000, 4.440000, 5.550000, 6.660000)
				r3.yzw = float3(mad(_Time.y, _WaveSpeed, 4.44), mad(_Time.y, _WaveSpeed, 5.55), mad(_Time.y, _WaveSpeed, 6.66));
				// mad r4.zw, r0.zzzz, r5.xxxy, r3.yyyz
				r4.zw = float2(mad(r0.z, r5.x, r3.y), mad(r0.z, r5.y, r3.z));
				// sincos r4.zw, null, r4.zzzw
				r4.zw = sin(r4.zzzw);
				// add r2.w, r2.w, r4.z
				r2.w = r2.w + r4.z;
				// mad r6.xy, r0.wwww, r5.xyxx, -r3.yzyy
				r6.xy = float2(mad(r0.w, r5.x, -(r3.y)), mad(r0.w, r5.y, -(r3.z)));
				// add r3.x, r3.x, r6.x
				r3.x = r3.x + r6.x;
				// mul r6.xzw, r4.xxxx, r5.xxyz
				r6.xzw = float3(r4.x * r5.x, r4.x * r5.y, r4.x * r5.z);
				// mad r6.xzw, r6.xxzw, l(0.600000, 0.000000, 0.600000, 0.600000), r3.yyzw
				r6.xzw = float3(mad(r6.x, 0.6, r3.y), mad(r6.z, 0.6, r3.z), mad(r6.w, 0.6, r3.w));
				// sincos r6.xzw, null, r6.xxzw
				r6.xzw = sin(r6.xxzw);
				// add r2.y, r2.y, r6.x
				r2.y = r2.y + r6.x;
				// add r2.w, r4.w, r2.w
				r2.w = r4.w + r2.w;
				// add r3.x, r6.y, r3.x
				r3.x = r6.y + r3.x;
				// add r2.y, r6.z, r2.y
				r2.y = r6.z + r2.y;
				// mad r3.y, r0.z, r5.z, r3.w
				r3.y = mad(r0.z, r5.z, r3.w);
				// sincos r3.y, null, r3.y
				r3.y = sin(r3.y);
				// add r6.x, r2.w, r3.y
				r6.x = r2.w + r3.y;
				// mad r2.w, r0.w, r5.z, -r3.w
				r2.w = mad(r0.w, r5.z, -(r3.w));
				// add r6.y, r2.w, r3.x
				r6.y = r2.w + r3.x;
				// add r6.z, r6.w, r2.y
				r6.z = r6.w + r2.y;
				// mad_sat r3.xyz, r6.xyzx, l(0.140000, 0.140000, 0.140000, 0.000000), l(0.500000, 0.500000, 0.500000, 0.000000)
				r3.xyz = float3(saturate(mad(r6.x, 0.14, 0.5)), saturate(mad(r6.y, 0.14, 0.5)), saturate(mad(r6.z, 0.14, 0.5)));
				// mad r3.xyz, r1.xzwx, r3.xyzx, -r1.xzwx
				r3.xyz = float3(mad(r1.x, r3.x, -(r1.x)), mad(r1.z, r3.y, -(r1.z)), mad(r1.w, r3.z, -(r1.w)));
				// mad r1.xzw, r3.xxyz, l(0.350000, 0.000000, 0.350000, 0.350000), r1.xxzw
				r1.xzw = float3(mad(r3.x, 0.35, r1.x), mad(r3.y, 0.35, r1.z), mad(r3.z, 0.35, r1.w));
					// if_nz r4.y
				if (any(r4.y)) {
					// mul r2.y, cb0[16].y, l(3.000000)
					r2.y = _Layers * 3.0;
					// max r2.y, r2.y, l(1.000000)
					r2.y = max(r2.y, 1.0);
					// min r2.y, r2.y, l(8.000000)
					r2.y = min(r2.y, 8.0);
					// ftoi r2.y, r2.y
					r2.y = int(r2.y);
					// mov r2.w, l(0)
					r2.w = 0.0;
					// mov r3.x, l(0)
					r3.x = 0.0;
						// loop
					for (;;) {
						// ige r3.y, r3.x, r2.y
						r3.y = (((int)(r3.x) >= (int)(r2.y)) ? 1 : 0);
						// breakc_nz r3.y
						if (any(r3.y)) break;
						// itof r3.y, r3.x
						r3.y = float(r3.x);
						// mul r3.z, r3.y, l(1.700000)
						r3.z = r3.y * 1.7;
						// sincos r4.x, r5.x, r3.z
						r4.x = sin(r3.z);
						r5.x = cos(r3.z);
						// mad r3.z, r3.y, l(0.030000), l(0.050000)
						r3.z = mad(r3.y, 0.03, 0.05);
						// mov r5.y, r4.x
						r5.y = r4.x;
						// mad r3.zw, r5.xxxy, r3.zzzz, r0.zzzw
						r3.zw = float2(mad(r5.x, r3.z, r0.z), mad(r5.y, r3.z, r0.w));
						// round_ni r4.xy, r3.zwzz
						r4.xy = float2(floor(r3.z), floor(r3.w));
						// frc r4.zw, r3.zzzw
						r4.zw = float2(frac(r3.z), frac(r3.w));
						// mul r5.xy, r4.zwzz, r4.zwzz
						r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
						// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
						r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
						// mul r4.zw, r4.zzzw, r5.xxxy
						r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
						// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
						r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
						// dp2 r5.z, r5.xyxx, r6.xyxx
						r5.z = r5.x * r6.x + r5.y * r6.y;
						// add r5.xy, r5.zzzz, r5.xyxx
						r5.xy = float2(r5.z + r5.x, r5.z + r5.y);
						// mul r5.x, r5.y, r5.x
						r5.x = r5.y * r5.x;
						// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.y, r6.xyxx, r7.xyxx
						r5.y = r6.x * r7.x + r6.y * r7.y;
						// add r5.yz, r5.yyyy, r6.xxyx
						r5.yz = float2(r5.y + r6.x, r5.y + r6.y);
						// mul r5.y, r5.z, r5.y
						r5.y = r5.z * r5.y;
						// dp2 r5.z, r6.zwzz, r7.zwzz
						r5.z = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r5.zzzz, r6.zwzz
						r6.xy = float2(r5.z + r6.z, r5.z + r6.w);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.xyz, r5.xyzx
						r5.xyz = float3(frac(r5.x), frac(r5.y), frac(r5.z));
						// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
						r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
						// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
						r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
						// frc r4.xy, r4.xyxx
						r4.xy = float2(frac(r4.x), frac(r4.y));
						// add r6.xy, r4.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r4.x + 34.23, r4.y + 34.23);
						// dp2 r6.x, r4.xyxx, r6.xyxx
						r6.x = r4.x * r6.x + r4.y * r6.y;
						// add r4.xy, r4.xyxx, r6.xxxx
						r4.xy = float2(r4.x + r6.x, r4.y + r6.x);
						// mul r4.x, r4.y, r4.x
						r4.x = r4.y * r4.x;
						// frc r4.x, r4.x
						r4.x = frac(r4.x);
						// add r4.y, -r5.x, r5.y
						r4.y = -(r5.x) + r5.y;
						// mad r4.y, r4.z, r4.y, r5.x
						r4.y = mad(r4.z, r4.y, r5.x);
						// add r4.x, -r5.z, r4.x
						r4.x = -(r5.z) + r4.x;
						// mad r4.x, r4.z, r4.x, r5.z
						r4.x = mad(r4.z, r4.x, r5.z);
						// add r4.x, -r4.y, r4.x
						r4.x = -(r4.y) + r4.x;
						// mad r4.x, r4.w, r4.x, r4.y
						r4.x = mad(r4.w, r4.x, r4.y);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mul r4.y, r4.y, l(0.270000)
						r4.y = r4.y * 0.27;
						// mad r4.x, r4.x, l(0.500000), r4.y
						r4.x = mad(r4.x, 0.5, r4.y);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mad r4.x, r4.y, l(0.145800), r4.x
						r4.x = mad(r4.y, 0.1458, r4.x);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mad r4.x, r4.y, l(0.078732), r4.x
						r4.x = mad(r4.y, 0.078732, r4.x);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mad r4.x, r4.y, l(0.042515), r4.x
						r4.x = mad(r4.y, 0.042515, r4.x);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r3.zw, r3.zzzw
						r3.zw = float2(frac(r3.z), frac(r3.w));
						// mul r5.xy, r3.zwzz, r3.zwzz
						r5.xy = float2(r3.z * r3.z, r3.w * r3.w);
						// mad r3.zw, -r3.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
						r3.zw = float2(mad(-(r3.z), 2.0, 3.0), mad(-(r3.w), 2.0, 3.0));
						// mul r3.zw, r3.zzzw, r5.xxxy
						r3.zw = float2(r3.z * r5.x, r3.w * r5.y);
						// mul r5.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r5.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
						// dp2 r4.w, r5.xyxx, r6.xyxx
						r4.w = r5.x * r6.x + r5.y * r6.y;
						// add r5.xy, r4.wwww, r5.xyxx
						r5.xy = float2(r4.w + r5.x, r4.w + r5.y);
						// mul r4.w, r5.y, r5.x
						r4.w = r5.y * r5.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.x, r6.xyxx, r7.xyxx
						r5.x = r6.x * r7.x + r6.y * r7.y;
						// add r5.xy, r5.xxxx, r6.xyxx
						r5.xy = float2(r5.x + r6.x, r5.x + r6.y);
						// mul r5.x, r5.y, r5.x
						r5.x = r5.y * r5.x;
						// dp2 r5.y, r6.zwzz, r7.zwzz
						r5.y = r6.z * r7.z + r6.w * r7.w;
						// add r5.yz, r5.yyyy, r6.zzwz
						r5.yz = float2(r5.y + r6.z, r5.y + r6.w);
						// mul r5.y, r5.z, r5.y
						r5.y = r5.z * r5.y;
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.xy, r4.yzyy, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r5.z, r4.yzyy, r6.xyxx
						r5.z = r4.y * r6.x + r4.z * r6.y;
						// add r4.yz, r4.yyzy, r5.zzzz
						r4.yz = float2(r4.y + r5.z, r4.z + r5.z);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.x
						r4.z = -(r4.w) + r5.x;
						// mad r4.z, r3.z, r4.z, r4.w
						r4.z = mad(r3.z, r4.z, r4.w);
						// add r4.y, -r5.y, r4.y
						r4.y = -(r5.y) + r4.y;
						// mad r3.z, r3.z, r4.y, r5.y
						r3.z = mad(r3.z, r4.y, r5.y);
						// add r3.z, -r4.z, r3.z
						r3.z = -(r4.z) + r3.z;
						// mad r3.z, r3.w, r3.z, r4.z
						r3.z = mad(r3.w, r3.z, r4.z);
						// mad r3.z, r3.z, l(0.022958), r4.x
						r3.z = mad(r3.z, 0.022958, r4.x);
						// add r3.y, r3.y, l(1.000000)
						r3.y = r3.y + 1.0;
						// div r3.y, l(1.000000, 1.000000, 1.000000, 1.000000), r3.y
						r3.y = 1.0 / r3.y;
						// mad r3.y, r3.z, r3.y, r2.w
						r3.y = mad(r3.z, r3.y, r2.w);
						// lt r3.z, l(0.900000), r3.y
						r3.z = (0.9 < r3.y) ? 1.0 : 0.0;
							// if_nz r3.z
						if (any(r3.z)) {
							// mov r2.w, r3.y
							r2.w = r3.y;
							// break
							break;
						// endif
						}
						// iadd r3.x, r3.x, l(1)
						r3.x = ((int)(r3.x) + (int)(1.0));
						// mov r2.w, r3.y
						r2.w = r3.y;
					// endloop
					}
					// mad r2.y, r2.w, l(0.600000), l(0.700000)
					r2.y = mad(r2.w, 0.6, 0.7);
					// mad r3.xyz, r1.xzwx, r2.yyyy, -r1.xzwx
					r3.xyz = float3(mad(r1.x, r2.y, -(r1.x)), mad(r1.z, r2.y, -(r1.z)), mad(r1.w, r2.y, -(r1.w)));
					// mad r3.xyz, cb0[14].zzzz, r3.xyzx, r1.xzwx
					r3.xyz = float3(mad(_Params4.z, r3.x, r1.x), mad(_Params4.z, r3.y, r1.z), mad(_Params4.z, r3.z, r1.w));
					// mul r0.zw, r0.zzzw, cb0[14].wwww
					r0.zw = float2(r0.z * _Params4.w, r0.w * _Params4.w);
					// round_ni r2.yw, r0.zzzw
					r2.yw = float2(floor(r0.z), floor(r0.w));
					// frc r4.xy, r0.zwzz
					r4.xy = float2(frac(r0.z), frac(r0.w));
					// mul r4.zw, r4.xxxy, r4.xxxy
					r4.zw = float2(r4.x * r4.x, r4.y * r4.y);
					// mad r4.xy, -r4.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r4.xy = float2(mad(-(r4.x), 2.0, 3.0), mad(-(r4.y), 2.0, 3.0));
					// mul r4.xy, r4.xyxx, r4.zwzz
					r4.xy = float2(r4.x * r4.z, r4.y * r4.w);
					// mul r4.zw, r2.yyyw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r2.y * 127.1, r2.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r5.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r3.w, r4.zwzz, r5.xyxx
					r3.w = r4.z * r5.x + r4.w * r5.y;
					// add r4.zw, r3.wwww, r4.zzzw
					r4.zw = float2(r3.w + r4.z, r3.w + r4.w);
					// mul r3.w, r4.w, r4.z
					r3.w = r4.w * r4.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r2.ywyw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r2.y + 1.0, r2.w + 0.0, r2.y + 0.0, r2.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r4.z, r6.xyxx, r7.xyxx
					r4.z = r6.x * r7.x + r6.y * r7.y;
					// add r4.zw, r4.zzzz, r6.xxxy
					r4.zw = float2(r4.z + r6.x, r4.z + r6.y);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// dp2 r4.w, r6.zwzz, r7.zwzz
					r4.w = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r4.wwww, r6.zwzz
					r5.xy = float2(r4.w + r6.z, r4.w + r6.w);
					// mul r4.w, r5.y, r5.x
					r4.w = r5.y * r5.x;
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r2.yw, r2.yyyw, l(0.000000, 1.000000, 0.000000, 1.000000)
					r2.yw = float2(r2.y + 1.0, r2.w + 1.0);
					// mul r2.yw, r2.yyyw, l(0.000000, 127.099998, 0.000000, 311.700012)
					r2.yw = float2(r2.y * 127.1, r2.w * 311.7);
					// frc r2.yw, r2.yyyw
					r2.yw = float2(frac(r2.y), frac(r2.w));
					// add r5.xy, r2.ywyy, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r2.y + 34.23, r2.w + 34.23);
					// dp2 r5.x, r2.ywyy, r5.xyxx
					r5.x = r2.y * r5.x + r2.w * r5.y;
					// add r2.yw, r2.yyyw, r5.xxxx
					r2.yw = float2(r2.y + r5.x, r2.w + r5.x);
					// mul r2.y, r2.w, r2.y
					r2.y = r2.w * r2.y;
					// frc r2.y, r2.y
					r2.y = frac(r2.y);
					// add r2.w, -r3.w, r4.z
					r2.w = -(r3.w) + r4.z;
					// mad r2.w, r4.x, r2.w, r3.w
					r2.w = mad(r4.x, r2.w, r3.w);
					// add r2.y, -r4.w, r2.y
					r2.y = -(r4.w) + r2.y;
					// mad r2.y, r4.x, r2.y, r4.w
					r2.y = mad(r4.x, r2.y, r4.w);
					// add r2.y, -r2.w, r2.y
					r2.y = -(r2.w) + r2.y;
					// mad r2.y, r4.y, r2.y, r2.w
					r2.y = mad(r4.y, r2.y, r2.w);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mul r2.w, r2.w, l(0.270000)
					r2.w = r2.w * 0.27;
					// mad r2.y, r2.y, l(0.500000), r2.w
					r2.y = mad(r2.y, 0.5, r2.w);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mad r2.y, r2.w, l(0.145800), r2.y
					r2.y = mad(r2.w, 0.1458, r2.y);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mad r2.y, r2.w, l(0.078732), r2.y
					r2.y = mad(r2.w, 0.078732, r2.y);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mad r2.y, r2.w, l(0.042515), r2.y
					r2.y = mad(r2.w, 0.042515, r2.y);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r0.zw, r0.zzzw
					r0.zw = float2(frac(r0.z), frac(r0.w));
					// mul r4.zw, r0.zzzw, r0.zzzw
					r4.zw = float2(r0.z * r0.z, r0.w * r0.w);
					// mad r0.zw, -r0.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r0.zw = float2(mad(-(r0.z), 2.0, 3.0), mad(-(r0.w), 2.0, 3.0));
					// mul r0.zw, r0.zzzw, r4.zzzw
					r0.zw = float2(r0.z * r4.z, r0.w * r4.w);
					// mul r4.zw, r4.xxxy, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r5.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r2.w, r4.zwzz, r5.xyxx
					r2.w = r4.z * r5.x + r4.w * r5.y;
					// add r4.zw, r2.wwww, r4.zzzw
					r4.zw = float2(r2.w + r4.z, r2.w + r4.w);
					// mul r2.w, r4.w, r4.z
					r2.w = r4.w * r4.z;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r4.zw, r3.wwww, r6.xxxy
					r4.zw = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r4.w, r4.z
					r3.w = r4.w * r4.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r4.z, r6.zwzz, r7.zwzz
					r4.z = r6.z * r7.z + r6.w * r7.w;
					// add r4.zw, r4.zzzz, r6.zzzw
					r4.zw = float2(r4.z + r6.z, r4.z + r6.w);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xyz, r4.xyzx
					r4.xyz = float3(frac(r4.x), frac(r4.y), frac(r4.z));
					// add r5.xy, r4.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r4.w, r4.xyxx, r5.xyxx
					r4.w = r4.x * r5.x + r4.y * r5.y;
					// add r4.xy, r4.wwww, r4.xyxx
					r4.xy = float2(r4.w + r4.x, r4.w + r4.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r0.z, r3.w, r2.w
					r2.w = mad(r0.z, r3.w, r2.w);
					// add r3.w, -r4.z, r4.x
					r3.w = -(r4.z) + r4.x;
					// mad r0.z, r0.z, r3.w, r4.z
					r0.z = mad(r0.z, r3.w, r4.z);
					// add r0.z, -r2.w, r0.z
					r0.z = -(r2.w) + r0.z;
					// mad r0.z, r0.w, r0.z, r2.w
					r0.z = mad(r0.w, r0.z, r2.w);
					// mad r0.z, r0.z, l(0.022958), r2.y
					r0.z = mad(r0.z, 0.022958, r2.y);
					// mul r4.xyz, r0.zzzz, cb0[8].xyzx
					r4.xyz = float3(r0.z * _ColorE.x, r0.z * _ColorE.y, r0.z * _ColorE.z);
					// mul r4.xyz, r4.xyzx, cb0[19].xxxx
					r4.xyz = float3(r4.x * _Emission, r4.y * _Emission, r4.z * _Emission);
					// max r1.xzw, r3.xxyz, r4.xxyz
					r1.xzw = float3(max(r3.x, r4.x), max(r3.y, r4.y), max(r3.z, r4.z));
				// endif
				}
				// mul r0.z, r1.y, cb0[12].w
				r0.z = r1.y * _Params2.w;
				// mad r0.w, r2.z, l(0.090000), l(0.400000)
				r0.w = mad(r2.z, 0.09, 0.4);
				// mad r0.z, r0.z, r0.w, r5.w
				r0.z = mad(r0.z, r0.w, r5.w);
				// add_sat r0.z, r0.z, -cb0[15].y
				r0.z = saturate(r0.z + -(_Params5.y));
				// lt r0.w, r0.z, cb0[19].y
				r0.w = (r0.z < _Threshold) ? 1.0 : 0.0;
				// discard_nz r0.w
				if (any(r0.w)) discard;
				// add r0.w, r2.x, cb0[17].z
				r0.w = r2.x + _HueShift;
				// add r0.w, r0.w, l(-0.500000)
				r0.w = r0.w + -0.5;
				// sincos r2.x, r3.x, r0.w
				r2.x = sin(r0.w);
				r3.x = cos(r0.w);
				// mul r2.yzw, r1.wwxz, l(0.000000, 0.577350, 0.577350, 0.577350)
				r2.yzw = float3(r1.w * 0.57735, r1.x * 0.57735, r1.z * 0.57735);
				// mad r2.yzw, r1.wwxz, l(0.000000, 0.577350, 0.577350, 0.577350), -r2.wwyz
				r2.yzw = float3(mad(r1.w, 0.57735, -(r2.w)), mad(r1.x, 0.57735, -(r2.y)), mad(r1.z, 0.57735, -(r2.z)));
				// mul r2.xyz, r2.xxxx, r2.yzwy
				r2.xyz = float3(r2.x * r2.y, r2.x * r2.z, r2.x * r2.w);
				// mad r2.xyz, r1.xzwx, r3.xxxx, r2.xyzx
				r2.xyz = float3(mad(r1.x, r3.x, r2.x), mad(r1.z, r3.x, r2.y), mad(r1.w, r3.x, r2.z));
				// dp3 r0.w, l(0.577350, 0.577350, 0.577350, 0.000000), r1.xzwx
				r0.w = 0.57735 * r1.x + 0.57735 * r1.z + 0.57735 * r1.w;
				// mul r0.w, r0.w, l(0.577350)
				r0.w = r0.w * 0.57735;
				// add r1.x, -r3.x, l(1.000000)
				r1.x = -(r3.x) + 1.0;
				// mad r1.xyz, r0.wwww, r1.xxxx, r2.xyzx
				r1.xyz = float3(mad(r0.w, r1.x, r2.x), mad(r0.w, r1.x, r2.y), mad(r0.w, r1.x, r2.z));
				// dp3 r0.w, r1.xyzx, l(0.212600, 0.715200, 0.072200, 0.000000)
				r0.w = r1.x * 0.2126 + r1.y * 0.7152 + r1.z * 0.0722;
				// add r1.xyz, -r0.wwww, r1.xyzx
				r1.xyz = float3(-(r0.w) + r1.x, -(r0.w) + r1.y, -(r0.w) + r1.z);
				// mad r1.xyz, cb0[18].zzzz, r1.xyzx, r0.wwww
				r1.xyz = float3(mad(_Saturation, r1.x, r0.w), mad(_Saturation, r1.y, r0.w), mad(_Saturation, r1.z, r0.w));
				// add r1.xyz, r1.xyzx, l(-0.500000, -0.500000, -0.500000, 0.000000)
				r1.xyz = float3(r1.x + -0.5, r1.y + -0.5, r1.z + -0.5);
				// mad_sat r1.xyz, r1.xyzx, cb0[18].xxxx, l(0.500000, 0.500000, 0.500000, 0.000000)
				r1.xyz = float3(saturate(mad(r1.x, _Contrast, 0.5)), saturate(mad(r1.y, _Contrast, 0.5)), saturate(mad(r1.z, _Contrast, 0.5)));
				// max r0.w, cb0[18].y, l(0.010000)
				r0.w = max(_Gamma, 0.01);
				// div r0.w, l(1.000000, 1.000000, 1.000000, 1.000000), r0.w
				r0.w = 1.0 / r0.w;
				// log r1.xyz, r1.xyzx
				r1.xyz = float3(log2(r1.x), log2(r1.y), log2(r1.z));
				// mul r1.xyz, r0.wwww, r1.xyzx
				r1.xyz = float3(r0.w * r1.x, r0.w * r1.y, r0.w * r1.z);
				// exp r1.xyz, r1.xyzx
				r1.xyz = float3(exp2(r1.x), exp2(r1.y), exp2(r1.z));
				// mul r2.xyz, v2.xyzx, cb0[3].xyzx
				r2.xyz = float3(v2.x * _Tint.x, v2.y * _Tint.y, v2.z * _Tint.z);
				// mul r1.xyz, r1.xyzx, r2.xyzx
				r1.xyz = float3(r1.x * r2.x, r1.y * r2.y, r1.z * r2.z);
				// add r2.xy, r0.xyxx, l(-0.500000, -0.500000, 0.000000, 0.000000)
				r2.xy = float2(r0.x + -0.5, r0.y + -0.5);
				// dp2 r0.w, r2.xyxx, r2.xyxx
				r0.w = r2.x * r2.x + r2.y * r2.y;
				// dp2 r0.w, r0.wwww, cb0[18].wwww
				r0.w = r0.w * _Vignette + r0.w * _Vignette;
				// add_sat r0.w, -r0.w, l(1.000000)
				r0.w = saturate(-(r0.w) + 1.0);
				// mad r0.w, r0.w, l(0.700000), l(0.500000)
				r0.w = mad(r0.w, 0.7, 0.5);
				// mul r1.xyz, r0.wwww, r1.xyzx
				r1.xyz = float3(r0.w * r1.x, r0.w * r1.y, r0.w * r1.z);
				// lt r2.xy, l(0.500000, 0.500000, 0.000000, 0.000000), cb0[20].xwxx
				r2.xy = float2((0.5 < _ToggleGlow) ? 1.0 : 0.0, (0.5 < _ToggleDither) ? 1.0 : 0.0);
				// deriv_rtx r0.w, r1.x
				r0.w = ddx(r1.x);
				// mul r3.x, r0.w, l(40.000000)
				r3.x = r0.w * 40.0;
				// deriv_rty r0.w, r1.x
				r0.w = ddy(r1.x);
				// mul r3.y, r0.w, l(40.000000)
				r3.y = r0.w * 40.0;
				// mov r3.z, l(1.000000)
				r3.z = 1.0;
				// dp3 r0.w, r3.xyzx, r3.xyzx
				r0.w = r3.x * r3.x + r3.y * r3.y + r3.z * r3.z;
				// rsq r0.w, r0.w
				r0.w = rsqrt(r0.w);
				// add r0.w, -r0.w, l(1.000000)
				r0.w = -(r0.w) + 1.0;
				// log r0.w, r0.w
				r0.w = log2(r0.w);
				// mul r0.w, r0.w, l(2.500000)
				r0.w = r0.w * 2.5;
				// exp r0.w, r0.w
				r0.w = exp2(r0.w);
				// mul r3.xyz, r0.wwww, cb0[5].xyzx
				r3.xyz = float3(r0.w * _ColorB.x, r0.w * _ColorB.y, r0.w * _ColorB.z);
				// mad r3.xyz, r3.xyzx, cb0[14].zzzz, r1.xyzx
				r3.xyz = float3(mad(r3.x, _Params4.z, r1.x), mad(r3.y, _Params4.z, r1.y), mad(r3.z, _Params4.z, r1.z));
				// movc r1.xyz, r2.xxxx, r3.xyzx, r1.xyzx
				r1.xyz = float3(r2.x != 0.0 ? r3.x : r1.x, r2.x != 0.0 ? r3.y : r1.y, r2.x != 0.0 ? r3.z : r1.z);
				// mul r0.xy, r0.xyxx, cb1[6].xyxx
				r0.xy = float2(r0.x * _ScreenParams.x, r0.y * _ScreenParams.y);
				// mul r0.xy, r0.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
				r0.xy = float2(r0.x * 127.1, r0.y * 311.7);
				// frc r0.xy, r0.xyxx
				r0.xy = float2(frac(r0.x), frac(r0.y));
				// add r2.xz, r0.xxyx, l(34.230000, 0.000000, 34.230000, 0.000000)
				r2.xz = float2(r0.x + 34.23, r0.y + 34.23);
				// dp2 r0.w, r0.xyxx, r2.xzxx
				r0.w = r0.x * r2.x + r0.y * r2.z;
				// add r0.xy, r0.wwww, r0.xyxx
				r0.xy = float2(r0.w + r0.x, r0.w + r0.y);
				// mul r0.x, r0.y, r0.x
				r0.x = r0.y * r0.x;
				// frc r0.x, r0.x
				r0.x = frac(r0.x);
				// add r0.x, r0.x, l(-0.500000)
				r0.x = r0.x + -0.5;
				// mad r0.xyw, r0.xxxx, l(0.003922, 0.003922, 0.000000, 0.003922), r1.xyxz
				r0.xyw = float3(mad(r0.x, 0.003922, r1.x), mad(r0.x, 0.003922, r1.y), mad(r0.x, 0.003922, r1.z));
				// movc r0.xyw, r2.yyyy, r0.xyxw, r1.xyxz
				r0.xyw = float3(r2.y != 0.0 ? r0.x : r1.x, r2.y != 0.0 ? r0.y : r1.y, r2.y != 0.0 ? r0.w : r1.z);
				// mul o0.xyz, r0.xywx, cb0[19].xxxx
				o0.xyz = float3(r0.x * _Emission, r0.y * _Emission, r0.w * _Emission);
				// mov o0.w, r0.z
				o0.w = r0.z;
				return o0;
			}

			// ---- fallback: combinations Unity never compiled ----
			#else
			struct appdata
			{
				float4  vertex       : POSITION;
				float2  uv0          : TEXCOORD0;
				float4  color        : COLOR0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float2  uv0          : TEXCOORD0;
				float4  color0       : COLOR0;
				float3  uv1          : TEXCOORD1;
			};

			sampler2D _MaskTex;
			sampler2D _DetailTex;
			sampler2D _NoiseTex;
			// cb0 = $Globals (material properties); 32 of them are named below
			float4 _MainTex_ST;
			float4 _Tint;
			float4 _ColorA;
			float4 _ColorB;
			float4 _ColorC;
			float4 _ColorD;
			float4 _ColorE;
			float4 _Scroll;
			float4 _Params0;
			float4 _Params2;
			float4 _Params4;
			float4 _Params5;
			float _Iterations;
			float _Layers;
			float _Steps;
			float _WarpDepth;
			float _WaveFreq;
			float _WaveSpeed;
			float _HueShift;
			float _Sharpness;
			float _Contrast;
			float _Gamma;
			float _Saturation;
			float _Vignette;
			float _Emission;
			float _Threshold;
			float _Pulse;
			float _Mirror;
			float _ToggleGlow;
			float _ToggleCell;
			float _ToggleNoise;
			float _ToggleDither;
			// $Globals cb0[21] = 21 row(s) / 336 bytes; 17 row(s) read, 17 named.
			//   read whole    : rows 2, 16, 17, 18, 19, 20
			//   read partial  : row 3.xyz row 4.xyz row 5.xyz row 6.xyz row 7.xyz row 8.xyz row 9.xy row 10.xyz row 12.w row 14.yzw row 15.xy
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 slot(s) named from the compiled constant-buffer member table
			// $Globals cb0: 31 member(s) named from the asset's own constant-buffer reflection (336 bytes)

			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float2 v1 = v.uv0;
				float4 v2 = v.color;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.uv0
				float4 o2;  // -> o.color0
				float4 o3;  // -> o.uv1
				float4 r0 = 0, r1 = 0;

				// mul r0.xyzw, v0.yyyy, cb1[1].xyzw
				o0 = UnityObjectToClipPos(v0.xyz);
				// mad o1.xy, v1.xyxx, cb0[2].xyxx, cb0[2].zwzz
				o1.xy = float2(mad(v1.x, _MainTex_ST.x, _MainTex_ST.z), mad(v1.y, _MainTex_ST.y, _MainTex_ST.w));
				// mov o2.xyzw, v2.xyzw
				o2.xyzw = float4(v2.x, v2.y, v2.z, v2.w);

				v2f o;
				o.pos = o0;
				o.uv0 = o1.xy;
				o.color0 = o2;
				o.uv1 = o3.xyz;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float2 v1 = i.uv0;
				float4 v2 = i.color0;
				float3 v3 = i.uv1;
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0, r8 = 0;

				// lt r0.x, l(0.500000), cb0[19].w
				r0.x = (0.5 < _Mirror) ? 1.0 : 0.0;
				// mad r0.yz, v1.xxyx, l(0.000000, 2.000000, 2.000000, 0.000000), l(0.000000, -1.000000, -1.000000, 0.000000)
				r0.yz = float2(mad(v1.x, 2.0, -1.0), mad(v1.y, 2.0, -1.0));
				// movc r0.xy, r0.xxxx, |r0.yzyy|, v1.xyxx
				r0.xy = float2(r0.x != 0.0 ? abs(r0.y) : v1.x, r0.x != 0.0 ? abs(r0.z) : v1.y);
				// sample r1.xyzw, r0.xyxx, t0.xyzw, s2
				r1.xyzw = tex2D(_MaskTex, float2(r0.x, r0.y));
				// lt r0.z, r1.w, cb0[19].y
				r0.z = (r1.w < _Threshold) ? 1.0 : 0.0;
				// discard_nz r0.z
				if (any(r0.z)) discard;
				// mul r0.zw, cb0[9].xxxy, cb1[0].yyyy
				r0.zw = float2(_Scroll.x * _Time.y, _Scroll.y * _Time.y);
				// mul r0.zw, r0.zzzw, cb0[10].zzzz
				r0.zw = float2(r0.z * _Params0.z, r0.w * _Params0.z);
				// mad r0.zw, r0.xxxy, cb0[10].xxxx, r0.zzzw
				r0.zw = float2(mad(r0.x, _Params0.x, r0.z), mad(r0.y, _Params0.x, r0.w));
				// max r1.xz, cb0[16].xxzx, l(0.000000, 0.000000, 1.000000, 0.000000)
				r1.xz = float2(max(_Iterations, 0.0), max(_Steps, 1.0));
				// min r1.xz, r1.xxzx, l(12.000000, 0.000000, 8.000000, 0.000000)
				r1.xz = float2(min(r1.x, 12.0), min(r1.z, 8.0));
				// ftoi r1.xz, r1.xxzx
				r1.xz = float2(int(r1.x), int(r1.z));
				// mov r2.xyz, l(0,0,0,0)
				r2.xyz = float3(0.0, 0.0, 0.0);
				// mov r1.w, l(0)
				r1.w = 0.0;
					// loop
				for (;;) {
					// ige r2.w, r1.w, r1.x
					r2.w = (((int)(r1.w) >= (int)(r1.x)) ? 1 : 0);
					// breakc_nz r2.w
					if (any(r2.w)) break;
					// itof r2.w, r1.w
					r2.w = float(r1.w);
					// mad r3.x, r2.w, l(0.370000), l(1.000000)
					r3.x = mad(r2.w, 0.37, 1.0);
					// mul r3.xy, r0.zwzz, r3.xxxx
					r3.xy = float2(r0.z * r3.x, r0.w * r3.x);
					// mul r3.zw, r3.xxxy, cb0[16].wwww
					r3.zw = float2(r3.x * _WarpDepth, r3.y * _WarpDepth);
					// mad r4.xy, r3.xyxx, cb0[16].wwww, r2.wwww
					r4.xy = float2(mad(r3.x, _WarpDepth, r2.w), mad(r3.y, _WarpDepth, r2.w));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r3.zw, r3.zzzw
					r3.zw = float2(frac(r3.z), frac(r3.w));
					// mul r5.xy, r3.zwzz, r3.zwzz
					r5.xy = float2(r3.z * r3.z, r3.w * r3.w);
					// mad r3.zw, -r3.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r3.zw = float2(mad(-(r3.z), 2.0, 3.0), mad(-(r3.w), 2.0, 3.0));
					// mul r3.zw, r3.zzzw, r5.xxxy
					r3.zw = float2(r3.z * r5.x, r3.w * r5.y);
					// mul r5.xy, r4.zwzz, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r5.zw, r5.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
					r5.zw = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r5.z, r5.xyxx, r5.zwzz
					r5.z = r5.x * r5.z + r5.y * r5.w;
					// add r5.xy, r5.zzzz, r5.xyxx
					r5.xy = float2(r5.z + r5.x, r5.z + r5.y);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.y, r6.xyxx, r7.xyxx
					r5.y = r6.x * r7.x + r6.y * r7.y;
					// add r5.yz, r5.yyyy, r6.xxyx
					r5.yz = float2(r5.y + r6.x, r5.y + r6.y);
					// mul r5.y, r5.z, r5.y
					r5.y = r5.z * r5.y;
					// dp2 r5.z, r6.zwzz, r7.zwzz
					r5.z = r6.z * r7.z + r6.w * r7.w;
					// add r5.zw, r5.zzzz, r6.zzzw
					r5.zw = float2(r5.z + r6.z, r5.z + r6.w);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// frc r5.xyz, r5.xyzx
					r5.xyz = float3(frac(r5.x), frac(r5.y), frac(r5.z));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r5.w, r4.zwzz, r6.xyxx
					r5.w = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r5.wwww
					r4.zw = float2(r4.z + r5.w, r4.w + r5.w);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r5.x, r5.y
					r4.w = -(r5.x) + r5.y;
					// mad r4.w, r3.z, r4.w, r5.x
					r4.w = mad(r3.z, r4.w, r5.x);
					// add r4.z, -r5.z, r4.z
					r4.z = -(r5.z) + r4.z;
					// mad r3.z, r3.z, r4.z, r5.z
					r3.z = mad(r3.z, r4.z, r5.z);
					// add r3.z, -r4.w, r3.z
					r3.z = -(r4.w) + r3.z;
					// mad r3.z, r3.w, r3.z, r4.w
					r3.z = mad(r3.w, r3.z, r4.w);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mul r3.w, r3.w, l(0.270000)
					r3.w = r3.w * 0.27;
					// mad r3.z, r3.z, l(0.500000), r3.w
					r3.z = mad(r3.z, 0.5, r3.w);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mad r3.z, r3.w, l(0.145800), r3.z
					r3.z = mad(r3.w, 0.1458, r3.z);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mad r3.z, r3.w, l(0.078732), r3.z
					r3.z = mad(r3.w, 0.078732, r3.z);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r5.xy, r4.xyxx
					r5.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.zw, r5.xxxy, r5.xxxy
					r5.zw = float2(r5.x * r5.x, r5.y * r5.y);
					// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
					// mul r5.xy, r5.xyxx, r5.zwzz
					r5.xy = float2(r5.x * r5.z, r5.y * r5.w);
					// mul r5.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r5.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r6.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.z + 34.23, r5.w + 34.23);
					// dp2 r3.w, r5.zwzz, r6.xyxx
					r3.w = r5.z * r6.x + r5.w * r6.y;
					// add r5.zw, r3.wwww, r5.zzzw
					r5.zw = float2(r3.w + r5.z, r3.w + r5.w);
					// mul r3.w, r5.w, r5.z
					r3.w = r5.w * r5.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r5.z, r6.xyxx, r7.xyxx
					r5.z = r6.x * r7.x + r6.y * r7.y;
					// add r5.zw, r5.zzzz, r6.xxxy
					r5.zw = float2(r5.z + r6.x, r5.z + r6.y);
					// mul r5.z, r5.w, r5.z
					r5.z = r5.w * r5.z;
					// dp2 r5.w, r6.zwzz, r7.zwzz
					r5.w = r6.z * r7.z + r6.w * r7.w;
					// add r6.xy, r5.wwww, r6.zwzz
					r6.xy = float2(r5.w + r6.z, r5.w + r6.w);
					// mul r5.w, r6.y, r6.x
					r5.w = r6.y * r6.x;
					// frc r5.zw, r5.zzzw
					r5.zw = float2(frac(r5.z), frac(r5.w));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r6.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r6.x, r4.zwzz, r6.xyxx
					r6.x = r4.z * r6.x + r4.w * r6.y;
					// add r4.zw, r4.zzzw, r6.xxxx
					r4.zw = float2(r4.z + r6.x, r4.w + r6.x);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.z
					r4.w = -(r3.w) + r5.z;
					// mad r3.w, r5.x, r4.w, r3.w
					r3.w = mad(r5.x, r4.w, r3.w);
					// add r4.z, -r5.w, r4.z
					r4.z = -(r5.w) + r4.z;
					// mad r4.z, r5.x, r4.z, r5.w
					r4.z = mad(r5.x, r4.z, r5.w);
					// add r4.z, -r3.w, r4.z
					r4.z = -(r3.w) + r4.z;
					// mad r3.w, r5.y, r4.z, r3.w
					r3.w = mad(r5.y, r4.z, r3.w);
					// mad r3.z, r3.w, l(0.042515), r3.z
					r3.z = mad(r3.w, 0.042515, r3.z);
					// mul r3.w, r4.y, l(0.651834)
					r3.w = r4.y * 0.651834;
					// mad r5.x, r4.x, l(0.758362), -r3.w
					r5.x = mad(r4.x, 0.758362, -(r3.w));
					// dp2 r5.y, r4.xyxx, l(0.651834, 0.758362, 0.000000, 0.000000)
					r5.y = r4.x * 0.651834 + r4.y * 0.758362;
					// mad r4.xy, r5.xyxx, l(2.070000, 2.070000, 0.000000, 0.000000), l(9.100000, 9.100000, 0.000000, 0.000000)
					r4.xy = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
					// round_ni r4.zw, r4.xxxy
					r4.zw = float2(floor(r4.x), floor(r4.y));
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// mul r5.xy, r4.xyxx, r4.xyxx
					r5.xy = float2(r4.x * r4.x, r4.y * r4.y);
					// mad r4.xy, -r4.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r4.xy = float2(mad(-(r4.x), 2.0, 3.0), mad(-(r4.y), 2.0, 3.0));
					// mul r4.xy, r4.xyxx, r5.xyxx
					r4.xy = float2(r4.x * r5.x, r4.y * r5.y);
					// mul r5.xy, r4.zwzz, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r5.zw, r5.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
					r5.zw = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r3.w, r5.xyxx, r5.zwzz
					r3.w = r5.x * r5.z + r5.y * r5.w;
					// add r5.xy, r3.wwww, r5.xyxx
					r5.xy = float2(r3.w + r5.x, r3.w + r5.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r5.xyzw, r4.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r5.xyzw = float4(r4.z + 1.0, r4.w + 0.0, r4.z + 0.0, r4.w + 1.0);
					// mul r5.xyzw, r5.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r5.xyzw = float4(r5.x * 127.1, r5.y * 311.7, r5.z * 127.1, r5.w * 311.7);
					// frc r5.xyzw, r5.xyzw
					r5.xyzw = float4(frac(r5.x), frac(r5.y), frac(r5.z), frac(r5.w));
					// add r6.xyzw, r5.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r6.xyzw = float4(r5.x + 34.23, r5.y + 34.23, r5.z + 34.23, r5.w + 34.23);
					// dp2 r6.x, r5.xyxx, r6.xyxx
					r6.x = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r5.xyxx, r6.xxxx
					r5.xy = float2(r5.x + r6.x, r5.y + r6.x);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// dp2 r5.y, r5.zwzz, r6.zwzz
					r5.y = r5.z * r6.z + r5.w * r6.w;
					// add r5.yz, r5.yyyy, r5.zzwz
					r5.yz = float2(r5.y + r5.z, r5.y + r5.w);
					// mul r5.y, r5.z, r5.y
					r5.y = r5.z * r5.y;
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r4.zw, r4.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
					r4.zw = float2(r4.z + 1.0, r4.w + 1.0);
					// mul r4.zw, r4.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.z * 127.1, r4.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r5.zw, r4.zzzw, l(0.000000, 0.000000, 34.230000, 34.230000)
					r5.zw = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r5.z, r4.zwzz, r5.zwzz
					r5.z = r4.z * r5.z + r4.w * r5.w;
					// add r4.zw, r4.zzzw, r5.zzzz
					r4.zw = float2(r4.z + r5.z, r4.w + r5.z);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// frc r4.z, r4.z
					r4.z = frac(r4.z);
					// add r4.w, -r3.w, r5.x
					r4.w = -(r3.w) + r5.x;
					// mad r3.w, r4.x, r4.w, r3.w
					r3.w = mad(r4.x, r4.w, r3.w);
					// add r4.z, -r5.y, r4.z
					r4.z = -(r5.y) + r4.z;
					// mad r4.x, r4.x, r4.z, r5.y
					r4.x = mad(r4.x, r4.z, r5.y);
					// add r4.x, -r3.w, r4.x
					r4.x = -(r3.w) + r4.x;
					// mad r3.w, r4.y, r4.x, r3.w
					r3.w = mad(r4.y, r4.x, r3.w);
					// mad r3.z, r3.w, l(0.022958), r3.z
					r3.z = mad(r3.w, 0.022958, r3.z);
					// add r3.z, r3.z, l(-0.500000)
					r3.z = r3.z + -0.5;
					// mad r3.xy, r3.zzzz, cb0[10].yyyy, r3.xyxx
					r3.xy = float2(mad(r3.z, _Params0.y, r3.x), mad(r3.z, _Params0.y, r3.y));
					// mov r4.xyz, r2.xyzx
					r4.xyz = float3(r2.x, r2.y, r2.z);
					// mov r3.zw, l(0,0,0,0)
					r3.zw = float2(0.0, 0.0);
						// loop
					for (;;) {
						// ige r4.w, r3.w, r1.z
						r4.w = (((int)(r3.w) >= (int)(r1.z)) ? 1 : 0);
						// breakc_nz r4.w
						if (any(r4.w)) break;
						// itof r4.w, r3.w
						r4.w = float(r3.w);
						// mad r5.x, r4.w, l(1.300000), r2.w
						r5.x = mad(r4.w, 1.3, r2.w);
						// sincos r5.x, r6.x, r5.x
						r5.x = sin(r5.x);
						r6.x = cos(r5.x);
						// mad r5.y, r4.w, l(0.500000), l(1.000000)
						r5.y = mad(r4.w, 0.5, 1.0);
						// mov r6.y, r5.x
						r6.y = r5.x;
						// mul r5.xz, r6.xxyx, l(0.310000, 0.000000, 0.310000, 0.000000)
						r5.xz = float2(r6.x * 0.31, r6.y * 0.31);
						// mad r5.xy, r3.xyxx, r5.yyyy, r5.xzxx
						r5.xy = float2(mad(r3.x, r5.y, r5.x), mad(r3.y, r5.y, r5.z));
						// round_ni r5.zw, r5.xxxy
						r5.zw = float2(floor(r5.x), floor(r5.y));
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r5.zwzz, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r5.z * 127.1, r5.w * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r6.z, r6.xyxx, r6.zwzz
						r6.z = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r6.zzzz, r6.xyxx
						r6.xy = float2(r6.z + r6.x, r6.z + r6.y);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// add r7.xyzw, r5.zwzw, l(1.000000, 0.000000, 0.000000, 1.000000)
						r7.xyzw = float4(r5.z + 1.0, r5.w + 0.0, r5.z + 0.0, r5.w + 1.0);
						// mul r7.xyzw, r7.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r7.xyzw = float4(r7.x * 127.1, r7.y * 311.7, r7.z * 127.1, r7.w * 311.7);
						// frc r7.xyzw, r7.xyzw
						r7.xyzw = float4(frac(r7.x), frac(r7.y), frac(r7.z), frac(r7.w));
						// add r8.xyzw, r7.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r8.xyzw = float4(r7.x + 34.23, r7.y + 34.23, r7.z + 34.23, r7.w + 34.23);
						// dp2 r6.y, r7.xyxx, r8.xyxx
						r6.y = r7.x * r8.x + r7.y * r8.y;
						// add r6.yz, r6.yyyy, r7.xxyx
						r6.yz = float2(r6.y + r7.x, r6.y + r7.y);
						// mul r6.y, r6.z, r6.y
						r6.y = r6.z * r6.y;
						// dp2 r6.z, r7.zwzz, r8.zwzz
						r6.z = r7.z * r8.z + r7.w * r8.w;
						// add r6.zw, r6.zzzz, r7.zzzw
						r6.zw = float2(r6.z + r7.z, r6.z + r7.w);
						// mul r6.z, r6.w, r6.z
						r6.z = r6.w * r6.z;
						// frc r6.xyz, r6.xyzx
						r6.xyz = float3(frac(r6.x), frac(r6.y), frac(r6.z));
						// add r5.zw, r5.zzzw, l(0.000000, 0.000000, 1.000000, 1.000000)
						r5.zw = float2(r5.z + 1.0, r5.w + 1.0);
						// mul r5.zw, r5.zzzw, l(0.000000, 0.000000, 127.099998, 311.700012)
						r5.zw = float2(r5.z * 127.1, r5.w * 311.7);
						// frc r5.zw, r5.zzzw
						r5.zw = float2(frac(r5.z), frac(r5.w));
						// add r7.xy, r5.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
						r7.xy = float2(r5.z + 34.23, r5.w + 34.23);
						// dp2 r6.w, r5.zwzz, r7.xyxx
						r6.w = r5.z * r7.x + r5.w * r7.y;
						// add r5.zw, r5.zzzw, r6.wwww
						r5.zw = float2(r5.z + r6.w, r5.w + r6.w);
						// mul r5.z, r5.w, r5.z
						r5.z = r5.w * r5.z;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// add r5.w, -r6.x, r6.y
						r5.w = -(r6.x) + r6.y;
						// mad r5.w, r5.x, r5.w, r6.x
						r5.w = mad(r5.x, r5.w, r6.x);
						// add r5.z, -r6.z, r5.z
						r5.z = -(r6.z) + r5.z;
						// mad r5.x, r5.x, r5.z, r6.z
						r5.x = mad(r5.x, r5.z, r6.z);
						// add r5.x, -r5.w, r5.x
						r5.x = -(r5.w) + r5.x;
						// mad r5.x, r5.y, r5.x, r5.w
						r5.x = mad(r5.y, r5.x, r5.w);
						// add r5.w, r5.x, l(-0.500000)
						r5.w = r5.x + -0.5;
						// mad r5.w, |r5.w|, l(2.000000), r3.z
						r5.w = mad(abs(r5.w), 2.0, r3.z);
						// lt r6.x, l(2.500000), r5.w
						r6.x = (2.5 < r5.w) ? 1.0 : 0.0;
							// if_nz r6.x
						if (any(r6.x)) {
							// iadd r6.x, r3.w, l(1)
							r6.x = ((int)(r3.w) + (int)(1.0));
							// mov r3.z, r5.w
							r3.z = r5.w;
							// mov r3.w, r6.x
							r3.w = r6.x;
							// continue
							continue;
						// endif
						}
						// mul r5.z, r5.x, r5.x
						r5.z = r5.x * r5.x;
						// add r4.w, r2.w, r4.w
						r4.w = r2.w + r4.w;
						// add r4.w, r4.w, l(1.000000)
						r4.w = r4.w + 1.0;
						// div r4.w, l(0.500000), r4.w
						r4.w = 0.5 / r4.w;
						// add r5.y, -r5.x, l(1.000000)
						r5.y = -(r5.x) + 1.0;
						// mad r4.xyz, r5.xyzx, r4.wwww, r4.xyzx
						r4.xyz = float3(mad(r5.x, r4.w, r4.x), mad(r5.y, r4.w, r4.y), mad(r5.z, r4.w, r4.z));
						// iadd r3.w, r3.w, l(1)
						r3.w = ((int)(r3.w) + (int)(1.0));
						// mov r3.z, r5.w
						r3.z = r5.w;
					// endloop
					}
					// mul r3.xyw, r4.xyxz, l(1.050000, 1.050000, 0.000000, 1.050000)
					r3.xyw = float3(r4.x * 1.05, r4.y * 1.05, r4.z * 1.05);
					// add r5.xyz, r4.xyzx, l(0.020000, 0.010000, 0.030000, 0.000000)
					r5.xyz = float3(r4.x + 0.02, r4.y + 0.01, r4.z + 0.03);
					// lt r6.xyz, r3.zzzz, l(0.500000, 1.000000, 1.800000, 0.000000)
					r6.xyz = float3((r3.z < 0.5) ? 1.0 : 0.0, (r3.z < 1.0) ? 1.0 : 0.0, (r3.z < 1.8) ? 1.0 : 0.0);
					// mad r7.xyz, r4.xyzx, cb0[6].xyzx, -r4.xyzx
					r7.xyz = float3(mad(r4.x, _ColorC.x, -(r4.x)), mad(r4.y, _ColorC.y, -(r4.y)), mad(r4.z, _ColorC.z, -(r4.z)));
					// mad r7.xyz, r7.xyzx, l(0.250000, 0.250000, 0.250000, 0.000000), r4.xyzx
					r7.xyz = float3(mad(r7.x, 0.25, r4.x), mad(r7.y, 0.25, r4.y), mad(r7.z, 0.25, r4.z));
					// mov_sat r4.xyz, r4.xyzx
					r4.xyz = float3(saturate(r4.x), saturate(r4.y), saturate(r4.z));
					// sincos r2.w, null, r2.w
					r2.w = sin(r2.w);
					// mad r2.w, r2.w, l(0.100000), l(0.900000)
					r2.w = mad(r2.w, 0.1, 0.9);
					// mul r8.xyz, r2.wwww, r4.xyzx
					r8.xyz = float3(r2.w * r4.x, r2.w * r4.y, r2.w * r4.z);
					// movc r7.xyz, r6.zzzz, r7.xyzx, r8.xyzx
					r7.xyz = float3(r6.z != 0.0 ? r7.x : r8.x, r6.z != 0.0 ? r7.y : r8.y, r6.z != 0.0 ? r7.z : r8.z);
					// movc r5.xyz, r6.yyyy, r5.xyzx, r7.xyzx
					r5.xyz = float3(r6.y != 0.0 ? r5.x : r7.x, r6.y != 0.0 ? r5.y : r7.y, r6.y != 0.0 ? r5.z : r7.z);
					// movc r3.xyw, r6.xxxx, r3.xyxw, r5.xyxz
					r3.xyw = float3(r6.x != 0.0 ? r3.x : r5.x, r6.x != 0.0 ? r3.y : r5.y, r6.x != 0.0 ? r3.w : r5.z);
					// lt r2.w, l(1.400000), r3.y
					r2.w = (1.4 < r3.y) ? 1.0 : 0.0;
						// if_nz r2.w
					if (any(r2.w)) {
						// mov r2.xyz, r3.xywx
						r2.xyz = float3(r3.x, r3.y, r3.w);
						// break
						break;
					// endif
					}
					// iadd r1.w, r1.w, l(1)
					r1.w = ((int)(r1.w) + (int)(1.0));
					// mov r2.xyz, r3.xywx
					r2.xyz = float3(r3.x, r3.y, r3.w);
				// endloop
				}
				// mul r1.xz, r0.zzwz, l(0.500000, 0.000000, 0.500000, 0.000000)
				r1.xz = float2(r0.z * 0.5, r0.w * 0.5);
				// sample r3.xyzw, r1.xzxx, t1.xyzw, s1
				r3.xyzw = tex2D(_DetailTex, float2(r1.x, r1.z));
				// add r1.x, r2.x, l(0.600000)
				r1.x = r2.x + 0.6;
				// mov_sat r1.zw, r2.yyyz
				r1.zw = float2(saturate(r2.y), saturate(r2.z));
				// mul r1.z, r1.z, r1.z
				r1.z = r1.z * r1.z;
				// mul r3.yzw, r1.zzzz, cb0[5].xxyz
				r3.yzw = float3(r1.z * _ColorB.x, r1.z * _ColorB.y, r1.z * _ColorB.z);
				// mad r3.yzw, cb0[4].xxyz, r1.xxxx, r3.yyzw
				r3.yzw = float3(mad(_ColorA.x, r1.x, r3.y), mad(_ColorA.y, r1.x, r3.z), mad(_ColorA.z, r1.x, r3.w));
				// lt r1.xz, l(0.660000, 0.000000, 0.330000, 0.000000), r3.xxxx
				r1.xz = float2((0.66 < r3.x) ? 1.0 : 0.0, (0.33 < r3.x) ? 1.0 : 0.0);
				// add r4.xyz, -cb0[5].xyzx, cb0[6].xyzx
				r4.xyz = float3(-(_ColorB.x) + _ColorC.x, -(_ColorB.y) + _ColorC.y, -(_ColorB.z) + _ColorC.z);
				// mad r4.xyz, r1.wwww, r4.xyzx, cb0[5].xyzx
				r4.xyz = float3(mad(r1.w, r4.x, _ColorB.x), mad(r1.w, r4.y, _ColorB.y), mad(r1.w, r4.z, _ColorB.z));
				// mul r3.x, r2.y, l(1.300000)
				r3.x = r2.y * 1.3;
				// mad r1.w, r2.x, l(0.400000), l(0.800000)
				r1.w = mad(r2.x, 0.4, 0.8);
				// mul r4.xyz, r1.wwww, r4.xyzx
				r4.xyz = float3(r1.w * r4.x, r1.w * r4.y, r1.w * r4.z);
				// mov_sat r3.x, r3.x
				r3.x = saturate(r3.x);
				// mad r5.xyz, cb0[7].xyzx, r3.xxxx, cb0[8].xyzx
				r5.xyz = float3(mad(_ColorD.x, r3.x, _ColorE.x), mad(_ColorD.y, r3.x, _ColorE.y), mad(_ColorD.z, r3.x, _ColorE.z));
				// dp3 r1.w, r2.xyzx, r2.xyzx
				r1.w = r2.x * r2.x + r2.y * r2.y + r2.z * r2.z;
				// sqrt r1.w, r1.w
				r1.w = sqrt(r1.w);
				// mul r1.w, r1.w, l(0.200000)
				r1.w = r1.w * 0.2;
				// min r1.w, r1.w, l(1.000000)
				r1.w = min(r1.w, 1.0);
				// add r2.yzw, -r5.xxyz, cb0[6].xxyz
				r2.yzw = float3(-(r5.x) + _ColorC.x, -(r5.y) + _ColorC.y, -(r5.z) + _ColorC.z);
				// mad r2.yzw, r1.wwww, r2.yyzw, r5.xxyz
				r2.yzw = float3(mad(r1.w, r2.y, r5.x), mad(r1.w, r2.z, r5.y), mad(r1.w, r2.w, r5.z));
				// movc r2.yzw, r1.zzzz, r4.xxyz, r2.yyzw
				r2.yzw = float3(r1.z != 0.0 ? r4.x : r2.y, r1.z != 0.0 ? r4.y : r2.z, r1.z != 0.0 ? r4.z : r2.w);
				// movc r1.xzw, r1.xxxx, r3.yyzw, r2.yyzw
				r1.xzw = float3(r1.x != 0.0 ? r3.y : r2.y, r1.x != 0.0 ? r3.z : r2.z, r1.x != 0.0 ? r3.w : r2.w);
				// mul r2.y, cb0[17].y, cb1[0].y
				r2.y = _WaveSpeed * _Time.y;
				// sincos r3.x, r4.x, r2.y
				r3.x = sin(r2.y);
				r4.x = cos(r2.y);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r2.zw, r4.xxxy, l(0.000000, 0.000000, 0.040000, 0.040000), r0.zzzw
				r2.zw = float2(mad(r4.x, 0.04, r0.z), mad(r4.y, 0.04, r0.w));
				// mad r2.zw, r2.zzzw, l(0.000000, 0.000000, 0.500000, 0.500000), l(0.000000, 0.000000, 0.500000, 0.500000)
				r2.zw = float2(mad(r2.z, 0.5, 0.5), mad(r2.w, 0.5, 0.5));
				// sample r3.xyzw, r2.zwzz, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r2.z, r2.w));
				// mad r4.xyzw, cb1[0].yyyy, cb0[17].yyyy, l(0.628318, 1.256637, 1.884955, 2.513274)
				r4.xyzw = float4(mad(_Time.y, _WaveSpeed, 0.628318), mad(_Time.y, _WaveSpeed, 1.256637), mad(_Time.y, _WaveSpeed, 1.884955), mad(_Time.y, _WaveSpeed, 2.513274));
				// sincos r4.x, r5.x, r4.x
				r4.x = sin(r4.x);
				r5.x = cos(r4.x);
				// mov r5.y, r4.x
				r5.y = r4.x;
				// mad r2.zw, r5.xxxy, l(0.000000, 0.000000, 0.060000, 0.060000), r0.zzzw
				r2.zw = float2(mad(r5.x, 0.06, r0.z), mad(r5.y, 0.06, r0.w));
				// mad r2.zw, r2.zzzw, l(0.000000, 0.000000, 0.500000, 0.500000), l(0.000000, 0.000000, 0.500000, 0.500000)
				r2.zw = float2(mad(r2.z, 0.5, 0.5), mad(r2.w, 0.5, 0.5));
				// sample r5.xyzw, r2.zwzz, t2.xyzw, s0
				r5.xyzw = tex2D(_NoiseTex, float2(r2.z, r2.w));
				// add r2.z, r3.x, r5.x
				r2.z = r3.x + r5.x;
				// sincos r3.x, r4.x, r4.y
				r3.x = sin(r4.y);
				r4.x = cos(r4.y);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.080000, 0.080000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.08, r0.z), mad(r4.y, 0.08, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// sincos r3.x, r4.x, r4.z
				r3.x = sin(r4.z);
				r4.x = cos(r4.z);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.100000, 0.100000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.1, r0.z), mad(r4.y, 0.1, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// sincos r3.x, r4.x, r4.w
				r3.x = sin(r4.w);
				r4.x = cos(r4.w);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.120000, 0.120000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.12, r0.z), mad(r4.y, 0.12, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// mad r3.xyzw, cb1[0].yyyy, cb0[17].yyyy, l(3.141593, 3.769911, 4.398230, 5.026548)
				r3.xyzw = float4(mad(_Time.y, _WaveSpeed, 3.141593), mad(_Time.y, _WaveSpeed, 3.769911), mad(_Time.y, _WaveSpeed, 4.39823), mad(_Time.y, _WaveSpeed, 5.026548));
				// sincos r3.x, r4.x, r3.x
				r3.x = sin(r3.x);
				r4.x = cos(r3.x);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r4.xy, r4.xyxx, l(0.140000, 0.140000, 0.000000, 0.000000), r0.zwzz
				r4.xy = float2(mad(r4.x, 0.14, r0.z), mad(r4.y, 0.14, r0.w));
				// mad r4.xy, r4.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r4.xy = float2(mad(r4.x, 0.5, 0.5), mad(r4.y, 0.5, 0.5));
				// sample r4.xyzw, r4.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r4.x, r4.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// sincos r3.x, r4.x, r3.y
				r3.x = sin(r3.y);
				r4.x = cos(r3.y);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.160000, 0.160000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.16, r0.z), mad(r4.y, 0.16, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r4.xyzw, r3.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// sincos r3.x, r4.x, r3.z
				r3.x = sin(r3.z);
				r4.x = cos(r3.z);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.180000, 0.180000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.18, r0.z), mad(r4.y, 0.18, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r4.xyzw, r3.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// sincos r3.x, r4.x, r3.w
				r3.x = sin(r3.w);
				r4.x = cos(r3.w);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r3.xy, r4.xyxx, l(0.200000, 0.200000, 0.000000, 0.000000), r0.zwzz
				r3.xy = float2(mad(r4.x, 0.2, r0.z), mad(r4.y, 0.2, r0.w));
				// mad r3.xy, r3.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r3.xy = float2(mad(r3.x, 0.5, 0.5), mad(r3.y, 0.5, 0.5));
				// sample r3.xyzw, r3.xyxx, t2.xyzw, s0
				r3.xyzw = tex2D(_NoiseTex, float2(r3.x, r3.y));
				// add r2.z, r2.z, r3.x
				r2.z = r2.z + r3.x;
				// mad r3.xyzw, cb1[0].yyyy, cb0[17].yyyy, l(5.654866, 1.110000, 2.220000, 3.330000)
				r3.xyzw = float4(mad(_Time.y, _WaveSpeed, 5.654866), mad(_Time.y, _WaveSpeed, 1.11), mad(_Time.y, _WaveSpeed, 2.22), mad(_Time.y, _WaveSpeed, 3.33));
				// sincos r3.x, r4.x, r3.x
				r3.x = sin(r3.x);
				r4.x = cos(r3.x);
				// mov r4.y, r3.x
				r4.y = r3.x;
				// mad r4.xy, r4.xyxx, l(0.220000, 0.220000, 0.000000, 0.000000), r0.zwzz
				r4.xy = float2(mad(r4.x, 0.22, r0.z), mad(r4.y, 0.22, r0.w));
				// mad r4.xy, r4.xyxx, l(0.500000, 0.500000, 0.000000, 0.000000), l(0.500000, 0.500000, 0.000000, 0.000000)
				r4.xy = float2(mad(r4.x, 0.5, 0.5), mad(r4.y, 0.5, 0.5));
				// sample r4.xyzw, r4.xyxx, t2.xyzw, s0
				r4.xyzw = tex2D(_NoiseTex, float2(r4.x, r4.y));
				// add r2.z, r2.z, r4.x
				r2.z = r2.z + r4.x;
				// lt r4.xy, l(0.500000, 0.500000, 0.000000, 0.000000), cb0[20].yzyy
				r4.xy = float2((0.5 < _ToggleCell) ? 1.0 : 0.0, (0.5 < _ToggleNoise) ? 1.0 : 0.0);
				// mul r4.zw, r0.zzzw, cb0[15].xxxx
				r4.zw = float2(r0.z * _Params5.x, r0.w * _Params5.x);
				// frc r4.zw, r4.zzzw
				r4.zw = float2(frac(r4.z), frac(r4.w));
				// add r4.zw, r4.zzzw, l(0.000000, 0.000000, -0.500000, -0.500000)
				r4.zw = float2(r4.z + -0.5, r4.w + -0.5);
				// dp2 r2.w, r4.zwzz, r4.zwzz
				r2.w = r4.z * r4.z + r4.w * r4.w;
				// sqrt r2.w, r2.w
				r2.w = sqrt(r2.w);
				// add r3.x, r2.w, -cb0[14].y
				r3.x = r2.w + -(_Params4.y);
				// mul_sat r3.x, r3.x, l(12.500000)
				r3.x = saturate(r3.x * 12.5);
				// mad r4.z, r3.x, l(-2.000000), l(3.000000)
				r4.z = mad(r3.x, -2.0, 3.0);
				// mul r3.x, r3.x, r3.x
				r3.x = r3.x * r3.x;
				// mul r3.x, r3.x, r4.z
				r3.x = r3.x * r4.z;
				// mad r3.x, r3.x, l(0.500000), l(0.750000)
				r3.x = mad(r3.x, 0.5, 0.75);
				// mad r2.w, -r2.w, l(2.000000), l(1.000000)
				r2.w = mad(-(r2.w), 2.0, 1.0);
				// max r2.w, r2.w, l(0.000000)
				r2.w = max(r2.w, 0.0);
				// mul r4.z, r2.w, r2.w
				r4.z = r2.w * r2.w;
				// mul r2.w, r2.w, r4.z
				r2.w = r2.w * r4.z;
				// mul r5.xyz, r2.wwww, cb0[7].xyzx
				r5.xyz = float3(r2.w * _ColorD.x, r2.w * _ColorD.y, r2.w * _ColorD.z);
				// mul r5.xyz, r5.xyzx, cb0[19].zzzz
				r5.xyz = float3(r5.x * _Pulse, r5.y * _Pulse, r5.z * _Pulse);
				// mad r5.xyz, r1.xzwx, r3.xxxx, r5.xyzx
				r5.xyz = float3(mad(r1.x, r3.x, r5.x), mad(r1.z, r3.x, r5.y), mad(r1.w, r3.x, r5.z));
				// movc r1.xzw, r4.xxxx, r5.xxyz, r1.xxzw
				r1.xzw = float3(r4.x != 0.0 ? r5.x : r1.x, r4.x != 0.0 ? r5.y : r1.z, r4.x != 0.0 ? r5.z : r1.w);
				// mul r5.xyzw, cb0[17].xxxx, l(0.500000, 0.810000, 1.120000, 1.430000)
				r5.xyzw = float4(_WaveFreq * 0.5, _WaveFreq * 0.81, _WaveFreq * 1.12, _WaveFreq * 1.43);
				// mad r2.w, r0.z, r5.x, r2.y
				r2.w = mad(r0.z, r5.x, r2.y);
				// mad r3.x, r0.w, r5.x, -r2.y
				r3.x = mad(r0.w, r5.x, -(r2.y));
				// add r4.x, r0.w, r0.z
				r4.x = r0.w + r0.z;
				// mul r6.xyzw, r5.xyzw, r4.xxxx
				r6.xyzw = float4(r5.x * r4.x, r5.y * r4.x, r5.z * r4.x, r5.w * r4.x);
				// mad r2.y, r6.x, l(0.600000), r2.y
				r2.y = mad(r6.x, 0.6, r2.y);
				// sincos r2.yw, null, r2.yyyw
				r2.yw = sin(r2.yyyw);
				// mad r4.z, r0.z, r5.y, r3.y
				r4.z = mad(r0.z, r5.y, r3.y);
				// sincos r4.z, null, r4.z
				r4.z = sin(r4.z);
				// add r2.w, r2.w, r4.z
				r2.w = r2.w + r4.z;
				// mad r4.z, r0.w, r5.y, -r3.y
				r4.z = mad(r0.w, r5.y, -(r3.y));
				// add r3.x, r3.x, r4.z
				r3.x = r3.x + r4.z;
				// mad r6.xyz, r6.yzwy, l(0.600000, 0.600000, 0.600000, 0.000000), r3.yzwy
				r6.xyz = float3(mad(r6.y, 0.6, r3.y), mad(r6.z, 0.6, r3.z), mad(r6.w, 0.6, r3.w));
				// sincos r6.xyz, null, r6.xyzx
				r6.xyz = sin(r6.xyzx);
				// add r2.y, r2.y, r6.x
				r2.y = r2.y + r6.x;
				// mad r4.zw, r0.zzzz, r5.zzzw, r3.zzzw
				r4.zw = float2(mad(r0.z, r5.z, r3.z), mad(r0.z, r5.w, r3.w));
				// sincos r4.zw, null, r4.zzzw
				r4.zw = sin(r4.zzzw);
				// add r2.w, r2.w, r4.z
				r2.w = r2.w + r4.z;
				// mad r3.yz, r0.wwww, r5.zzwz, -r3.zzwz
				r3.yz = float2(mad(r0.w, r5.z, -(r3.z)), mad(r0.w, r5.w, -(r3.w)));
				// add r3.x, r3.y, r3.x
				r3.x = r3.y + r3.x;
				// add r2.y, r6.y, r2.y
				r2.y = r6.y + r2.y;
				// add r2.w, r4.w, r2.w
				r2.w = r4.w + r2.w;
				// add r3.x, r3.z, r3.x
				r3.x = r3.z + r3.x;
				// add r2.y, r6.z, r2.y
				r2.y = r6.z + r2.y;
				// mul r5.xyzw, cb0[17].xxxw, l(1.740000, 2.050000, 2.360000, 0.100000)
				r5.xyzw = float4(_WaveFreq * 1.74, _WaveFreq * 2.05, _WaveFreq * 2.36, _Sharpness * 0.1);
				// mad r3.yzw, cb1[0].yyyy, cb0[17].yyyy, l(0.000000, 4.440000, 5.550000, 6.660000)
				r3.yzw = float3(mad(_Time.y, _WaveSpeed, 4.44), mad(_Time.y, _WaveSpeed, 5.55), mad(_Time.y, _WaveSpeed, 6.66));
				// mad r4.zw, r0.zzzz, r5.xxxy, r3.yyyz
				r4.zw = float2(mad(r0.z, r5.x, r3.y), mad(r0.z, r5.y, r3.z));
				// sincos r4.zw, null, r4.zzzw
				r4.zw = sin(r4.zzzw);
				// add r2.w, r2.w, r4.z
				r2.w = r2.w + r4.z;
				// mad r6.xy, r0.wwww, r5.xyxx, -r3.yzyy
				r6.xy = float2(mad(r0.w, r5.x, -(r3.y)), mad(r0.w, r5.y, -(r3.z)));
				// add r3.x, r3.x, r6.x
				r3.x = r3.x + r6.x;
				// mul r6.xzw, r4.xxxx, r5.xxyz
				r6.xzw = float3(r4.x * r5.x, r4.x * r5.y, r4.x * r5.z);
				// mad r6.xzw, r6.xxzw, l(0.600000, 0.000000, 0.600000, 0.600000), r3.yyzw
				r6.xzw = float3(mad(r6.x, 0.6, r3.y), mad(r6.z, 0.6, r3.z), mad(r6.w, 0.6, r3.w));
				// sincos r6.xzw, null, r6.xxzw
				r6.xzw = sin(r6.xxzw);
				// add r2.y, r2.y, r6.x
				r2.y = r2.y + r6.x;
				// add r2.w, r4.w, r2.w
				r2.w = r4.w + r2.w;
				// add r3.x, r6.y, r3.x
				r3.x = r6.y + r3.x;
				// add r2.y, r6.z, r2.y
				r2.y = r6.z + r2.y;
				// mad r3.y, r0.z, r5.z, r3.w
				r3.y = mad(r0.z, r5.z, r3.w);
				// sincos r3.y, null, r3.y
				r3.y = sin(r3.y);
				// add r6.x, r2.w, r3.y
				r6.x = r2.w + r3.y;
				// mad r2.w, r0.w, r5.z, -r3.w
				r2.w = mad(r0.w, r5.z, -(r3.w));
				// add r6.y, r2.w, r3.x
				r6.y = r2.w + r3.x;
				// add r6.z, r6.w, r2.y
				r6.z = r6.w + r2.y;
				// mad_sat r3.xyz, r6.xyzx, l(0.140000, 0.140000, 0.140000, 0.000000), l(0.500000, 0.500000, 0.500000, 0.000000)
				r3.xyz = float3(saturate(mad(r6.x, 0.14, 0.5)), saturate(mad(r6.y, 0.14, 0.5)), saturate(mad(r6.z, 0.14, 0.5)));
				// mad r3.xyz, r1.xzwx, r3.xyzx, -r1.xzwx
				r3.xyz = float3(mad(r1.x, r3.x, -(r1.x)), mad(r1.z, r3.y, -(r1.z)), mad(r1.w, r3.z, -(r1.w)));
				// mad r1.xzw, r3.xxyz, l(0.350000, 0.000000, 0.350000, 0.350000), r1.xxzw
				r1.xzw = float3(mad(r3.x, 0.35, r1.x), mad(r3.y, 0.35, r1.z), mad(r3.z, 0.35, r1.w));
					// if_nz r4.y
				if (any(r4.y)) {
					// mul r2.y, cb0[16].y, l(3.000000)
					r2.y = _Layers * 3.0;
					// max r2.y, r2.y, l(1.000000)
					r2.y = max(r2.y, 1.0);
					// min r2.y, r2.y, l(8.000000)
					r2.y = min(r2.y, 8.0);
					// ftoi r2.y, r2.y
					r2.y = int(r2.y);
					// mov r2.w, l(0)
					r2.w = 0.0;
					// mov r3.x, l(0)
					r3.x = 0.0;
						// loop
					for (;;) {
						// ige r3.y, r3.x, r2.y
						r3.y = (((int)(r3.x) >= (int)(r2.y)) ? 1 : 0);
						// breakc_nz r3.y
						if (any(r3.y)) break;
						// itof r3.y, r3.x
						r3.y = float(r3.x);
						// mul r3.z, r3.y, l(1.700000)
						r3.z = r3.y * 1.7;
						// sincos r4.x, r5.x, r3.z
						r4.x = sin(r3.z);
						r5.x = cos(r3.z);
						// mad r3.z, r3.y, l(0.030000), l(0.050000)
						r3.z = mad(r3.y, 0.03, 0.05);
						// mov r5.y, r4.x
						r5.y = r4.x;
						// mad r3.zw, r5.xxxy, r3.zzzz, r0.zzzw
						r3.zw = float2(mad(r5.x, r3.z, r0.z), mad(r5.y, r3.z, r0.w));
						// round_ni r4.xy, r3.zwzz
						r4.xy = float2(floor(r3.z), floor(r3.w));
						// frc r4.zw, r3.zzzw
						r4.zw = float2(frac(r3.z), frac(r3.w));
						// mul r5.xy, r4.zwzz, r4.zwzz
						r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
						// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
						r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
						// mul r4.zw, r4.zzzw, r5.xxxy
						r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
						// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
						r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
						// dp2 r5.z, r5.xyxx, r6.xyxx
						r5.z = r5.x * r6.x + r5.y * r6.y;
						// add r5.xy, r5.zzzz, r5.xyxx
						r5.xy = float2(r5.z + r5.x, r5.z + r5.y);
						// mul r5.x, r5.y, r5.x
						r5.x = r5.y * r5.x;
						// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.y, r6.xyxx, r7.xyxx
						r5.y = r6.x * r7.x + r6.y * r7.y;
						// add r5.yz, r5.yyyy, r6.xxyx
						r5.yz = float2(r5.y + r6.x, r5.y + r6.y);
						// mul r5.y, r5.z, r5.y
						r5.y = r5.z * r5.y;
						// dp2 r5.z, r6.zwzz, r7.zwzz
						r5.z = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r5.zzzz, r6.zwzz
						r6.xy = float2(r5.z + r6.z, r5.z + r6.w);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.xyz, r5.xyzx
						r5.xyz = float3(frac(r5.x), frac(r5.y), frac(r5.z));
						// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
						r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
						// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
						r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
						// frc r4.xy, r4.xyxx
						r4.xy = float2(frac(r4.x), frac(r4.y));
						// add r6.xy, r4.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r4.x + 34.23, r4.y + 34.23);
						// dp2 r6.x, r4.xyxx, r6.xyxx
						r6.x = r4.x * r6.x + r4.y * r6.y;
						// add r4.xy, r4.xyxx, r6.xxxx
						r4.xy = float2(r4.x + r6.x, r4.y + r6.x);
						// mul r4.x, r4.y, r4.x
						r4.x = r4.y * r4.x;
						// frc r4.x, r4.x
						r4.x = frac(r4.x);
						// add r4.y, -r5.x, r5.y
						r4.y = -(r5.x) + r5.y;
						// mad r4.y, r4.z, r4.y, r5.x
						r4.y = mad(r4.z, r4.y, r5.x);
						// add r4.x, -r5.z, r4.x
						r4.x = -(r5.z) + r4.x;
						// mad r4.x, r4.z, r4.x, r5.z
						r4.x = mad(r4.z, r4.x, r5.z);
						// add r4.x, -r4.y, r4.x
						r4.x = -(r4.y) + r4.x;
						// mad r4.x, r4.w, r4.x, r4.y
						r4.x = mad(r4.w, r4.x, r4.y);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mul r4.y, r4.y, l(0.270000)
						r4.y = r4.y * 0.27;
						// mad r4.x, r4.x, l(0.500000), r4.y
						r4.x = mad(r4.x, 0.5, r4.y);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mad r4.x, r4.y, l(0.145800), r4.x
						r4.x = mad(r4.y, 0.1458, r4.x);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mad r4.x, r4.y, l(0.078732), r4.x
						r4.x = mad(r4.y, 0.078732, r4.x);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r5.xy, r3.zwzz
						r5.xy = float2(frac(r3.z), frac(r3.w));
						// mul r6.xy, r5.xyxx, r5.xyxx
						r6.xy = float2(r5.x * r5.x, r5.y * r5.y);
						// mad r5.xy, -r5.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
						r5.xy = float2(mad(-(r5.x), 2.0, 3.0), mad(-(r5.y), 2.0, 3.0));
						// mul r5.xy, r5.xyxx, r6.xyxx
						r5.xy = float2(r5.x * r6.x, r5.y * r6.y);
						// mul r6.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r6.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r6.xy, r6.xyxx
						r6.xy = float2(frac(r6.x), frac(r6.y));
						// add r6.zw, r6.xxxy, l(0.000000, 0.000000, 34.230000, 34.230000)
						r6.zw = float2(r6.x + 34.23, r6.y + 34.23);
						// dp2 r4.w, r6.xyxx, r6.zwzz
						r4.w = r6.x * r6.z + r6.y * r6.w;
						// add r6.xy, r4.wwww, r6.xyxx
						r6.xy = float2(r4.w + r6.x, r4.w + r6.y);
						// mul r4.w, r6.y, r6.x
						r4.w = r6.y * r6.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.z, r6.xyxx, r7.xyxx
						r5.z = r6.x * r7.x + r6.y * r7.y;
						// add r6.xy, r5.zzzz, r6.xyxx
						r6.xy = float2(r5.z + r6.x, r5.z + r6.y);
						// mul r5.z, r6.y, r6.x
						r5.z = r6.y * r6.x;
						// frc r5.z, r5.z
						r5.z = frac(r5.z);
						// dp2 r6.x, r6.zwzz, r7.zwzz
						r6.x = r6.z * r7.z + r6.w * r7.w;
						// add r6.xy, r6.xxxx, r6.zwzz
						r6.xy = float2(r6.x + r6.z, r6.x + r6.w);
						// mul r6.x, r6.y, r6.x
						r6.x = r6.y * r6.x;
						// frc r6.x, r6.x
						r6.x = frac(r6.x);
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.yz, r4.yyzy, l(0.000000, 34.230000, 34.230000, 0.000000)
						r6.yz = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r6.y, r4.yzyy, r6.yzyy
						r6.y = r4.y * r6.y + r4.z * r6.z;
						// add r4.yz, r4.yyzy, r6.yyyy
						r4.yz = float2(r4.y + r6.y, r4.z + r6.y);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.z
						r4.z = -(r4.w) + r5.z;
						// mad r4.z, r5.x, r4.z, r4.w
						r4.z = mad(r5.x, r4.z, r4.w);
						// add r4.y, -r6.x, r4.y
						r4.y = -(r6.x) + r4.y;
						// mad r4.y, r5.x, r4.y, r6.x
						r4.y = mad(r5.x, r4.y, r6.x);
						// add r4.y, -r4.z, r4.y
						r4.y = -(r4.z) + r4.y;
						// mad r4.y, r5.y, r4.y, r4.z
						r4.y = mad(r5.y, r4.y, r4.z);
						// mad r4.x, r4.y, l(0.042515), r4.x
						r4.x = mad(r4.y, 0.042515, r4.x);
						// mul r4.y, r3.w, l(0.651834)
						r4.y = r3.w * 0.651834;
						// mad r5.x, r3.z, l(0.758362), -r4.y
						r5.x = mad(r3.z, 0.758362, -(r4.y));
						// dp2 r5.y, r3.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
						r5.y = r3.z * 0.651834 + r3.w * 0.758362;
						// mad r3.zw, r5.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
						r3.zw = float2(mad(r5.x, 2.07, 9.1), mad(r5.y, 2.07, 9.1));
						// round_ni r4.yz, r3.zzwz
						r4.yz = float2(floor(r3.z), floor(r3.w));
						// frc r3.zw, r3.zzzw
						r3.zw = float2(frac(r3.z), frac(r3.w));
						// mul r5.xy, r3.zwzz, r3.zwzz
						r5.xy = float2(r3.z * r3.z, r3.w * r3.w);
						// mad r3.zw, -r3.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
						r3.zw = float2(mad(-(r3.z), 2.0, 3.0), mad(-(r3.w), 2.0, 3.0));
						// mul r3.zw, r3.zzzw, r5.xxxy
						r3.zw = float2(r3.z * r5.x, r3.w * r5.y);
						// mul r5.xy, r4.yzyy, l(127.099998, 311.700012, 0.000000, 0.000000)
						r5.xy = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
						// dp2 r4.w, r5.xyxx, r6.xyxx
						r4.w = r5.x * r6.x + r5.y * r6.y;
						// add r5.xy, r4.wwww, r5.xyxx
						r5.xy = float2(r4.w + r5.x, r4.w + r5.y);
						// mul r4.w, r5.y, r5.x
						r4.w = r5.y * r5.x;
						// add r6.xyzw, r4.yzyz, l(1.000000, 0.000000, 0.000000, 1.000000)
						r6.xyzw = float4(r4.y + 1.0, r4.z + 0.0, r4.y + 0.0, r4.z + 1.0);
						// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
						r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
						// frc r6.xyzw, r6.xyzw
						r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
						// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
						r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
						// dp2 r5.x, r6.xyxx, r7.xyxx
						r5.x = r6.x * r7.x + r6.y * r7.y;
						// add r5.xy, r5.xxxx, r6.xyxx
						r5.xy = float2(r5.x + r6.x, r5.x + r6.y);
						// mul r5.x, r5.y, r5.x
						r5.x = r5.y * r5.x;
						// dp2 r5.y, r6.zwzz, r7.zwzz
						r5.y = r6.z * r7.z + r6.w * r7.w;
						// add r5.yz, r5.yyyy, r6.zzwz
						r5.yz = float2(r5.y + r6.z, r5.y + r6.w);
						// mul r5.y, r5.z, r5.y
						r5.y = r5.z * r5.y;
						// frc r5.xy, r5.xyxx
						r5.xy = float2(frac(r5.x), frac(r5.y));
						// add r4.yz, r4.yyzy, l(0.000000, 1.000000, 1.000000, 0.000000)
						r4.yz = float2(r4.y + 1.0, r4.z + 1.0);
						// mul r4.yz, r4.yyzy, l(0.000000, 127.099998, 311.700012, 0.000000)
						r4.yz = float2(r4.y * 127.1, r4.z * 311.7);
						// frc r4.yzw, r4.yyzw
						r4.yzw = float3(frac(r4.y), frac(r4.z), frac(r4.w));
						// add r6.xy, r4.yzyy, l(34.230000, 34.230000, 0.000000, 0.000000)
						r6.xy = float2(r4.y + 34.23, r4.z + 34.23);
						// dp2 r5.z, r4.yzyy, r6.xyxx
						r5.z = r4.y * r6.x + r4.z * r6.y;
						// add r4.yz, r4.yyzy, r5.zzzz
						r4.yz = float2(r4.y + r5.z, r4.z + r5.z);
						// mul r4.y, r4.z, r4.y
						r4.y = r4.z * r4.y;
						// frc r4.y, r4.y
						r4.y = frac(r4.y);
						// add r4.z, -r4.w, r5.x
						r4.z = -(r4.w) + r5.x;
						// mad r4.z, r3.z, r4.z, r4.w
						r4.z = mad(r3.z, r4.z, r4.w);
						// add r4.y, -r5.y, r4.y
						r4.y = -(r5.y) + r4.y;
						// mad r3.z, r3.z, r4.y, r5.y
						r3.z = mad(r3.z, r4.y, r5.y);
						// add r3.z, -r4.z, r3.z
						r3.z = -(r4.z) + r3.z;
						// mad r3.z, r3.w, r3.z, r4.z
						r3.z = mad(r3.w, r3.z, r4.z);
						// mad r3.z, r3.z, l(0.022958), r4.x
						r3.z = mad(r3.z, 0.022958, r4.x);
						// add r3.y, r3.y, l(1.000000)
						r3.y = r3.y + 1.0;
						// div r3.y, l(1.000000, 1.000000, 1.000000, 1.000000), r3.y
						r3.y = 1.0 / r3.y;
						// mad r3.y, r3.z, r3.y, r2.w
						r3.y = mad(r3.z, r3.y, r2.w);
						// lt r3.z, l(0.900000), r3.y
						r3.z = (0.9 < r3.y) ? 1.0 : 0.0;
							// if_nz r3.z
						if (any(r3.z)) {
							// mov r2.w, r3.y
							r2.w = r3.y;
							// break
							break;
						// endif
						}
						// iadd r3.x, r3.x, l(1)
						r3.x = ((int)(r3.x) + (int)(1.0));
						// mov r2.w, r3.y
						r2.w = r3.y;
					// endloop
					}
					// mad r2.y, r2.w, l(0.600000), l(0.700000)
					r2.y = mad(r2.w, 0.6, 0.7);
					// mad r3.xyz, r1.xzwx, r2.yyyy, -r1.xzwx
					r3.xyz = float3(mad(r1.x, r2.y, -(r1.x)), mad(r1.z, r2.y, -(r1.z)), mad(r1.w, r2.y, -(r1.w)));
					// mad r3.xyz, cb0[14].zzzz, r3.xyzx, r1.xzwx
					r3.xyz = float3(mad(_Params4.z, r3.x, r1.x), mad(_Params4.z, r3.y, r1.z), mad(_Params4.z, r3.z, r1.w));
					// mul r0.zw, r0.zzzw, cb0[14].wwww
					r0.zw = float2(r0.z * _Params4.w, r0.w * _Params4.w);
					// round_ni r2.yw, r0.zzzw
					r2.yw = float2(floor(r0.z), floor(r0.w));
					// frc r4.xy, r0.zwzz
					r4.xy = float2(frac(r0.z), frac(r0.w));
					// mul r4.zw, r4.xxxy, r4.xxxy
					r4.zw = float2(r4.x * r4.x, r4.y * r4.y);
					// mad r4.xy, -r4.xyxx, l(2.000000, 2.000000, 0.000000, 0.000000), l(3.000000, 3.000000, 0.000000, 0.000000)
					r4.xy = float2(mad(-(r4.x), 2.0, 3.0), mad(-(r4.y), 2.0, 3.0));
					// mul r4.xy, r4.xyxx, r4.zwzz
					r4.xy = float2(r4.x * r4.z, r4.y * r4.w);
					// mul r4.zw, r2.yyyw, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r2.y * 127.1, r2.w * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r5.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r3.w, r4.zwzz, r5.xyxx
					r3.w = r4.z * r5.x + r4.w * r5.y;
					// add r4.zw, r3.wwww, r4.zzzw
					r4.zw = float2(r3.w + r4.z, r3.w + r4.w);
					// mul r3.w, r4.w, r4.z
					r3.w = r4.w * r4.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// add r6.xyzw, r2.ywyw, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r2.y + 1.0, r2.w + 0.0, r2.y + 0.0, r2.w + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r4.z, r6.xyxx, r7.xyxx
					r4.z = r6.x * r7.x + r6.y * r7.y;
					// add r4.zw, r4.zzzz, r6.xxxy
					r4.zw = float2(r4.z + r6.x, r4.z + r6.y);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// dp2 r4.w, r6.zwzz, r7.zwzz
					r4.w = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r4.wwww, r6.zwzz
					r5.xy = float2(r4.w + r6.z, r4.w + r6.w);
					// mul r4.w, r5.y, r5.x
					r4.w = r5.y * r5.x;
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r2.yw, r2.yyyw, l(0.000000, 1.000000, 0.000000, 1.000000)
					r2.yw = float2(r2.y + 1.0, r2.w + 1.0);
					// mul r2.yw, r2.yyyw, l(0.000000, 127.099998, 0.000000, 311.700012)
					r2.yw = float2(r2.y * 127.1, r2.w * 311.7);
					// frc r2.yw, r2.yyyw
					r2.yw = float2(frac(r2.y), frac(r2.w));
					// add r5.xy, r2.ywyy, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r2.y + 34.23, r2.w + 34.23);
					// dp2 r5.x, r2.ywyy, r5.xyxx
					r5.x = r2.y * r5.x + r2.w * r5.y;
					// add r2.yw, r2.yyyw, r5.xxxx
					r2.yw = float2(r2.y + r5.x, r2.w + r5.x);
					// mul r2.y, r2.w, r2.y
					r2.y = r2.w * r2.y;
					// frc r2.y, r2.y
					r2.y = frac(r2.y);
					// add r2.w, -r3.w, r4.z
					r2.w = -(r3.w) + r4.z;
					// mad r2.w, r4.x, r2.w, r3.w
					r2.w = mad(r4.x, r2.w, r3.w);
					// add r2.y, -r4.w, r2.y
					r2.y = -(r4.w) + r2.y;
					// mad r2.y, r4.x, r2.y, r4.w
					r2.y = mad(r4.x, r2.y, r4.w);
					// add r2.y, -r2.w, r2.y
					r2.y = -(r2.w) + r2.y;
					// mad r2.y, r4.y, r2.y, r2.w
					r2.y = mad(r4.y, r2.y, r2.w);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mul r2.w, r2.w, l(0.270000)
					r2.w = r2.w * 0.27;
					// mad r2.y, r2.y, l(0.500000), r2.w
					r2.y = mad(r2.y, 0.5, r2.w);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mad r2.y, r2.w, l(0.145800), r2.y
					r2.y = mad(r2.w, 0.1458, r2.y);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mad r2.y, r2.w, l(0.078732), r2.y
					r2.y = mad(r2.w, 0.078732, r2.y);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r4.zw, r0.zzzw
					r4.zw = float2(frac(r0.z), frac(r0.w));
					// mul r5.xy, r4.zwzz, r4.zwzz
					r5.xy = float2(r4.z * r4.z, r4.w * r4.w);
					// mad r4.zw, -r4.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r4.zw = float2(mad(-(r4.z), 2.0, 3.0), mad(-(r4.w), 2.0, 3.0));
					// mul r4.zw, r4.zzzw, r5.xxxy
					r4.zw = float2(r4.z * r5.x, r4.w * r5.y);
					// mul r5.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r5.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r5.xy, r5.xyxx
					r5.xy = float2(frac(r5.x), frac(r5.y));
					// add r6.xy, r5.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r6.xy = float2(r5.x + 34.23, r5.y + 34.23);
					// dp2 r2.w, r5.xyxx, r6.xyxx
					r2.w = r5.x * r6.x + r5.y * r6.y;
					// add r5.xy, r2.wwww, r5.xyxx
					r5.xy = float2(r2.w + r5.x, r2.w + r5.y);
					// mul r2.w, r5.y, r5.x
					r2.w = r5.y * r5.x;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r5.xy, r3.wwww, r6.xyxx
					r5.xy = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r5.y, r5.x
					r3.w = r5.y * r5.x;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r5.x, r6.zwzz, r7.zwzz
					r5.x = r6.z * r7.z + r6.w * r7.w;
					// add r5.xy, r5.xxxx, r6.zwzz
					r5.xy = float2(r5.x + r6.z, r5.x + r6.w);
					// mul r5.x, r5.y, r5.x
					r5.x = r5.y * r5.x;
					// frc r5.x, r5.x
					r5.x = frac(r5.x);
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xy, r4.xyxx
					r4.xy = float2(frac(r4.x), frac(r4.y));
					// add r5.yz, r4.xxyx, l(0.000000, 34.230000, 34.230000, 0.000000)
					r5.yz = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r5.y, r4.xyxx, r5.yzyy
					r5.y = r4.x * r5.y + r4.y * r5.z;
					// add r4.xy, r4.xyxx, r5.yyyy
					r4.xy = float2(r4.x + r5.y, r4.y + r5.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.z, r3.w, r2.w
					r2.w = mad(r4.z, r3.w, r2.w);
					// add r3.w, -r5.x, r4.x
					r3.w = -(r5.x) + r4.x;
					// mad r3.w, r4.z, r3.w, r5.x
					r3.w = mad(r4.z, r3.w, r5.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r4.w, r3.w, r2.w
					r2.w = mad(r4.w, r3.w, r2.w);
					// mad r2.y, r2.w, l(0.042515), r2.y
					r2.y = mad(r2.w, 0.042515, r2.y);
					// mul r2.w, r0.w, l(0.651834)
					r2.w = r0.w * 0.651834;
					// mad r4.x, r0.z, l(0.758362), -r2.w
					r4.x = mad(r0.z, 0.758362, -(r2.w));
					// dp2 r4.y, r0.zwzz, l(0.651834, 0.758362, 0.000000, 0.000000)
					r4.y = r0.z * 0.651834 + r0.w * 0.758362;
					// mad r0.zw, r4.xxxy, l(0.000000, 0.000000, 2.070000, 2.070000), l(0.000000, 0.000000, 9.100000, 9.100000)
					r0.zw = float2(mad(r4.x, 2.07, 9.1), mad(r4.y, 2.07, 9.1));
					// round_ni r4.xy, r0.zwzz
					r4.xy = float2(floor(r0.z), floor(r0.w));
					// frc r0.zw, r0.zzzw
					r0.zw = float2(frac(r0.z), frac(r0.w));
					// mul r4.zw, r0.zzzw, r0.zzzw
					r4.zw = float2(r0.z * r0.z, r0.w * r0.w);
					// mad r0.zw, -r0.zzzw, l(0.000000, 0.000000, 2.000000, 2.000000), l(0.000000, 0.000000, 3.000000, 3.000000)
					r0.zw = float2(mad(-(r0.z), 2.0, 3.0), mad(-(r0.w), 2.0, 3.0));
					// mul r0.zw, r0.zzzw, r4.zzzw
					r0.zw = float2(r0.z * r4.z, r0.w * r4.w);
					// mul r4.zw, r4.xxxy, l(0.000000, 0.000000, 127.099998, 311.700012)
					r4.zw = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.zw, r4.zzzw
					r4.zw = float2(frac(r4.z), frac(r4.w));
					// add r5.xy, r4.zwzz, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r4.z + 34.23, r4.w + 34.23);
					// dp2 r2.w, r4.zwzz, r5.xyxx
					r2.w = r4.z * r5.x + r4.w * r5.y;
					// add r4.zw, r2.wwww, r4.zzzw
					r4.zw = float2(r2.w + r4.z, r2.w + r4.w);
					// mul r2.w, r4.w, r4.z
					r2.w = r4.w * r4.z;
					// frc r2.w, r2.w
					r2.w = frac(r2.w);
					// add r6.xyzw, r4.xyxy, l(1.000000, 0.000000, 0.000000, 1.000000)
					r6.xyzw = float4(r4.x + 1.0, r4.y + 0.0, r4.x + 0.0, r4.y + 1.0);
					// mul r6.xyzw, r6.xyzw, l(127.099998, 311.700012, 127.099998, 311.700012)
					r6.xyzw = float4(r6.x * 127.1, r6.y * 311.7, r6.z * 127.1, r6.w * 311.7);
					// frc r6.xyzw, r6.xyzw
					r6.xyzw = float4(frac(r6.x), frac(r6.y), frac(r6.z), frac(r6.w));
					// add r7.xyzw, r6.xyzw, l(34.230000, 34.230000, 34.230000, 34.230000)
					r7.xyzw = float4(r6.x + 34.23, r6.y + 34.23, r6.z + 34.23, r6.w + 34.23);
					// dp2 r3.w, r6.xyxx, r7.xyxx
					r3.w = r6.x * r7.x + r6.y * r7.y;
					// add r4.zw, r3.wwww, r6.xxxy
					r4.zw = float2(r3.w + r6.x, r3.w + r6.y);
					// mul r3.w, r4.w, r4.z
					r3.w = r4.w * r4.z;
					// frc r3.w, r3.w
					r3.w = frac(r3.w);
					// dp2 r4.z, r6.zwzz, r7.zwzz
					r4.z = r6.z * r7.z + r6.w * r7.w;
					// add r4.zw, r4.zzzz, r6.zzzw
					r4.zw = float2(r4.z + r6.z, r4.z + r6.w);
					// mul r4.z, r4.w, r4.z
					r4.z = r4.w * r4.z;
					// add r4.xy, r4.xyxx, l(1.000000, 1.000000, 0.000000, 0.000000)
					r4.xy = float2(r4.x + 1.0, r4.y + 1.0);
					// mul r4.xy, r4.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
					r4.xy = float2(r4.x * 127.1, r4.y * 311.7);
					// frc r4.xyz, r4.xyzx
					r4.xyz = float3(frac(r4.x), frac(r4.y), frac(r4.z));
					// add r5.xy, r4.xyxx, l(34.230000, 34.230000, 0.000000, 0.000000)
					r5.xy = float2(r4.x + 34.23, r4.y + 34.23);
					// dp2 r4.w, r4.xyxx, r5.xyxx
					r4.w = r4.x * r5.x + r4.y * r5.y;
					// add r4.xy, r4.wwww, r4.xyxx
					r4.xy = float2(r4.w + r4.x, r4.w + r4.y);
					// mul r4.x, r4.y, r4.x
					r4.x = r4.y * r4.x;
					// frc r4.x, r4.x
					r4.x = frac(r4.x);
					// add r3.w, -r2.w, r3.w
					r3.w = -(r2.w) + r3.w;
					// mad r2.w, r0.z, r3.w, r2.w
					r2.w = mad(r0.z, r3.w, r2.w);
					// add r3.w, -r4.z, r4.x
					r3.w = -(r4.z) + r4.x;
					// mad r0.z, r0.z, r3.w, r4.z
					r0.z = mad(r0.z, r3.w, r4.z);
					// add r0.z, -r2.w, r0.z
					r0.z = -(r2.w) + r0.z;
					// mad r0.z, r0.w, r0.z, r2.w
					r0.z = mad(r0.w, r0.z, r2.w);
					// mad r0.z, r0.z, l(0.022958), r2.y
					r0.z = mad(r0.z, 0.022958, r2.y);
					// mul r4.xyz, r0.zzzz, cb0[8].xyzx
					r4.xyz = float3(r0.z * _ColorE.x, r0.z * _ColorE.y, r0.z * _ColorE.z);
					// mul r4.xyz, r4.xyzx, cb0[19].xxxx
					r4.xyz = float3(r4.x * _Emission, r4.y * _Emission, r4.z * _Emission);
					// max r1.xzw, r3.xxyz, r4.xxyz
					r1.xzw = float3(max(r3.x, r4.x), max(r3.y, r4.y), max(r3.z, r4.z));
				// endif
				}
				// mul r0.z, r1.y, cb0[12].w
				r0.z = r1.y * _Params2.w;
				// mad r0.w, r2.z, l(0.090000), l(0.400000)
				r0.w = mad(r2.z, 0.09, 0.4);
				// mad r0.z, r0.z, r0.w, r5.w
				r0.z = mad(r0.z, r0.w, r5.w);
				// add_sat r0.z, r0.z, -cb0[15].y
				r0.z = saturate(r0.z + -(_Params5.y));
				// lt r0.w, r0.z, cb0[19].y
				r0.w = (r0.z < _Threshold) ? 1.0 : 0.0;
				// discard_nz r0.w
				if (any(r0.w)) discard;
				// add r0.w, r2.x, cb0[17].z
				r0.w = r2.x + _HueShift;
				// add r0.w, r0.w, l(-0.500000)
				r0.w = r0.w + -0.5;
				// sincos r2.x, r3.x, r0.w
				r2.x = sin(r0.w);
				r3.x = cos(r0.w);
				// mul r2.yzw, r1.wwxz, l(0.000000, 0.577350, 0.577350, 0.577350)
				r2.yzw = float3(r1.w * 0.57735, r1.x * 0.57735, r1.z * 0.57735);
				// mad r2.yzw, r1.wwxz, l(0.000000, 0.577350, 0.577350, 0.577350), -r2.wwyz
				r2.yzw = float3(mad(r1.w, 0.57735, -(r2.w)), mad(r1.x, 0.57735, -(r2.y)), mad(r1.z, 0.57735, -(r2.z)));
				// mul r2.xyz, r2.xxxx, r2.yzwy
				r2.xyz = float3(r2.x * r2.y, r2.x * r2.z, r2.x * r2.w);
				// mad r2.xyz, r1.xzwx, r3.xxxx, r2.xyzx
				r2.xyz = float3(mad(r1.x, r3.x, r2.x), mad(r1.z, r3.x, r2.y), mad(r1.w, r3.x, r2.z));
				// dp3 r0.w, l(0.577350, 0.577350, 0.577350, 0.000000), r1.xzwx
				r0.w = 0.57735 * r1.x + 0.57735 * r1.z + 0.57735 * r1.w;
				// mul r0.w, r0.w, l(0.577350)
				r0.w = r0.w * 0.57735;
				// add r1.x, -r3.x, l(1.000000)
				r1.x = -(r3.x) + 1.0;
				// mad r1.xyz, r0.wwww, r1.xxxx, r2.xyzx
				r1.xyz = float3(mad(r0.w, r1.x, r2.x), mad(r0.w, r1.x, r2.y), mad(r0.w, r1.x, r2.z));
				// dp3 r0.w, r1.xyzx, l(0.212600, 0.715200, 0.072200, 0.000000)
				r0.w = r1.x * 0.2126 + r1.y * 0.7152 + r1.z * 0.0722;
				// add r1.xyz, -r0.wwww, r1.xyzx
				r1.xyz = float3(-(r0.w) + r1.x, -(r0.w) + r1.y, -(r0.w) + r1.z);
				// mad r1.xyz, cb0[18].zzzz, r1.xyzx, r0.wwww
				r1.xyz = float3(mad(_Saturation, r1.x, r0.w), mad(_Saturation, r1.y, r0.w), mad(_Saturation, r1.z, r0.w));
				// add r1.xyz, r1.xyzx, l(-0.500000, -0.500000, -0.500000, 0.000000)
				r1.xyz = float3(r1.x + -0.5, r1.y + -0.5, r1.z + -0.5);
				// mad_sat r1.xyz, r1.xyzx, cb0[18].xxxx, l(0.500000, 0.500000, 0.500000, 0.000000)
				r1.xyz = float3(saturate(mad(r1.x, _Contrast, 0.5)), saturate(mad(r1.y, _Contrast, 0.5)), saturate(mad(r1.z, _Contrast, 0.5)));
				// max r0.w, cb0[18].y, l(0.010000)
				r0.w = max(_Gamma, 0.01);
				// div r0.w, l(1.000000, 1.000000, 1.000000, 1.000000), r0.w
				r0.w = 1.0 / r0.w;
				// log r1.xyz, r1.xyzx
				r1.xyz = float3(log2(r1.x), log2(r1.y), log2(r1.z));
				// mul r1.xyz, r0.wwww, r1.xyzx
				r1.xyz = float3(r0.w * r1.x, r0.w * r1.y, r0.w * r1.z);
				// exp r1.xyz, r1.xyzx
				r1.xyz = float3(exp2(r1.x), exp2(r1.y), exp2(r1.z));
				// mul r2.xyz, v2.xyzx, cb0[3].xyzx
				r2.xyz = float3(v2.x * _Tint.x, v2.y * _Tint.y, v2.z * _Tint.z);
				// mul r1.xyz, r1.xyzx, r2.xyzx
				r1.xyz = float3(r1.x * r2.x, r1.y * r2.y, r1.z * r2.z);
				// add r2.xy, r0.xyxx, l(-0.500000, -0.500000, 0.000000, 0.000000)
				r2.xy = float2(r0.x + -0.5, r0.y + -0.5);
				// dp2 r0.w, r2.xyxx, r2.xyxx
				r0.w = r2.x * r2.x + r2.y * r2.y;
				// dp2 r0.w, r0.wwww, cb0[18].wwww
				r0.w = r0.w * _Vignette + r0.w * _Vignette;
				// add_sat r0.w, -r0.w, l(1.000000)
				r0.w = saturate(-(r0.w) + 1.0);
				// mad r0.w, r0.w, l(0.700000), l(0.500000)
				r0.w = mad(r0.w, 0.7, 0.5);
				// mul r1.xyz, r0.wwww, r1.xyzx
				r1.xyz = float3(r0.w * r1.x, r0.w * r1.y, r0.w * r1.z);
				// lt r2.xy, l(0.500000, 0.500000, 0.000000, 0.000000), cb0[20].xwxx
				r2.xy = float2((0.5 < _ToggleGlow) ? 1.0 : 0.0, (0.5 < _ToggleDither) ? 1.0 : 0.0);
				// deriv_rtx r0.w, r1.x
				r0.w = ddx(r1.x);
				// mul r3.x, r0.w, l(40.000000)
				r3.x = r0.w * 40.0;
				// deriv_rty r0.w, r1.x
				r0.w = ddy(r1.x);
				// mul r3.y, r0.w, l(40.000000)
				r3.y = r0.w * 40.0;
				// mov r3.z, l(1.000000)
				r3.z = 1.0;
				// dp3 r0.w, r3.xyzx, r3.xyzx
				r0.w = r3.x * r3.x + r3.y * r3.y + r3.z * r3.z;
				// rsq r0.w, r0.w
				r0.w = rsqrt(r0.w);
				// add r0.w, -r0.w, l(1.000000)
				r0.w = -(r0.w) + 1.0;
				// log r0.w, r0.w
				r0.w = log2(r0.w);
				// mul r0.w, r0.w, l(2.500000)
				r0.w = r0.w * 2.5;
				// exp r0.w, r0.w
				r0.w = exp2(r0.w);
				// mul r3.xyz, r0.wwww, cb0[5].xyzx
				r3.xyz = float3(r0.w * _ColorB.x, r0.w * _ColorB.y, r0.w * _ColorB.z);
				// mad r3.xyz, r3.xyzx, cb0[14].zzzz, r1.xyzx
				r3.xyz = float3(mad(r3.x, _Params4.z, r1.x), mad(r3.y, _Params4.z, r1.y), mad(r3.z, _Params4.z, r1.z));
				// movc r1.xyz, r2.xxxx, r3.xyzx, r1.xyzx
				r1.xyz = float3(r2.x != 0.0 ? r3.x : r1.x, r2.x != 0.0 ? r3.y : r1.y, r2.x != 0.0 ? r3.z : r1.z);
				// mul r0.xy, r0.xyxx, cb1[6].xyxx
				r0.xy = float2(r0.x * _ScreenParams.x, r0.y * _ScreenParams.y);
				// mul r0.xy, r0.xyxx, l(127.099998, 311.700012, 0.000000, 0.000000)
				r0.xy = float2(r0.x * 127.1, r0.y * 311.7);
				// frc r0.xy, r0.xyxx
				r0.xy = float2(frac(r0.x), frac(r0.y));
				// add r2.xz, r0.xxyx, l(34.230000, 0.000000, 34.230000, 0.000000)
				r2.xz = float2(r0.x + 34.23, r0.y + 34.23);
				// dp2 r0.w, r0.xyxx, r2.xzxx
				r0.w = r0.x * r2.x + r0.y * r2.z;
				// add r0.xy, r0.wwww, r0.xyxx
				r0.xy = float2(r0.w + r0.x, r0.w + r0.y);
				// mul r0.x, r0.y, r0.x
				r0.x = r0.y * r0.x;
				// frc r0.x, r0.x
				r0.x = frac(r0.x);
				// add r0.x, r0.x, l(-0.500000)
				r0.x = r0.x + -0.5;
				// mad r0.xyw, r0.xxxx, l(0.003922, 0.003922, 0.000000, 0.003922), r1.xyxz
				r0.xyw = float3(mad(r0.x, 0.003922, r1.x), mad(r0.x, 0.003922, r1.y), mad(r0.x, 0.003922, r1.z));
				// movc r0.xyw, r2.yyyy, r0.xyxw, r1.xyxz
				r0.xyw = float3(r2.y != 0.0 ? r0.x : r1.x, r2.y != 0.0 ? r0.y : r1.y, r2.y != 0.0 ? r0.w : r1.z);
				// mul o0.xyz, r0.xywx, cb0[19].xxxx
				o0.xyz = float3(r0.x * _Emission, r0.y * _Emission, r0.w * _Emission);
				// mov o0.w, r0.z
				o0.w = r0.z;
				return o0;
			}
			#endif

			ENDCG
		}
	}
}
