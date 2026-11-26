using System.Text.RegularExpressions;
using NUnit.Framework;
using ShaderRipper.Ast;
using ShaderRipper.Emit;

namespace ShaderRipper.Tests;

/// <summary>
/// Two ways a per-component value can be collapsed into one: a GLSL fetch whose
/// coordinate swizzle is rewritten to .xy, and a DXBC movc whose condition is read
/// as a scalar.  Both forms still compile, so only the emitted text catches them
/// before a shader quietly samples the wrong texel or picks the wrong branch.
/// </summary>
[TestFixture]
public class SampleSelectionTests
{
    private const string Glsl = """
#ifdef VERTEX
void main()
{
}
#endif
#ifdef FRAGMENT
uniform sampler2D _MainTex;
void main()
{
    vec4 uv = vec4(0.0);
    vec4 a = texture(_MainTex, uv.xy);
    vec4 b = texture(_MainTex, uv.zw);
    vec4 c = texture(_MainTex, uv.xyzw);
    vec4 d = texture(_MainTex, uv);
}
#endif
""";

    [Test]
    public void AGlslFetchKeepsTheSwizzleThatPicksItsPosition()
    {
        var r = GlslTranslator.Translate(Glsl);

        Assert.That(r.ParseErrors, Is.Zero);
        // HLSLcc packs a pair of UVs into one vec4 and fetches them separately.
        // Rewriting uv.zw to uv.xy makes both fetches read the same texel.
        Assert.That(r.Hlsl, Does.Contain("_MainTex.Sample(sampler__MainTex, (uv).zw)"));
        Assert.That(Regex.Matches(r.Hlsl, @"\(uv\)\.zw").Count, Is.EqualTo(1));
        // A vector-wide coordinate is still narrowed to what Texture2D.Sample takes.
        Assert.That(Regex.Matches(r.Hlsl, @"\(uv\)\.xy").Count, Is.EqualTo(3));
    }

    [Test]
    public void GlslTextureQueriesAndFetchesKeepMipSliceAndOffsets()
    {
        var r = GlslTranslator.Translate("""
#ifdef FRAGMENT
uniform sampler2DArray slices;
uniform samplerBuffer records;
void main()
{
    ivec3 coord = ivec3(1, 2, 3);
    ivec2 offset = ivec2(1, -1);
    int lod = 2;
    ivec3 dims = textureSize(slices, lod);
    int count = textureSize(records);
    vec4 a = texelFetchOffset(slices, coord, lod, offset);
    vec4 b = texelFetch(records, count);
}
#endif
""");
        Assert.That(r.ParseErrors, Is.Zero);
        Assert.That(r.Hlsl, Does.Contain("int3 _sri_textureSize_sampler2DArray(Texture2DArray tex, int lod)")
            .And.Contain("tex.GetDimensions(lod, width, height, depth, levels);")
            .And.Contain("_sri_textureSize_sampler2DArray(slices, lod)")
            .And.Contain("int _sri_textureSize_samplerBuffer(Buffer<float4> tex)")
            .And.Contain("slices.Load(int4(coord, lod), offset)")
            .And.Contain("records.Load(count)")
            .And.Contain("return o0;"));
    }

    [Test]
    public void GlslOffsetAndGradientSamplingKeepsHlslArgumentOrder()
    {
        var r = GlslTranslator.Translate("""
#ifdef FRAGMENT
uniform sampler2D tex;
void main()
{
    vec2 uv = vec2(0.0);
    ivec2 offset = ivec2(1, -1);
    vec4 a = texture2D(tex, uv);
    vec4 b = textureOffset(tex, uv, offset, 0.5);
    vec4 c = textureGradOffset(tex, uv, dFdx(uv), dFdy(uv), offset);
}
#endif
""");
        Assert.That(r.ParseErrors, Is.Zero);
        Assert.That(r.Hlsl, Does.Contain("tex.Sample(sampler_tex, (uv).xy)")
            .And.Contain("tex.SampleBias(sampler_tex, (uv).xy, 0.5, offset)")
            .And.Contain("tex.SampleGrad(sampler_tex, (uv).xy, ddx(uv), ddy(uv), offset)"));
    }

