using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// The platform table is the one place where a wrong number silently decodes
/// another backend's bytes, so both directions (id -> name, name -> id) and the
/// slot-picking order are pinned here.
/// </summary>
[TestFixture]
public class PlatformsTests
{
    [TestCase(4, "d3d11", "dxbc")]
    [TestCase(9, "gles3", "glsl")]
    [TestCase(5, "gles20", "glsl")]
    [TestCase(15, "openglcore", "glsl")]
    [TestCase(1, "opengl", "glsl")]
    [TestCase(2, "d3d9", "d3d9")]
    [TestCase(14, "metal", "metal")]
    [TestCase(18, "vulkan", "spirv")]
    [TestCase(26, "webgpu", "wgsl")]
    public void KnownIdsDescribeTheirBackend(int id, string name, string format)
    {
        var info = Platforms.Get(id);
        Assert.That(info.Name, Is.EqualTo(name));
        Assert.That(info.Format, Is.EqualTo(format));
    }

    [Test]
    public void UnknownIdIsNamedRatherThanThrowing()
    {
        var info = Platforms.Get(9999);
        Assert.That(info.Name, Is.EqualTo("unknown9999"));
        Assert.That(info.Format, Is.EqualTo("unknown"));
    }

    [TestCase("d3d11", 4)]
    [TestCase("gles3", 9)]
    [TestCase("gles", 9)]
    [TestCase("gles2", 5)]
    [TestCase("gles20", 5)]
    [TestCase("glcore", 15)]
    [TestCase("gl", 1)]
    [TestCase("4", 4)]
    public void NameLookupAcceptsAliasesAndNumbers(string text, int expected) =>
        Assert.That(Platforms.ByName(text), Is.EqualTo(expected));

    [TestCase("nonsense")]
    [TestCase("")]
    [TestCase("   ")]
    public void NameLookupRejectsUnknownText(string text) =>
        Assert.That(Platforms.ByName(text), Is.Null);

    /// <summary>
    /// d3d11 wins over every other slot because it is the backend the DXBC route
    /// translates instruction by instruction; the GLSL slots come next because a
    /// source payload back-translates better than an assembly transliteration.
    /// </summary>
    [Test]
    public void D3d11WinsWhenTheBuildShipsSeveralBackends()
    {
        var ids = new uint[] { 18, 15, 9, 4 };
        Assert.That(ids[Platforms.PickMain(ids, null)], Is.EqualTo(4u));
    }

    [Test]
    public void GlslIsChosenWhenNoDxbcSlotExists()
    {
        var ids = new uint[] { 18, 9, 14 };
        Assert.That(ids[Platforms.PickMain(ids, null)], Is.EqualTo(9u));
    }

    [Test]
    public void ForcedPlatformOverridesTheOrder()
    {
        var ids = new uint[] { 4, 9, 1 };
        Assert.That(ids[Platforms.PickMain(ids, 9)], Is.EqualTo(9u));
    }

    [Test]
    public void ForcingAPlatformTheBuildDoesNotShipReportsNoSlot()
    {
        var ids = new uint[] { 4, 9 };
        Assert.That(Platforms.PickMain(ids, 18), Is.EqualTo(-1));
    }

    [Test]
    public void OtherIndicesSkipsTheMainSlot()
    {
        var ids = new uint[] { 4, 9, 1 };
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, Platforms.OtherIndices(ids, 0));
    }
}
