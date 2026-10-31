using NUnit.Framework;
using ShaderRipper.Data;
using ShaderRipper.Emit;

namespace ShaderRipper.Tests;

/// <summary>
/// The ShaderLab well-formedness check is what decides whether a translated
/// shader is written at all, so it has to reject the shapes Unity would reject
/// and accept the ones the emitter produces.
/// </summary>
[TestFixture]
public class ShaderLabCheckTests
{
    private static Property Prop(string name, int type = 0, int dim = 0,
                                 string texture = "", float[] value = null, int flags = 0) =>
        new()
        {
            Name = name,
            Type = type,
            DefTextureDim = dim,
            DefTextureName = texture,
            DefValue = value ?? new float[4],
            Flags = flags,
        };
    [Test]
    public void AMinimalShaderIsAccepted()
    {
        const string text = """
Shader "Test/Simple"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert() : SV_Position { return 0; }
            float4 frag() : SV_Target { return 1; }
            ENDHLSL
        }
    }
}
""";
        Assert.That(ShaderLab.Check(text), Is.Empty);
    }

    [Test]
    public void AMissingClosingBraceIsRejected()
    {
        const string text = """
Shader "Test/Broken"
{
    SubShader
    {
        Pass
        {
        }
""";
        Assert.That(ShaderLab.Check(text), Is.Not.Empty);
    }

    [Test]
    public void AnEmptyFileIsRejected()
    {
        Assert.That(ShaderLab.Check(""), Is.Not.Empty);
    }

    [Test]
    public void AMissingShaderNameIsRejected()
    {
        Assert.That(ShaderLab.Check("SubShader { Pass { } }"), Is.Not.Empty);
    }

    /// <summary>
    /// The emitter marks its own output in the header comment, and the export's
    /// "am I overwriting someone else's file" check keys off that marker.
    /// </summary>
    [Test]
    public void WrapWritesTheShaderNameAndFallback()
    {
        var text = ShaderLab.Wrap("Test/Name", "Diffuse",
            new List<ShaderLab.SubShaderText>(), new List<Property>());

        Assert.That(text, Does.Contain("Shader \"Test/Name\""));
        Assert.That(text, Does.Contain("Fallback \"Diffuse\""));
        Assert.That(ShaderLab.Check(text), Is.Empty);
    }

    [Test]
    public void WrapOmitsAnEmptyFallbackRatherThanWritingAnEmptyString()
    {
        var text = ShaderLab.Wrap("Test/Name", "", new List<ShaderLab.SubShaderText>(),
            new List<Property>());

        Assert.That(text, Does.Not.Contain("Fallback \"\""));
        Assert.That(ShaderLab.Check(text), Is.Empty);
    }

    [Test]
    public void WrapEmitsCustomEditorWhenTheAssetNamesOne()
    {
        var text = ShaderLab.Wrap("Test/Name", "", new List<ShaderLab.SubShaderText>(),
            new List<Property>(), "Test.Editor");

        Assert.That(text, Does.Contain("CustomEditor \"Test.Editor\""));
    }

    /// <summary>A quote inside the editor name would end the string early.</summary>
    [Test]
    public void WrapSanitisesAQuoteInTheCustomEditorName()
    {
        var text = ShaderLab.Wrap("Test/Name", "", new List<ShaderLab.SubShaderText>(),
            new List<Property>(), "Evil\"Name");

        Assert.That(text, Does.Contain("CustomEditor \"Evil_Name\""));
        Assert.That(ShaderLab.Check(text), Is.Empty);
    }

    [Test]
    public void WrapKeepsPropertyAttributesAndTypes()
    {
        var props = new List<Property>
        {
            Prop("_MainTex", type: 4, dim: 2, texture: "white", flags: 0x02 | 0x04),
            Prop("_Color", type: 0, value: new[] { 1f, 0.5f, 0f, 1f }),
            Prop("_Amount", type: 2, value: new[] { 0.25f, 0f, 0f, 0f }),
        };
        props[0].Description = "Albedo";
        props[1].Description = "Tint";

        var text = ShaderLab.Wrap("Test/Props", "", new List<ShaderLab.SubShaderText>(), props);

        Assert.That(text, Does.Contain("_MainTex (\"Albedo\", 2D) = \"white\" {}"));
        Assert.That(text, Does.Contain("[PerRendererData]"));
        Assert.That(text, Does.Contain("[NoScaleOffset]"));
        Assert.That(text, Does.Contain("_Color (\"Tint\", Color) = (1, 0.5, 0, 1)"));
        Assert.That(text, Does.Contain("_Amount (\"_Amount\""));
        Assert.That(ShaderLab.Check(text), Is.Empty);
    }

