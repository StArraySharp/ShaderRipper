// Copyright 2014-2018 Elringus (Artyom Sovetnikov). All Rights Reserved.

#ifndef BLEND_MODES_CG_INCLUDED
#define BLEND_MODES_CG_INCLUDED

#include "UnityCG.cginc"
#include "BlendFunctions.cginc"

///////////////////////////////////////////////////////////////////////
//  Applies a hue level adjustment to the provided color. 
//  color: The color to adjust. 
//  hue: Hue adjustment level in 0.0 to 360.0 range. 
inline float3 ApplyHue(float3 color, float hue)
{
    float angle = radians(hue);
    float3 k = float3(0.57735, 0.57735, 0.57735);
    float cosAngle = cos(angle);

    return color * cosAngle + cross(k, color) * sin(angle) + k * dot(k, color) * (1 - cosAngle);
}

///////////////////////////////////////////////////////////////////////
//  Applies an HSBC (hue, saturation, brightness, contrast) levels adjustment to the provided color. 
//  color: The color to adjust. 
//  hsbc: HSBC (hue, saturation, brightness, contrast) adjustment levels in -1.0 to 1.0 range. 
inline float3 ApplyHsbc(float3 color, fixed4 hsbc)
{
    float hue = 360 * hsbc.r;
    float saturation = hsbc.g + 1;
    float brightness = hsbc.b;
    float contrast = hsbc.a + 1;
    float3 outputColor = color;
    outputColor = ApplyHue(outputColor, hue);
    outputColor = (outputColor - 0.5) * contrast + 0.5 + brightness;
    outputColor = lerp(Luminance(outputColor), outputColor, saturation);

    return outputColor;
}

///////////////////////////////////////////////////////////////////////
//  Defines a grab position property for vertex output (pixel input) struct. 
//  idx: Texcoord index to use for the property (should be available). 
#   define BLENDMODES_GRAB_POSITION(idx) float4 BLENDMODES_GrabPosition : TEXCOORD ## idx;

///////////////////////////////////////////////////////////////////////
//  Defines an overlay texture coordinate property for the vertex output struct. 
//  idx: Texcoord index to use for the property (should be available). 
#   define BLENDMODES_OVERLAY_TEX_COORD(idx) float2 BLENDMODES_OverlayTexCoord : TEXCOORD ## idx;

///////////////////////////////////////////////////////////////////////
//  Defines a grab texture sampler.
#   define BLENDMODES_GRAB_TEXTURE_SAMPLER sampler2D _GrabTexture;

///////////////////////////////////////////////////////////////////////
//  Defines a set of varibles required in overlay shader variants.
#   define BLENDMODES_OVERLAY_VARIABLES \
        sampler2D _BLENDMODES_OverlayTexture; \
        float4 _BLENDMODES_OverlayTexture_ST; \
        fixed4 _BLENDMODES_OverlayColor;

///////////////////////////////////////////////////////////////////////
//  Defines a unified grab texture sampler.
#   define BLENDMODES_UNIFIED_GRAB_TEXTURE_SAMPLER sampler2D _BLENDMODES_UnifiedGrabTexture;

///////////////////////////////////////////////////////////////////////
//  Computes grab screen position and assigns the result to provided vertex output variable.
//  out: Vertex output struct variable with a grab position property.
//  clipv: Clipped vertex coord proprety of the output vertex struct.
#   define BLENDMODES_COMPUTE_GRAB_POSITION(out, clipv) out.BLENDMODES_GrabPosition = ComputeGrabScreenPos(clipv);

///////////////////////////////////////////////////////////////////////
//  Transforms overlay texture.
//  texcoord: Reference texture coordinates (usually trasformed main texcoord).
//  out: Vertex output struct variable.
#   define BLENDMODES_TRANSFORM_OVERLAY_TEX(texcoord, out) out.BLENDMODES_OverlayTexCoord = TRANSFORM_TEX(texcoord, _BLENDMODES_OverlayTexture);

///////////////////////////////////////////////////////////////////////
//  Applies a blend effect to the provided pixel using projection of the provided texture as a base source.
//  color: Color of the pixel to blend.
//  out: Vertex ouput struct variable.
//  tex: Texture to use a base source.
#   define BLENDMODES_BLEND_PIXEL_PROJ(color, out, tex) \
        half4 grabColor = tex2Dproj(tex, out.BLENDMODES_GrabPosition); \
        color.rgb = BLENDMODES_BLEND_COLORS(grabColor.rgb, color.rgb);

///////////////////////////////////////////////////////////////////////
//  Applies a blend effect to the provided pixel using grab texture as a base source.
//  color: Color of the pixel to blend.
//  out: Vertex ouput struct variable.
#   define BLENDMODES_BLEND_PIXEL_GRAB(color, out) BLENDMODES_BLEND_PIXEL_PROJ(color, out, _GrabTexture)

///////////////////////////////////////////////////////////////////////
//  Applies a blend effect to the provided pixel using unified grab texture as a base source.
//  color: Color of the pixel to blend.
//  out: Vertex ouput struct variable.
#   define BLENDMODES_BLEND_PIXEL_UNIFIED_GRAB(color, out) BLENDMODES_BLEND_PIXEL_PROJ(color, out, _BLENDMODES_UnifiedGrabTexture)

