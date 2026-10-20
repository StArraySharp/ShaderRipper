// Copyright 2014-2018 Elringus (Artyom Sovetnikov). All Rights Reserved.

#ifndef BLEND_FUNCTIONS_INCLUDED
#define BLEND_FUNCTIONS_INCLUDED

fixed HueToRgb(fixed3 pqt)
{
    if (pqt.z < .0) pqt.z += 1.0;
    if (pqt.z > 1.0) pqt.z -= 1.0;
    if (pqt.z < 1.0 / 6.0) return pqt.x + (pqt.y - pqt.x) * 6.0 * pqt.z;
    if (pqt.z < 1.0 / 2.0) return pqt.y;
    if (pqt.z < 2.0 / 3.0) return pqt.x + (pqt.y - pqt.x) * (2.0 / 3.0 - pqt.z) * 6.0;

    return pqt.x;
}

fixed3 HslToRgb (fixed3 hsl)
{ 
    fixed3 rgb;
    fixed3 pqt;

    if (hsl.y == 0) rgb = hsl.z; 
    else
    {
        pqt.y = hsl.z < .5 ? hsl.z * (1.0 + hsl.y) : hsl.z + hsl.y - hsl.z * hsl.y;
        pqt.x = 2.0 * hsl.z - pqt.y;
        rgb.r = HueToRgb(fixed3(pqt.x, pqt.y, hsl.x + 1.0 / 3.0));
        rgb.g = HueToRgb(fixed3(pqt.x, pqt.y, hsl.x));
        rgb.b = HueToRgb(fixed3(pqt.x, pqt.y, hsl.x - 1.0 / 3.0));
    }

    return rgb;
}

fixed3 RgbToHsl(fixed3 rgb)
{
    fixed maxC = max(rgb.r, max(rgb.g, rgb.b));
    fixed minC = min(rgb.r, min(rgb.g, rgb.b));

    fixed3 hsl;

    hsl = (maxC + minC) / 2.0;

    if (maxC == minC) hsl.x = hsl.y = .0;
    else
    {
        fixed d = maxC - minC;
        hsl.y = (hsl.z > .5) ? d / (2.0 - maxC - minC) : d / (maxC + minC);

        if (rgb.r > rgb.g && rgb.r > rgb.b) 
            hsl.x = (rgb.g - rgb.b) / d + (rgb.g < rgb.b ? 6.0 : .0);
        else if (rgb.g > rgb.b) 
            hsl.x = (rgb.b - rgb.r) / d + 2.0;
        else 
            hsl.x = (rgb.r - rgb.g) / d + 4.0;

        hsl.x /= 6.0f;
    }

    return hsl;
}

fixed G (fixed3 c) { return .299 * c.r + .587 * c.g + .114 * c.b; }
 
fixed3 Darken (fixed3 a, fixed3 b)
{ 
    fixed3 r = min(a, b);
    return r;
}

fixed3 Multiply (fixed3 a, fixed3 b)
{ 
    fixed3 r = a * b;
    return r;
}

fixed3 ColorBurn (fixed3 a, fixed3 b) 
{ 
    fixed3 r = 1.0 - (1.0 - a) / b;
    return saturate(r);
}

fixed3 LinearBurn (fixed3 a, fixed3 b)
{ 
    fixed3 r = a + b - 1.0;
    return r;
}

fixed3 DarkerColor (fixed3 a, fixed3 b) 
{ 
    fixed3 r = G(a) < G(b) ? a : b;
    return r; 
}

fixed3 Lighten (fixed3 a, fixed3 b)
{ 
    fixed3 r = max(a, b);
    return r;
}

fixed3 Screen (fixed3 a, fixed3 b) 
{   
    fixed3 r = 1.0 - (1.0 - a) * (1.0 - b);
    return r;
}

fixed3 ColorDodge (fixed3 a, fixed3 b) 
{ 
    fixed3 r = a / (1.0 - b);
    return saturate(r);
}

fixed3 LinearDodge (fixed3 a, fixed3 b)
{ 
    fixed3 r = a + b;
    return r;
} 

fixed3 LighterColor (fixed3 a, fixed3 b) 
{ 
    fixed3 r = G(a) > G(b) ? a : b;
    return r; 
}

fixed3 Overlay (fixed3 a, fixed3 b) 
{
    fixed3 r = a > .5 ? 1.0 - 2.0 * (1.0 - a) * (1.0 - b) : 2.0 * a * b;
    return r;
}

fixed3 SoftLight (fixed3 a, fixed3 b)
{
    fixed3 r = (1.0 - a) * a * b + a * (1.0 - (1.0 - a) * (1.0 - b));
    return r;
}

fixed3 HardLight (fixed3 a, fixed3 b)
{
    fixed3 r = b > .5 ? 1.0 - (1.0 - a) * (1.0 - 2.0 * (b - .5)) : a * (2.0 * b);
    return r;
}

fixed3 VividLight (fixed3 a, fixed3 b)
{
    fixed3 r = b > .5 ? a / (1.0 - (b - .5) * 2.0) : 1.0 - (1.0 - a) / (b * 2.0);
    return saturate(r);
}

fixed3 LinearLight (fixed3 a, fixed3 b)
{
    fixed3 r = b > .5 ? a + 2.0 * (b - .5) : a + 2.0 * b - 1.0;
    return r;
}

fixed3 PinLight (fixed3 a, fixed3 b)
{
    fixed3 r = b > .5 ? max(a, 2.0 * (b - .5)) : min(a, 2.0 * b);
    return r;
}

fixed3 HardMix (fixed3 a, fixed3 b)
{
    fixed3 r = (b > 1.0 - a) ? 1.0 : .0;
    return r;
}

fixed3 Difference (fixed3 a, fixed3 b) 
{ 
    fixed3 r = abs(a - b);
    return r; 
}

fixed3 Exclusion (fixed3 a, fixed3 b)
{ 
    fixed3 r = a + b - 2.0 * a * b;
    return r; 
}

fixed3 Subtract (fixed3 a, fixed3 b)
{ 
    fixed3 r = a - b;
    return r; 
}

fixed3 Divide (fixed3 a, fixed3 b)
{ 
    fixed3 r = a / b;
    return saturate(r); 
}

fixed3 Hue (fixed3 a, fixed3 b)
{ 
    fixed3 aHsl = RgbToHsl(a.rgb);
    fixed3 bHsl = RgbToHsl(b.rgb);
    fixed3 rHsl = fixed3(bHsl.x, aHsl.y, aHsl.z);

    return HslToRgb(rHsl);
}

fixed3 Saturation (fixed3 a, fixed3 b)
{ 
    fixed3 aHsl = RgbToHsl(a.rgb);
    fixed3 bHsl = RgbToHsl(b.rgb);
    fixed3 rHsl = fixed3(aHsl.x, bHsl.y, aHsl.z);

    return HslToRgb(rHsl);
}

fixed3 Color (fixed3 a, fixed3 b)
{ 
    fixed3 aHsl = RgbToHsl(a.rgb);
    fixed3 bHsl = RgbToHsl(b.rgb);
    fixed3 rHsl = fixed3(bHsl.x, bHsl.y, aHsl.z);

    return HslToRgb(rHsl);
}

fixed3 Luminosity (fixed3 a, fixed3 b)
{ 
    fixed3 aHsl = RgbToHsl(a.rgb);
    fixed3 bHsl = RgbToHsl(b.rgb);
    fixed3 rHsl = fixed3(aHsl.x, aHsl.y, bHsl.z);

    return HslToRgb(rHsl);
}
 
#endif // BLEND_FUNCTIONS_INCLUDED