    [Test]
    public void GlslModuloUsesFloorAndAcceptsScalarAndVectorDivisors()
    {
        var r = GlslTranslator.Translate("""
#ifdef FRAGMENT
void main()
{
    vec2 p = vec2(-1.5, 1.5);
    vec2 a = mod(p, 1.0);
    vec2 b = mod(p, vec2(1.0, -1.0));
}
#endif
""");
        Assert.That(r.ParseErrors, Is.Zero);
        Assert.That(r.Hlsl, Does.Contain("float2 _sri_mod(float2 x, float y)")
            .And.Contain("float2 _sri_mod(float2 x, float2 y)")
            .And.Contain("return x - y * floor(x / y);")
            .And.Contain("_sri_mod(p, 1.0)")
            .And.Contain("_sri_mod(p, float2(1.0, -1.0))"));
    }

    [Test]
    public void GlslBitFieldsAndBooleanMixPreserveTypesAndValueSelection()
    {
        var r = GlslTranslator.Translate("""
#ifdef FRAGMENT
out vec4 SV_Target0;
void main()
{
    ivec2 signedField = bitfieldExtract(ivec2(15, 7), 1, 3);
    uvec2 wholeWord = bitfieldInsert(uvec2(0u), uvec2(9u, 8u), 0, 32);
    vec2 selected = mix(vec2(signedField), vec2(wholeWord), bvec2(false, true));
    SV_Target0 = vec4(selected, mix(0.0, 2.0, 0.25), 1.0);
}
#endif
""");
        Assert.That(r.ParseErrors, Is.Zero);
        Assert.That(r.Hlsl, Does.Contain("int2 _sri_bitfieldExtract(int2 value, int offset, int bits)")
            .And.Contain("asint(asuint(value) << ((32 - offset - bits) & 31)) >> ((32 - bits) & 31)")
            .And.Contain("uint2 _sri_bitfieldInsert(uint2 value, uint2 insert, int offset, int bits)")
            .And.Contain("if (bits == 0) return value;")
            .And.Contain("float2 _sri_mix(float2 x, float2 y, bool2 a) { return a ? y : x; }")
            .And.Contain("_sri_mix(0.0, 2.0, 0.25)"));
    }

    [Test]
    public void GlslMultisampleFetchUsesTheSampleIndexAndArrayLayer()
    {
        var r = GlslTranslator.Translate("""
#ifdef FRAGMENT
uniform sampler2DMS colorSamples;
uniform sampler2DMSArray slices;
out vec4 SV_Target0;
void main()
{
    ivec2 size = textureSize(colorSamples);
    int count = textureSamples(slices);
    vec4 a = texelFetch(colorSamples, size - ivec2(1), gl_SampleID);
    vec4 b = texelFetch(slices, ivec3(1, 2, 3), count - 1);
    SV_Target0 = a + b;
}
#endif
""");
        Assert.That(r.ParseErrors, Is.Zero);
        Assert.That(r.Hlsl, Does.Contain("Texture2DMS<float4> colorSamples;")
            .And.Contain("Texture2DMSArray<float4> slices;")
            .And.Contain("uint sv_sample_index : SV_SampleIndex")
            .And.Contain("colorSamples.Load(")
            .And.Contain(", gl_SampleID)")
            .And.Contain("slices.Load(int3(1, 2, 3), ")
            .And.Contain("_sri_textureSize_sampler2DMS(colorSamples)")
            .And.Contain("_sri_textureSamples_sampler2DMSArray(slices)")
            .And.Contain("tex.GetDimensions(width, height, depth, samples);")
            .And.Not.Contain("SamplerState sampler_colorSamples"));
    }

