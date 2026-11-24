using NUnit.Framework;
using ShaderRipper.Ast;
using ShaderRipper.Emit;
using ShaderRipper.Resolve;

namespace ShaderRipper.Tests;

/// <summary>
/// One program is two independently translated stages, so the constant buffers
/// they declare have to survive the merge together.  A stage whose block is not
/// read at all loses its declaration while its body keeps naming the members --
/// the output then references identifiers nothing declares.
/// </summary>
[TestFixture]
public class ShaderLabMergeConstantBuffers
{
    private static string Merged(string vs, string ps)
    {
        var merged = ShaderLab.Merge(
            new ShaderLab.Stage { Entry = "vert", Hlsl = vs },
            new ShaderLab.Stage { Entry = "frag", Hlsl = ps });
        Assert.That(merged, Is.Not.Null);
        return string.Join("\n", merged);
    }

    [Test]
    public void RecordedBufferBindingDisplacesAnInferredBindingAtAnotherSlot()
    {
        var module = Module.Read("""
ps_5_0
dcl_constantbuffer CB1[8], immediateIndexed
dcl_constantbuffer CB3[8], immediateIndexed
dcl_output o0.xyzw
add o0, cb1[5].xyzw, cb3[5].xyzw
ret
""");
        module.Cb = CbResolver.Resolve(module, new[] { "UnityPerCamera", "_ProjectionParams" }, null,
            assetBindings: new Dictionary<int, string> { [1] = "UnityPerCamera" });

        var program = HlslEmitter.Emit(module, standalone: true);
        Assert.That(program.Hlsl, Does.Contain("cbuffer UnityPerCamera : register(b1)")
            .And.Contain("cbuffer CB3 : register(b3)")
            .And.Contain("_ProjectionParams.xyzw + _cb3_row5.xyzw"));
        Assert.That(program.Reflection.Buffers.Single(cb => cb.Register == 3).Members,
            Has.None.Matches<ShaderRipper.Data.DxbcReflection.Member>(member => member.Name == "_ProjectionParams"));
    }

    [Test]
    public void StandaloneProgramKeepsReadsFromRepeatedMemberNamesInTwoBuffers()
    {
        var module = Module.Read("""
ps_5_0
dcl_constantbuffer CB0[4], immediateIndexed
dcl_constantbuffer CB1[2], immediateIndexed
dcl_output o0.xyzw
add o0, cb0[3].xyzw, cb1[1].xyzw
ret
""");
        module.Cb = CbResolver.Resolve(module, Array.Empty<string>(), null);
        module.Cb.CbName[0] = "$Globals";
        module.Cb.CbName[1] = "$Globals";
        module.Cb.RowExpr[(0, 3)] = "_Tint";
        module.Cb.RowExpr[(1, 1)] = "_Tint";

        var program = HlslEmitter.Emit(module, standalone: true);
        Assert.That(program.Hlsl, Does.Contain("float4 _Tint : packoffset(c3.x);")
            .And.Contain("float4 _Tint_cb1 : packoffset(c1.x);")
            .And.Contain("_Tint.xyzw + _Tint_cb1.xyzw"));
        Assert.That(program.Reflection.Buffers.Single(cb => cb.Register == 1).Members,
            Has.Some.Matches<ShaderRipper.Data.DxbcReflection.Member>(member =>
                member.Name == "_Tint_cb1" && member.Offset == 16));
    }

    [Test]
    public void ArrayRegistersAreRemappedUsingTheOriginalSlotOfEachStage()
    {
        const string vs = """
cbuffer Material : register(b4)
{
    float4 cb4[3];
};
float4 vert() : SV_Position { return cb4[0]; }
""";
        const string ps = """
cbuffer Other : register(b4)
{
    float4 cb4[3];
};
cbuffer Material : register(b5)
{
    float4 cb5[3];
};
float4 frag() : SV_Target { return cb4[1] + cb5[2]; }
""";
        Assert.That(Merged(vs, ps), Does.Contain("return cb5[1] + cb4[2];"));
    }

    [Test]
    public void ArrayBackedPackedMembersKeepTheirLanesAndTypes()
    {
        const string vs = """
cbuffer Material : register(b4)
{
    float4 cb4[3];
};
float4 vert() : SV_Position { return cb4[0]; }
""";
        const string ps = """
cbuffer Material : register(b5)
{
    float ScaleX : packoffset(c2.x);
    uint ScaleY : packoffset(c2.y);
    float2 Offset : packoffset(c2.z);
};
float4 frag() : SV_Target { return float4(float2(ScaleX, ScaleY) + Offset.gr, Offset); }
""";
        Assert.That(Merged(vs, ps),
            Does.Contain("float4(float2(cb4[2].x, asuint(cb4[2].y)) + cb4[2].wz, cb4[2].zw)"));
    }

