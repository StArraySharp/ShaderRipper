using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShaderRipper.Data;
using ShaderRipper.Game;

namespace ShaderRipper.Tests;

/// <summary>
/// Two passes whose compiled programs are byte-identical share one blob slot in
/// the serialized asset.  The pass map must therefore carry every owner per
/// slot: keying a slot by a single pass silently dropped the program from every
/// other pass that declared it.
/// </summary>
[TestFixture]
public class SharedPassProgramTests
{
    private static Pass PassWithBlob(int blobIndex, int cbCount)
    {
        var sp = new SubProgram { BlobIndex = blobIndex };
        for (int i = 0; i < cbCount; i++)
            sp.ConstantBuffers.Add(new AssetConstantBuffer
            {
                Name = $"cb{i}",
                Members = { new AssetCbMember { Name = $"m{i}" } },
            });
        return new Pass { SubPrograms = { sp } };
    }

    private static Shader ShaderWith(params Pass[] passes)
    {
        return new Shader
        {
            ParsedForm = new ParsedForm
            {
                Name = "Test/SharedPasses",
                SubShaders = { new SubShader { Passes = new List<Pass>(passes) } },
            },
        };
    }

    [Test]
    public void SharedBlobSlotListsEveryOwnerPass()
    {
        var passA = PassWithBlob(7, 2);
        var passB = PassWithBlob(7, 2);
        var sh = ShaderWith(passA, passB);

        var (passOf, spOf) = GameExport.PassMaps(sh);

        Assert.That(passOf[7], Is.EquivalentTo(new[] { passA, passB }));
        // Equal reflection: the first pass seen keeps the slot.
        Assert.That(spOf[7], Is.SameAs(passA.SubPrograms[0]));
    }

    [Test]
    public void DistinctBlobSlotsKeepTheirOwnPasses()
    {
        var passA = PassWithBlob(1, 2);
        var passB = PassWithBlob(2, 2);
        var sh = ShaderWith(passA, passB);

        var (passOf, _) = GameExport.PassMaps(sh);

        Assert.That(passOf[1], Is.EquivalentTo(new[] { passA }));
        Assert.That(passOf[2], Is.EquivalentTo(new[] { passB }));
    }

    [Test]
    public void RicherReflectionWinsTheSharedSlot()
    {
        var thin = PassWithBlob(3, 1);
        var rich = PassWithBlob(3, 4);
        var sh = ShaderWith(thin, rich);

        var (_, spOf) = GameExport.PassMaps(sh);

        Assert.That(spOf[3], Is.SameAs(rich.SubPrograms[0]));
    }
}