///////////////////////////////////////////////////////////////////////
//  Applies a blend effect to the provided pixel using a framebuffer fetched color as a base source.
//  color: Color of the pixel to blend.
//  frmb: Color of the fethed framebuffer pixel.
#   define BLENDMODES_BLEND_PIXEL_FRAMEBUFFER(color, frmb) color.rgb = BLENDMODES_BLEND_COLORS(frmb.rgb, color.rgb);

///////////////////////////////////////////////////////////////////////
//  Applies a blend effect to the provided pixel using overlay texture as a base source tinted by the overlay color.
//  Intesity of the effect is controlled by alpha of the overlay color variable.
//  color: Color of the pixel to blend (rgb).
//  out: Vertex ouput struct variable.
#   define BLENDMODES_BLEND_PIXEL_OVERLAY(color, out) \
        fixed4 overlayColor = tex2D(_BLENDMODES_OverlayTexture, out.BLENDMODES_OverlayTexCoord) * _BLENDMODES_OverlayColor; \
        fixed3 blendColor = BLENDMODES_BLEND_COLORS(color.rgb, overlayColor.rgb); \
        color.rgb = lerp(color.rgb, blendColor, _BLENDMODES_OverlayColor.a); 

///////////////////////////////////////////////////////////////////////
//  Blend function selector based on enabled shader keyword.
//  base: Color (rgb) of the pixel from the base (bottom) layer.
//  top: Color (rgb) of the pixel from the top (upper) layer.
#if defined(BLENDMODES_MODE_DARKEN)
#   define BLENDMODES_BLEND_COLORS(base, top) Darken(base, top)
#elif defined(BLENDMODES_MODE_MULTIPLY)
#   define BLENDMODES_BLEND_COLORS(base, top) Multiply(base, top)
#elif defined(BLENDMODES_MODE_COLORBURN)
#   define BLENDMODES_BLEND_COLORS(base, top) ColorBurn(base, top)
#elif defined(BLENDMODES_MODE_LINEARBURN)
#   define BLENDMODES_BLEND_COLORS(base, top) LinearBurn(base, top)
#elif defined(BLENDMODES_MODE_DARKERCOLOR)
#   define BLENDMODES_BLEND_COLORS(base, top) DarkerColor(base, top)
#elif defined(BLENDMODES_MODE_LIGHTEN)
#   define BLENDMODES_BLEND_COLORS(base, top) Lighten(base, top)
#elif defined(BLENDMODES_MODE_SCREEN)
#   define BLENDMODES_BLEND_COLORS(base, top) Screen(base, top)
#elif defined(BLENDMODES_MODE_COLORDODGE)
#   define BLENDMODES_BLEND_COLORS(base, top) ColorDodge(base, top)
#elif defined(BLENDMODES_MODE_LINEARDODGE)
#   define BLENDMODES_BLEND_COLORS(base, top) LinearDodge(base, top)
#elif defined(BLENDMODES_MODE_LIGHTERCOLOR)
#   define BLENDMODES_BLEND_COLORS(base, top) LighterColor(base, top)
#elif defined(BLENDMODES_MODE_OVERLAY)
#   define BLENDMODES_BLEND_COLORS(base, top) Overlay(base, top)
#elif defined(BLENDMODES_MODE_SOFTLIGHT)
#   define BLENDMODES_BLEND_COLORS(base, top) SoftLight(base, top)
#elif defined(BLENDMODES_MODE_HARDLIGHT)
#   define BLENDMODES_BLEND_COLORS(base, top) HardLight(base, top)
#elif defined(BLENDMODES_MODE_VIVIDLIGHT)
#   define BLENDMODES_BLEND_COLORS(base, top) VividLight(base, top)
#elif defined(BLENDMODES_MODE_LINEARLIGHT)
#   define BLENDMODES_BLEND_COLORS(base, top) LinearLight(base, top)
#elif defined(BLENDMODES_MODE_PINLIGHT)
#   define BLENDMODES_BLEND_COLORS(base, top) PinLight(base, top)
#elif defined(BLENDMODES_MODE_HARDMIX)
#   define BLENDMODES_BLEND_COLORS(base, top) HardMix(base, top)
#elif defined(BLENDMODES_MODE_DIFFERENCE)
#   define BLENDMODES_BLEND_COLORS(base, top) Difference(base, top)
#elif defined(BLENDMODES_MODE_EXCLUSION)
#   define BLENDMODES_BLEND_COLORS(base, top) Exclusion(base, top)
#elif defined(BLENDMODES_MODE_SUBTRACT)
#   define BLENDMODES_BLEND_COLORS(base, top) Subtract(base, top)
#elif defined(BLENDMODES_MODE_DIVIDE)
#   define BLENDMODES_BLEND_COLORS(base, top) Divide(base, top)
#elif defined(BLENDMODES_MODE_HUE)
#   define BLENDMODES_BLEND_COLORS(base, top) Hue(base, top)
#elif defined(BLENDMODES_MODE_SATURATION)
#   define BLENDMODES_BLEND_COLORS(base, top) Saturation(base, top)
#elif defined(BLENDMODES_MODE_COLOR)
#   define BLENDMODES_BLEND_COLORS(base, top) Color(base, top)
#elif defined(BLENDMODES_MODE_LUMINOSITY)
#   define BLENDMODES_BLEND_COLORS(base, top) Luminosity(base, top)
#else
#   define BLENDMODES_BLEND_COLORS(base, top) top
#endif

#endif // BLEND_MODES_CG_INCLUDED