    [Test]
    public void ArrayBackedMatricesKeepTheirWholeValueAndIndividualRegisters()
    {
        const string vs = """
cbuffer Material : register(b4)
{
    float4 cb4[5];
};
float4 vert() : SV_Position { return cb4[0]; }
""";
        const string ps = """
cbuffer Material : register(b5)
{
    float4x4 Projector : packoffset(c4.x);
};
float4 frag() : SV_Target { return mul(Projector, float4(1, 2, 3, 0)) + Projector[2]; }
""";
        Assert.That(Merged(vs, ps), Does.Contain("float4 cb4[8];")
            .And.Contain("mul(transpose(float4x4(cb4[4], cb4[5], cb4[6], cb4[7])), float4(1, 2, 3, 0)) + cb4[6]"));
    }

    [Test]
    public void DifferentResourceTypesAtOneRegisterKeepTheirOwnSamplerAssociation()
    {
        const string vs = """
ByteAddressBuffer t0 : register(t0);
float4 vert() : SV_Position { return asfloat(t0.Load4(0)); }
""";
        const string ps = """
Texture3D t0 : register(t0);
SamplerState sampler_t0 : register(s0);
float4 frag() : SV_Target { return t0.Sample(sampler_t0, float3(0, 0, 0)); }
""";
        Assert.That(Merged(vs, ps), Does.Contain("ByteAddressBuffer t0;")
            .And.Contain("Texture3D t0_frag;")
            .And.Contain("t0_frag.Sample(sampler_t0_frag"));
    }

