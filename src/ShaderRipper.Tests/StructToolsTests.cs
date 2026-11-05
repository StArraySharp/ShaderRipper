using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShaderRipper.Plugins;
using ShaderRipper.Plugins.Hlsl;

namespace ShaderRipper.Tests;

/// <summary>
/// The structural HLSL helpers a plugin backend needs: reading and rewriting struct
/// declarations, patching semantics from a name table, and folding a stage pair into
/// one interface.
///
/// They are pure functions over text, which is what makes them testable here and safe
/// to call concurrently from a translator.
/// </summary>
[TestFixture]
public class StructToolsTests
{
    private const string Vertex = @"struct SPIRV_Cross_Input_vs
{
    float4 vs_11 : POSITION;
    float4 vs_156 : TEXCOORD0;
};

struct SPIRV_Cross_Output_vs
{
    float4 vs_134 : TEXCOORD0;
    float2 vs_TEXCOORD0 : TEXCOORD1;
    float4 gl_Position : SV_Position;
};

SPIRV_Cross_Output_vs vert(SPIRV_Cross_Input_vs stage_input)
{
    return stage_output;
}";

    private const string Fragment = @"struct SPIRV_Cross_Input_ps
{
    float4 pos : SV_Position;
    float4 ps_22 : TEXCOORD0;
    float2 vs_TEXCOORD0 : TEXCOORD1;
};

struct SPIRV_Cross_Output_ps
{
    float4 ps_5 : SV_Target0;
};

SPIRV_Cross_Output_ps frag(SPIRV_Cross_Input_ps stage_input)
{
    return stage_output;
}";

    [Test]
    public void ReadStruct_FindsTheDeclaration()
    {
        string decl = StructTools.ReadStruct(Vertex, "SPIRV_Cross_Output_vs");
        Assert.That(decl, Does.Contain("vs_134 : TEXCOORD0"));
        Assert.That(decl, Does.EndWith("};"));
    }

    [Test]
    public void ReadStruct_DoesNotMatchANamePrefix()
    {
        // `SPIRV_Cross_Output` would be a prefix of `SPIRV_Cross_Output_vs`; a plain
        // IndexOf would find the longer one and return the wrong body.
        Assert.That(StructTools.ReadStruct(Vertex, "SPIRV_Cross_Output"), Is.Null);
    }

    [Test]
    public void Members_ListsEveryDeclaredMember()
    {
        string decl = StructTools.ReadStruct(Fragment, "SPIRV_Cross_Input_ps");
        var members = StructTools.Members(decl);
        Assert.That(members.Count, Is.EqualTo(3));
        Assert.That(members[0].Name, Is.EqualTo("pos"));
        Assert.That(members[0].Semantic, Is.EqualTo("SV_Position"));
        Assert.That(members[1].Name, Is.EqualTo("ps_22"));
    }

    [Test]
    public void IsSystemSemantic_RecognisesPositionAndTargets()
    {
        Assert.That(StructTools.IsSystemSemantic("SV_Position"), Is.True);
        Assert.That(StructTools.IsSystemSemantic("SV_Target0"), Is.True);
        Assert.That(StructTools.IsSystemSemantic("TEXCOORD0"), Is.False);
    }

    [Test]
    public void SemanticOf_MapsTheSourceLanguageStems()
    {
        Assert.That(StructTools.SemanticOf("in_POSITION0"), Is.EqualTo("POSITION"));
        Assert.That(StructTools.SemanticOf("in_COLOR0"), Is.EqualTo("COLOR0"));
        Assert.That(StructTools.SemanticOf("vs_COLOR0"), Is.EqualTo("COLOR0"));
        Assert.That(StructTools.SemanticOf("vs_TEXCOORD1"), Is.EqualTo("TEXCOORD1"));
        Assert.That(StructTools.SemanticOf("weird"), Is.Null);
    }

    [Test]
    public void PatchSemantics_RewritesOnlyTheNamedStructAndSkipsSystemSemantics()
    {
        // The table is the vertex's interpolator list: the colour occupies the first
        // interpolator slot.  `gl_Position` keeps SV_Position and does not consume a
        // table entry -- counting it would shift every following member by one, which
        // is exactly the bug this guards.
        string patched = StructTools.PatchSemantics(Vertex, "SPIRV_Cross_Output_vs",
            new List<string> { "vs_COLOR0", "vs_TEXCOORD0" });

        Assert.That(patched, Does.Contain("vs_134 : COLOR0"));
        Assert.That(patched, Does.Contain("vs_TEXCOORD0 : TEXCOORD0"));
        Assert.That(patched, Does.Contain("gl_Position : SV_Position"));
        // The other struct in the same text is untouched.
        Assert.That(patched, Does.Contain("vs_156 : TEXCOORD0"));
    }

    [Test]
    public void PatchSemantics_StopsAtTheEndOfTheTable()
    {
        // One entry for a struct with two interpolators: the second keeps what it had
        // rather than being given an invented binding.
        string patched = StructTools.PatchSemantics(Vertex, "SPIRV_Cross_Output_vs",
            new List<string> { "vs_COLOR0" });
        Assert.That(patched, Does.Contain("vs_134 : COLOR0"));
        Assert.That(patched, Does.Contain("vs_TEXCOORD0 : TEXCOORD1"));
    }

    [Test]
    public void MergeInterface_FoldsThePairAndRewritesTheFragmentAccesses()
    {
        var merged = StructTools.MergeInterface(Vertex, Fragment,
            "SPIRV_Cross_Output_vs", "SPIRV_Cross_Input_ps");

        Assert.That(merged.Merged, Is.True);
        Assert.That(merged.StructName, Is.EqualTo("v2f"));

        // One declaration carrying the position and the interpolators.
        Assert.That(merged.Vertex, Does.Contain("struct v2f"));
        Assert.That(merged.Vertex, Does.Contain("gl_Position : SV_Position"));
        Assert.That(merged.Vertex, Does.Not.Contain("struct SPIRV_Cross_Output_vs"));
        Assert.That(merged.Vertex, Does.Contain("v2f vert("));

        // The fragment's parameter takes the merged type, and its member name moves to
        // the vertex's spelling.
        Assert.That(merged.Fragment, Does.Contain("frag(v2f stage_input)"));
        Assert.That(merged.Fragment, Does.Not.Contain("struct SPIRV_Cross_Input_ps"));
    }

    [Test]
    public void MergeInterface_DeclinesWhenTheSidesDescribeDifferentInterpolators()
    {
        // Same member count, different semantics: the pair is not one interface, and a
        // merge would silently bind the wrong stream.
        const string other = @"struct SPIRV_Cross_Input_ps
{
    float4 pos : SV_Position;
    float4 ps_22 : COLOR0;
    float2 vs_TEXCOORD0 : TEXCOORD2;
};";
        var merged = StructTools.MergeInterface(Vertex, other,
            "SPIRV_Cross_Output_vs", "SPIRV_Cross_Input_ps");
        Assert.That(merged.Merged, Is.False);
        Assert.That(merged.Vertex, Is.Null);
    }

    [Test]
    public void MergeInterface_DeclinesWhenAStructIsAbsent()
    {
        var merged = StructTools.MergeInterface(Vertex, "float4 frag() : SV_Target { return 0; }",
            "SPIRV_Cross_Output_vs", "SPIRV_Cross_Input_ps");
        Assert.That(merged.Merged, Is.False);
    }
}
