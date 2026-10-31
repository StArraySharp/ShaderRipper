using NUnit.Framework;
using ShaderRipper.Emit;
using System.Collections.Generic;

namespace ShaderRipper.Tests;

[TestFixture]
public class ShaderLabMergePreservesPassOutput
{
    [Test]
    public void CommentsFromABodyReachTheMergedProgram()
    {
        // An emitter pass may append provenance or diagnostics to a stage body.  Merge
        // is what turns the two stages into one program, and it must not drop lines it
        // does not recognise -- otherwise a pass runs and its output silently vanishes.
        var vs = new ShaderLab.Stage
        {
            Entry = "vert",
            Hlsl = "float4 vert() : SV_Position { return 0; }\n// MARKER-VS\n",
        };
        var ps = new ShaderLab.Stage
        {
            Entry = "frag",
            Hlsl = "float4 frag() : SV_Target { return 1; }\n// MARKER-PS\n",
        };

        var merged = ShaderLab.Merge(vs, ps);
        Assert.That(merged, Is.Not.Null);
        string text = string.Join("\n", merged);

        Assert.That(text, Does.Contain("MARKER-VS"));
        Assert.That(text, Does.Contain("MARKER-PS"));
    }
}