    [Test]
    public void CubeAndArrayTexturePropertiesUseTheirOwnKeyword()
    {
        var props = new List<Property>
        {
            Prop("_Cube", type: 4, dim: 4),
            Prop("_Array", type: 4, dim: 5),
        };

        var text = ShaderLab.Wrap("Test/Dims", "", new List<ShaderLab.SubShaderText>(), props);

        Assert.That(text, Does.Contain("_Cube (\"_Cube\", Cube)"));
        Assert.That(text, Does.Contain("_Array (\"_Array\", 2DArray)"));
        Assert.That(ShaderLab.Check(text), Is.Empty);
    }

    /// <summary>
    /// A 1D property would fail Unity's import outright -- ShaderLab has no such
    /// type -- so the dimension table has to fold it into 2D.
    /// </summary>
    [Test]
    public void AnUnknownTextureDimensionFallsBackTo2D()
    {
        var props = new List<Property>
        {
            Prop("_Odd", type: 4, dim: 0),
            Prop("_Any", type: 4, dim: 1),
        };

        var text = ShaderLab.Wrap("Test/Odd", "", new List<ShaderLab.SubShaderText>(), props);

        Assert.That(text, Does.Contain("_Odd (\"_Odd\", 2D)"));
        Assert.That(text, Does.Contain("_Any (\"_Any\", 2D)"));
        Assert.That(text, Does.Not.Contain("1D"));
    }
}

/// <summary>
/// The matrix-chain folder turns the instruction-level spelling of a matrix
/// product into the one <c>mul</c> (or the Unity macro) that means the same
/// thing.  It is text in, text out, so each shape can be asserted directly.
/// </summary>
[TestFixture]
public class MatrixChainTests
{
    [Test]
    public void ThreeDotsOverWorldToObjectBecomeTheMacro()
    {
        var lines = new List<string>
        {
            "u_xlat0.x = dot(v.xyz, unity_WorldToObject[0].xyz);",
            "u_xlat0.y = dot(v.xyz, unity_WorldToObject[1].xyz);",
            "u_xlat0.z = dot(v.xyz, unity_WorldToObject[2].xyz);",
        };

        var folded = MatrixChain.Fold(lines, macros: true);
        string text = string.Join('\n', folded);

        Assert.That(text, Does.Contain("UnityObjectToWorldNormal"));
        Assert.That(text, Does.Not.Contain("dot("));
    }

    [Test]
    public void TheMacroIsNotWrittenWithoutIncludesAvailable()
    {
        var lines = new List<string>
        {
            "u_xlat0.x = dot(v.xyz, unity_WorldToObject[0].xyz);",
            "u_xlat0.y = dot(v.xyz, unity_WorldToObject[1].xyz);",
            "u_xlat0.z = dot(v.xyz, unity_WorldToObject[2].xyz);",
        };

        // --programs / --self-contained have no UnityCG.cginc, so the expansion has
        // to survive: the macro would be an undefined identifier.
        var folded = MatrixChain.Fold(lines, macros: false);
        string text = string.Join('\n', folded);

        Assert.That(text, Does.Not.Contain("UnityObjectToWorldNormal"));
        Assert.That(text, Does.Contain("mul("));
    }

    [Test]
    public void FoldTextLeavesUnrelatedTextAlone()
    {
        const string text = "float4 r0 = float4(1, 2, 3, 4);\nreturn r0;";
        Assert.That(MatrixChain.FoldText(text, macros: true), Is.EqualTo(text));
    }

    [Test]
    public void FoldDoesNotConsumeADeclarationItCannotProveIsAMatrix()
    {
        // `unity_LightColor` is a real float4[8] in Unity's includes, and `mul` has
        // no overload for it -- the expansion has to stay.
        var lines = new List<string>
        {
            "float4 unity_LightColor[8];",
            "u_xlat0.x = dot(v.xyz, unity_LightColor[0].xyz);",
            "u_xlat0.y = dot(v.xyz, unity_LightColor[1].xyz);",
            "u_xlat0.z = dot(v.xyz, unity_LightColor[2].xyz);",
        };

        string text = string.Join('\n', MatrixChain.Fold(lines, macros: true));
        Assert.That(text, Does.Contain("dot("));
    }
}

