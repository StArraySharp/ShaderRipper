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
}
