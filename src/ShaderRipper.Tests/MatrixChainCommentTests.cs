using NUnit.Framework;
using ShaderRipper.Emit;

namespace ShaderRipper.Tests;

[TestFixture]
public class MatrixChainPreservesUnrelatedLines
{
    [Test]
    public void ACommentAppendedAtTheEndSurvivesFolding()
    {
        // An emitter pass may append diagnostics or provenance comments; the fold that
        // runs after it must not silently drop lines it does not recognise.  This is
        // the failure mode that made a plugin pass look like it never ran.
        const string body = "float4 r0 = float4(1, 2, 3, 4);\nreturn r0;\n// post-processed by Example\n";
        string folded = MatrixChain.FoldText(body, macros: true);
        Assert.That(folded, Does.Contain("post-processed by Example"));
    }

    [Test]
    public void ACommentBetweenStatementsSurvives()
    {
        const string body = "float4 r0 = float4(1, 2, 3, 4);\n// middle note\nreturn r0;\n";
        string folded = MatrixChain.FoldText(body, macros: true);
        Assert.That(folded, Does.Contain("middle note"));
    }
}