    [Test]
    public void DxbcTextureReadsPreserveMipResultSwizzlesAndArrayGradients()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2darray (float,float,float,float) t1
dcl_resource_texture2dms(4) (float,float,float,float) t2
dcl_sampler s0, mode_default
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
sample_b r0.yw, v0.zwzw, t0.wzyx, s0, l(1.0)
ld r0.xz, l(11, 19, 99, 3), t0.wzyx
sample_d r1.zw, v0.xyzw, t1.yxwz, s0, v0.wyxx, v0.zxxx
ldms r1.yw, l(6, 7, 0, 0), t2.yzwx, l(2)
gather4 r1.xz, v0.xyxy, t0.zwxy, s0.y
add o0, r0, r1
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain(".SampleBias(sampler_t0, (v0).zw, 1.0f).zx")
            .And.Contain(".Load(int3(11, 19, 3)).wy")
            .And.Contain(".SampleGrad(sampler_t0, (v0).xyz, (v0).wy, (v0).zx).wz")
            .And.Contain(".Load(int2(6, 7), 2).zx")
            .And.Contain(".GatherGreen(sampler_t0, (v0).xy).zx"));
    }

    [Test]
    public void DxbcTextureSizeUsesTheResourceShapeAndReturnMode()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2darray (float,float,float,float) t1
dcl_resource_texture2dmsarray(4) (float,float,float,float) t2
dcl_output o0.xyzw
dcl_temps 2
resinfo_indexable(texture2d)(float,float,float,float)_uint r0.yw, l(3), t0.wxyz
resinfo_rcpFloat r1.xyzw, l(2), t1.xyzw
resinfo r0.xz, l(0), t2.xzyw
sampleinfo_uint r1.yw, t2.wxxw
add o0, r0, r1
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain("t0.GetDimensions(3u, _ri0w, _ri0h, _ri0n);")
            .And.Contain("asfloat(uint2(_ri0w, _ri0d))")
            .And.Contain("t1.GetDimensions(2u, _ri1w, _ri1h, _ri1d, _ri1n);")
            .And.Contain("float4(1.0 / float(_ri1w), 1.0 / float(_ri1h), float(_ri1d), float(_ri1n))")
            .And.Contain("t2.GetDimensions(_ri2w, _ri2h, _ri2d, _ri2samples);")
            .And.Contain("float2(float(_ri2w), float(_ri2h))")
            .And.Contain("t2.GetDimensions(_si3w, _si3h, _si3d, _si3n);")
            .And.Contain("asfloat(uint2(_si3n, 0u))"));
    }

    [Test]
    public void DxbcArrayTypesPreserveSliceCoordinatesOutsideTheirGradients()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_resource_texture1darray (float,float,float,float) t0
dcl_resource_texturecubearray (float,float,float,float) t1
dcl_sampler s0, mode_default
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
sample_d r0.xyzw, v0.xyzw, t0.xyzw, s0, v0.yxxx, v0.zxxx
sample_d r1.xyzw, v0.xyzw, t1.xyzw, s0, v0.wyzx, v0.zxyw
ld r0.y, l(5, 6, 99, 2), t0.xyzw
add o0, r0, r1
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain("Texture1DArray t0")
            .And.Contain("TextureCubeArray t1")
            .And.Contain("t0.SampleGrad(sampler_t0, (v0).xy, (v0).y, (v0).z)")
            .And.Contain("t1.SampleGrad(sampler_t0, (v0).xyzw, (v0).wyz, (v0).zxy)")
            .And.Contain("t0.Load(int3(5, 6, 2)).y"));
    }

    private const string MovcAsm = """
ps_5_0
dcl_globalFlags refactoringAllowed
dcl_temps 4
dcl_output o0.xyzw
movc r0.xyzw, r1.xyzw, r2.xyzw, r3.xyzw
movc r0.xy, r1.xxxx, r2.zwzw, r3.xyxy
mov o0, r0
ret
""";

    [Test]
    public void MovcSelectsWithTheWholeConditionVector()
    {
        string hlsl = HlslEmitter.Emit(Module.Read(MovcAsm)).Hlsl;

        Assert.That(hlsl, Does.Contain("r0.xyzw = r1.xyzw ? r2.xyzw : r3.xyzw"));
        // A scalar condition would apply lane x to every component of the select.
        Assert.That(hlsl, Does.Not.Contain("r1.x ?"));
    }

    [Test]
    public void MaskedUnaryAndConversionsReadTheWrittenLanes()
    {
        var hlsl = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
mov r0, v0
frc r0.yz, |r0.yyzy|
ftoi r1.yz, r0.yyzy
ftou r1.xw, r0.xyzz
mov o0, r1
ret
""")).Hlsl;

        Assert.That(hlsl, Does.Contain("frac(abs(r0.yz))")
            .And.Contain("int2(r0.yz)")
            .And.Contain("uint2(r0.xz)"));
    }

    [Test]
    public void ReciprocalAndDerivativeKeepDistinctSourceLanes()
    {
        var hlsl = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 1
rcp r0.yw, v0.xyzw
deriv_rty_fine r0.xz, v0.wzxy
mov o0, r0
ret
""")).Hlsl;

        Assert.That(hlsl, Does.Contain("1.0 / v0.yw")
            .And.Contain("ddy_fine(v0.wx)"));
    }

    [Test]
    public void UnsignedDivisionComputesBothMaskedOutputsBeforeOverwritingSources()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
