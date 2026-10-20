///////////////////////////////////////////////
///// CameraFilterPack - by VETASOFT 2018 /////
///////////////////////////////////////////////

Shader "CameraFilterPack/RainFX" { 
Properties 
{
_MainTex ("Base (RGB)", 2D) = "white" {}
_TimeX ("Time", Range(0.0, 1.0)) = 1.0
_ScreenResolution ("_ScreenResolution", Vector) = (0.,0.,0.,0.)
}
SubShader
{
Pass
{
ZTest Always
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma fragmentoption ARB_precision_hint_fastest
#pragma target 3.0
#pragma glsl
#include "UnityCG.cginc"
uniform sampler2D _MainTex;
uniform sampler2D Texture2;
uniform sampler2D Texture3;
uniform float _TimeX;
uniform float count;
uniform float _Value2;
uniform float _Value;
uniform float _Speed;

uniform float4 Coord1;
uniform float4 Coord2;
uniform float4 Coord3;
uniform float4 Coord4;

uniform float4 _ScreenResolution;

uniform float2 _MainTex_TexelSize;


struct appdata_t
{
float4 vertex   : POSITION;
float4 color    : COLOR;
float2 texcoord : TEXCOORD0;

};
struct v2f
{
float2 texcoord  : TEXCOORD0;
float4 vertex   : SV_POSITION;
float4 color    : COLOR;
};
v2f vert(appdata_t IN)
{
v2f OUT;
OUT.vertex = UnityObjectToClipPos(IN.vertex);
OUT.texcoord = IN.texcoord;
OUT.color = IN.color;
return OUT;
}

float4 Dropflow(float2 uv, float2 p, float2 zoom, float count, float num, float3 mask)
{
	
	float d = smoothstep(0.01, 0.1 , count / 256);
	d -= smoothstep(0.7, 0.98, count / 256);
	d = saturate(d);
	zoom.y *= 0.15*(num+1);
	uv *= zoom;
	float2 uv2 = uv;
	float x = 0;
	float2 c = float2(fmod(count, 32), 8 - floor(count / 32));
	float2 m = float2(0.03125 / zoom.x, 0.125 / zoom.y);
	float2 pos = float2(m.x * c.x, m.y * c.y);
	pos *= zoom;
	float2 spos = float2(p.x * zoom.x, p.y * zoom.y);
	pos -= spos;
	float4 n = float4(0, 0, 0, 0);
	n = tex2D(Texture3, uv2 + pos);
	n.rgb *= mask;
	n = n.r + n.g + n.b;
	pos = spos;
	pos.y = 1 - pos.y;
	x = smoothstep(pos.x + 0.03122, pos.x + 0.03125, uv.x);
	x += smoothstep(pos.x + 0.0003, pos.x, uv.x);
	x += smoothstep(pos.y + 0.1242, pos.y + 0.125, 1 - uv.y);
	x += smoothstep(pos.y + 0.0003, pos.y, 1 - uv.y);
	n = lerp(n, 0, saturate(x));
	
	return n*d;
}
float4 Drop(float2 uv)
{
	float2 uvd = uv;
	uvd.y -= 0.16;
	float4 n = tex2D(Texture2, uvd);
	uvd.y = n.b + uv.y + (_Time*_Speed*(n.b*n.b));
	float4 rd = tex2D(Texture2, float2(uv.x, uvd.y)).g;
	return rd*(1-n.r);
}

half4 _MainTex_ST;
float4 frag(v2f i) : COLOR
{
float2 uvst = UnityStereoScreenSpaceUVAdjust(i.texcoord, _MainTex_ST);
	float2 uv = i.texcoord.xy;
	float4 rd = float4(0, 0, 0, 0);
	#if UNITY_UV_STARTS_AT_TOP
	if (_MainTex_TexelSize.y < 0)
	uv.y = 1 - uv.y;
	#endif
	rd = Dropflow(uv, Coord1.rg, float2(0.85, 0.85), Coord1.b, Coord1.a,  float3(1, 0, 0));
	rd += Dropflow(uv, Coord2.rg, float2(0.85, 0.85), Coord2.b, Coord2.a, float3(0, 1, 0));
	rd += Dropflow(uv, Coord3.rg, float2(0.85, 0.85), Coord3.b, Coord3.a, float3(0, 0, 1));
	rd += Dropflow(uv, Coord4.rg, float2(0.85, 0.85), Coord4.b, Coord4.a, float3(1, 0, 0));

	rd += Drop(uv);
	rd += Drop(uv*0.75 +float2(0.5,0.5));

	rd *= _Value;
	rd = saturate(rd);
	float2 df = float2(0, rd.r)*0.25;
	float4 tx = tex2D(_MainTex, uvst.xy+df);
	tx += rd*0.35;
	

	return tx;
}
ENDCG
}
}
}
