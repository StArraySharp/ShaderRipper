// Reconstructed ShaderLab by ShaderRipper
// pathID=2984  platforms=[4]
Shader "CameraFilterPack/RainFX"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_TimeX ("Time", Range(0, 1)) = 1
		_ScreenResolution ("_ScreenResolution", Vector) = (0,0,0,0)
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" }
		LOD 100

		Pass
		{
			Tags { "LightMode"="Always" }
			ZTest Always
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
			// 1 keyword variant(s), 0 independent toggle(s)
			// variants: no keywords: single variant
			
			// ---- variant 0 (base) ----
			#if 1
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float2  uv0          : TEXCOORD0;
				float4  color0       : COLOR0;
			};
			
			sampler2D Texture3;
			sampler2D Texture2;
			sampler2D _MainTex;
			// cb0 = $Globals (material properties); 8 of them are named below
			float _Value;
			float _Speed;
			float4 Coord1;
			float4 Coord2;
			float4 Coord3;
			float4 Coord4;
			float2 _MainTex_TexelSize;
			float4 _MainTex_ST;
			// $Globals cb0[11] = 11 row(s) / 176 bytes; 8 row(s) read, 8 named.
			//   read whole    : rows 4, 5, 6, 7, 10
			//   read partial  : row 2.w row 3.x row 9.y
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerFrame  (declared by UnityCG.cginc)
			float _TimeX;
			float4 _ScreenResolution;
			// $Globals cb0: 8 member(s) named from the asset's own constant-buffer reflection (176 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.uv0
				float4 o1;  // -> o.pos
				float4 o2;  // -> o.color0
				float4 r0 = 0, r1 = 0;
			
				o0.xy = float2(v2.x, v2.y);
			o1 = UnityObjectToClipPos(v0.xyz);
				o2.xyzw = float4(v1.x, v1.y, v1.z, v1.w);
			
				v2f o;
				o.uv0 = o0.xy;
				o.pos = o1;
				o.color0 = o2;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float2 v0 = i.uv0;
				float4 v1 = i.pos;
				float4 v2 = i.color0;
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = float4(mad(Coord2.z, 0.003906, -0.01), mad(Coord2.z, 0.003906, -0.7), mad(Coord2.x, 0.85, 0.03122), mad(Coord2.x, 0.85, 0.0003));
				r1.x = mad(Coord2.x, 0.85, -(r0.w));
				r1.x = 1.0 / r1.x;
				r2.y = v0.x;
				r0.zw = float2(mad(r2.y, 0.85, -(r0.z)), mad(r2.y, 0.85, -(r0.w)));
				r0.w = saturate(r1.x * r0.w);
				r0.xyz = float3(saturate(r0.x * 11.11111), saturate(r0.y * 3.571428), saturate(r0.z * 33333.6));
				r1.x = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.w = r0.w * r1.x;
				r1.x = mad(r0.z, -2.0, 3.0);
				r0.z = r0.z * r0.z;
				r0.z = mad(r1.x, r0.z, r0.w);
				r0.w = Coord2.w + 1.0;
				r0.w = r0.w * 0.1275;
				r1.xy = float2(mad(-(Coord2.y), r0.w, 1.1242), mad(-(Coord2.y), r0.w, 1.0003));
				r1.z = (_MainTex_TexelSize.y < 0.0) ? 1.0 : 0.0;
				r1.w = -(v0.y) + 1.0;
				r2.x = r1.z != 0.0 ? r1.w : v0.y;
				r1.z = mad(-(r2.x), r0.w, 1.0);
				r1.xy = float2(-(r1.x) + r1.z, -(r1.y) + r1.z);
				r1.xy = float2(saturate(r1.x * 1250.002), saturate(r1.y * -3332.78));
				r1.zw = float2(mad(r1.x, -2.0, 3.0), mad(r1.y, -2.0, 3.0));
				r1.xy = float2(r1.x * r1.x, r1.y * r1.y);
				r0.z = mad(r1.z, r1.x, r0.z);
				r0.z = mad(r1.w, r1.y, r0.z);
				r0.z = min(r0.z, 1.0);
				r1.xy = float2(Coord2.z * 0.03125, Coord2.x * 0.85);
				r3.xy = float2(-(r1.x), -(r1.y));
				r1.y = (r1.x >= r3.x) ? 1.0 : 0.0;
				r1.z = frac(r1.x);
				r1.x = floor(r1.x);
				r1.x = -(r1.x) + 8.0;
				r1.y = r1.y != 0.0 ? r1.z : -(r1.z);
				r4.x = r1.y * 1.0;
				r1.y = 0.125 / r0.w;
				r1.x = r1.x * r1.y;
				r4.y = r0.w * r1.x;
				r1.x = r0.w * Coord2.y;
				r5.w = r0.w * r2.x;
				r3.z = -(r1.x);
				r1.xy = float2(r3.y + r4.x, r3.z + r4.y);
				r5.xz = float2(r2.y * 0.85, r2.y * 0.85);
				r1.xy = float2(r1.x + r5.z, r1.y + r5.w);
				r1.xyzw = tex2D(Texture3, float2(r1.x, r1.y));
				r0.z = mad(r0.z, -(r1.y), r1.y);
				r1.xy = float2(mad(r0.x, -2.0, 3.0), mad(r0.y, -2.0, 3.0));
				r0.xy = float2(r0.x * r0.x, r0.y * r0.y);
				r0.y = r0.y * r1.y;
				r0.x = saturate(mad(r1.x, r0.x, -(r0.y)));
				r0.x = r0.x * r0.z;
				r0.yz = float2(Coord1.z * 0.85, Coord1.x * 0.85);
				r1.xy = float2(-(r0.y), -(r0.z));
				r0.z = (r0.y >= r1.x) ? 1.0 : 0.0;
				r0.w = frac(r0.y);
				r0.y = floor(r0.y);
				r0.y = -(r0.y) + 8.0;
				r0.z = r0.z != 0.0 ? r0.w : -(r0.w);
				r3.x = r0.z * 1.0;
				r0.z = Coord1.w + 1.0;
				r0.z = r0.z * 0.1275;
				r0.w = 0.125 / r0.z;
				r0.y = r0.y * r0.w;
				r3.y = r0.z * r0.y;
				r0.y = r0.z * Coord1.y;
				r1.z = -(r0.y);
				r0.yw = float2(r1.y + r3.x, r1.z + r3.y);
				r5.y = r0.z * r2.x;
				r0.yw = float2(r0.y + r5.x, r0.w + r5.y);
				r1.xyzw = tex2D(Texture3, float2(r0.y, r0.w));
				r3.xyzw = float4(mad(Coord1.z, 0.003906, -0.01), mad(Coord1.z, 0.003906, -0.7), mad(Coord1.x, 0.85, 0.03122), mad(Coord1.x, 0.85, 0.0003));
				r0.y = mad(Coord1.x, 0.85, -(r3.w));
				r0.y = 1.0 / r0.y;
				r1.yz = float2(mad(r2.y, 0.85, -(r3.z)), mad(r2.y, 0.85, -(r3.w)));
				r3.xy = float2(saturate(r3.x * 11.11111), saturate(r3.y * 3.571428));
				r0.y = saturate(r0.y * r1.z);
				r0.w = saturate(r1.y * 33333.6);
				r1.y = mad(r0.y, -2.0, 3.0);
				r0.y = r0.y * r0.y;
				r0.y = r0.y * r1.y;
				r1.y = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.y = mad(r1.y, r0.w, r0.y);
				r1.yz = float2(mad(-(Coord1.y), r0.z, 1.0003), mad(-(Coord1.y), r0.z, 1.0003));
				r0.z = mad(-(r2.x), r0.z, 1.0);
				r0.zw = float2(-(r1.y) + r0.z, -(r1.z) + r0.z);
				r0.zw = float2(saturate(r0.z * -3332.78), saturate(r0.w * -3332.78));
				r1.yz = float2(mad(r0.z, -2.0, 3.0), mad(r0.w, -2.0, 3.0));
				r0.zw = float2(r0.z * r0.z, r0.w * r0.w);
				r0.y = mad(r1.y, r0.z, r0.y);
				r0.y = mad(r1.z, r0.w, r0.y);
				r0.y = min(r0.y, 1.0);
				r0.y = mad(r0.y, -(r1.x), r1.x);
				r0.zw = float2(mad(r3.x, -2.0, 3.0), mad(r3.y, -2.0, 3.0));
				r1.xy = float2(r3.x * r3.x, r3.y * r3.y);
				r0.w = r0.w * r1.y;
				r0.z = saturate(mad(r0.z, r1.x, -(r0.w)));
				r0.x = mad(r0.y, r0.z, r0.x);
				r0.yz = float2(Coord3.z * 0.85, Coord3.x * 0.85);
				r1.xy = float2(-(r0.y), -(r0.z));
				r0.z = (r0.y >= r1.x) ? 1.0 : 0.0;
				r0.w = frac(r0.y);
				r0.y = floor(r0.y);
				r0.y = -(r0.y) + 8.0;
				r0.z = r0.z != 0.0 ? r0.w : -(r0.w);
				r3.x = r0.z * 1.0;
				r0.z = Coord3.w + 1.0;
				r0.z = r0.z * 0.1275;
				r0.w = 0.125 / r0.z;
				r0.y = r0.y * r0.w;
				r3.y = r0.z * r0.y;
				r0.y = r0.z * Coord3.y;
				r1.z = -(r0.y);
				r0.yw = float2(r1.y + r3.x, r1.z + r3.y);
				r1.y = r0.z * r2.x;
				r1.xz = float2(r2.y * 0.85, r2.y * 0.85);
				r0.yw = float2(r0.y + r1.x, r0.w + r1.y);
				r3.xyzw = tex2D(Texture3, float2(r0.y, r0.w));
				r4.xyzw = float4(mad(Coord3.z, 0.003906, -0.01), mad(Coord3.z, 0.003906, -0.7), mad(Coord3.x, 0.85, 0.03122), mad(Coord3.x, 0.85, 0.0003));
				r0.y = mad(Coord3.x, 0.85, -(r4.w));
				r0.y = 1.0 / r0.y;
				r1.xy = float2(mad(r2.y, 0.85, -(r4.z)), mad(r2.y, 0.85, -(r4.w)));
				r3.xy = float2(saturate(r4.x * 11.11111), saturate(r4.y * 3.571428));
				r0.y = saturate(r0.y * r1.y);
				r0.w = saturate(r1.x * 33333.6);
				r1.x = mad(r0.y, -2.0, 3.0);
				r0.y = r0.y * r0.y;
				r0.y = r0.y * r1.x;
				r1.x = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.y = mad(r1.x, r0.w, r0.y);
				r1.xy = float2(mad(-(Coord3.y), r0.z, 1.1242), mad(-(Coord3.y), r0.z, 1.0003));
				r0.z = mad(-(r2.x), r0.z, 1.0);
				r0.zw = float2(-(r1.x) + r0.z, -(r1.y) + r0.z);
				r0.zw = float2(saturate(r0.z * -3332.78), saturate(r0.w * -3332.78));
				r1.xy = float2(mad(r0.z, -2.0, 3.0), mad(r0.w, -2.0, 3.0));
				r0.zw = float2(r0.z * r0.z, r0.w * r0.w);
				r0.y = mad(r1.x, r0.z, r0.y);
				r0.y = mad(r1.y, r0.w, r0.y);
				r0.y = min(r0.y, 1.0);
				r0.y = mad(r0.y, -(r3.z), r3.z);
				r0.zw = float2(mad(r3.x, -2.0, 3.0), mad(r3.y, -2.0, 3.0));
				r1.xy = float2(r3.x * r3.x, r3.y * r3.y);
				r0.w = r0.w * r1.y;
				r0.z = saturate(mad(r0.z, r1.x, -(r0.w)));
				r0.x = mad(r0.y, r0.z, r0.x);
				r0.yz = float2(Coord4.z * 0.85, Coord4.x * 0.85);
				r3.xy = float2(-(r0.y), -(r0.z));
				r0.z = (r0.y >= r3.x) ? 1.0 : 0.0;
				r0.w = frac(r0.y);
				r0.y = floor(r0.y);
				r0.y = -(r0.y) + 8.0;
				r0.z = r0.z != 0.0 ? r0.w : -(r0.w);
				r1.x = r0.z * 1.0;
				r0.z = Coord4.w + 1.0;
				r0.z = r0.z * 0.1275;
				r0.w = 0.125 / r0.z;
				r0.y = r0.y * r0.w;
				r1.y = r0.z * r0.y;
				r0.y = r0.z * Coord4.y;
				r3.z = -(r0.y);
				r0.yw = float2(r1.x + r3.y, r1.y + r3.z);
				r1.w = r0.z * r2.x;
				r0.yw = float2(r0.y + r1.z, r0.w + r1.w);
				r1.xyzw = tex2D(Texture3, float2(r0.y, r0.w));
				r3.xyzw = float4(mad(Coord4.z, 0.003906, -0.01), mad(Coord4.z, 0.003906, -0.7), mad(Coord4.x, 0.85, 0.03122), mad(Coord4.x, 0.85, 0.0003));
				r0.y = mad(Coord4.x, 0.85, -(r3.w));
				r0.y = 1.0 / r0.y;
				r1.yz = float2(mad(r2.y, 0.85, -(r3.z)), mad(r2.y, 0.85, -(r3.w)));
				r3.xy = float2(saturate(r3.x * 11.11111), saturate(r3.y * 3.571428));
				r0.y = saturate(r0.y * r1.z);
				r0.w = saturate(r1.y * 33333.6);
				r1.y = mad(r0.y, -2.0, 3.0);
				r0.y = r0.y * r0.y;
				r0.y = r0.y * r1.y;
				r1.y = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.y = mad(r1.y, r0.w, r0.y);
				r1.yz = float2(mad(-(Coord4.y), r0.z, 1.0003), mad(-(Coord4.y), r0.z, 1.0003));
				r0.z = mad(-(r2.x), r0.z, 1.0);
				r0.zw = float2(-(r1.y) + r0.z, -(r1.z) + r0.z);
				r0.zw = float2(saturate(r0.z * -3332.78), saturate(r0.w * -3332.78));
				r1.yz = float2(mad(r0.z, -2.0, 3.0), mad(r0.w, -2.0, 3.0));
				r0.zw = float2(r0.z * r0.z, r0.w * r0.w);
				r0.y = mad(r1.y, r0.z, r0.y);
				r0.y = mad(r1.z, r0.w, r0.y);
				r0.y = min(r0.y, 1.0);
				r0.y = mad(r0.y, -(r1.x), r1.x);
				r0.zw = float2(mad(r3.x, -2.0, 3.0), mad(r3.y, -2.0, 3.0));
				r1.xy = float2(r3.x * r3.x, r3.y * r3.y);
				r0.w = r0.w * r1.y;
				r0.z = saturate(mad(r0.z, r1.x, -(r0.w)));
				r0.x = mad(r0.y, r0.z, r0.x);
				r2.z = r2.x + -0.16;
				r1.xyzw = tex2D(Texture2, float2(r2.y, r2.z));
				r3.xyz = float3(mad(r2.y, 0.75, 0.5), mad(r2.x, 0.75, 0.5), mad(r2.x, 0.75, 0.34));
				r0.y = r2.x + r1.z;
				r0.z = r1.z * r1.z;
				r0.w = -(r1.x) + 1.0;
				r1.x = _Speed * _Time.x;
				r2.y = mad(r1.x, r0.z, r0.y);
				r2.x = v0.x;
				r2.xyzw = tex2D(Texture2, float2(r2.x, r2.y));
				r0.x = mad(r2.y, r0.w, r0.x);
				r2.xyzw = tex2D(Texture2, float2(r3.x, r3.z));
				r0.y = r3.y + r2.z;
				r0.z = r2.z * r2.z;
				r0.w = -(r2.x) + 1.0;
				r3.w = mad(r1.x, r0.z, r0.y);
				r1.xyzw = tex2D(Texture2, float2(r3.x, r3.w));
				r0.x = mad(r1.y, r0.w, r0.x);
				r0.y = saturate(r0.x * _Value);
				r0.zw = float2(mad(v0.x, _MainTex_ST.x, _MainTex_ST.z), mad(v0.y, _MainTex_ST.y, _MainTex_ST.w));
				r0.x = 0.25;
				r0.xz = float2(mad(0.0, r0.x, r0.z), mad(0.25, r0.y, r0.w));
				r1.xyzw = tex2D(_MainTex, float2(r0.x, r0.z));
				o0.xyzw = float4(mad(0.35, r0.y, r1.x), mad(0.35, r0.y, r1.y), mad(0.35, r0.y, r1.z), mad(0.35, r0.y, r1.w));
				return o0;
			}
			
			// ---- fallback: combinations Unity never compiled ----
			#else
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float2  uv0          : TEXCOORD0;
				float4  color0       : COLOR0;
			};
			
			sampler2D Texture3;
			sampler2D Texture2;
			sampler2D _MainTex;
			// cb0 = $Globals (material properties); 8 of them are named below
			float _Value;
			float _Speed;
			float4 Coord1;
			float4 Coord2;
			float4 Coord3;
			float4 Coord4;
			float2 _MainTex_TexelSize;
			float4 _MainTex_ST;
			// $Globals cb0[11] = 11 row(s) / 176 bytes; 8 row(s) read, 8 named.
			//   read whole    : rows 4, 5, 6, 7, 10
			//   read partial  : row 2.w row 3.x row 9.y
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerFrame  (declared by UnityCG.cginc)
			// $Globals cb0: 8 member(s) named from the asset's own constant-buffer reflection (176 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.uv0
				float4 o1;  // -> o.pos
				float4 o2;  // -> o.color0
				float4 r0 = 0, r1 = 0;
			
				o0.xy = float2(v2.x, v2.y);
			o1 = UnityObjectToClipPos(v0.xyz);
				o2.xyzw = float4(v1.x, v1.y, v1.z, v1.w);
			
				v2f o;
				o.uv0 = o0.xy;
				o.pos = o1;
				o.color0 = o2;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float2 v0 = i.uv0;
				float4 v1 = i.pos;
				float4 v2 = i.color0;
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = float4(mad(Coord2.z, 0.003906, -0.01), mad(Coord2.z, 0.003906, -0.7), mad(Coord2.x, 0.85, 0.03122), mad(Coord2.x, 0.85, 0.0003));
				r1.x = mad(Coord2.x, 0.85, -(r0.w));
				r1.x = 1.0 / r1.x;
				r2.y = v0.x;
				r0.zw = float2(mad(r2.y, 0.85, -(r0.z)), mad(r2.y, 0.85, -(r0.w)));
				r0.w = saturate(r1.x * r0.w);
				r0.xyz = float3(saturate(r0.x * 11.11111), saturate(r0.y * 3.571428), saturate(r0.z * 33333.6));
				r1.x = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.w = r0.w * r1.x;
				r1.x = mad(r0.z, -2.0, 3.0);
				r0.z = r0.z * r0.z;
				r0.z = mad(r1.x, r0.z, r0.w);
				r0.w = Coord2.w + 1.0;
				r0.w = r0.w * 0.1275;
				r1.xy = float2(mad(-(Coord2.y), r0.w, 1.1242), mad(-(Coord2.y), r0.w, 1.0003));
				r1.z = (_MainTex_TexelSize.y < 0.0) ? 1.0 : 0.0;
				r1.w = -(v0.y) + 1.0;
				r2.x = r1.z != 0.0 ? r1.w : v0.y;
				r1.z = mad(-(r2.x), r0.w, 1.0);
				r1.xy = float2(-(r1.x) + r1.z, -(r1.y) + r1.z);
				r1.xy = float2(saturate(r1.x * 1250.002), saturate(r1.y * -3332.78));
				r1.zw = float2(mad(r1.x, -2.0, 3.0), mad(r1.y, -2.0, 3.0));
				r1.xy = float2(r1.x * r1.x, r1.y * r1.y);
				r0.z = mad(r1.z, r1.x, r0.z);
				r0.z = mad(r1.w, r1.y, r0.z);
				r0.z = min(r0.z, 1.0);
				r1.xy = float2(Coord2.z * 0.03125, Coord2.x * 0.85);
				r3.xy = float2(-(r1.x), -(r1.y));
				r1.y = (r1.x >= r3.x) ? 1.0 : 0.0;
				r1.z = frac(r1.x);
				r1.x = floor(r1.x);
				r1.x = -(r1.x) + 8.0;
				r1.y = r1.y != 0.0 ? r1.z : -(r1.z);
				r4.x = r1.y * 1.0;
				r1.y = 0.125 / r0.w;
				r1.x = r1.x * r1.y;
				r4.y = r0.w * r1.x;
				r1.x = r0.w * Coord2.y;
				r5.w = r0.w * r2.x;
				r3.z = -(r1.x);
				r1.xy = float2(r3.y + r4.x, r3.z + r4.y);
				r5.xz = float2(r2.y * 0.85, r2.y * 0.85);
				r1.xy = float2(r1.x + r5.z, r1.y + r5.w);
				r1.xyzw = tex2D(Texture3, float2(r1.x, r1.y));
				r0.z = mad(r0.z, -(r1.y), r1.y);
				r1.xy = float2(mad(r0.x, -2.0, 3.0), mad(r0.y, -2.0, 3.0));
				r0.xy = float2(r0.x * r0.x, r0.y * r0.y);
				r0.y = r0.y * r1.y;
				r0.x = saturate(mad(r1.x, r0.x, -(r0.y)));
				r0.x = r0.x * r0.z;
				r0.yz = float2(Coord1.z * 0.85, Coord1.x * 0.85);
				r1.xy = float2(-(r0.y), -(r0.z));
				r0.z = (r0.y >= r1.x) ? 1.0 : 0.0;
				r0.w = frac(r0.y);
				r0.y = floor(r0.y);
				r0.y = -(r0.y) + 8.0;
				r0.z = r0.z != 0.0 ? r0.w : -(r0.w);
				r3.x = r0.z * 1.0;
				r0.z = Coord1.w + 1.0;
				r0.z = r0.z * 0.1275;
				r0.w = 0.125 / r0.z;
				r0.y = r0.y * r0.w;
				r3.y = r0.z * r0.y;
				r0.y = r0.z * Coord1.y;
				r1.z = -(r0.y);
				r0.yw = float2(r1.y + r3.x, r1.z + r3.y);
				r5.y = r0.z * r2.x;
				r0.yw = float2(r0.y + r5.x, r0.w + r5.y);
				r1.xyzw = tex2D(Texture3, float2(r0.y, r0.w));
				r3.xyzw = float4(mad(Coord1.z, 0.003906, -0.01), mad(Coord1.z, 0.003906, -0.7), mad(Coord1.x, 0.85, 0.03122), mad(Coord1.x, 0.85, 0.0003));
				r0.y = mad(Coord1.x, 0.85, -(r3.w));
				r0.y = 1.0 / r0.y;
				r1.yz = float2(mad(r2.y, 0.85, -(r3.z)), mad(r2.y, 0.85, -(r3.w)));
				r3.xy = float2(saturate(r3.x * 11.11111), saturate(r3.y * 3.571428));
				r0.y = saturate(r0.y * r1.z);
				r0.w = saturate(r1.y * 33333.6);
				r1.y = mad(r0.y, -2.0, 3.0);
				r0.y = r0.y * r0.y;
				r0.y = r0.y * r1.y;
				r1.y = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.y = mad(r1.y, r0.w, r0.y);
				r1.yz = float2(mad(-(Coord1.y), r0.z, 1.0003), mad(-(Coord1.y), r0.z, 1.0003));
				r0.z = mad(-(r2.x), r0.z, 1.0);
				r0.zw = float2(-(r1.y) + r0.z, -(r1.z) + r0.z);
				r0.zw = float2(saturate(r0.z * -3332.78), saturate(r0.w * -3332.78));
				r1.yz = float2(mad(r0.z, -2.0, 3.0), mad(r0.w, -2.0, 3.0));
				r0.zw = float2(r0.z * r0.z, r0.w * r0.w);
				r0.y = mad(r1.y, r0.z, r0.y);
				r0.y = mad(r1.z, r0.w, r0.y);
				r0.y = min(r0.y, 1.0);
				r0.y = mad(r0.y, -(r1.x), r1.x);
				r0.zw = float2(mad(r3.x, -2.0, 3.0), mad(r3.y, -2.0, 3.0));
				r1.xy = float2(r3.x * r3.x, r3.y * r3.y);
				r0.w = r0.w * r1.y;
				r0.z = saturate(mad(r0.z, r1.x, -(r0.w)));
				r0.x = mad(r0.y, r0.z, r0.x);
				r0.yz = float2(Coord3.z * 0.85, Coord3.x * 0.85);
				r1.xy = float2(-(r0.y), -(r0.z));
				r0.z = (r0.y >= r1.x) ? 1.0 : 0.0;
				r0.w = frac(r0.y);
				r0.y = floor(r0.y);
				r0.y = -(r0.y) + 8.0;
				r0.z = r0.z != 0.0 ? r0.w : -(r0.w);
				r3.x = r0.z * 1.0;
				r0.z = Coord3.w + 1.0;
				r0.z = r0.z * 0.1275;
				r0.w = 0.125 / r0.z;
				r0.y = r0.y * r0.w;
				r3.y = r0.z * r0.y;
				r0.y = r0.z * Coord3.y;
				r1.z = -(r0.y);
				r0.yw = float2(r1.y + r3.x, r1.z + r3.y);
				r1.y = r0.z * r2.x;
				r1.xz = float2(r2.y * 0.85, r2.y * 0.85);
				r0.yw = float2(r0.y + r1.x, r0.w + r1.y);
				r3.xyzw = tex2D(Texture3, float2(r0.y, r0.w));
				r4.xyzw = float4(mad(Coord3.z, 0.003906, -0.01), mad(Coord3.z, 0.003906, -0.7), mad(Coord3.x, 0.85, 0.03122), mad(Coord3.x, 0.85, 0.0003));
				r0.y = mad(Coord3.x, 0.85, -(r4.w));
				r0.y = 1.0 / r0.y;
				r1.xy = float2(mad(r2.y, 0.85, -(r4.z)), mad(r2.y, 0.85, -(r4.w)));
				r3.xy = float2(saturate(r4.x * 11.11111), saturate(r4.y * 3.571428));
				r0.y = saturate(r0.y * r1.y);
				r0.w = saturate(r1.x * 33333.6);
				r1.x = mad(r0.y, -2.0, 3.0);
				r0.y = r0.y * r0.y;
				r0.y = r0.y * r1.x;
				r1.x = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.y = mad(r1.x, r0.w, r0.y);
				r1.xy = float2(mad(-(Coord3.y), r0.z, 1.1242), mad(-(Coord3.y), r0.z, 1.0003));
				r0.z = mad(-(r2.x), r0.z, 1.0);
				r0.zw = float2(-(r1.x) + r0.z, -(r1.y) + r0.z);
				r0.zw = float2(saturate(r0.z * -3332.78), saturate(r0.w * -3332.78));
				r1.xy = float2(mad(r0.z, -2.0, 3.0), mad(r0.w, -2.0, 3.0));
				r0.zw = float2(r0.z * r0.z, r0.w * r0.w);
				r0.y = mad(r1.x, r0.z, r0.y);
				r0.y = mad(r1.y, r0.w, r0.y);
				r0.y = min(r0.y, 1.0);
				r0.y = mad(r0.y, -(r3.z), r3.z);
				r0.zw = float2(mad(r3.x, -2.0, 3.0), mad(r3.y, -2.0, 3.0));
				r1.xy = float2(r3.x * r3.x, r3.y * r3.y);
				r0.w = r0.w * r1.y;
				r0.z = saturate(mad(r0.z, r1.x, -(r0.w)));
				r0.x = mad(r0.y, r0.z, r0.x);
				r0.yz = float2(Coord4.z * 0.85, Coord4.x * 0.85);
				r3.xy = float2(-(r0.y), -(r0.z));
				r0.z = (r0.y >= r3.x) ? 1.0 : 0.0;
				r0.w = frac(r0.y);
				r0.y = floor(r0.y);
				r0.y = -(r0.y) + 8.0;
				r0.z = r0.z != 0.0 ? r0.w : -(r0.w);
				r1.x = r0.z * 1.0;
				r0.z = Coord4.w + 1.0;
				r0.z = r0.z * 0.1275;
				r0.w = 0.125 / r0.z;
				r0.y = r0.y * r0.w;
				r1.y = r0.z * r0.y;
				r0.y = r0.z * Coord4.y;
				r3.z = -(r0.y);
				r0.yw = float2(r1.x + r3.y, r1.y + r3.z);
				r1.w = r0.z * r2.x;
				r0.yw = float2(r0.y + r1.z, r0.w + r1.w);
				r1.xyzw = tex2D(Texture3, float2(r0.y, r0.w));
				r3.xyzw = float4(mad(Coord4.z, 0.003906, -0.01), mad(Coord4.z, 0.003906, -0.7), mad(Coord4.x, 0.85, 0.03122), mad(Coord4.x, 0.85, 0.0003));
				r0.y = mad(Coord4.x, 0.85, -(r3.w));
				r0.y = 1.0 / r0.y;
				r1.yz = float2(mad(r2.y, 0.85, -(r3.z)), mad(r2.y, 0.85, -(r3.w)));
				r3.xy = float2(saturate(r3.x * 11.11111), saturate(r3.y * 3.571428));
				r0.y = saturate(r0.y * r1.z);
				r0.w = saturate(r1.y * 33333.6);
				r1.y = mad(r0.y, -2.0, 3.0);
				r0.y = r0.y * r0.y;
				r0.y = r0.y * r1.y;
				r1.y = mad(r0.w, -2.0, 3.0);
				r0.w = r0.w * r0.w;
				r0.y = mad(r1.y, r0.w, r0.y);
				r1.yz = float2(mad(-(Coord4.y), r0.z, 1.0003), mad(-(Coord4.y), r0.z, 1.0003));
				r0.z = mad(-(r2.x), r0.z, 1.0);
				r0.zw = float2(-(r1.y) + r0.z, -(r1.z) + r0.z);
				r0.zw = float2(saturate(r0.z * -3332.78), saturate(r0.w * -3332.78));
				r1.yz = float2(mad(r0.z, -2.0, 3.0), mad(r0.w, -2.0, 3.0));
				r0.zw = float2(r0.z * r0.z, r0.w * r0.w);
				r0.y = mad(r1.y, r0.z, r0.y);
				r0.y = mad(r1.z, r0.w, r0.y);
				r0.y = min(r0.y, 1.0);
				r0.y = mad(r0.y, -(r1.x), r1.x);
				r0.zw = float2(mad(r3.x, -2.0, 3.0), mad(r3.y, -2.0, 3.0));
				r1.xy = float2(r3.x * r3.x, r3.y * r3.y);
				r0.w = r0.w * r1.y;
				r0.z = saturate(mad(r0.z, r1.x, -(r0.w)));
				r0.x = mad(r0.y, r0.z, r0.x);
				r2.z = r2.x + -0.16;
				r1.xyzw = tex2D(Texture2, float2(r2.y, r2.z));
				r3.xyz = float3(mad(r2.y, 0.75, 0.5), mad(r2.x, 0.75, 0.5), mad(r2.x, 0.75, 0.34));
				r0.y = r2.x + r1.z;
				r0.z = r1.z * r1.z;
				r0.w = -(r1.x) + 1.0;
				r1.x = _Speed * _Time.x;
				r2.y = mad(r1.x, r0.z, r0.y);
				r2.x = v0.x;
				r2.xyzw = tex2D(Texture2, float2(r2.x, r2.y));
				r0.x = mad(r2.y, r0.w, r0.x);
				r2.xyzw = tex2D(Texture2, float2(r3.x, r3.z));
				r0.y = r3.y + r2.z;
				r0.z = r2.z * r2.z;
				r0.w = -(r2.x) + 1.0;
				r3.w = mad(r1.x, r0.z, r0.y);
				r1.xyzw = tex2D(Texture2, float2(r3.x, r3.w));
				r0.x = mad(r1.y, r0.w, r0.x);
				r0.y = saturate(r0.x * _Value);
				r0.zw = float2(mad(v0.x, _MainTex_ST.x, _MainTex_ST.z), mad(v0.y, _MainTex_ST.y, _MainTex_ST.w));
				r0.x = 0.25;
				r0.xz = float2(mad(0.0, r0.x, r0.z), mad(0.25, r0.y, r0.w));
				r1.xyzw = tex2D(_MainTex, float2(r0.x, r0.z));
				o0.xyzw = float4(mad(0.35, r0.y, r1.x), mad(0.35, r0.y, r1.y), mad(0.35, r0.y, r1.z), mad(0.35, r0.y, r1.w));
				return o0;
			}
			#endif
			
			ENDCG
		}
	}
}