ftou r0, v0
udiv r0.yw, r1.xz, r0.wzyx, l(3, 0, 5, 7)
udiv null, r0.xz, r1.xyzw, l(2, 2, 2, 2)
utof o0, r0
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain("_divisor0 = uint2(0u, 7u)")
            .And.Contain("_divisor1 = uint2(3u, 5u)")
            .And.Contain("(asuint(i0.zx)) / max(_divisor0, 1u)")
            .And.Contain("(asuint(i0.wy)) % max(_divisor1, 1u)")
            .And.Contain("i0.xz = _divResult1;"));
        Assert.That(r.Hlsl.IndexOf("uint2 _divResult1", StringComparison.Ordinal),
            Is.LessThan(r.Hlsl.IndexOf("i0.yw = _divResult0", StringComparison.Ordinal)));
    }

    [Test]
    public void ExtendedMultiplyKeepsBothWordsAndDistinguishesSignedHighBits()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
ftoi r0, v0
imul r0.x, r0.y, r0.yyyy, l(-7)
umul null, r0.zw, r0.xyxy, l(1, 2, 3, 4)
itof o0, r0
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain("_mulHigh -= (asint(_mulL) < 0 ? _mulR : 0u)")
            .And.Contain("i0.x = _mulHigh;")
            .And.Contain("i0.y = _mulLow;")
            .And.Contain("uint2(3u, 4u)")
            .And.Contain("i0.zw = _mulLow;"));
    }

    [Test]
    public void BitFieldsKeepMaskedLanesAndInsertSourceBitsIntoTheBase()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
ftou r0, v0
bfi r1.yw, l(2, 34, 2, 8), r0.xyzw, l(31, 12, 0, 1), l(11, 27, 0, 42)
ubfe r1.x, l(3), l(35), r1.yyyy
ibfe r1.z, l(7), l(30), r1.wwww
utof o0, r1
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain("uint2 _bitWidth = uint2(34u, 8u) & 31u;")
            .And.Contain("uint2 _bitOffset = asuint(i0.yw) & 31u;")
            .And.Contain("((uint2(12u, 1u)) << _bitOffset) & _bitMask")
            .And.Contain("(uint2(27u, 42u)) & ~_bitMask")
            .And.Contain("_bitOffset = 35u & 31u;")
            .And.Contain("_bitLength = min(_bitWidth, 32u - _bitOffset);")
            .And.Contain("asint((asuint(i1.w)) << _bitLeft) >> _bitRight"));
    }

    [Test]
    public void IntegerComparisonsAndShiftsPreserveSignednessAndFullWidthLiterals()
    {
        var r = HlslEmitter.Emit(Module.Read("""
ps_5_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 2
ftoi r0, v0
ult r1, r0, l(0, 1, 2147483648, 4294967295)
ishr r0.yw, r0.xyzw, l(33)
movc o0, r1, r0, v0
ret
"""));

        Assert.That(r.Missing, Is.Empty);
        Assert.That(r.Hlsl, Does.Contain("asuint(i0.xyzw) < uint4(0u, 1u, 2147483648u, 4294967295u)")
            .And.Contain("i0.yw >> (33u & 31u)"));
    }
}