    [Test]
    public void ProbeVolumeFallbackIsGuardedWhenTheUnityHeaderIsIncluded()
    {
        const string vs = """
float4 vert(float4 p : POSITION) : SV_Position { return UnityObjectToClipPos(p); }
""";
        const string ps = """
cbuffer UnityProbeVolume : register(b1)
{
    float4 unity_ProbeVolumeParams : packoffset(c0.x);
};
Texture3D unity_ProbeVolumeSH : register(t0);
SamplerState sampler_unity_ProbeVolumeSH : register(s0);
float4 frag() : SV_Target { return unity_ProbeVolumeSH.Sample(sampler_unity_ProbeVolumeSH, unity_ProbeVolumeParams.xyz); }
""";
        string text = Merged(vs, ps);
        Assert.That(text, Does.StartWith("#include \"UnityCG.cginc\"")
            .And.Contain("#if !UNITY_LIGHT_PROBE_PROXY_VOLUME\ncbuffer UnityProbeVolume")
            .And.Contain("#define sampler_unity_ProbeVolumeSH samplerunity_ProbeVolumeSH"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UnityCgSuppliesLightmapHdrAndTheShadowMatrixColumns(bool explicitInclude)
    {
        const string vs = """
cbuffer VGlobals : register(b1)
{
    float4 unity_WorldToShadow_2_col1 : packoffset(c0.x);
};
float4 vert() : SV_Position { return unity_WorldToShadow_2_col1; }
""";
        const string ps = """
float4 vertex;
cbuffer PGlobals : register(b0)
{
    float4 unity_DynamicLightmap_HDR : packoffset(c0.x);
};
float4 frag() : SV_Target { return unity_DynamicLightmap_HDR; }
""";

        string text = Merged(explicitInclude ? "#include \"UnityCG.cginc\"\n" + vs : vs, ps);
        Assert.That(text, Does.Contain("#include \"UnityCG.cginc\"")
            .And.Contain("unity_WorldToShadow[2]._m01_m11_m21_m31")
            .And.Contain("return unity_DynamicLightmap_HDR;")
            .And.Not.Contain("float4 unity_DynamicLightmap_HDR")
            .And.Not.Contain("unity_WorldToShadow_2_col1"));
    }

    [Test]
    public void UnityCgSuppliesStandaloneLightingGlobals()
    {
        const string vs = """
float4x4 unity_WorldToShadow[4];
float4 vert() : SV_Position { return unity_WorldToShadow[1][0]; }
""";
        const string ps = """
uniform half4 unity_DynamicLightmap_HDR;
float4 frag() : SV_Target { return unity_DynamicLightmap_HDR; }
""";

        Assert.That(Merged(vs, ps), Does.Contain("#include \"UnityCG.cginc\"")
            .And.Contain("return unity_WorldToShadow[1][0];")
            .And.Contain("return unity_DynamicLightmap_HDR;")
            .And.Not.Contain("float4x4 unity_WorldToShadow")
            .And.Not.Contain("half4 unity_DynamicLightmap_HDR"));
    }

    [Test]
    public void APackoffsetWithoutALaneIsStillAMember()
    {
        // The lane is optional in HLSL and defaults to x.  A translator that lays
        // members out by row alone writes the short form; refusing it here dropped
        // every member, and a block left empty is not declared at all.
        const string vs = """
cbuffer Vs0 : register(b0)
{
    float4 _10_m0 : packoffset(c0);
};

float4 vert() : SV_Position { return _10_m0; }
""";
        const string ps = """
cbuffer Ps0 : register(b1)
{
    float4 _20_m0 : packoffset(c0);
    float _20_m1 : packoffset(c1.y);
};

float4 frag() : SV_Target { return _20_m0 + _20_m1; }
""";

        string text = Merged(vs, ps);
        Assert.That(text, Does.Contain("cbuffer Vs0"));
        Assert.That(text, Does.Contain("cbuffer Ps0"));
        Assert.That(text, Does.Contain("float4 _10_m0 : packoffset(c0.x);"));
        Assert.That(text, Does.Contain("float _20_m1 : packoffset(c1.y);"));
    }

    [Test]
    public void BothStagesDeclareTheirOwnBlocksEvenAtTheSameRegister()
    {
        // `b0` is a per-stage number: the vertex stage's globals and the fragment
        // stage's material block routinely share it and mean different things, so
        // the merge must keep two declared blocks rather than one.
        const string vs = """
cbuffer VGlobals : register(b0)
{
    float4 _5_m0 : packoffset(c0);
};

float4 vert() : SV_Position { return _5_m0; }
""";
        const string ps = """
cbuffer PGlobals : register(b0)
{
    float4 _9_m0 : packoffset(c0);
};

float4 frag() : SV_Target { return _9_m0; }
""";

        string text = Merged(vs, ps);
        Assert.That(text, Does.Contain("_5_m0 : packoffset"));
        Assert.That(text, Does.Contain("_9_m0 : packoffset"));
        Assert.That(CountOf(text, "float4 _5_m0"), Is.EqualTo(1));
        Assert.That(CountOf(text, "float4 _9_m0"), Is.EqualTo(1));
    }

    [Test]
    public void AMemberNamedInTwoBlocksKeepsOnlyOneDeclaration()
    {
        // Unity records a shader's property in whichever block the program that reads
        // it puts it in, and the two stages of a pass do not have to agree: the vertex
        // module reads `_Color` from its globals while the fragment module reads a
        // different `_Color` from the material block.  Both names survive the payload
        // scan, but HLSL has one namespace, so declaring both is "redefinition of
        // '_Color'" and the whole program is rejected.
        //
        // A name the Properties block lists stays a real member -- it is what Unity
        // uploads the material's value into -- so the demoted one is the other stage's.
        ShaderLab.SetPropertyNames(new[]
        {
            new Data.Property { Name = "_Color" },
            new Data.Property { Name = "_Tint" },
        });

        const string vs = """
cbuffer VGlobals : register(b0)
{
    float4 _Color : packoffset(c9);
};

float4 vert() : SV_Position { return _Color; }
""";
        const string ps = """
cbuffer PGlobals : register(b1)
{
    float4 _Color : packoffset(c2);
    float4 _Tint : packoffset(c3);
};

float4 frag() : SV_Target { return _Color + _Tint; }
""";

        string text = Merged(vs, ps);
        Assert.That(CountOf(text, "float4 _Color "), Is.EqualTo(1));
        Assert.That(text, Does.Contain("float4 _Tint : packoffset(c3.x);"));
        // A MATERIAL PROPERTY has no suffixed fallback: Unity fills values by
        // property name, so a copy renamed `_Color_2` would read zero forever (under
        // premultiplied-alpha blending that is a fully transparent material, with no
        // compile error).  The duplicate is dropped and the bodies that spelled it
        // read the surviving declaration instead.
        Assert.That(text, Does.Not.Contain("_Color_2"));
        Assert.That(text, Does.Contain("return _Color;"));
    }

    private static int CountOf(string text, string needle)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0)
        {
            n++;
            at += needle.Length;
        }
        return n;
    }
}
