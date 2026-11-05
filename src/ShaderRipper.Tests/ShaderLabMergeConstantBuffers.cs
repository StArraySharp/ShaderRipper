using NUnit.Framework;
using ShaderRipper.Emit;

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
