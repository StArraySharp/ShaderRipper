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
