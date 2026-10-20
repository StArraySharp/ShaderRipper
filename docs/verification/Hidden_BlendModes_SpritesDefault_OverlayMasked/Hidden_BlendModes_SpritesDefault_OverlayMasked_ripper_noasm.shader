// Reconstructed ShaderLab by ShaderRipper
// pathID=2949  platforms=[4]
Shader "Hidden/BlendModes/SpritesDefault/OverlayMasked"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		PixelSnap ("Pixel snap", Float) = 0
		[HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
		[HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
		[PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
		[PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
		_BLENDMODES_OverlayTexture ("Overlay Texture", 2D) = "white" {}
		_BLENDMODES_OverlayColor ("Overlay Color", Color) = (1,1,1,1)
		_BLENDMODES_StencilId ("Stencil ID", Float) = 0
		_BLENDMODES_BlendStencilComp ("Blend Stencil Comparison", Float) = 0
		_BLENDMODES_NormalStencilComp ("Normal Stencil Comparison", Float) = 1
	}

	SubShader
	{
		Tags { "CanUseSpriteAtlas"="True" "IgnoreProjector"="True" "PreviewType"="Plane" "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 100

		Pass
		{
			Tags { "LightMode"="Always" }
			ZWrite Off
			Cull Off
			Stencil
			{
				Ref [_BLENDMODES_StencilId]
				ReadMask 255
				WriteMask 255
				Comp [_BLENDMODES_BlendStencilComp]
			}
			Blend One OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#pragma target 4.0
			#pragma multi_compile __ BLENDMODES_MODE_COLOR
			#pragma multi_compile __ BLENDMODES_MODE_COLORBURN
			#pragma multi_compile __ BLENDMODES_MODE_COLORDODGE
			#pragma multi_compile __ BLENDMODES_MODE_DARKEN
			#pragma multi_compile __ BLENDMODES_MODE_DARKERCOLOR
			#pragma multi_compile __ BLENDMODES_MODE_DIFFERENCE
			#pragma multi_compile __ BLENDMODES_MODE_DIVIDE
			#pragma multi_compile __ BLENDMODES_MODE_EXCLUSION
			#pragma multi_compile __ BLENDMODES_MODE_HARDLIGHT
			#pragma multi_compile __ BLENDMODES_MODE_HARDMIX
			#pragma multi_compile __ BLENDMODES_MODE_HUE
			#pragma multi_compile __ BLENDMODES_MODE_LIGHTEN
			#pragma multi_compile __ BLENDMODES_MODE_LIGHTERCOLOR
			#pragma multi_compile __ BLENDMODES_MODE_LINEARBURN
			#pragma multi_compile __ BLENDMODES_MODE_LINEARDODGE
			#pragma multi_compile __ BLENDMODES_MODE_LINEARLIGHT
			#pragma multi_compile __ BLENDMODES_MODE_LUMINOSITY
			#pragma multi_compile __ BLENDMODES_MODE_MULTIPLY
			#pragma multi_compile __ BLENDMODES_MODE_OVERLAY
			#pragma multi_compile __ BLENDMODES_MODE_PINLIGHT
			#pragma multi_compile __ BLENDMODES_MODE_SATURATION
			#pragma multi_compile __ BLENDMODES_MODE_SCREEN
			#pragma multi_compile __ BLENDMODES_MODE_SOFTLIGHT
			#pragma multi_compile __ BLENDMODES_MODE_SUBTRACT
			#pragma multi_compile __ BLENDMODES_MODE_VIVIDLIGHT
			#pragma multi_compile __ ETC1_EXTERNAL_ALPHA
			#pragma multi_compile __ PIXELSNAP_ON
			
			// 100 keyword variant(s), 27 independent toggle(s)
			// variants: partial coverage (100/134217728): 134217628 combination(s) absent from the build = stripped variants (shader_feature or unused); runtime can never select them, guard chain is complete as-is
			
			// ---- variant 0: BLENDMODES_MODE_COLOR ----
			#if defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			float4 _MainTex_TexelSize;
			float PixelSnap;
			sampler2D _AlphaTex;
			float4 _AlphaTex_ST;
			float4 _AlphaTex_TexelSize;
			float _EnableExternalAlpha;
			sampler2D _BLENDMODES_OverlayTexture;
			float4 _BLENDMODES_OverlayTexture_TexelSize;
			float _BLENDMODES_StencilId;
			float _BLENDMODES_BlendStencilComp;
			float _BLENDMODES_NormalStencilComp;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r3.xyz = float3(r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z, r2.x * _BLENDMODES_OverlayColor.x);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r0.w = r0.w + r2.w;
				r4.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = max(r3.y, r3.x);
				r2.w = max(r2.w, r3.z);
				r3.w = min(r3.y, r3.x);
				r3.w = min(r3.w, r3.z);
				r4.w = r2.w + r3.w;
				r5.x = (r2.w == r3.w) ? 1.0 : 0.0;
				r5.y = r2.w + -(r3.w);
				r5.z = (1.0 < r4.w) ? 1.0 : 0.0;
				r2.w = -(r2.w) + -(r3.w);
				r2.w = r2.w + 2.0;
				r2.w = r5.y / r2.w;
				r3.w = r5.y / r4.w;
				r6.x = r5.z != 0.0 ? r2.w : r3.w;
				r7.xyzw = float4((r3.x < r3.z) ? 1.0 : 0.0, (r3.y < r3.z) ? 1.0 : 0.0, (r3.x < r3.y) ? 1.0 : 0.0, (r3.y < r3.x) ? 1.0 : 0.0);
				r2.w = ((((r7.y) != 0.0) && ((r7.x) != 0.0)) ? 1.0 : 0.0);
				r2.xyz = float3(mad(r2.y, _BLENDMODES_OverlayColor.y, -(r3.y)), mad(r2.z, _BLENDMODES_OverlayColor.z, -(r3.z)), mad(r2.x, _BLENDMODES_OverlayColor.x, -(r3.x)));
				r2.xyz = float3(r2.x / r5.y, r2.y / r5.y, r2.z / r5.y);
				r3.x = (((r7.z) != 0.0) ? 6.0 : 0.0);
				r2.x = r2.x + r3.x;
				r2.yz = float2(r2.y + 4.0, r2.z + 4.0);
				r2.y = r7.w != 0.0 ? r2.y : r2.z;
				r2.x = r2.w != 0.0 ? r2.x : r2.y;
				r6.yzw = float3(r2.x * 0.166667, r2.x * 0.166667, r2.x * 0.166667);
				r2.xyzw = float4(r5.x != 0.0 ? 0.0 : r6.x, r5.x != 0.0 ? 0.0 : r6.y, r5.x != 0.0 ? 0.0 : r6.z, r5.x != 0.0 ? 0.0 : r6.w);
				r3.x = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r3.x)) {
					r3.x = (r0.w < 1.0) ? 1.0 : 0.0;
					r5.xyzw = float4(r2.x + 1.0, r2.y + 0.333333, r2.z + 1.333333, r2.w + 1.0);
					r2.y = r4.z * r5.x;
					r2.z = mad(r0.w, 0.5, r2.x);
					r2.x = mad(-(r4.z), r2.x, r2.z);
					r2.x = r3.x != 0.0 ? r2.y : r2.x;
					r0.w = r0.w + -(r2.x);
					r2.y = (r5.y < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.z : r5.y;
					r2.z = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.x = r2.y + -1.0;
					r2.y = r2.z != 0.0 ? r3.x : r2.y;
					r2.z = -(r0.w) + r2.x;
					r3.x = r2.z * 6.0;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.x = r3.z != 0.0 ? r2.y : r3.y;
					r2.y = (r2.w < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.w : r2.w;
					r3.y = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.z = r2.y + -1.0;
					r2.y = r3.y != 0.0 ? r3.z : r2.y;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.y = r3.z != 0.0 ? r2.y : r3.y;
					r2.yw = float2(r2.w + 0.666667, r2.w + 0.666667);
					r3.y = (r2.y < 0.0) ? 1.0 : 0.0;
					r2.y = r3.y != 0.0 ? r2.w : r2.y;
					r2.w = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.y = r2.y + -1.0;
					r2.y = r2.w != 0.0 ? r3.y : r2.y;
					r2.w = mad(r3.x, r2.y, r0.w);
					r3.x = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r3.yz = float2((r2.y < 0.666667) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r0.w = r3.z != 0.0 ? r2.y : r0.w;
					r0.w = r3.y != 0.0 ? r2.x : r0.w;
					r4.z = r3.x != 0.0 ? r0.w : r2.w;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r4.x), mad(-(r0.y), v1.y, r4.y), mad(-(r0.z), v1.z, r4.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 1: BLENDMODES_MODE_COLOR + ETC1_EXTERNAL_ALPHA ----
			#elif defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r3.xyz = float3(r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z, r2.x * _BLENDMODES_OverlayColor.x);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r0.w = r0.w + r2.w;
				r4.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = max(r3.y, r3.x);
				r2.w = max(r2.w, r3.z);
				r3.w = min(r3.y, r3.x);
				r3.w = min(r3.w, r3.z);
				r4.w = r2.w + r3.w;
				r5.x = (r2.w == r3.w) ? 1.0 : 0.0;
				r5.y = r2.w + -(r3.w);
				r5.z = (1.0 < r4.w) ? 1.0 : 0.0;
				r2.w = -(r2.w) + -(r3.w);
				r2.w = r2.w + 2.0;
				r2.w = r5.y / r2.w;
				r3.w = r5.y / r4.w;
				r6.x = r5.z != 0.0 ? r2.w : r3.w;
				r7.xyzw = float4((r3.x < r3.z) ? 1.0 : 0.0, (r3.y < r3.z) ? 1.0 : 0.0, (r3.x < r3.y) ? 1.0 : 0.0, (r3.y < r3.x) ? 1.0 : 0.0);
				r2.w = ((((r7.y) != 0.0) && ((r7.x) != 0.0)) ? 1.0 : 0.0);
				r2.xyz = float3(mad(r2.y, _BLENDMODES_OverlayColor.y, -(r3.y)), mad(r2.z, _BLENDMODES_OverlayColor.z, -(r3.z)), mad(r2.x, _BLENDMODES_OverlayColor.x, -(r3.x)));
				r2.xyz = float3(r2.x / r5.y, r2.y / r5.y, r2.z / r5.y);
				r3.x = (((r7.z) != 0.0) ? 6.0 : 0.0);
				r2.x = r2.x + r3.x;
				r2.yz = float2(r2.y + 4.0, r2.z + 4.0);
				r2.y = r7.w != 0.0 ? r2.y : r2.z;
				r2.x = r2.w != 0.0 ? r2.x : r2.y;
				r6.yzw = float3(r2.x * 0.166667, r2.x * 0.166667, r2.x * 0.166667);
				r2.xyzw = float4(r5.x != 0.0 ? 0.0 : r6.x, r5.x != 0.0 ? 0.0 : r6.y, r5.x != 0.0 ? 0.0 : r6.z, r5.x != 0.0 ? 0.0 : r6.w);
				r3.x = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r3.x)) {
					r3.x = (r0.w < 1.0) ? 1.0 : 0.0;
					r5.xyzw = float4(r2.x + 1.0, r2.y + 0.333333, r2.z + 1.333333, r2.w + 1.0);
					r2.y = r4.z * r5.x;
					r2.z = mad(r0.w, 0.5, r2.x);
					r2.x = mad(-(r4.z), r2.x, r2.z);
					r2.x = r3.x != 0.0 ? r2.y : r2.x;
					r0.w = r0.w + -(r2.x);
					r2.y = (r5.y < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.z : r5.y;
					r2.z = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.x = r2.y + -1.0;
					r2.y = r2.z != 0.0 ? r3.x : r2.y;
					r2.z = -(r0.w) + r2.x;
					r3.x = r2.z * 6.0;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.x = r3.z != 0.0 ? r2.y : r3.y;
					r2.y = (r2.w < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.w : r2.w;
					r3.y = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.z = r2.y + -1.0;
					r2.y = r3.y != 0.0 ? r3.z : r2.y;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.y = r3.z != 0.0 ? r2.y : r3.y;
					r2.yw = float2(r2.w + 0.666667, r2.w + 0.666667);
					r3.y = (r2.y < 0.0) ? 1.0 : 0.0;
					r2.y = r3.y != 0.0 ? r2.w : r2.y;
					r2.w = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.y = r2.y + -1.0;
					r2.y = r2.w != 0.0 ? r3.y : r2.y;
					r2.w = mad(r3.x, r2.y, r0.w);
					r3.x = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r3.yz = float2((r2.y < 0.666667) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r0.w = r3.z != 0.0 ? r2.y : r0.w;
					r0.w = r3.y != 0.0 ? r2.x : r0.w;
					r4.z = r3.x != 0.0 ? r0.w : r2.w;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r4.x), mad(-(r0.y), v1.y, r4.y), mad(-(r0.z), v1.z, r4.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 2: BLENDMODES_MODE_COLOR + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r3.xyz = float3(r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z, r2.x * _BLENDMODES_OverlayColor.x);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r0.w = r0.w + r2.w;
				r4.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = max(r3.y, r3.x);
				r2.w = max(r2.w, r3.z);
				r3.w = min(r3.y, r3.x);
				r3.w = min(r3.w, r3.z);
				r4.w = r2.w + r3.w;
				r5.x = (r2.w == r3.w) ? 1.0 : 0.0;
				r5.y = r2.w + -(r3.w);
				r5.z = (1.0 < r4.w) ? 1.0 : 0.0;
				r2.w = -(r2.w) + -(r3.w);
				r2.w = r2.w + 2.0;
				r2.w = r5.y / r2.w;
				r3.w = r5.y / r4.w;
				r6.x = r5.z != 0.0 ? r2.w : r3.w;
				r7.xyzw = float4((r3.x < r3.z) ? 1.0 : 0.0, (r3.y < r3.z) ? 1.0 : 0.0, (r3.x < r3.y) ? 1.0 : 0.0, (r3.y < r3.x) ? 1.0 : 0.0);
				r2.w = ((((r7.y) != 0.0) && ((r7.x) != 0.0)) ? 1.0 : 0.0);
				r2.xyz = float3(mad(r2.y, _BLENDMODES_OverlayColor.y, -(r3.y)), mad(r2.z, _BLENDMODES_OverlayColor.z, -(r3.z)), mad(r2.x, _BLENDMODES_OverlayColor.x, -(r3.x)));
				r2.xyz = float3(r2.x / r5.y, r2.y / r5.y, r2.z / r5.y);
				r3.x = (((r7.z) != 0.0) ? 6.0 : 0.0);
				r2.x = r2.x + r3.x;
				r2.yz = float2(r2.y + 4.0, r2.z + 4.0);
				r2.y = r7.w != 0.0 ? r2.y : r2.z;
				r2.x = r2.w != 0.0 ? r2.x : r2.y;
				r6.yzw = float3(r2.x * 0.166667, r2.x * 0.166667, r2.x * 0.166667);
				r2.xyzw = float4(r5.x != 0.0 ? 0.0 : r6.x, r5.x != 0.0 ? 0.0 : r6.y, r5.x != 0.0 ? 0.0 : r6.z, r5.x != 0.0 ? 0.0 : r6.w);
				r3.x = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r3.x)) {
					r3.x = (r0.w < 1.0) ? 1.0 : 0.0;
					r5.xyzw = float4(r2.x + 1.0, r2.y + 0.333333, r2.z + 1.333333, r2.w + 1.0);
					r2.y = r4.z * r5.x;
					r2.z = mad(r0.w, 0.5, r2.x);
					r2.x = mad(-(r4.z), r2.x, r2.z);
					r2.x = r3.x != 0.0 ? r2.y : r2.x;
					r0.w = r0.w + -(r2.x);
					r2.y = (r5.y < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.z : r5.y;
					r2.z = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.x = r2.y + -1.0;
					r2.y = r2.z != 0.0 ? r3.x : r2.y;
					r2.z = -(r0.w) + r2.x;
					r3.x = r2.z * 6.0;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.x = r3.z != 0.0 ? r2.y : r3.y;
					r2.y = (r2.w < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.w : r2.w;
					r3.y = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.z = r2.y + -1.0;
					r2.y = r3.y != 0.0 ? r3.z : r2.y;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.y = r3.z != 0.0 ? r2.y : r3.y;
					r2.yw = float2(r2.w + 0.666667, r2.w + 0.666667);
					r3.y = (r2.y < 0.0) ? 1.0 : 0.0;
					r2.y = r3.y != 0.0 ? r2.w : r2.y;
					r2.w = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.y = r2.y + -1.0;
					r2.y = r2.w != 0.0 ? r3.y : r2.y;
					r2.w = mad(r3.x, r2.y, r0.w);
					r3.x = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r3.yz = float2((r2.y < 0.666667) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r0.w = r3.z != 0.0 ? r2.y : r0.w;
					r0.w = r3.y != 0.0 ? r2.x : r0.w;
					r4.z = r3.x != 0.0 ? r0.w : r2.w;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r4.x), mad(-(r0.y), v1.y, r4.y), mad(-(r0.z), v1.z, r4.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 3: BLENDMODES_MODE_COLOR + PIXELSNAP_ON ----
			#elif defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r3.xyz = float3(r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z, r2.x * _BLENDMODES_OverlayColor.x);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r0.w = r0.w + r2.w;
				r4.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = max(r3.y, r3.x);
				r2.w = max(r2.w, r3.z);
				r3.w = min(r3.y, r3.x);
				r3.w = min(r3.w, r3.z);
				r4.w = r2.w + r3.w;
				r5.x = (r2.w == r3.w) ? 1.0 : 0.0;
				r5.y = r2.w + -(r3.w);
				r5.z = (1.0 < r4.w) ? 1.0 : 0.0;
				r2.w = -(r2.w) + -(r3.w);
				r2.w = r2.w + 2.0;
				r2.w = r5.y / r2.w;
				r3.w = r5.y / r4.w;
				r6.x = r5.z != 0.0 ? r2.w : r3.w;
				r7.xyzw = float4((r3.x < r3.z) ? 1.0 : 0.0, (r3.y < r3.z) ? 1.0 : 0.0, (r3.x < r3.y) ? 1.0 : 0.0, (r3.y < r3.x) ? 1.0 : 0.0);
				r2.w = ((((r7.y) != 0.0) && ((r7.x) != 0.0)) ? 1.0 : 0.0);
				r2.xyz = float3(mad(r2.y, _BLENDMODES_OverlayColor.y, -(r3.y)), mad(r2.z, _BLENDMODES_OverlayColor.z, -(r3.z)), mad(r2.x, _BLENDMODES_OverlayColor.x, -(r3.x)));
				r2.xyz = float3(r2.x / r5.y, r2.y / r5.y, r2.z / r5.y);
				r3.x = (((r7.z) != 0.0) ? 6.0 : 0.0);
				r2.x = r2.x + r3.x;
				r2.yz = float2(r2.y + 4.0, r2.z + 4.0);
				r2.y = r7.w != 0.0 ? r2.y : r2.z;
				r2.x = r2.w != 0.0 ? r2.x : r2.y;
				r6.yzw = float3(r2.x * 0.166667, r2.x * 0.166667, r2.x * 0.166667);
				r2.xyzw = float4(r5.x != 0.0 ? 0.0 : r6.x, r5.x != 0.0 ? 0.0 : r6.y, r5.x != 0.0 ? 0.0 : r6.z, r5.x != 0.0 ? 0.0 : r6.w);
				r3.x = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r3.x)) {
					r3.x = (r0.w < 1.0) ? 1.0 : 0.0;
					r5.xyzw = float4(r2.x + 1.0, r2.y + 0.333333, r2.z + 1.333333, r2.w + 1.0);
					r2.y = r4.z * r5.x;
					r2.z = mad(r0.w, 0.5, r2.x);
					r2.x = mad(-(r4.z), r2.x, r2.z);
					r2.x = r3.x != 0.0 ? r2.y : r2.x;
					r0.w = r0.w + -(r2.x);
					r2.y = (r5.y < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.z : r5.y;
					r2.z = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.x = r2.y + -1.0;
					r2.y = r2.z != 0.0 ? r3.x : r2.y;
					r2.z = -(r0.w) + r2.x;
					r3.x = r2.z * 6.0;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.x = r3.z != 0.0 ? r2.y : r3.y;
					r2.y = (r2.w < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.w : r2.w;
					r3.y = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.z = r2.y + -1.0;
					r2.y = r3.y != 0.0 ? r3.z : r2.y;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.y = r3.z != 0.0 ? r2.y : r3.y;
					r2.yw = float2(r2.w + 0.666667, r2.w + 0.666667);
					r3.y = (r2.y < 0.0) ? 1.0 : 0.0;
					r2.y = r3.y != 0.0 ? r2.w : r2.y;
					r2.w = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.y = r2.y + -1.0;
					r2.y = r2.w != 0.0 ? r3.y : r2.y;
					r2.w = mad(r3.x, r2.y, r0.w);
					r3.x = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r3.yz = float2((r2.y < 0.666667) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r0.w = r3.z != 0.0 ? r2.y : r0.w;
					r0.w = r3.y != 0.0 ? r2.x : r0.w;
					r4.z = r3.x != 0.0 ? r0.w : r2.w;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r4.x), mad(-(r0.y), v1.y, r4.y), mad(-(r0.z), v1.z, r4.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 4: BLENDMODES_MODE_COLORBURN ----
			#elif !defined(BLENDMODES_MODE_COLOR) && defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3(r2.x / r0.x, r2.y / r0.y, r2.z / r0.z);
				r0.xyz = float3(saturate(-(r0.x) + 1.0), saturate(-(r0.y) + 1.0), saturate(-(r0.z) + 1.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 5: BLENDMODES_MODE_COLORBURN + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3(r2.x / r0.x, r2.y / r0.y, r2.z / r0.z);
				r0.xyz = float3(saturate(-(r0.x) + 1.0), saturate(-(r0.y) + 1.0), saturate(-(r0.z) + 1.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r0.w = -(r1.w) + r2.x;
				r1.w = mad(_EnableExternalAlpha, r0.w, r1.w);
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 6: BLENDMODES_MODE_COLORBURN + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3(r2.x / r0.x, r2.y / r0.y, r2.z / r0.z);
				r0.xyz = float3(saturate(-(r0.x) + 1.0), saturate(-(r0.y) + 1.0), saturate(-(r0.z) + 1.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r0.w = -(r1.w) + r2.x;
				r1.w = mad(_EnableExternalAlpha, r0.w, r1.w);
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 7: BLENDMODES_MODE_COLORBURN + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3(r2.x / r0.x, r2.y / r0.y, r2.z / r0.z);
				r0.xyz = float3(saturate(-(r0.x) + 1.0), saturate(-(r0.y) + 1.0), saturate(-(r0.z) + 1.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 8: BLENDMODES_MODE_COLORDODGE ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(saturate(r2.x / r0.x), saturate(r2.y / r0.y), saturate(r2.z / r0.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 9: BLENDMODES_MODE_COLORDODGE + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(saturate(r1.x / r0.x), saturate(r1.y / r0.y), saturate(r1.z / r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 10: BLENDMODES_MODE_COLORDODGE + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(saturate(r1.x / r0.x), saturate(r1.y / r0.y), saturate(r1.z / r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 11: BLENDMODES_MODE_COLORDODGE + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(saturate(r2.x / r0.x), saturate(r2.y / r0.y), saturate(r2.z / r0.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 12: BLENDMODES_MODE_DARKEN ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(min(r0.x, r2.x), min(r0.y, r2.y), min(r0.z, r2.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 13: BLENDMODES_MODE_DARKEN + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(min(r0.x, r1.x), min(r0.y, r1.y), min(r0.z, r1.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 14: BLENDMODES_MODE_DARKEN + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(min(r0.x, r1.x), min(r0.y, r1.y), min(r0.z, r1.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 15: BLENDMODES_MODE_DARKEN + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(min(r0.x, r2.x), min(r0.y, r2.y), min(r0.z, r2.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 16: BLENDMODES_MODE_DARKERCOLOR ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r1.w = r2.x * 0.299 + r2.y * 0.587 + r2.z * 0.114;
				r0.w = (r1.w < r0.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r2.x : r0.x, r0.w != 0.0 ? r2.y : r0.y, r0.w != 0.0 ? r2.z : r0.z);
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 17: BLENDMODES_MODE_DARKERCOLOR + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.x = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r1.x, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r2.w = r1.x * 0.299 + r1.y * 0.587 + r1.z * 0.114;
				r0.w = (r2.w < r0.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r1.x : r0.x, r0.w != 0.0 ? r1.y : r0.y, r0.w != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 18: BLENDMODES_MODE_DARKERCOLOR + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.x = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r1.x, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r2.w = r1.x * 0.299 + r1.y * 0.587 + r1.z * 0.114;
				r0.w = (r2.w < r0.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r1.x : r0.x, r0.w != 0.0 ? r1.y : r0.y, r0.w != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 19: BLENDMODES_MODE_DARKERCOLOR + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r1.w = r2.x * 0.299 + r2.y * 0.587 + r2.z * 0.114;
				r0.w = (r1.w < r0.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r2.x : r0.x, r0.w != 0.0 ? r2.y : r0.y, r0.w != 0.0 ? r2.z : r0.z);
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 20: BLENDMODES_MODE_DIFFERENCE ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r2.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r1.xyz = float3(mad(-(r1.x), _BLENDMODES_OverlayColor.x, r2.x), mad(-(r1.y), _BLENDMODES_OverlayColor.y, r2.y), mad(-(r1.z), _BLENDMODES_OverlayColor.z, r2.z));
				r0.xyz = float3(mad(-(r0.x), v1.x, r1.x), mad(-(r0.y), v1.y, r1.y), mad(-(r0.z), v1.z, r1.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 21: BLENDMODES_MODE_DIFFERENCE + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, r1.x), mad(-(r0.y), _BLENDMODES_OverlayColor.y, r1.y), mad(-(r0.z), _BLENDMODES_OverlayColor.z, r1.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 22: BLENDMODES_MODE_DIFFERENCE + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, r1.x), mad(-(r0.y), _BLENDMODES_OverlayColor.y, r1.y), mad(-(r0.z), _BLENDMODES_OverlayColor.z, r1.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 23: BLENDMODES_MODE_DIFFERENCE + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r2.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r1.xyz = float3(mad(-(r1.x), _BLENDMODES_OverlayColor.x, r2.x), mad(-(r1.y), _BLENDMODES_OverlayColor.y, r2.y), mad(-(r1.z), _BLENDMODES_OverlayColor.z, r2.z));
				r0.xyz = float3(mad(-(r0.x), v1.x, r1.x), mad(-(r0.y), v1.y, r1.y), mad(-(r0.z), v1.z, r1.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 24: BLENDMODES_MODE_DIVIDE ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(saturate(r2.x / r0.x), saturate(r2.y / r0.y), saturate(r2.z / r0.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 25: BLENDMODES_MODE_DIVIDE + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(saturate(r1.x / r0.x), saturate(r1.y / r0.y), saturate(r1.z / r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 26: BLENDMODES_MODE_DIVIDE + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(saturate(r1.x / r0.x), saturate(r1.y / r0.y), saturate(r1.z / r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 27: BLENDMODES_MODE_DIVIDE + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(saturate(r2.x / r0.x), saturate(r2.y / r0.y), saturate(r2.z / r0.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 28: BLENDMODES_MODE_EXCLUSION ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(r1.x, v1.x, r0.x), mad(r1.y, v1.y, r0.y), mad(r1.z, v1.z, r0.z));
				r3.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(r0.x * r3.x, r0.y * r3.y, r0.z * r3.z);
				r0.xyz = float3(mad(-(r0.x), 2.0, r2.x), mad(-(r0.y), 2.0, r2.y), mad(-(r0.z), 2.0, r2.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r3.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r3.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r3.z));
				o0.xyz = float3(r3.w * r0.x, r3.w * r0.y, r3.w * r0.z);
				o0.w = r3.w;
				return o0;
			}
			
			// ---- variant 29: BLENDMODES_MODE_EXCLUSION + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r3.xyz = float3(r0.x * r1.x, r0.y * r1.y, r0.z * r1.z);
				r0.xyz = float3(mad(r2.x, v1.x, r0.x), mad(r2.y, v1.y, r0.y), mad(r2.z, v1.z, r0.z));
				r0.xyz = float3(mad(-(r3.x), 2.0, r0.x), mad(-(r3.y), 2.0, r0.y), mad(-(r3.z), 2.0, r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 30: BLENDMODES_MODE_EXCLUSION + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r3.xyz = float3(r0.x * r1.x, r0.y * r1.y, r0.z * r1.z);
				r0.xyz = float3(mad(r2.x, v1.x, r0.x), mad(r2.y, v1.y, r0.y), mad(r2.z, v1.z, r0.z));
				r0.xyz = float3(mad(-(r3.x), 2.0, r0.x), mad(-(r3.y), 2.0, r0.y), mad(-(r3.z), 2.0, r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 31: BLENDMODES_MODE_EXCLUSION + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(r1.x, v1.x, r0.x), mad(r1.y, v1.y, r0.y), mad(r1.z, v1.z, r0.z));
				r3.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(r0.x * r3.x, r0.y * r3.y, r0.z * r3.z);
				r0.xyz = float3(mad(-(r0.x), 2.0, r2.x), mad(-(r0.y), 2.0, r2.y), mad(-(r0.z), 2.0, r2.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r3.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r3.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r3.z));
				o0.xyz = float3(r3.w * r0.x, r3.w * r0.y, r3.w * r0.z);
				o0.w = r3.w;
				return o0;
			}
			
			// ---- variant 32: BLENDMODES_MODE_HARDLIGHT ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyz = float3(mad(-(r1.x), 2.0, 1.0), mad(-(r1.y), 2.0, 1.0), mad(-(r1.z), 2.0, 1.0));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r1.xyz = float3(mad(-(r3.x), r1.x, 1.0), mad(-(r3.y), r1.y, 1.0), mad(-(r3.z), r1.z, 1.0));
				r3.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r4.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r4.x, r0.y * r4.y, r0.z * r4.z);
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r0.xyz = float3(r3.x != 0.0 ? r1.x : r0.x, r3.y != 0.0 ? r1.y : r0.y, r3.z != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 33: BLENDMODES_MODE_HARDLIGHT + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.x = r0.x + -(r1.w);
				r1.w = mad(_EnableExternalAlpha, r0.x, r1.w);
				r0.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r3.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r2.xyz = float3(mad(r2.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r2.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r2.z, _BLENDMODES_OverlayColor.z, -0.5));
				r2.xyz = float3(mad(-(r2.x), 2.0, 1.0), mad(-(r2.y), 2.0, 1.0), mad(-(r2.z), 2.0, 1.0));
				r4.xyz = float3(r0.x * r3.x, r0.y * r3.y, r0.z * r3.z);
				r3.xyz = float3((0.5 < r3.x) ? 1.0 : 0.0, (0.5 < r3.y) ? 1.0 : 0.0, (0.5 < r3.z) ? 1.0 : 0.0);
				r4.xyz = float3(r4.x + r4.x, r4.y + r4.y, r4.z + r4.z);
				r5.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r2.xyz = float3(mad(-(r5.x), r2.x, 1.0), mad(-(r5.y), r2.y, 1.0), mad(-(r5.z), r2.z, 1.0));
				r2.xyz = float3(r3.x != 0.0 ? r2.x : r4.x, r3.y != 0.0 ? r2.y : r4.y, r3.z != 0.0 ? r2.z : r4.z);
				r1.xyz = float3(mad(-(r1.x), v1.x, r2.x), mad(-(r1.y), v1.y, r2.y), mad(-(r1.z), v1.z, r2.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r1.x, r0.x), mad(_BLENDMODES_OverlayColor.w, r1.y, r0.y), mad(_BLENDMODES_OverlayColor.w, r1.z, r0.z));
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
				return o0;
			}
			
			// ---- variant 34: BLENDMODES_MODE_HARDLIGHT + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.x = r0.x + -(r1.w);
				r1.w = mad(_EnableExternalAlpha, r0.x, r1.w);
				r0.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r3.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r2.xyz = float3(mad(r2.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r2.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r2.z, _BLENDMODES_OverlayColor.z, -0.5));
				r2.xyz = float3(mad(-(r2.x), 2.0, 1.0), mad(-(r2.y), 2.0, 1.0), mad(-(r2.z), 2.0, 1.0));
				r4.xyz = float3(r0.x * r3.x, r0.y * r3.y, r0.z * r3.z);
				r3.xyz = float3((0.5 < r3.x) ? 1.0 : 0.0, (0.5 < r3.y) ? 1.0 : 0.0, (0.5 < r3.z) ? 1.0 : 0.0);
				r4.xyz = float3(r4.x + r4.x, r4.y + r4.y, r4.z + r4.z);
				r5.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r2.xyz = float3(mad(-(r5.x), r2.x, 1.0), mad(-(r5.y), r2.y, 1.0), mad(-(r5.z), r2.z, 1.0));
				r2.xyz = float3(r3.x != 0.0 ? r2.x : r4.x, r3.y != 0.0 ? r2.y : r4.y, r3.z != 0.0 ? r2.z : r4.z);
				r1.xyz = float3(mad(-(r1.x), v1.x, r2.x), mad(-(r1.y), v1.y, r2.y), mad(-(r1.z), v1.z, r2.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r1.x, r0.x), mad(_BLENDMODES_OverlayColor.w, r1.y, r0.y), mad(_BLENDMODES_OverlayColor.w, r1.z, r0.z));
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
				return o0;
			}
			
			// ---- variant 35: BLENDMODES_MODE_HARDLIGHT + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyz = float3(mad(-(r1.x), 2.0, 1.0), mad(-(r1.y), 2.0, 1.0), mad(-(r1.z), 2.0, 1.0));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r1.xyz = float3(mad(-(r3.x), r1.x, 1.0), mad(-(r3.y), r1.y, 1.0), mad(-(r3.z), r1.z, 1.0));
				r3.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r4.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r4.x, r0.y * r4.y, r0.z * r4.z);
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r0.xyz = float3(r3.x != 0.0 ? r1.x : r0.x, r3.y != 0.0 ? r1.y : r0.y, r3.z != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 36: BLENDMODES_MODE_HARDMIX ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3((r2.x < r0.x) ? 1.0 : 0.0, (r2.y < r0.y) ? 1.0 : 0.0, (r2.z < r0.z) ? 1.0 : 0.0);
				r0.xyz = float3((((r0.x) != 0.0) ? 1.0 : 0.0), (((r0.y) != 0.0) ? 1.0 : 0.0), (((r0.z) != 0.0) ? 1.0 : 0.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 37: BLENDMODES_MODE_HARDMIX + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3((r2.x < r0.x) ? 1.0 : 0.0, (r2.y < r0.y) ? 1.0 : 0.0, (r2.z < r0.z) ? 1.0 : 0.0);
				r0.xyz = float3((((r0.x) != 0.0) ? 1.0 : 0.0), (((r0.y) != 0.0) ? 1.0 : 0.0), (((r0.z) != 0.0) ? 1.0 : 0.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r0.w = -(r1.w) + r2.x;
				r1.w = mad(_EnableExternalAlpha, r0.w, r1.w);
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 38: BLENDMODES_MODE_HARDMIX + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3((r2.x < r0.x) ? 1.0 : 0.0, (r2.y < r0.y) ? 1.0 : 0.0, (r2.z < r0.z) ? 1.0 : 0.0);
				r0.xyz = float3((((r0.x) != 0.0) ? 1.0 : 0.0), (((r0.y) != 0.0) ? 1.0 : 0.0), (((r0.z) != 0.0) ? 1.0 : 0.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r0.w = -(r1.w) + r2.x;
				r1.w = mad(_EnableExternalAlpha, r0.w, r1.w);
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 39: BLENDMODES_MODE_HARDMIX + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r0.xyz = float3((r2.x < r0.x) ? 1.0 : 0.0, (r2.y < r0.y) ? 1.0 : 0.0, (r2.z < r0.z) ? 1.0 : 0.0);
				r0.xyz = float3((((r0.x) != 0.0) ? 1.0 : 0.0), (((r0.y) != 0.0) ? 1.0 : 0.0), (((r0.z) != 0.0) ? 1.0 : 0.0));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 40: BLENDMODES_MODE_HUE ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.x = min(r1.z, r1.y);
				r2.x = min(r1.x, r2.x);
				r2.y = r0.w + r2.x;
				r3.xyz = float3(r2.y * 0.5, r2.y * 0.5, r2.y * 0.5);
				r2.z = (r0.w == r2.x) ? 1.0 : 0.0;
				r2.w = r0.w + -(r2.x);
				r3.w = (1.0 < r2.y) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.x);
				r0.w = r0.w + 2.0;
				r0.w = r2.w / r0.w;
				r2.x = r2.w / r2.y;
				r0.w = r3.w != 0.0 ? r0.w : r2.x;
				r0.w = r2.z != 0.0 ? 0.0 : r0.w;
				r2.x = (r0.w == 0.0) ? 1.0 : 0.0;
				if (!any(r2.x)) {
					r4.xyzw = tex2D(t1, float2(v2.z, v2.w));
					r2.xzw = float3(r4.y * _BLENDMODES_OverlayColor.y, r4.z * _BLENDMODES_OverlayColor.z, r4.x * _BLENDMODES_OverlayColor.x);
					r3.w = max(r2.z, r2.x);
					r3.w = max(r2.w, r3.w);
					r4.w = min(r2.z, r2.x);
					r4.w = min(r2.w, r4.w);
					r5.x = (r3.w == r4.w) ? 1.0 : 0.0;
					r3.w = r3.w + -(r4.w);
					r6.xyzw = float4((r2.x < r2.w) ? 1.0 : 0.0, (r2.z < r2.w) ? 1.0 : 0.0, (r2.x < r2.z) ? 1.0 : 0.0, (r2.z < r2.x) ? 1.0 : 0.0);
					r4.w = ((((r6.y) != 0.0) && ((r6.x) != 0.0)) ? 1.0 : 0.0);
					r2.xzw = float3(mad(r4.y, _BLENDMODES_OverlayColor.y, -(r2.z)), mad(r4.z, _BLENDMODES_OverlayColor.z, -(r2.w)), mad(r4.x, _BLENDMODES_OverlayColor.x, -(r2.x)));
					r2.xzw = float3(r2.x / r3.w, r2.z / r3.w, r2.w / r3.w);
					r3.w = (((r6.z) != 0.0) ? 6.0 : 0.0);
					r2.x = r2.x + r3.w;
					r2.zw = float2(r2.z + 4.0, r2.w + 4.0);
					r2.z = r6.w != 0.0 ? r2.z : r2.w;
					r2.x = r4.w != 0.0 ? r2.x : r2.z;
					r2.x = r2.x * 0.166667;
					r2.x = r5.x != 0.0 ? 0.0 : r2.x;
					r2.z = (r2.y < 1.0) ? 1.0 : 0.0;
					r2.w = r0.w + 1.0;
					r2.w = r2.w * r3.z;
					r3.w = mad(r2.y, 0.5, r0.w);
					r0.w = mad(-(r3.z), r0.w, r3.w);
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.y = -(r0.w) + r2.y;
					r4.xyzw = float4(r2.x + 0.333333, r2.x + 1.333333, r2.x + 1.0, r2.x + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.w = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.w != 0.0 ? r4.x : r2.z;
					r3.w = r0.w + -(r2.y);
					r4.x = r3.w * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r0.w : r2.z;
					r3.x = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r2.x < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r2.x;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r0.w : r2.z;
					r3.y = r4.z != 0.0 ? r2.z : r4.y;
					r2.x = r2.x + 0.666667;
					r2.x = r2.w != 0.0 ? r2.x : r4.w;
					r2.z = (1.0 < r2.x) ? 1.0 : 0.0;
					r2.w = r2.x + -1.0;
					r2.x = r2.z != 0.0 ? r2.w : r2.x;
					r2.z = mad(r4.x, r2.x, r2.y);
					r2.w = (r2.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r2.x < 0.5) ? 1.0 : 0.0, (r2.x < 0.666667) ? 1.0 : 0.0);
					r2.x = -(r2.x) + 0.666667;
					r2.x = r2.x * r3.w;
					r2.x = mad(r2.x, 6.0, r2.y);
					r2.x = r4.y != 0.0 ? r2.x : r2.y;
					r0.w = r4.x != 0.0 ? r0.w : r2.x;
					r3.z = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.x), mad(-(r0.y), v1.y, r3.y), mad(-(r0.z), v1.z, r3.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 41: BLENDMODES_MODE_HUE + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.x = min(r1.z, r1.y);
				r2.x = min(r1.x, r2.x);
				r2.y = r0.w + r2.x;
				r3.xyz = float3(r2.y * 0.5, r2.y * 0.5, r2.y * 0.5);
				r2.z = (r0.w == r2.x) ? 1.0 : 0.0;
				r2.w = r0.w + -(r2.x);
				r3.w = (1.0 < r2.y) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.x);
				r0.w = r0.w + 2.0;
				r0.w = r2.w / r0.w;
				r2.x = r2.w / r2.y;
				r0.w = r3.w != 0.0 ? r0.w : r2.x;
				r0.w = r2.z != 0.0 ? 0.0 : r0.w;
				r2.x = (r0.w == 0.0) ? 1.0 : 0.0;
				if (!any(r2.x)) {
					r4.xyzw = tex2D(t2, float2(v2.z, v2.w));
					r2.xzw = float3(r4.y * _BLENDMODES_OverlayColor.y, r4.z * _BLENDMODES_OverlayColor.z, r4.x * _BLENDMODES_OverlayColor.x);
					r3.w = max(r2.z, r2.x);
					r3.w = max(r2.w, r3.w);
					r4.w = min(r2.z, r2.x);
					r4.w = min(r2.w, r4.w);
					r5.x = (r3.w == r4.w) ? 1.0 : 0.0;
					r3.w = r3.w + -(r4.w);
					r6.xyzw = float4((r2.x < r2.w) ? 1.0 : 0.0, (r2.z < r2.w) ? 1.0 : 0.0, (r2.x < r2.z) ? 1.0 : 0.0, (r2.z < r2.x) ? 1.0 : 0.0);
					r4.w = ((((r6.y) != 0.0) && ((r6.x) != 0.0)) ? 1.0 : 0.0);
					r2.xzw = float3(mad(r4.y, _BLENDMODES_OverlayColor.y, -(r2.z)), mad(r4.z, _BLENDMODES_OverlayColor.z, -(r2.w)), mad(r4.x, _BLENDMODES_OverlayColor.x, -(r2.x)));
					r2.xzw = float3(r2.x / r3.w, r2.z / r3.w, r2.w / r3.w);
					r3.w = (((r6.z) != 0.0) ? 6.0 : 0.0);
					r2.x = r2.x + r3.w;
					r2.zw = float2(r2.z + 4.0, r2.w + 4.0);
					r2.z = r6.w != 0.0 ? r2.z : r2.w;
					r2.x = r4.w != 0.0 ? r2.x : r2.z;
					r2.x = r2.x * 0.166667;
					r2.x = r5.x != 0.0 ? 0.0 : r2.x;
					r2.z = (r2.y < 1.0) ? 1.0 : 0.0;
					r2.w = r0.w + 1.0;
					r2.w = r2.w * r3.z;
					r3.w = mad(r2.y, 0.5, r0.w);
					r0.w = mad(-(r3.z), r0.w, r3.w);
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.y = -(r0.w) + r2.y;
					r4.xyzw = float4(r2.x + 0.333333, r2.x + 1.333333, r2.x + 1.0, r2.x + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.w = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.w != 0.0 ? r4.x : r2.z;
					r3.w = r0.w + -(r2.y);
					r4.x = r3.w * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r0.w : r2.z;
					r3.x = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r2.x < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r2.x;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r0.w : r2.z;
					r3.y = r4.z != 0.0 ? r2.z : r4.y;
					r2.x = r2.x + 0.666667;
					r2.x = r2.w != 0.0 ? r2.x : r4.w;
					r2.z = (1.0 < r2.x) ? 1.0 : 0.0;
					r2.w = r2.x + -1.0;
					r2.x = r2.z != 0.0 ? r2.w : r2.x;
					r2.z = mad(r4.x, r2.x, r2.y);
					r2.w = (r2.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r2.x < 0.5) ? 1.0 : 0.0, (r2.x < 0.666667) ? 1.0 : 0.0);
					r2.x = -(r2.x) + 0.666667;
					r2.x = r2.x * r3.w;
					r2.x = mad(r2.x, 6.0, r2.y);
					r2.x = r4.y != 0.0 ? r2.x : r2.y;
					r0.w = r4.x != 0.0 ? r0.w : r2.x;
					r3.z = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.x), mad(-(r0.y), v1.y, r3.y), mad(-(r0.z), v1.z, r3.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 42: BLENDMODES_MODE_HUE + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.x = min(r1.z, r1.y);
				r2.x = min(r1.x, r2.x);
				r2.y = r0.w + r2.x;
				r3.xyz = float3(r2.y * 0.5, r2.y * 0.5, r2.y * 0.5);
				r2.z = (r0.w == r2.x) ? 1.0 : 0.0;
				r2.w = r0.w + -(r2.x);
				r3.w = (1.0 < r2.y) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.x);
				r0.w = r0.w + 2.0;
				r0.w = r2.w / r0.w;
				r2.x = r2.w / r2.y;
				r0.w = r3.w != 0.0 ? r0.w : r2.x;
				r0.w = r2.z != 0.0 ? 0.0 : r0.w;
				r2.x = (r0.w == 0.0) ? 1.0 : 0.0;
				if (!any(r2.x)) {
					r4.xyzw = tex2D(t2, float2(v2.z, v2.w));
					r2.xzw = float3(r4.y * _BLENDMODES_OverlayColor.y, r4.z * _BLENDMODES_OverlayColor.z, r4.x * _BLENDMODES_OverlayColor.x);
					r3.w = max(r2.z, r2.x);
					r3.w = max(r2.w, r3.w);
					r4.w = min(r2.z, r2.x);
					r4.w = min(r2.w, r4.w);
					r5.x = (r3.w == r4.w) ? 1.0 : 0.0;
					r3.w = r3.w + -(r4.w);
					r6.xyzw = float4((r2.x < r2.w) ? 1.0 : 0.0, (r2.z < r2.w) ? 1.0 : 0.0, (r2.x < r2.z) ? 1.0 : 0.0, (r2.z < r2.x) ? 1.0 : 0.0);
					r4.w = ((((r6.y) != 0.0) && ((r6.x) != 0.0)) ? 1.0 : 0.0);
					r2.xzw = float3(mad(r4.y, _BLENDMODES_OverlayColor.y, -(r2.z)), mad(r4.z, _BLENDMODES_OverlayColor.z, -(r2.w)), mad(r4.x, _BLENDMODES_OverlayColor.x, -(r2.x)));
					r2.xzw = float3(r2.x / r3.w, r2.z / r3.w, r2.w / r3.w);
					r3.w = (((r6.z) != 0.0) ? 6.0 : 0.0);
					r2.x = r2.x + r3.w;
					r2.zw = float2(r2.z + 4.0, r2.w + 4.0);
					r2.z = r6.w != 0.0 ? r2.z : r2.w;
					r2.x = r4.w != 0.0 ? r2.x : r2.z;
					r2.x = r2.x * 0.166667;
					r2.x = r5.x != 0.0 ? 0.0 : r2.x;
					r2.z = (r2.y < 1.0) ? 1.0 : 0.0;
					r2.w = r0.w + 1.0;
					r2.w = r2.w * r3.z;
					r3.w = mad(r2.y, 0.5, r0.w);
					r0.w = mad(-(r3.z), r0.w, r3.w);
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.y = -(r0.w) + r2.y;
					r4.xyzw = float4(r2.x + 0.333333, r2.x + 1.333333, r2.x + 1.0, r2.x + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.w = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.w != 0.0 ? r4.x : r2.z;
					r3.w = r0.w + -(r2.y);
					r4.x = r3.w * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r0.w : r2.z;
					r3.x = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r2.x < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r2.x;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r0.w : r2.z;
					r3.y = r4.z != 0.0 ? r2.z : r4.y;
					r2.x = r2.x + 0.666667;
					r2.x = r2.w != 0.0 ? r2.x : r4.w;
					r2.z = (1.0 < r2.x) ? 1.0 : 0.0;
					r2.w = r2.x + -1.0;
					r2.x = r2.z != 0.0 ? r2.w : r2.x;
					r2.z = mad(r4.x, r2.x, r2.y);
					r2.w = (r2.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r2.x < 0.5) ? 1.0 : 0.0, (r2.x < 0.666667) ? 1.0 : 0.0);
					r2.x = -(r2.x) + 0.666667;
					r2.x = r2.x * r3.w;
					r2.x = mad(r2.x, 6.0, r2.y);
					r2.x = r4.y != 0.0 ? r2.x : r2.y;
					r0.w = r4.x != 0.0 ? r0.w : r2.x;
					r3.z = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.x), mad(-(r0.y), v1.y, r3.y), mad(-(r0.z), v1.z, r3.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 43: BLENDMODES_MODE_HUE + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.x = min(r1.z, r1.y);
				r2.x = min(r1.x, r2.x);
				r2.y = r0.w + r2.x;
				r3.xyz = float3(r2.y * 0.5, r2.y * 0.5, r2.y * 0.5);
				r2.z = (r0.w == r2.x) ? 1.0 : 0.0;
				r2.w = r0.w + -(r2.x);
				r3.w = (1.0 < r2.y) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.x);
				r0.w = r0.w + 2.0;
				r0.w = r2.w / r0.w;
				r2.x = r2.w / r2.y;
				r0.w = r3.w != 0.0 ? r0.w : r2.x;
				r0.w = r2.z != 0.0 ? 0.0 : r0.w;
				r2.x = (r0.w == 0.0) ? 1.0 : 0.0;
				if (!any(r2.x)) {
					r4.xyzw = tex2D(t1, float2(v2.z, v2.w));
					r2.xzw = float3(r4.y * _BLENDMODES_OverlayColor.y, r4.z * _BLENDMODES_OverlayColor.z, r4.x * _BLENDMODES_OverlayColor.x);
					r3.w = max(r2.z, r2.x);
					r3.w = max(r2.w, r3.w);
					r4.w = min(r2.z, r2.x);
					r4.w = min(r2.w, r4.w);
					r5.x = (r3.w == r4.w) ? 1.0 : 0.0;
					r3.w = r3.w + -(r4.w);
					r6.xyzw = float4((r2.x < r2.w) ? 1.0 : 0.0, (r2.z < r2.w) ? 1.0 : 0.0, (r2.x < r2.z) ? 1.0 : 0.0, (r2.z < r2.x) ? 1.0 : 0.0);
					r4.w = ((((r6.y) != 0.0) && ((r6.x) != 0.0)) ? 1.0 : 0.0);
					r2.xzw = float3(mad(r4.y, _BLENDMODES_OverlayColor.y, -(r2.z)), mad(r4.z, _BLENDMODES_OverlayColor.z, -(r2.w)), mad(r4.x, _BLENDMODES_OverlayColor.x, -(r2.x)));
					r2.xzw = float3(r2.x / r3.w, r2.z / r3.w, r2.w / r3.w);
					r3.w = (((r6.z) != 0.0) ? 6.0 : 0.0);
					r2.x = r2.x + r3.w;
					r2.zw = float2(r2.z + 4.0, r2.w + 4.0);
					r2.z = r6.w != 0.0 ? r2.z : r2.w;
					r2.x = r4.w != 0.0 ? r2.x : r2.z;
					r2.x = r2.x * 0.166667;
					r2.x = r5.x != 0.0 ? 0.0 : r2.x;
					r2.z = (r2.y < 1.0) ? 1.0 : 0.0;
					r2.w = r0.w + 1.0;
					r2.w = r2.w * r3.z;
					r3.w = mad(r2.y, 0.5, r0.w);
					r0.w = mad(-(r3.z), r0.w, r3.w);
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.y = -(r0.w) + r2.y;
					r4.xyzw = float4(r2.x + 0.333333, r2.x + 1.333333, r2.x + 1.0, r2.x + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.w = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.w != 0.0 ? r4.x : r2.z;
					r3.w = r0.w + -(r2.y);
					r4.x = r3.w * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r0.w : r2.z;
					r3.x = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r2.x < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r2.x;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.w;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r0.w : r2.z;
					r3.y = r4.z != 0.0 ? r2.z : r4.y;
					r2.x = r2.x + 0.666667;
					r2.x = r2.w != 0.0 ? r2.x : r4.w;
					r2.z = (1.0 < r2.x) ? 1.0 : 0.0;
					r2.w = r2.x + -1.0;
					r2.x = r2.z != 0.0 ? r2.w : r2.x;
					r2.z = mad(r4.x, r2.x, r2.y);
					r2.w = (r2.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r2.x < 0.5) ? 1.0 : 0.0, (r2.x < 0.666667) ? 1.0 : 0.0);
					r2.x = -(r2.x) + 0.666667;
					r2.x = r2.x * r3.w;
					r2.x = mad(r2.x, 6.0, r2.y);
					r2.x = r4.y != 0.0 ? r2.x : r2.y;
					r0.w = r4.x != 0.0 ? r0.w : r2.x;
					r3.z = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.x), mad(-(r0.y), v1.y, r3.y), mad(-(r0.z), v1.z, r3.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 44: BLENDMODES_MODE_LIGHTEN ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(max(r0.x, r2.x), max(r0.y, r2.y), max(r0.z, r2.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 45: BLENDMODES_MODE_LIGHTEN + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(max(r0.x, r1.x), max(r0.y, r1.y), max(r0.z, r1.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 46: BLENDMODES_MODE_LIGHTEN + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(max(r0.x, r1.x), max(r0.y, r1.y), max(r0.z, r1.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 47: BLENDMODES_MODE_LIGHTEN + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(max(r0.x, r2.x), max(r0.y, r2.y), max(r0.z, r2.z));
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 48: BLENDMODES_MODE_LIGHTERCOLOR ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r1.w = r2.x * 0.299 + r2.y * 0.587 + r2.z * 0.114;
				r0.w = (r0.w < r1.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r2.x : r0.x, r0.w != 0.0 ? r2.y : r0.y, r0.w != 0.0 ? r2.z : r0.z);
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 49: BLENDMODES_MODE_LIGHTERCOLOR + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.x = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r1.x, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r2.w = r1.x * 0.299 + r1.y * 0.587 + r1.z * 0.114;
				r0.w = (r0.w < r2.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r1.x : r0.x, r0.w != 0.0 ? r1.y : r0.y, r0.w != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 50: BLENDMODES_MODE_LIGHTERCOLOR + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.x = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r1.x, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r2.w = r1.x * 0.299 + r1.y * 0.587 + r1.z * 0.114;
				r0.w = (r0.w < r2.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r1.x : r0.x, r0.w != 0.0 ? r1.y : r0.y, r0.w != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 51: BLENDMODES_MODE_LIGHTERCOLOR + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.w = r0.x * 0.299 + r0.y * 0.587 + r0.z * 0.114;
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r1.w = r2.x * 0.299 + r2.y * 0.587 + r2.z * 0.114;
				r0.w = (r0.w < r1.w) ? 1.0 : 0.0;
				r0.xyz = float3(r0.w != 0.0 ? r2.x : r0.x, r0.w != 0.0 ? r2.y : r0.y, r0.w != 0.0 ? r2.z : r0.z);
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 52: BLENDMODES_MODE_LINEARBURN ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, r2.x), mad(r0.y, _BLENDMODES_OverlayColor.y, r2.y), mad(r0.z, _BLENDMODES_OverlayColor.z, r2.z));
				r0.xyz = float3(r0.x + -1.0, r0.y + -1.0, r0.z + -1.0);
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 53: BLENDMODES_MODE_LINEARBURN + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, r1.x), mad(r0.y, _BLENDMODES_OverlayColor.y, r1.y), mad(r0.z, _BLENDMODES_OverlayColor.z, r1.z));
				r0.xyz = float3(r0.x + -1.0, r0.y + -1.0, r0.z + -1.0);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 54: BLENDMODES_MODE_LINEARBURN + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, r1.x), mad(r0.y, _BLENDMODES_OverlayColor.y, r1.y), mad(r0.z, _BLENDMODES_OverlayColor.z, r1.z));
				r0.xyz = float3(r0.x + -1.0, r0.y + -1.0, r0.z + -1.0);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 55: BLENDMODES_MODE_LINEARBURN + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, r2.x), mad(r0.y, _BLENDMODES_OverlayColor.y, r2.y), mad(r0.z, _BLENDMODES_OverlayColor.z, r2.z));
				r0.xyz = float3(r0.x + -1.0, r0.y + -1.0, r0.z + -1.0);
				r0.xyz = float3(mad(-(r1.x), v1.x, r0.x), mad(-(r1.y), v1.y, r0.y), mad(-(r1.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 56: BLENDMODES_MODE_LINEARDODGE ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 57: BLENDMODES_MODE_LINEARDODGE + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 58: BLENDMODES_MODE_LINEARDODGE + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 59: BLENDMODES_MODE_LINEARDODGE + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 60: BLENDMODES_MODE_LINEARLIGHT ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r2.xyz = float3((0.5 < r1.x) ? 1.0 : 0.0, (0.5 < r1.y) ? 1.0 : 0.0, (0.5 < r1.z) ? 1.0 : 0.0);
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r4.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(mad(r1.x, 2.0, r4.x), mad(r1.y, 2.0, r4.y), mad(r1.z, 2.0, r4.z));
				r1.xyz = float3(r1.x + -1.0, r1.y + -1.0, r1.z + -1.0);
				r0.xyz = float3(mad(r0.x, 2.0, r4.x), mad(r0.y, 2.0, r4.y), mad(r0.z, 2.0, r4.z));
				r0.xyz = float3(r2.x != 0.0 ? r0.x : r1.x, r2.y != 0.0 ? r0.y : r1.y, r2.z != 0.0 ? r0.z : r1.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 61: BLENDMODES_MODE_LINEARLIGHT + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(mad(r1.x, 2.0, r2.x), mad(r1.y, 2.0, r2.y), mad(r1.z, 2.0, r2.z));
				r4.xyz = float3(mad(r0.x, 2.0, r2.x), mad(r0.y, 2.0, r2.y), mad(r0.z, 2.0, r2.z));
				r0.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r4.xyz = float3(r4.x + -1.0, r4.y + -1.0, r4.z + -1.0);
				r0.xyz = float3(r0.x != 0.0 ? r1.x : r4.x, r0.y != 0.0 ? r1.y : r4.y, r0.z != 0.0 ? r1.z : r4.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 62: BLENDMODES_MODE_LINEARLIGHT + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(mad(r1.x, 2.0, r2.x), mad(r1.y, 2.0, r2.y), mad(r1.z, 2.0, r2.z));
				r4.xyz = float3(mad(r0.x, 2.0, r2.x), mad(r0.y, 2.0, r2.y), mad(r0.z, 2.0, r2.z));
				r0.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r4.xyz = float3(r4.x + -1.0, r4.y + -1.0, r4.z + -1.0);
				r0.xyz = float3(r0.x != 0.0 ? r1.x : r4.x, r0.y != 0.0 ? r1.y : r4.y, r0.z != 0.0 ? r1.z : r4.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 63: BLENDMODES_MODE_LINEARLIGHT + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r2.xyz = float3((0.5 < r1.x) ? 1.0 : 0.0, (0.5 < r1.y) ? 1.0 : 0.0, (0.5 < r1.z) ? 1.0 : 0.0);
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r4.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(mad(r1.x, 2.0, r4.x), mad(r1.y, 2.0, r4.y), mad(r1.z, 2.0, r4.z));
				r1.xyz = float3(r1.x + -1.0, r1.y + -1.0, r1.z + -1.0);
				r0.xyz = float3(mad(r0.x, 2.0, r4.x), mad(r0.y, 2.0, r4.y), mad(r0.z, 2.0, r4.z));
				r0.xyz = float3(r2.x != 0.0 ? r0.x : r1.x, r2.y != 0.0 ? r0.y : r1.y, r2.z != 0.0 ? r0.z : r1.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 64: BLENDMODES_MODE_LUMINOSITY ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.y = (r0.w == r2.w) ? 1.0 : 0.0;
				r3.z = r0.w + -(r2.w);
				r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.w);
				r0.w = r0.w + 2.0;
				r0.w = r3.z / r0.w;
				r2.w = r3.z / r3.x;
				r4.x = r3.w != 0.0 ? r0.w : r2.w;
				r5.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
				r0.w = ((((r5.y) != 0.0) && ((r5.x) != 0.0)) ? 1.0 : 0.0);
				r6.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
				r3.xzw = float3(r6.x / r3.z, r6.y / r3.z, r6.z / r3.z);
				r2.w = (((r5.z) != 0.0) ? 6.0 : 0.0);
				r2.w = r2.w + r3.x;
				r3.xz = float2(r3.z + 2.0, r3.w + 4.0);
				r3.x = r5.w != 0.0 ? r3.x : r3.z;
				r0.w = r0.w != 0.0 ? r2.w : r3.x;
				r4.yzw = float3(r0.w * 0.166667, r0.w * 0.166667, r0.w * 0.166667);
				r3.xyzw = float4(r3.y != 0.0 ? 0.0 : r4.x, r3.y != 0.0 ? 0.0 : r4.y, r3.y != 0.0 ? 0.0 : r4.z, r3.y != 0.0 ? 0.0 : r4.w);
				r0.w = max(r2.z, r2.y);
				r0.w = max(r0.w, r2.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r0.w = r0.w + r2.x;
				r2.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = (r3.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.w)) {
					r2.w = (r0.w < 1.0) ? 1.0 : 0.0;
					r4.xyzw = float4(r3.x + 1.0, r3.y + 0.333333, r3.z + 1.333333, r3.w + 1.0);
					r3.y = r2.z * r4.x;
					r3.z = mad(r0.w, 0.5, r3.x);
					r3.x = mad(-(r2.z), r3.x, r3.z);
					r2.w = r2.w != 0.0 ? r3.y : r3.x;
					r0.w = r0.w + -(r2.w);
					r3.x = (r4.y < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.z : r4.y;
					r3.y = (1.0 < r3.x) ? 1.0 : 0.0;
					r3.z = r3.x + -1.0;
					r3.x = r3.y != 0.0 ? r3.z : r3.x;
					r3.y = -(r0.w) + r2.w;
					r3.z = r3.y * 6.0;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r5.y != 0.0 ? r3.x : r0.w;
					r3.x = r5.x != 0.0 ? r2.w : r3.x;
					r2.x = r4.y != 0.0 ? r3.x : r4.x;
					r3.x = (r3.w < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.w : r3.w;
					r4.x = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.y = r3.x + -1.0;
					r3.x = r4.x != 0.0 ? r4.y : r3.x;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.zw = float2((r3.x < 0.666667) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r4.w != 0.0 ? r3.x : r0.w;
					r3.x = r4.z != 0.0 ? r2.w : r3.x;
					r2.y = r4.y != 0.0 ? r3.x : r4.x;
					r3.xw = float2(r3.w + -0.333333, r3.w + 0.666667);
					r4.x = (r3.x < 0.0) ? 1.0 : 0.0;
					r3.x = r4.x != 0.0 ? r3.w : r3.x;
					r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.x = r3.x + -1.0;
					r3.x = r3.w != 0.0 ? r4.x : r3.x;
					r3.z = mad(r3.z, r3.x, r0.w);
					r3.w = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r0.w = r4.y != 0.0 ? r3.x : r0.w;
					r0.w = r4.x != 0.0 ? r2.w : r0.w;
					r2.z = r3.w != 0.0 ? r0.w : r3.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r2.x), mad(-(r0.y), v1.y, r2.y), mad(-(r0.z), v1.z, r2.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 65: BLENDMODES_MODE_LUMINOSITY + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.y = (r0.w == r2.w) ? 1.0 : 0.0;
				r3.z = r0.w + -(r2.w);
				r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.w);
				r0.w = r0.w + 2.0;
				r0.w = r3.z / r0.w;
				r2.w = r3.z / r3.x;
				r4.x = r3.w != 0.0 ? r0.w : r2.w;
				r5.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
				r0.w = ((((r5.y) != 0.0) && ((r5.x) != 0.0)) ? 1.0 : 0.0);
				r6.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
				r3.xzw = float3(r6.x / r3.z, r6.y / r3.z, r6.z / r3.z);
				r2.w = (((r5.z) != 0.0) ? 6.0 : 0.0);
				r2.w = r2.w + r3.x;
				r3.xz = float2(r3.z + 2.0, r3.w + 4.0);
				r3.x = r5.w != 0.0 ? r3.x : r3.z;
				r0.w = r0.w != 0.0 ? r2.w : r3.x;
				r4.yzw = float3(r0.w * 0.166667, r0.w * 0.166667, r0.w * 0.166667);
				r3.xyzw = float4(r3.y != 0.0 ? 0.0 : r4.x, r3.y != 0.0 ? 0.0 : r4.y, r3.y != 0.0 ? 0.0 : r4.z, r3.y != 0.0 ? 0.0 : r4.w);
				r0.w = max(r2.z, r2.y);
				r0.w = max(r0.w, r2.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r0.w = r0.w + r2.x;
				r2.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = (r3.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.w)) {
					r2.w = (r0.w < 1.0) ? 1.0 : 0.0;
					r4.xyzw = float4(r3.x + 1.0, r3.y + 0.333333, r3.z + 1.333333, r3.w + 1.0);
					r3.y = r2.z * r4.x;
					r3.z = mad(r0.w, 0.5, r3.x);
					r3.x = mad(-(r2.z), r3.x, r3.z);
					r2.w = r2.w != 0.0 ? r3.y : r3.x;
					r0.w = r0.w + -(r2.w);
					r3.x = (r4.y < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.z : r4.y;
					r3.y = (1.0 < r3.x) ? 1.0 : 0.0;
					r3.z = r3.x + -1.0;
					r3.x = r3.y != 0.0 ? r3.z : r3.x;
					r3.y = -(r0.w) + r2.w;
					r3.z = r3.y * 6.0;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r5.y != 0.0 ? r3.x : r0.w;
					r3.x = r5.x != 0.0 ? r2.w : r3.x;
					r2.x = r4.y != 0.0 ? r3.x : r4.x;
					r3.x = (r3.w < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.w : r3.w;
					r4.x = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.y = r3.x + -1.0;
					r3.x = r4.x != 0.0 ? r4.y : r3.x;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.zw = float2((r3.x < 0.666667) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r4.w != 0.0 ? r3.x : r0.w;
					r3.x = r4.z != 0.0 ? r2.w : r3.x;
					r2.y = r4.y != 0.0 ? r3.x : r4.x;
					r3.xw = float2(r3.w + -0.333333, r3.w + 0.666667);
					r4.x = (r3.x < 0.0) ? 1.0 : 0.0;
					r3.x = r4.x != 0.0 ? r3.w : r3.x;
					r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.x = r3.x + -1.0;
					r3.x = r3.w != 0.0 ? r4.x : r3.x;
					r3.z = mad(r3.z, r3.x, r0.w);
					r3.w = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r0.w = r4.y != 0.0 ? r3.x : r0.w;
					r0.w = r4.x != 0.0 ? r2.w : r0.w;
					r2.z = r3.w != 0.0 ? r0.w : r3.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r2.x), mad(-(r0.y), v1.y, r2.y), mad(-(r0.z), v1.z, r2.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 66: BLENDMODES_MODE_LUMINOSITY + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.y = (r0.w == r2.w) ? 1.0 : 0.0;
				r3.z = r0.w + -(r2.w);
				r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.w);
				r0.w = r0.w + 2.0;
				r0.w = r3.z / r0.w;
				r2.w = r3.z / r3.x;
				r4.x = r3.w != 0.0 ? r0.w : r2.w;
				r5.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
				r0.w = ((((r5.y) != 0.0) && ((r5.x) != 0.0)) ? 1.0 : 0.0);
				r6.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
				r3.xzw = float3(r6.x / r3.z, r6.y / r3.z, r6.z / r3.z);
				r2.w = (((r5.z) != 0.0) ? 6.0 : 0.0);
				r2.w = r2.w + r3.x;
				r3.xz = float2(r3.z + 2.0, r3.w + 4.0);
				r3.x = r5.w != 0.0 ? r3.x : r3.z;
				r0.w = r0.w != 0.0 ? r2.w : r3.x;
				r4.yzw = float3(r0.w * 0.166667, r0.w * 0.166667, r0.w * 0.166667);
				r3.xyzw = float4(r3.y != 0.0 ? 0.0 : r4.x, r3.y != 0.0 ? 0.0 : r4.y, r3.y != 0.0 ? 0.0 : r4.z, r3.y != 0.0 ? 0.0 : r4.w);
				r0.w = max(r2.z, r2.y);
				r0.w = max(r0.w, r2.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r0.w = r0.w + r2.x;
				r2.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = (r3.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.w)) {
					r2.w = (r0.w < 1.0) ? 1.0 : 0.0;
					r4.xyzw = float4(r3.x + 1.0, r3.y + 0.333333, r3.z + 1.333333, r3.w + 1.0);
					r3.y = r2.z * r4.x;
					r3.z = mad(r0.w, 0.5, r3.x);
					r3.x = mad(-(r2.z), r3.x, r3.z);
					r2.w = r2.w != 0.0 ? r3.y : r3.x;
					r0.w = r0.w + -(r2.w);
					r3.x = (r4.y < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.z : r4.y;
					r3.y = (1.0 < r3.x) ? 1.0 : 0.0;
					r3.z = r3.x + -1.0;
					r3.x = r3.y != 0.0 ? r3.z : r3.x;
					r3.y = -(r0.w) + r2.w;
					r3.z = r3.y * 6.0;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r5.y != 0.0 ? r3.x : r0.w;
					r3.x = r5.x != 0.0 ? r2.w : r3.x;
					r2.x = r4.y != 0.0 ? r3.x : r4.x;
					r3.x = (r3.w < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.w : r3.w;
					r4.x = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.y = r3.x + -1.0;
					r3.x = r4.x != 0.0 ? r4.y : r3.x;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.zw = float2((r3.x < 0.666667) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r4.w != 0.0 ? r3.x : r0.w;
					r3.x = r4.z != 0.0 ? r2.w : r3.x;
					r2.y = r4.y != 0.0 ? r3.x : r4.x;
					r3.xw = float2(r3.w + -0.333333, r3.w + 0.666667);
					r4.x = (r3.x < 0.0) ? 1.0 : 0.0;
					r3.x = r4.x != 0.0 ? r3.w : r3.x;
					r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.x = r3.x + -1.0;
					r3.x = r3.w != 0.0 ? r4.x : r3.x;
					r3.z = mad(r3.z, r3.x, r0.w);
					r3.w = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r0.w = r4.y != 0.0 ? r3.x : r0.w;
					r0.w = r4.x != 0.0 ? r2.w : r0.w;
					r2.z = r3.w != 0.0 ? r0.w : r3.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r2.x), mad(-(r0.y), v1.y, r2.y), mad(-(r0.z), v1.z, r2.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 67: BLENDMODES_MODE_LUMINOSITY + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.y = (r0.w == r2.w) ? 1.0 : 0.0;
				r3.z = r0.w + -(r2.w);
				r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
				r0.w = -(r0.w) + -(r2.w);
				r0.w = r0.w + 2.0;
				r0.w = r3.z / r0.w;
				r2.w = r3.z / r3.x;
				r4.x = r3.w != 0.0 ? r0.w : r2.w;
				r5.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
				r0.w = ((((r5.y) != 0.0) && ((r5.x) != 0.0)) ? 1.0 : 0.0);
				r6.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
				r3.xzw = float3(r6.x / r3.z, r6.y / r3.z, r6.z / r3.z);
				r2.w = (((r5.z) != 0.0) ? 6.0 : 0.0);
				r2.w = r2.w + r3.x;
				r3.xz = float2(r3.z + 2.0, r3.w + 4.0);
				r3.x = r5.w != 0.0 ? r3.x : r3.z;
				r0.w = r0.w != 0.0 ? r2.w : r3.x;
				r4.yzw = float3(r0.w * 0.166667, r0.w * 0.166667, r0.w * 0.166667);
				r3.xyzw = float4(r3.y != 0.0 ? 0.0 : r4.x, r3.y != 0.0 ? 0.0 : r4.y, r3.y != 0.0 ? 0.0 : r4.z, r3.y != 0.0 ? 0.0 : r4.w);
				r0.w = max(r2.z, r2.y);
				r0.w = max(r0.w, r2.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r0.w = r0.w + r2.x;
				r2.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = (r3.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.w)) {
					r2.w = (r0.w < 1.0) ? 1.0 : 0.0;
					r4.xyzw = float4(r3.x + 1.0, r3.y + 0.333333, r3.z + 1.333333, r3.w + 1.0);
					r3.y = r2.z * r4.x;
					r3.z = mad(r0.w, 0.5, r3.x);
					r3.x = mad(-(r2.z), r3.x, r3.z);
					r2.w = r2.w != 0.0 ? r3.y : r3.x;
					r0.w = r0.w + -(r2.w);
					r3.x = (r4.y < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.z : r4.y;
					r3.y = (1.0 < r3.x) ? 1.0 : 0.0;
					r3.z = r3.x + -1.0;
					r3.x = r3.y != 0.0 ? r3.z : r3.x;
					r3.y = -(r0.w) + r2.w;
					r3.z = r3.y * 6.0;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r5.y != 0.0 ? r3.x : r0.w;
					r3.x = r5.x != 0.0 ? r2.w : r3.x;
					r2.x = r4.y != 0.0 ? r3.x : r4.x;
					r3.x = (r3.w < 0.0) ? 1.0 : 0.0;
					r3.x = r3.x != 0.0 ? r4.w : r3.w;
					r4.x = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.y = r3.x + -1.0;
					r3.x = r4.x != 0.0 ? r4.y : r3.x;
					r4.x = mad(r3.z, r3.x, r0.w);
					r4.y = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.zw = float2((r3.x < 0.666667) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r3.x = r4.w != 0.0 ? r3.x : r0.w;
					r3.x = r4.z != 0.0 ? r2.w : r3.x;
					r2.y = r4.y != 0.0 ? r3.x : r4.x;
					r3.xw = float2(r3.w + -0.333333, r3.w + 0.666667);
					r4.x = (r3.x < 0.0) ? 1.0 : 0.0;
					r3.x = r4.x != 0.0 ? r3.w : r3.x;
					r3.w = (1.0 < r3.x) ? 1.0 : 0.0;
					r4.x = r3.x + -1.0;
					r3.x = r3.w != 0.0 ? r4.x : r3.x;
					r3.z = mad(r3.z, r3.x, r0.w);
					r3.w = (r3.x >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r3.x < 0.5) ? 1.0 : 0.0, (r3.x < 0.666667) ? 1.0 : 0.0);
					r3.x = -(r3.x) + 0.666667;
					r3.x = r3.x * r3.y;
					r3.x = mad(r3.x, 6.0, r0.w);
					r0.w = r4.y != 0.0 ? r3.x : r0.w;
					r0.w = r4.x != 0.0 ? r2.w : r0.w;
					r2.z = r3.w != 0.0 ? r0.w : r3.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r2.x), mad(-(r0.y), v1.y, r2.y), mad(-(r0.z), v1.z, r2.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 68: BLENDMODES_MODE_MULTIPLY ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(r1.x, r0.x, -(r1.x)), mad(r1.y, r0.y, -(r1.y)), mad(r1.z, r0.z, -(r1.z)));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 69: BLENDMODES_MODE_MULTIPLY + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(r1.x, r0.x, -(r1.x)), mad(r1.y, r0.y, -(r1.y)), mad(r1.z, r0.z, -(r1.z)));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 70: BLENDMODES_MODE_MULTIPLY + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(r1.x, r0.x, -(r1.x)), mad(r1.y, r0.y, -(r1.y)), mad(r1.z, r0.z, -(r1.z)));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 71: BLENDMODES_MODE_MULTIPLY + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(r1.x, r0.x, -(r1.x)), mad(r1.y, r0.y, -(r1.y)), mad(r1.z, r0.z, -(r1.z)));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 72: BLENDMODES_MODE_OVERLAY ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r3.xyz = float3(r3.x + r3.x, r3.y + r3.y, r3.z + r3.z);
				r1.xyz = float3(mad(-(r3.x), r1.x, 1.0), mad(-(r3.y), r1.y, 1.0), mad(-(r3.z), r1.z, 1.0));
				r3.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r3.x, r0.y * r3.y, r0.z * r3.z);
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r4.xyz = float3((0.5 < r3.x) ? 1.0 : 0.0, (0.5 < r3.y) ? 1.0 : 0.0, (0.5 < r3.z) ? 1.0 : 0.0);
				r0.xyz = float3(r4.x != 0.0 ? r1.x : r0.x, r4.y != 0.0 ? r1.y : r0.y, r4.z != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r3.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r3.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r3.z));
				o0.xyz = float3(r3.w * r0.x, r3.w * r0.y, r3.w * r0.z);
				o0.w = r3.w;
				return o0;
			}
			
			// ---- variant 73: BLENDMODES_MODE_OVERLAY + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(r1.x * r2.x, r1.y * r2.y, r1.z * r2.z);
				r1.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r4.xyz = float3(mad(-(r3.x), v1.x, 1.0), mad(-(r3.y), v1.y, 1.0), mad(-(r3.z), v1.z, 1.0));
				r4.xyz = float3(r4.x + r4.x, r4.y + r4.y, r4.z + r4.z);
				r0.xyz = float3(mad(-(r4.x), r0.x, 1.0), mad(-(r4.y), r0.y, 1.0), mad(-(r4.z), r0.z, 1.0));
				r4.xyz = float3((0.5 < r2.x) ? 1.0 : 0.0, (0.5 < r2.y) ? 1.0 : 0.0, (0.5 < r2.z) ? 1.0 : 0.0);
				r0.xyz = float3(r4.x != 0.0 ? r0.x : r1.x, r4.y != 0.0 ? r0.y : r1.y, r4.z != 0.0 ? r0.z : r1.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 74: BLENDMODES_MODE_OVERLAY + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(r1.x * r2.x, r1.y * r2.y, r1.z * r2.z);
				r1.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r4.xyz = float3(mad(-(r3.x), v1.x, 1.0), mad(-(r3.y), v1.y, 1.0), mad(-(r3.z), v1.z, 1.0));
				r4.xyz = float3(r4.x + r4.x, r4.y + r4.y, r4.z + r4.z);
				r0.xyz = float3(mad(-(r4.x), r0.x, 1.0), mad(-(r4.y), r0.y, 1.0), mad(-(r4.z), r0.z, 1.0));
				r4.xyz = float3((0.5 < r2.x) ? 1.0 : 0.0, (0.5 < r2.y) ? 1.0 : 0.0, (0.5 < r2.z) ? 1.0 : 0.0);
				r0.xyz = float3(r4.x != 0.0 ? r0.x : r1.x, r4.y != 0.0 ? r0.y : r1.y, r4.z != 0.0 ? r0.z : r1.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 75: BLENDMODES_MODE_OVERLAY + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r3.xyz = float3(r3.x + r3.x, r3.y + r3.y, r3.z + r3.z);
				r1.xyz = float3(mad(-(r3.x), r1.x, 1.0), mad(-(r3.y), r1.y, 1.0), mad(-(r3.z), r1.z, 1.0));
				r3.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r3.x, r0.y * r3.y, r0.z * r3.z);
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r4.xyz = float3((0.5 < r3.x) ? 1.0 : 0.0, (0.5 < r3.y) ? 1.0 : 0.0, (0.5 < r3.z) ? 1.0 : 0.0);
				r0.xyz = float3(r4.x != 0.0 ? r1.x : r0.x, r4.y != 0.0 ? r1.y : r0.y, r4.z != 0.0 ? r1.z : r0.z);
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r3.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r3.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r3.z));
				o0.xyz = float3(r3.w * r0.x, r3.w * r0.y, r3.w * r0.z);
				o0.w = r3.w;
				return o0;
			}
			
			// ---- variant 76: BLENDMODES_MODE_PINLIGHT ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r2.xyz = float3((0.5 < r1.x) ? 1.0 : 0.0, (0.5 < r1.y) ? 1.0 : 0.0, (0.5 < r1.z) ? 1.0 : 0.0);
				r1.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r4.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r0.xyz = float3(max(r0.x, r4.x), max(r0.y, r4.y), max(r0.z, r4.z));
				r1.xyz = float3(min(r1.x, r4.x), min(r1.y, r4.y), min(r1.z, r4.z));
				r0.xyz = float3(r2.x != 0.0 ? r0.x : r1.x, r2.y != 0.0 ? r0.y : r1.y, r2.z != 0.0 ? r0.z : r1.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 77: BLENDMODES_MODE_PINLIGHT + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(max(r1.x, r2.x), max(r1.y, r2.y), max(r1.z, r2.z));
				r4.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r0.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r4.xyz = float3(min(r2.x, r4.x), min(r2.y, r4.y), min(r2.z, r4.z));
				r0.xyz = float3(r0.x != 0.0 ? r1.x : r4.x, r0.y != 0.0 ? r1.y : r4.y, r0.z != 0.0 ? r1.z : r4.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 78: BLENDMODES_MODE_PINLIGHT + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(max(r1.x, r2.x), max(r1.y, r2.y), max(r1.z, r2.z));
				r4.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r0.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r4.xyz = float3(min(r2.x, r4.x), min(r2.y, r4.y), min(r2.z, r4.z));
				r0.xyz = float3(r0.x != 0.0 ? r1.x : r4.x, r0.y != 0.0 ? r1.y : r4.y, r0.z != 0.0 ? r1.z : r4.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 79: BLENDMODES_MODE_PINLIGHT + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r2.xyz = float3((0.5 < r1.x) ? 1.0 : 0.0, (0.5 < r1.y) ? 1.0 : 0.0, (0.5 < r1.z) ? 1.0 : 0.0);
				r1.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r4.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r0.xyz = float3(max(r0.x, r4.x), max(r0.y, r4.y), max(r0.z, r4.z));
				r1.xyz = float3(min(r1.x, r4.x), min(r1.y, r4.y), min(r1.z, r4.z));
				r0.xyz = float3(r2.x != 0.0 ? r0.x : r1.x, r2.y != 0.0 ? r0.y : r1.y, r2.z != 0.0 ? r0.z : r1.z);
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 80: BLENDMODES_MODE_SATURATION ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.yzw = float3(r3.x * 0.5, r3.x * 0.5, r3.x * 0.5);
				r4.x = max(r2.z, r2.y);
				r4.x = max(r2.x, r4.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r2.y = r2.x + r4.x;
				r2.z = (r2.x == r4.x) ? 1.0 : 0.0;
				r4.y = -(r2.x) + r4.x;
				r4.z = (1.0 < r2.y) ? 1.0 : 0.0;
				r2.x = -(r4.x) + -(r2.x);
				r2.x = r2.x + 2.0;
				r2.xy = float2(r4.y / r2.x, r4.y / r2.y);
				r2.x = r4.z != 0.0 ? r2.x : r2.y;
				r2.x = r2.z != 0.0 ? 0.0 : r2.x;
				r2.y = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.y)) {
					r2.y = (r0.w == r2.w) ? 1.0 : 0.0;
					r0.w = r0.w + -(r2.w);
					r4.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
					r2.z = ((((r4.y) != 0.0) && ((r4.x) != 0.0)) ? 1.0 : 0.0);
					r5.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
					r5.xyz = float3(r5.x / r0.w, r5.y / r0.w, r5.z / r0.w);
					r0.w = (((r4.z) != 0.0) ? 6.0 : 0.0);
					r0.w = r0.w + r5.x;
					r4.xy = float2(r5.y + 2.0, r5.z + 4.0);
					r2.w = r4.w != 0.0 ? r4.x : r4.y;
					r0.w = r2.z != 0.0 ? r0.w : r2.w;
					r0.w = r0.w * 0.166667;
					r0.w = r2.y != 0.0 ? 0.0 : r0.w;
					r2.y = (r3.x < 1.0) ? 1.0 : 0.0;
					r2.z = r2.x + 1.0;
					r2.z = r2.z * r3.w;
					r2.w = mad(r3.x, 0.5, r2.x);
					r2.x = mad(-(r3.w), r2.x, r2.w);
					r2.x = r2.y != 0.0 ? r2.z : r2.x;
					r2.y = -(r2.x) + r3.x;
					r4.xyzw = float4(r0.w + 0.333333, r0.w + 1.333333, r0.w + 1.0, r0.w + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.x = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.x != 0.0 ? r4.x : r2.z;
					r3.x = -(r2.y) + r2.x;
					r4.x = r3.x * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r2.x : r2.z;
					r3.y = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r0.w < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r0.w;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r2.x : r2.z;
					r3.z = r4.z != 0.0 ? r2.z : r4.y;
					r0.w = r0.w + 0.666667;
					r0.w = r2.w != 0.0 ? r0.w : r4.w;
					r2.z = (1.0 < r0.w) ? 1.0 : 0.0;
					r2.w = r0.w + -1.0;
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.z = mad(r4.x, r0.w, r2.y);
					r2.w = (r0.w >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r0.w < 0.5) ? 1.0 : 0.0, (r0.w < 0.666667) ? 1.0 : 0.0);
					r0.w = -(r0.w) + 0.666667;
					r0.w = r0.w * r3.x;
					r0.w = mad(r0.w, 6.0, r2.y);
					r0.w = r4.y != 0.0 ? r0.w : r2.y;
					r0.w = r4.x != 0.0 ? r2.x : r0.w;
					r3.w = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.y), mad(-(r0.y), v1.y, r3.z), mad(-(r0.z), v1.z, r3.w));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 81: BLENDMODES_MODE_SATURATION + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.yzw = float3(r3.x * 0.5, r3.x * 0.5, r3.x * 0.5);
				r4.x = max(r2.z, r2.y);
				r4.x = max(r2.x, r4.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r2.y = r2.x + r4.x;
				r2.z = (r2.x == r4.x) ? 1.0 : 0.0;
				r4.y = -(r2.x) + r4.x;
				r4.z = (1.0 < r2.y) ? 1.0 : 0.0;
				r2.x = -(r4.x) + -(r2.x);
				r2.x = r2.x + 2.0;
				r2.xy = float2(r4.y / r2.x, r4.y / r2.y);
				r2.x = r4.z != 0.0 ? r2.x : r2.y;
				r2.x = r2.z != 0.0 ? 0.0 : r2.x;
				r2.y = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.y)) {
					r2.y = (r0.w == r2.w) ? 1.0 : 0.0;
					r0.w = r0.w + -(r2.w);
					r4.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
					r2.z = ((((r4.y) != 0.0) && ((r4.x) != 0.0)) ? 1.0 : 0.0);
					r5.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
					r5.xyz = float3(r5.x / r0.w, r5.y / r0.w, r5.z / r0.w);
					r0.w = (((r4.z) != 0.0) ? 6.0 : 0.0);
					r0.w = r0.w + r5.x;
					r4.xy = float2(r5.y + 2.0, r5.z + 4.0);
					r2.w = r4.w != 0.0 ? r4.x : r4.y;
					r0.w = r2.z != 0.0 ? r0.w : r2.w;
					r0.w = r0.w * 0.166667;
					r0.w = r2.y != 0.0 ? 0.0 : r0.w;
					r2.y = (r3.x < 1.0) ? 1.0 : 0.0;
					r2.z = r2.x + 1.0;
					r2.z = r2.z * r3.w;
					r2.w = mad(r3.x, 0.5, r2.x);
					r2.x = mad(-(r3.w), r2.x, r2.w);
					r2.x = r2.y != 0.0 ? r2.z : r2.x;
					r2.y = -(r2.x) + r3.x;
					r4.xyzw = float4(r0.w + 0.333333, r0.w + 1.333333, r0.w + 1.0, r0.w + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.x = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.x != 0.0 ? r4.x : r2.z;
					r3.x = -(r2.y) + r2.x;
					r4.x = r3.x * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r2.x : r2.z;
					r3.y = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r0.w < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r0.w;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r2.x : r2.z;
					r3.z = r4.z != 0.0 ? r2.z : r4.y;
					r0.w = r0.w + 0.666667;
					r0.w = r2.w != 0.0 ? r0.w : r4.w;
					r2.z = (1.0 < r0.w) ? 1.0 : 0.0;
					r2.w = r0.w + -1.0;
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.z = mad(r4.x, r0.w, r2.y);
					r2.w = (r0.w >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r0.w < 0.5) ? 1.0 : 0.0, (r0.w < 0.666667) ? 1.0 : 0.0);
					r0.w = -(r0.w) + 0.666667;
					r0.w = r0.w * r3.x;
					r0.w = mad(r0.w, 6.0, r2.y);
					r0.w = r4.y != 0.0 ? r0.w : r2.y;
					r0.w = r4.x != 0.0 ? r2.x : r0.w;
					r3.w = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.y), mad(-(r0.y), v1.y, r3.z), mad(-(r0.z), v1.z, r3.w));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 82: BLENDMODES_MODE_SATURATION + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.x = -(r0.w) + r1.x;
				r0.w = mad(_EnableExternalAlpha, r1.x, r0.w);
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.yzw = float3(r3.x * 0.5, r3.x * 0.5, r3.x * 0.5);
				r4.x = max(r2.z, r2.y);
				r4.x = max(r2.x, r4.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r2.y = r2.x + r4.x;
				r2.z = (r2.x == r4.x) ? 1.0 : 0.0;
				r4.y = -(r2.x) + r4.x;
				r4.z = (1.0 < r2.y) ? 1.0 : 0.0;
				r2.x = -(r4.x) + -(r2.x);
				r2.x = r2.x + 2.0;
				r2.xy = float2(r4.y / r2.x, r4.y / r2.y);
				r2.x = r4.z != 0.0 ? r2.x : r2.y;
				r2.x = r2.z != 0.0 ? 0.0 : r2.x;
				r2.y = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.y)) {
					r2.y = (r0.w == r2.w) ? 1.0 : 0.0;
					r0.w = r0.w + -(r2.w);
					r4.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
					r2.z = ((((r4.y) != 0.0) && ((r4.x) != 0.0)) ? 1.0 : 0.0);
					r5.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
					r5.xyz = float3(r5.x / r0.w, r5.y / r0.w, r5.z / r0.w);
					r0.w = (((r4.z) != 0.0) ? 6.0 : 0.0);
					r0.w = r0.w + r5.x;
					r4.xy = float2(r5.y + 2.0, r5.z + 4.0);
					r2.w = r4.w != 0.0 ? r4.x : r4.y;
					r0.w = r2.z != 0.0 ? r0.w : r2.w;
					r0.w = r0.w * 0.166667;
					r0.w = r2.y != 0.0 ? 0.0 : r0.w;
					r2.y = (r3.x < 1.0) ? 1.0 : 0.0;
					r2.z = r2.x + 1.0;
					r2.z = r2.z * r3.w;
					r2.w = mad(r3.x, 0.5, r2.x);
					r2.x = mad(-(r3.w), r2.x, r2.w);
					r2.x = r2.y != 0.0 ? r2.z : r2.x;
					r2.y = -(r2.x) + r3.x;
					r4.xyzw = float4(r0.w + 0.333333, r0.w + 1.333333, r0.w + 1.0, r0.w + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.x = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.x != 0.0 ? r4.x : r2.z;
					r3.x = -(r2.y) + r2.x;
					r4.x = r3.x * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r2.x : r2.z;
					r3.y = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r0.w < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r0.w;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r2.x : r2.z;
					r3.z = r4.z != 0.0 ? r2.z : r4.y;
					r0.w = r0.w + 0.666667;
					r0.w = r2.w != 0.0 ? r0.w : r4.w;
					r2.z = (1.0 < r0.w) ? 1.0 : 0.0;
					r2.w = r0.w + -1.0;
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.z = mad(r4.x, r0.w, r2.y);
					r2.w = (r0.w >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r0.w < 0.5) ? 1.0 : 0.0, (r0.w < 0.666667) ? 1.0 : 0.0);
					r0.w = -(r0.w) + 0.666667;
					r0.w = r0.w * r3.x;
					r0.w = mad(r0.w, 6.0, r2.y);
					r0.w = r4.y != 0.0 ? r0.w : r2.y;
					r0.w = r4.x != 0.0 ? r2.x : r0.w;
					r3.w = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.y), mad(-(r0.y), v1.y, r3.z), mad(-(r0.z), v1.z, r3.w));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 83: BLENDMODES_MODE_SATURATION + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r2.xyz = float3(r2.x * _BLENDMODES_OverlayColor.x, r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r3.x = r0.w + r2.w;
				r3.yzw = float3(r3.x * 0.5, r3.x * 0.5, r3.x * 0.5);
				r4.x = max(r2.z, r2.y);
				r4.x = max(r2.x, r4.x);
				r2.y = min(r2.z, r2.y);
				r2.x = min(r2.y, r2.x);
				r2.y = r2.x + r4.x;
				r2.z = (r2.x == r4.x) ? 1.0 : 0.0;
				r4.y = -(r2.x) + r4.x;
				r4.z = (1.0 < r2.y) ? 1.0 : 0.0;
				r2.x = -(r4.x) + -(r2.x);
				r2.x = r2.x + 2.0;
				r2.xy = float2(r4.y / r2.x, r4.y / r2.y);
				r2.x = r4.z != 0.0 ? r2.x : r2.y;
				r2.x = r2.z != 0.0 ? 0.0 : r2.x;
				r2.y = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r2.y)) {
					r2.y = (r0.w == r2.w) ? 1.0 : 0.0;
					r0.w = r0.w + -(r2.w);
					r4.xyzw = float4((r1.y < r1.x) ? 1.0 : 0.0, (r1.z < r1.x) ? 1.0 : 0.0, (r1.y < r1.z) ? 1.0 : 0.0, (r1.z < r1.y) ? 1.0 : 0.0);
					r2.z = ((((r4.y) != 0.0) && ((r4.x) != 0.0)) ? 1.0 : 0.0);
					r5.xyz = float3(mad(r0.y, v1.y, -(r1.z)), mad(r0.z, v1.z, -(r1.x)), mad(r0.x, v1.x, -(r1.y)));
					r5.xyz = float3(r5.x / r0.w, r5.y / r0.w, r5.z / r0.w);
					r0.w = (((r4.z) != 0.0) ? 6.0 : 0.0);
					r0.w = r0.w + r5.x;
					r4.xy = float2(r5.y + 2.0, r5.z + 4.0);
					r2.w = r4.w != 0.0 ? r4.x : r4.y;
					r0.w = r2.z != 0.0 ? r0.w : r2.w;
					r0.w = r0.w * 0.166667;
					r0.w = r2.y != 0.0 ? 0.0 : r0.w;
					r2.y = (r3.x < 1.0) ? 1.0 : 0.0;
					r2.z = r2.x + 1.0;
					r2.z = r2.z * r3.w;
					r2.w = mad(r3.x, 0.5, r2.x);
					r2.x = mad(-(r3.w), r2.x, r2.w);
					r2.x = r2.y != 0.0 ? r2.z : r2.x;
					r2.y = -(r2.x) + r3.x;
					r4.xyzw = float4(r0.w + 0.333333, r0.w + 1.333333, r0.w + 1.0, r0.w + -0.333333);
					r2.zw = float2((r4.x < 0.0) ? 1.0 : 0.0, (r4.w < 0.0) ? 1.0 : 0.0);
					r2.z = r2.z != 0.0 ? r4.y : r4.x;
					r3.x = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.x = r2.z + -1.0;
					r2.z = r3.x != 0.0 ? r4.x : r2.z;
					r3.x = -(r2.y) + r2.x;
					r4.x = r3.x * 6.0;
					r4.y = mad(r4.x, r2.z, r2.y);
					r5.x = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.yz = float2((r2.z < 0.666667) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.z != 0.0 ? r2.z : r2.y;
					r2.z = r5.y != 0.0 ? r2.x : r2.z;
					r3.y = r5.x != 0.0 ? r2.z : r4.y;
					r2.z = (r0.w < 0.0) ? 1.0 : 0.0;
					r2.z = r2.z != 0.0 ? r4.z : r0.w;
					r4.y = (1.0 < r2.z) ? 1.0 : 0.0;
					r4.z = r2.z + -1.0;
					r2.z = r4.y != 0.0 ? r4.z : r2.z;
					r4.y = mad(r4.x, r2.z, r2.y);
					r4.z = (r2.z >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.z < 0.5) ? 1.0 : 0.0, (r2.z < 0.666667) ? 1.0 : 0.0);
					r2.z = -(r2.z) + 0.666667;
					r2.z = r2.z * r3.x;
					r2.z = mad(r2.z, 6.0, r2.y);
					r2.z = r5.y != 0.0 ? r2.z : r2.y;
					r2.z = r5.x != 0.0 ? r2.x : r2.z;
					r3.z = r4.z != 0.0 ? r2.z : r4.y;
					r0.w = r0.w + 0.666667;
					r0.w = r2.w != 0.0 ? r0.w : r4.w;
					r2.z = (1.0 < r0.w) ? 1.0 : 0.0;
					r2.w = r0.w + -1.0;
					r0.w = r2.z != 0.0 ? r2.w : r0.w;
					r2.z = mad(r4.x, r0.w, r2.y);
					r2.w = (r0.w >= 0.166667) ? 1.0 : 0.0;
					r4.xy = float2((r0.w < 0.5) ? 1.0 : 0.0, (r0.w < 0.666667) ? 1.0 : 0.0);
					r0.w = -(r0.w) + 0.666667;
					r0.w = r0.w * r3.x;
					r0.w = mad(r0.w, 6.0, r2.y);
					r0.w = r4.y != 0.0 ? r0.w : r2.y;
					r0.w = r4.x != 0.0 ? r2.x : r0.w;
					r3.w = r2.w != 0.0 ? r0.w : r2.z;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r3.y), mad(-(r0.y), v1.y, r3.z), mad(-(r0.z), v1.z, r3.w));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 84: BLENDMODES_MODE_SCREEN ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(-(r2.x), r0.x, -(r1.x)), mad(-(r2.y), r0.y, -(r1.y)), mad(-(r2.z), r0.z, -(r1.z)));
				r0.xyz = float3(r0.x + 1.0, r0.y + 1.0, r0.z + 1.0);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 85: BLENDMODES_MODE_SCREEN + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r2.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r0.xyz = float3(mad(-(r2.x), r0.x, -(r1.x)), mad(-(r2.y), r0.y, -(r1.y)), mad(-(r2.z), r0.z, -(r1.z)));
				r0.xyz = float3(r0.x + 1.0, r0.y + 1.0, r0.z + 1.0);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 86: BLENDMODES_MODE_SCREEN + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r2.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r0.xyz = float3(mad(-(r2.x), r0.x, -(r1.x)), mad(-(r2.y), r0.y, -(r1.y)), mad(-(r2.z), r0.z, -(r1.z)));
				r0.xyz = float3(r0.x + 1.0, r0.y + 1.0, r0.z + 1.0);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 87: BLENDMODES_MODE_SCREEN + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r2.xyz = float3(mad(-(r1.x), v1.x, 1.0), mad(-(r1.y), v1.y, 1.0), mad(-(r1.z), v1.z, 1.0));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(-(r2.x), r0.x, -(r1.x)), mad(-(r2.y), r0.y, -(r1.y)), mad(-(r2.z), r0.z, -(r1.z)));
				r0.xyz = float3(r0.x + 1.0, r0.y + 1.0, r0.z + 1.0);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 88: BLENDMODES_MODE_SOFTLIGHT ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r0.xyz = float3(mad(-(r3.x), r0.x, 1.0), mad(-(r3.y), r0.y, 1.0), mad(-(r3.z), r0.z, 1.0));
				r4.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r4.x, r0.y * r4.y, r0.z * r4.z);
				r3.xyz = float3(r3.x * r4.x, r3.y * r4.y, r3.z * r4.z);
				r0.xyz = float3(mad(r3.x, r1.x, r0.x), mad(r3.y, r1.y, r0.y), mad(r3.z, r1.z, r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 89: BLENDMODES_MODE_SOFTLIGHT + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r0.xyz = float3(mad(-(r3.x), r0.x, 1.0), mad(-(r3.y), r0.y, 1.0), mad(-(r3.z), r0.z, 1.0));
				r4.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r0.w = -(r2.w) + r4.x;
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r4.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r4.x, r0.y * r4.y, r0.z * r4.z);
				r3.xyz = float3(r3.x * r4.x, r3.y * r4.y, r3.z * r4.z);
				r0.xyz = float3(mad(r3.x, r1.x, r0.x), mad(r3.y, r1.y, r0.y), mad(r3.z, r1.z, r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 90: BLENDMODES_MODE_SOFTLIGHT + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r0.xyz = float3(mad(-(r3.x), r0.x, 1.0), mad(-(r3.y), r0.y, 1.0), mad(-(r3.z), r0.z, 1.0));
				r4.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r0.w = -(r2.w) + r4.x;
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r4.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r4.x, r0.y * r4.y, r0.z * r4.z);
				r3.xyz = float3(r3.x * r4.x, r3.y * r4.y, r3.z * r4.z);
				r0.xyz = float3(mad(r3.x, r1.x, r0.x), mad(r3.y, r1.y, r0.y), mad(r3.z, r1.z, r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 91: BLENDMODES_MODE_SOFTLIGHT + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(-(r0.x), _BLENDMODES_OverlayColor.x, 1.0), mad(-(r0.y), _BLENDMODES_OverlayColor.y, 1.0), mad(-(r0.z), _BLENDMODES_OverlayColor.z, 1.0));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r3.xyz = float3(mad(-(r2.x), v1.x, 1.0), mad(-(r2.y), v1.y, 1.0), mad(-(r2.z), v1.z, 1.0));
				r0.xyz = float3(mad(-(r3.x), r0.x, 1.0), mad(-(r3.y), r0.y, 1.0), mad(-(r3.z), r0.z, 1.0));
				r4.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(r0.x * r4.x, r0.y * r4.y, r0.z * r4.z);
				r3.xyz = float3(r3.x * r4.x, r3.y * r4.y, r3.z * r4.z);
				r0.xyz = float3(mad(r3.x, r1.x, r0.x), mad(r3.y, r1.y, r0.y), mad(r3.z, r1.z, r0.z));
				r0.xyz = float3(mad(-(r2.x), v1.x, r0.x), mad(-(r2.y), v1.y, r0.y), mad(-(r2.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 92: BLENDMODES_MODE_SUBTRACT ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, -(r0.x), r1.x), mad(_BLENDMODES_OverlayColor.w, -(r0.y), r1.y), mad(_BLENDMODES_OverlayColor.w, -(r0.z), r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 93: BLENDMODES_MODE_SUBTRACT + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, -(r0.x), r1.x), mad(_BLENDMODES_OverlayColor.w, -(r0.y), r1.y), mad(_BLENDMODES_OverlayColor.w, -(r0.z), r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 94: BLENDMODES_MODE_SUBTRACT + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r2.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r1.x + -(r2.w);
				r2.w = mad(_EnableExternalAlpha, r0.w, r2.w);
				r1.xyzw = float4(r2.x * v1.x, r2.y * v1.y, r2.z * v1.z, r2.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, -(r0.x), r1.x), mad(_BLENDMODES_OverlayColor.w, -(r0.y), r1.y), mad(_BLENDMODES_OverlayColor.w, -(r0.z), r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 95: BLENDMODES_MODE_SUBTRACT + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && defined(BLENDMODES_MODE_SUBTRACT) && !defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, -(r0.x), r1.x), mad(_BLENDMODES_OverlayColor.w, -(r0.y), r1.y), mad(_BLENDMODES_OverlayColor.w, -(r0.z), r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			
			// ---- variant 96: BLENDMODES_MODE_VIVIDLIGHT ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(mad(-(r0.x), 2.0, 1.0), mad(-(r0.y), 2.0, 1.0), mad(-(r0.z), 2.0, 1.0));
				r2.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r1.xyz = float3((0.5 < r1.x) ? 1.0 : 0.0, (0.5 < r1.y) ? 1.0 : 0.0, (0.5 < r1.z) ? 1.0 : 0.0);
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r4.xyz = float3(mad(-(r3.x), v1.x, 1.0), mad(-(r3.y), v1.y, 1.0), mad(-(r3.z), v1.z, 1.0));
				r2.xyz = float3(r4.x / r2.x, r4.y / r2.y, r4.z / r2.z);
				r2.xyz = float3(-(r2.x) + 1.0, -(r2.y) + 1.0, -(r2.z) + 1.0);
				r4.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r0.xyz = float3(r4.x / r0.x, r4.y / r0.y, r4.z / r0.z);
				r0.xyz = float3(saturate(r1.x != 0.0 ? r0.x : r2.x), saturate(r1.y != 0.0 ? r0.y : r2.y), saturate(r1.z != 0.0 ? r0.z : r2.z));
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
				return o0;
			}
			
			// ---- variant 97: BLENDMODES_MODE_VIVIDLIGHT + ETC1_EXTERNAL_ALPHA ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyz = float3(mad(-(r1.x), 2.0, 1.0), mad(-(r1.y), 2.0, 1.0), mad(-(r1.z), 2.0, 1.0));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(r2.x / r1.x, r2.y / r1.y, r2.z / r1.z);
				r4.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r5.xyz = float3(mad(-(r3.x), v1.x, 1.0), mad(-(r3.y), v1.y, 1.0), mad(-(r3.z), v1.z, 1.0));
				r0.xyz = float3(r5.x / r0.x, r5.y / r0.y, r5.z / r0.z);
				r0.xyz = float3(-(r0.x) + 1.0, -(r0.y) + 1.0, -(r0.z) + 1.0);
				r0.xyz = float3(saturate(r4.x != 0.0 ? r1.x : r0.x), saturate(r4.y != 0.0 ? r1.y : r0.y), saturate(r4.z != 0.0 ? r1.z : r0.z));
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 98: BLENDMODES_MODE_VIVIDLIGHT + ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && defined(BLENDMODES_MODE_VIVIDLIGHT) && defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			sampler2D t2;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0;
			
				r0.xyzw = tex2D(t2, float2(v2.z, v2.w));
				r1.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r1.xyz = float3(mad(-(r1.x), 2.0, 1.0), mad(-(r1.y), 2.0, 1.0), mad(-(r1.z), 2.0, 1.0));
				r2.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.w = r2.x + -(r3.w);
				r3.w = mad(_EnableExternalAlpha, r0.w, r3.w);
				r2.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r1.xyz = float3(r2.x / r1.x, r2.y / r1.y, r2.z / r1.z);
				r4.xyz = float3((0.5 < r0.x) ? 1.0 : 0.0, (0.5 < r0.y) ? 1.0 : 0.0, (0.5 < r0.z) ? 1.0 : 0.0);
				r0.xyz = float3(r0.x + r0.x, r0.y + r0.y, r0.z + r0.z);
				r5.xyz = float3(mad(-(r3.x), v1.x, 1.0), mad(-(r3.y), v1.y, 1.0), mad(-(r3.z), v1.z, 1.0));
				r0.xyz = float3(r5.x / r0.x, r5.y / r0.y, r5.z / r0.z);
				r0.xyz = float3(-(r0.x) + 1.0, -(r0.y) + 1.0, -(r0.z) + 1.0);
				r0.xyz = float3(saturate(r4.x != 0.0 ? r1.x : r0.x), saturate(r4.y != 0.0 ? r1.y : r0.y), saturate(r4.z != 0.0 ? r1.z : r0.z));
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r2.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r2.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r2.z));
				o0.xyz = float3(r2.w * r0.x, r2.w * r0.y, r2.w * r0.z);
				o0.w = r2.w;
				return o0;
			}
			
			// ---- variant 99: BLENDMODES_MODE_VIVIDLIGHT + PIXELSNAP_ON ----
			#elif !defined(BLENDMODES_MODE_COLOR) && !defined(BLENDMODES_MODE_COLORBURN) && !defined(BLENDMODES_MODE_COLORDODGE) && !defined(BLENDMODES_MODE_DARKEN) && !defined(BLENDMODES_MODE_DARKERCOLOR) && !defined(BLENDMODES_MODE_DIFFERENCE) && !defined(BLENDMODES_MODE_DIVIDE) && !defined(BLENDMODES_MODE_EXCLUSION) && !defined(BLENDMODES_MODE_HARDLIGHT) && !defined(BLENDMODES_MODE_HARDMIX) && !defined(BLENDMODES_MODE_HUE) && !defined(BLENDMODES_MODE_LIGHTEN) && !defined(BLENDMODES_MODE_LIGHTERCOLOR) && !defined(BLENDMODES_MODE_LINEARBURN) && !defined(BLENDMODES_MODE_LINEARDODGE) && !defined(BLENDMODES_MODE_LINEARLIGHT) && !defined(BLENDMODES_MODE_LUMINOSITY) && !defined(BLENDMODES_MODE_MULTIPLY) && !defined(BLENDMODES_MODE_OVERLAY) && !defined(BLENDMODES_MODE_PINLIGHT) && !defined(BLENDMODES_MODE_SATURATION) && !defined(BLENDMODES_MODE_SCREEN) && !defined(BLENDMODES_MODE_SOFTLIGHT) && !defined(BLENDMODES_MODE_SUBTRACT) && defined(BLENDMODES_MODE_VIVIDLIGHT) && !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r1.xyz = float3(r0.x * _BLENDMODES_OverlayColor.x, r0.y * _BLENDMODES_OverlayColor.y, r0.z * _BLENDMODES_OverlayColor.z);
				r0.xyz = float3(mad(r0.x, _BLENDMODES_OverlayColor.x, -0.5), mad(r0.y, _BLENDMODES_OverlayColor.y, -0.5), mad(r0.z, _BLENDMODES_OverlayColor.z, -0.5));
				r0.xyz = float3(mad(-(r0.x), 2.0, 1.0), mad(-(r0.y), 2.0, 1.0), mad(-(r0.z), 2.0, 1.0));
				r2.xyz = float3(r1.x + r1.x, r1.y + r1.y, r1.z + r1.z);
				r1.xyz = float3((0.5 < r1.x) ? 1.0 : 0.0, (0.5 < r1.y) ? 1.0 : 0.0, (0.5 < r1.z) ? 1.0 : 0.0);
				r3.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r4.xyz = float3(mad(-(r3.x), v1.x, 1.0), mad(-(r3.y), v1.y, 1.0), mad(-(r3.z), v1.z, 1.0));
				r2.xyz = float3(r4.x / r2.x, r4.y / r2.y, r4.z / r2.z);
				r2.xyz = float3(-(r2.x) + 1.0, -(r2.y) + 1.0, -(r2.z) + 1.0);
				r4.xyzw = float4(r3.x * v1.x, r3.y * v1.y, r3.z * v1.z, r3.w * v1.w);
				r0.xyz = float3(r4.x / r0.x, r4.y / r0.y, r4.z / r0.z);
				r0.xyz = float3(saturate(r1.x != 0.0 ? r0.x : r2.x), saturate(r1.y != 0.0 ? r0.y : r2.y), saturate(r1.z != 0.0 ? r0.z : r2.z));
				r0.xyz = float3(mad(-(r3.x), v1.x, r0.x), mad(-(r3.y), v1.y, r0.y), mad(-(r3.z), v1.z, r0.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r4.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r4.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r4.z));
				o0.xyz = float3(r4.w * r0.x, r4.w * r0.y, r4.w * r0.z);
				o0.w = r4.w;
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
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
				float2  uv1          : TEXCOORD1;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 4 of them are named below
			float4 _MainTex_ST;
			float4 _Color;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayColor;
			// $Globals cb0[6] = 6 row(s) / 96 bytes; 4 row(s) read, 4 named.
			//   read whole    : rows 2, 3, 4, 5
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 3 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (96 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0 / o.uv1
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				r0.xy = float2(mad(v2.x, _MainTex_ST.x, _MainTex_ST.z), mad(v2.y, _MainTex_ST.y, _MainTex_ST.w));
				o2.zw = float2(mad(r0.x, _BLENDMODES_OverlayTexture_ST.x, _BLENDMODES_OverlayTexture_ST.z), mad(r0.y, _BLENDMODES_OverlayTexture_ST.y, _BLENDMODES_OverlayTexture_ST.w));
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				o.uv1 = o2.zw;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float4 v2 = float4(i.uv0.x, i.uv0.y, i.uv1.x, i.uv1.y);
				float4 o0;
				float4 r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r1.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				r2.xyzw = tex2D(t1, float2(v2.z, v2.w));
				r3.xyz = float3(r2.y * _BLENDMODES_OverlayColor.y, r2.z * _BLENDMODES_OverlayColor.z, r2.x * _BLENDMODES_OverlayColor.x);
				r0.w = max(r1.z, r1.y);
				r0.w = max(r0.w, r1.x);
				r2.w = min(r1.z, r1.y);
				r2.w = min(r1.x, r2.w);
				r0.w = r0.w + r2.w;
				r4.xyz = float3(r0.w * 0.5, r0.w * 0.5, r0.w * 0.5);
				r2.w = max(r3.y, r3.x);
				r2.w = max(r2.w, r3.z);
				r3.w = min(r3.y, r3.x);
				r3.w = min(r3.w, r3.z);
				r4.w = r2.w + r3.w;
				r5.x = (r2.w == r3.w) ? 1.0 : 0.0;
				r5.y = r2.w + -(r3.w);
				r5.z = (1.0 < r4.w) ? 1.0 : 0.0;
				r2.w = -(r2.w) + -(r3.w);
				r2.w = r2.w + 2.0;
				r2.w = r5.y / r2.w;
				r3.w = r5.y / r4.w;
				r6.x = r5.z != 0.0 ? r2.w : r3.w;
				r7.xyzw = float4((r3.x < r3.z) ? 1.0 : 0.0, (r3.y < r3.z) ? 1.0 : 0.0, (r3.x < r3.y) ? 1.0 : 0.0, (r3.y < r3.x) ? 1.0 : 0.0);
				r2.w = ((((r7.y) != 0.0) && ((r7.x) != 0.0)) ? 1.0 : 0.0);
				r2.xyz = float3(mad(r2.y, _BLENDMODES_OverlayColor.y, -(r3.y)), mad(r2.z, _BLENDMODES_OverlayColor.z, -(r3.z)), mad(r2.x, _BLENDMODES_OverlayColor.x, -(r3.x)));
				r2.xyz = float3(r2.x / r5.y, r2.y / r5.y, r2.z / r5.y);
				r3.x = (((r7.z) != 0.0) ? 6.0 : 0.0);
				r2.x = r2.x + r3.x;
				r2.yz = float2(r2.y + 4.0, r2.z + 4.0);
				r2.y = r7.w != 0.0 ? r2.y : r2.z;
				r2.x = r2.w != 0.0 ? r2.x : r2.y;
				r6.yzw = float3(r2.x * 0.166667, r2.x * 0.166667, r2.x * 0.166667);
				r2.xyzw = float4(r5.x != 0.0 ? 0.0 : r6.x, r5.x != 0.0 ? 0.0 : r6.y, r5.x != 0.0 ? 0.0 : r6.z, r5.x != 0.0 ? 0.0 : r6.w);
				r3.x = (r2.x == 0.0) ? 1.0 : 0.0;
				if (!any(r3.x)) {
					r3.x = (r0.w < 1.0) ? 1.0 : 0.0;
					r5.xyzw = float4(r2.x + 1.0, r2.y + 0.333333, r2.z + 1.333333, r2.w + 1.0);
					r2.y = r4.z * r5.x;
					r2.z = mad(r0.w, 0.5, r2.x);
					r2.x = mad(-(r4.z), r2.x, r2.z);
					r2.x = r3.x != 0.0 ? r2.y : r2.x;
					r0.w = r0.w + -(r2.x);
					r2.y = (r5.y < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.z : r5.y;
					r2.z = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.x = r2.y + -1.0;
					r2.y = r2.z != 0.0 ? r3.x : r2.y;
					r2.z = -(r0.w) + r2.x;
					r3.x = r2.z * 6.0;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.x = r3.z != 0.0 ? r2.y : r3.y;
					r2.y = (r2.w < 0.0) ? 1.0 : 0.0;
					r2.y = r2.y != 0.0 ? r5.w : r2.w;
					r3.y = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.z = r2.y + -1.0;
					r2.y = r3.y != 0.0 ? r3.z : r2.y;
					r3.y = mad(r3.x, r2.y, r0.w);
					r3.z = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r5.xy = float2((r2.y < 0.5) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r2.y = r5.y != 0.0 ? r2.y : r0.w;
					r2.y = r5.x != 0.0 ? r2.x : r2.y;
					r4.y = r3.z != 0.0 ? r2.y : r3.y;
					r2.yw = float2(r2.w + 0.666667, r2.w + 0.666667);
					r3.y = (r2.y < 0.0) ? 1.0 : 0.0;
					r2.y = r3.y != 0.0 ? r2.w : r2.y;
					r2.w = (1.0 < r2.y) ? 1.0 : 0.0;
					r3.y = r2.y + -1.0;
					r2.y = r2.w != 0.0 ? r3.y : r2.y;
					r2.w = mad(r3.x, r2.y, r0.w);
					r3.x = (r2.y >= 0.166667) ? 1.0 : 0.0;
					r3.yz = float2((r2.y < 0.666667) ? 1.0 : 0.0, (r2.y < 0.666667) ? 1.0 : 0.0);
					r2.y = -(r2.y) + 0.666667;
					r2.y = r2.y * r2.z;
					r2.y = mad(r2.y, 6.0, r0.w);
					r0.w = r3.z != 0.0 ? r2.y : r0.w;
					r0.w = r3.y != 0.0 ? r2.x : r0.w;
					r4.z = r3.x != 0.0 ? r0.w : r2.w;
				// endif
				}
				r0.xyz = float3(mad(-(r0.x), v1.x, r4.x), mad(-(r0.y), v1.y, r4.y), mad(-(r0.z), v1.z, r4.z));
				r0.xyz = float3(mad(_BLENDMODES_OverlayColor.w, r0.x, r1.x), mad(_BLENDMODES_OverlayColor.w, r0.y, r1.y), mad(_BLENDMODES_OverlayColor.w, r0.z, r1.z));
				o0.xyz = float3(r1.w * r0.x, r1.w * r0.y, r1.w * r0.z);
				o0.w = r1.w;
				return o0;
			}
			#endif
			
			ENDCG
		}

		Pass
		{
			Tags { "LightMode"="Always" }
			ZWrite Off
			Cull Off
			Stencil
			{
				Ref [_BLENDMODES_StencilId]
				ReadMask 255
				WriteMask 255
				Comp [_BLENDMODES_NormalStencilComp]
			}
			Blend One OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#pragma target 4.0
			#pragma multi_compile __ ETC1_EXTERNAL_ALPHA
			#pragma multi_compile __ PIXELSNAP_ON
			
			// 4 keyword variant(s), 2 independent toggle(s)
			// variants: full cartesian coverage (4/4): every toggle combination was compiled -- multi_compile semantics
			
			// ---- variant 0 (base) ----
			#if !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			
			sampler2D _MainTex;
			// cb0 = $Globals (material properties); 1 of them are named below
			float4 _Color;
			// $Globals cb0[4] = 4 row(s) / 64 bytes; 1 row(s) read, 1 named.
			//   read whole    : rows 3
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			float4 _MainTex_ST;
			float4 _MainTex_TexelSize;
			float PixelSnap;
			sampler2D _AlphaTex;
			float4 _AlphaTex_ST;
			float4 _AlphaTex_TexelSize;
			float _EnableExternalAlpha;
			sampler2D _BLENDMODES_OverlayTexture;
			float4 _BLENDMODES_OverlayTexture_ST;
			float4 _BLENDMODES_OverlayTexture_TexelSize;
			float4 _BLENDMODES_OverlayColor;
			float _BLENDMODES_StencilId;
			float _BLENDMODES_BlendStencilComp;
			float _BLENDMODES_NormalStencilComp;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (64 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float2 v2 = i.uv0;
				float4 o0;
				float4 r0 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
				return o0;
			}
			
			// ---- variant 1: ETC1_EXTERNAL_ALPHA ----
			#elif defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 1 of them are named below
			float4 _Color;
			// $Globals cb0[4] = 4 row(s) / 64 bytes; 1 row(s) read, 1 named.
			//   read whole    : rows 3
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (64 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float2 v2 = i.uv0;
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.x = r0.x + -(r1.w);
				r1.w = mad(_EnableExternalAlpha, r0.x, r1.w);
				r0.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
				return o0;
			}
			
			// ---- variant 2: ETC1_EXTERNAL_ALPHA + PIXELSNAP_ON ----
			#elif defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			
			sampler2D _MainTex;
			sampler2D t1;
			// cb0 = $Globals (material properties); 1 of them are named below
			float4 _Color;
			// $Globals cb0[4] = 4 row(s) / 64 bytes; 1 row(s) read, 1 named.
			//   read whole    : rows 3
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (64 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float2 v2 = i.uv0;
				float4 o0;
				float4 r0 = 0, r1 = 0;
			
				r0.xyzw = tex2D(t1, float2(v2.x, v2.y));
				r1.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.x = r0.x + -(r1.w);
				r1.w = mad(_EnableExternalAlpha, r0.x, r1.w);
				r0.xyzw = float4(r1.x * v1.x, r1.y * v1.y, r1.z * v1.z, r1.w * v1.w);
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
				return o0;
			}
			
			// ---- variant 3: PIXELSNAP_ON ----
			#elif !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
			struct appdata
			{
				float4  vertex       : POSITION;
				float4  color        : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			struct v2f
			{
				float4  pos          : SV_POSITION;
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			
			sampler2D _MainTex;
			// cb0 = $Globals (material properties); 1 of them are named below
			float4 _Color;
			// $Globals cb0[4] = 4 row(s) / 64 bytes; 1 row(s) read, 1 named.
			//   read whole    : rows 3
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerCamera  (declared by UnityCG.cginc)
			// cb2 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb3 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb4 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (64 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			r0 = UnityObjectToClipPos(r0.xyz);
				r0.xy = float2(r0.x / r0.w, r0.y / r0.w);
				r1.xy = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
				r0.xy = float2(r0.x * r1.x, r0.y * r1.y);
				r0.xy = float2(round(r0.x), round(r0.y));
				r0.xy = float2(r0.x / r1.x, r0.y / r1.y);
				o0.xy = float2(r0.w * r0.x, r0.w * r0.y);
				o0.zw = float2(r0.z, r0.w);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float2 v2 = i.uv0;
				float4 o0;
				float4 r0 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
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
				float4  color0       : COLOR0;
				float2  uv0          : TEXCOORD0;
			};
			
			sampler2D _MainTex;
			// cb0 = $Globals (material properties); 1 of them are named below
			float4 _Color;
			// $Globals cb0[4] = 4 row(s) / 64 bytes; 1 row(s) read, 1 named.
			//   read whole    : rows 3
			// Every read row is named.  The block also holds rows neither stage
			// touches; they stay undeclared because nothing can pin them.
			// cb1 = UnityPerDraw  (declared by UnityCG.cginc)
			// cb2 = UnityPerFrame  (declared by UnityCG.cginc)
			// cb3 = UnityPerDrawSprite  (not in UnityCG.cginc; declared here)
			float4 _RendererColor;
			float2 _Flip;
			// cbuffer order confirmed by the asset's own register index
			// $Globals cb0: 1 member(s) named from the asset's own constant-buffer reflection (64 bytes)
			
			v2f vert (in appdata v)
			{
				float4 v0 = v.vertex;
				float4 v1 = v.color;
				float2 v2 = v.uv0;
				float4 o0;  // -> o.pos
				float4 o1;  // -> o.color0
				float4 o2;  // -> o.uv0
				float4 r0 = 0, r1 = 0;
			
				r0.xy = float2(v0.x * _Flip.x, v0.y * _Flip.y);
			o0 = UnityObjectToClipPos(r0.xyz);
				r0.xyzw = float4(v1.x * _Color.x, v1.y * _Color.y, v1.z * _Color.z, v1.w * _Color.w);
				o1.xyzw = float4(r0.x * _RendererColor.x, r0.y * _RendererColor.y, r0.z * _RendererColor.z, r0.w * _RendererColor.w);
				o2.xy = float2(v2.x, v2.y);
			
				v2f o;
				o.pos = o0;
				o.color0 = o1;
				o.uv0 = o2.xy;
				return o;
			}
			float4 frag (in v2f i) : SV_Target
			{
				float4 v0 = i.pos;
				float4 v1 = i.color0;
				float2 v2 = i.uv0;
				float4 o0;
				float4 r0 = 0;
			
				r0.xyzw = tex2D(_MainTex, float2(v2.x, v2.y));
				r0.xyzw = float4(r0.x * v1.x, r0.y * v1.y, r0.z * v1.z, r0.w * v1.w);
				o0.xyz = float3(r0.w * r0.x, r0.w * r0.y, r0.w * r0.z);
				o0.w = r0.w;
				return o0;
			}
			#endif
			
			ENDCG
		}
	}
	Fallback "Sprites/Default"
}
