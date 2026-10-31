using NUnit.Framework;
using ShaderRipper.Emit;

namespace ShaderRipper.Tests;

/// <summary>
/// Recovering <c>multi_compile</c> lines from the compiled variant set is pure
/// set algebra on the keyword table, so it can be pinned exactly: a group is a
/// set of alternatives that never co-occur, a variant is a per-group choice, and
/// the guard chain has to select exactly one branch per variant.
/// </summary>
[TestFixture]
public class VariantPlanTests
{
    private static List<string> Names(IEnumerable<VariantPlan.Group> groups) =>
        groups.Select(g => g.Name).ToList();

    private static List<string> Members(VariantPlan.Group g) =>
        new[] { g.Name }.Concat(g.Alternatives).ToList();

    [Test]
    public void KeywordsThatNeverCoOccurFormOneGroup()
    {
        // Three compiled variants of one multi_compile line: off, A, B.
        var groups = VariantPlan.Groups(new[]
        {
            Array.Empty<string>(),
            new[] { "A" },
            new[] { "B" },
        });

        Assert.That(groups, Has.Count.EqualTo(1));
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, Members(groups[0]));
    }

    [Test]
    public void KeywordsThatDoCoOccurStayInSeparateGroups()
    {
        // Two independent flags: all four combinations compiled.
        var groups = VariantPlan.Groups(new[]
        {
            Array.Empty<string>(),
            new[] { "A" },
            new[] { "B" },
            new[] { "A", "B" },
        });

        Assert.That(groups, Has.Count.EqualTo(2));
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, Names(groups));
    }

    [Test]
    public void EmptyInputProducesNoGroups()
    {
        Assert.That(VariantPlan.Groups(Array.Empty<IEnumerable<string>>()), Is.Empty);
    }

    [Test]
    public void GroupDeclarationOrderFollowsTheInputOrder()
    {
        var groups = VariantPlan.Groups(new[]
        {
            new[] { "FIRST" },
            new[] { "SECOND" },
        });

        // Neither set contains the other's keyword, so both could share a group;
        // they do, and the first keyword names it.
        CollectionAssert.AreEquivalent(new[] { "FIRST", "SECOND" }, Members(groups[0]));
        Assert.That(groups[0].Name, Is.EqualTo("FIRST"));
    }

    // ---- pragma selection ----------------------------------------------------

    [Test]
    public void PragmaListsEveryAlternativeBehindTheEmptySlot()
    {
        var g = new VariantPlan.Group { Name = "A", Alternatives = { "B" } };
        Assert.That(VariantPlan.Pragma(g), Is.EqualTo("#pragma multi_compile __ A B"));
    }

    /// <summary>
    /// A group that is on in every compiled variant and has no alternative is the
    /// fingerprint of a stripped <c>shader_feature</c>: the build kept the keyword,
    /// no material ever turned it off, and no second value exists to switch to.
    /// Emitting <c>multi_compile</c> there would invent variants.
    /// </summary>
    [Test]
    public void AlwaysOnSingleValueBecomesShaderFeature()
    {
        var g = new VariantPlan.Group { Name = "STATIC_ON" };
        var sets = new List<HashSet<string>> { new() { "STATIC_ON" }, new() { "STATIC_ON" } };

        Assert.That(VariantPlan.Pragma(g, sets), Is.EqualTo("#pragma shader_feature STATIC_ON"));
    }

    [Test]
    public void AlwaysOnGroupWithAlternativesKeepsEveryAlternative()
    {
        var g = new VariantPlan.Group { Name = "A", Alternatives = { "B" } };
        var sets = new List<HashSet<string>> { new() { "A" }, new() { "B" } };

        // Each variant has one alternative on, so the group is never fully off --
        // the off state was stripped, but both on states shipped.
        Assert.That(VariantPlan.Pragma(g, sets),
            Is.EqualTo("#pragma shader_feature __ A B"));
    }

    [Test]
    public void AGroupWithAnOffVariantStaysMultiCompile()
    {
        var g = new VariantPlan.Group { Name = "A" };
        var sets = new List<HashSet<string>> { new(), new() { "A" } };

        Assert.That(VariantPlan.Pragma(g, sets), Is.EqualTo("#pragma multi_compile __ A"));
    }

    [Test]
    public void PragmaWithoutVariantSetsFallsBackToMultiCompile()
    {
        var g = new VariantPlan.Group { Name = "A" };
        Assert.That(VariantPlan.Pragma(g, null), Is.EqualTo("#pragma multi_compile __ A"));
        Assert.That(VariantPlan.Pragma(g, new List<HashSet<string>>()),
            Is.EqualTo("#pragma multi_compile __ A"));
    }

    // ---- guards --------------------------------------------------------------

    [Test]
    public void GuardSpellsOutEveryGroupState()
    {
        var groups = new List<VariantPlan.Group>
        {
            new() { Name = "A", Alternatives = { "B" } },
            new() { Name = "C" },
        };

        string on = VariantPlan.Guard(groups, new HashSet<string> { "A", "C" });
        Assert.That(on, Is.EqualTo("defined(A) && defined(C)"));

        string off = VariantPlan.Guard(groups, new HashSet<string>());
        Assert.That(off, Is.EqualTo("!defined(A) && !defined(B) && !defined(C)"));
    }

    [Test]
    public void GuardUsesOrWhenSeveralAlternativesAreDefined()
    {
        // Not something a multi_compile line can produce, but a hand-built shader
        // can, and the guard must not silently drop one of them.
        var groups = new List<VariantPlan.Group> { new() { Name = "A", Alternatives = { "B" } } };
        Assert.That(VariantPlan.Guard(groups, new HashSet<string> { "A", "B" }),
            Is.EqualTo("(defined(A) || defined(B))"));
    }

    [Test]
    public void GuardIsEmptyWithoutGroups()
    {
        Assert.That(VariantPlan.Guard(new List<VariantPlan.Group>(), new HashSet<string>()),
            Is.EqualTo(""));
    }

    // ---- analysis ------------------------------------------------------------

    [Test]
    public void FullCartesianCoverageIsReportedAsComplete()
    {
        var groups = new List<VariantPlan.Group> { new() { Name = "A" } };
        var sets = new List<HashSet<string>> { new(), new() { "A" } };

        var a = VariantPlan.Analyze(groups, sets);
        Assert.That(a.Present, Is.EqualTo(2));
        Assert.That(a.Expected, Is.EqualTo(2));
        Assert.That(a.FullCartesian, Is.True);
        Assert.That(a.Notes[0], Does.Contain("full cartesian"));
    }

    [Test]
    public void StrippedCombinationsAreReportedAsPartialCoverage()
    {
        // Two groups would enumerate four combinations; only two shipped.
        var groups = new List<VariantPlan.Group> { new() { Name = "A" }, new() { Name = "B" } };
        var sets = new List<HashSet<string>> { new(), new() { "A", "B" } };

        var a = VariantPlan.Analyze(groups, sets);
        Assert.That(a.Expected, Is.EqualTo(4));
        Assert.That(a.Present, Is.EqualTo(2));
        Assert.That(a.Missing, Is.EqualTo(2));
        Assert.That(a.FullCartesian, Is.False);
        Assert.That(a.Notes[0], Does.Contain("stripped"));
    }

    [Test]
    public void AGroupNeverSwitchedOffIsListedAsAlwaysOn()
    {
        var groups = new List<VariantPlan.Group> { new() { Name = "A" }, new() { Name = "B" } };
        var sets = new List<HashSet<string>> { new() { "A" }, new() { "A", "B" } };

        var a = VariantPlan.Analyze(groups, sets);
        CollectionAssert.Contains(a.AlwaysOn, "A");
        CollectionAssert.DoesNotContain(a.AlwaysOn, "B");
    }

    [Test]
    public void ASingleVariantSaysSoInsteadOfBlamingStripping()
    {
        var a = VariantPlan.Analyze(new List<VariantPlan.Group>(), new List<HashSet<string>> { new() });
        Assert.That(a.Notes[0], Does.Contain("single variant"));
    }

    // ---- keyword resolution --------------------------------------------------

    /// <summary>The name table is the only way a keyword index becomes a name.</summary>
    [Test]
    public void KeywordIndicesResolveThroughTheNameTable()
    {
        var sp = new Data.SubProgram { KeywordIndices = { 0, 2 } };
        var table = new Dictionary<int, string> { [0] = "A", [1] = "B", [2] = "C" };

        CollectionAssert.AreEqual(new[] { "A", "C" }, VariantPlan.KeywordsOf(sp, table));
    }

    [Test]
    public void UnknownIndicesAreDroppedRatherThanNamedAfterTheirNumber()
    {
        var sp = new Data.SubProgram { KeywordIndices = { 0, 7 } };
        var table = new Dictionary<int, string> { [0] = "A" };

        CollectionAssert.AreEqual(new[] { "A" }, VariantPlan.KeywordsOf(sp, table));
    }

    [Test]
    public void ADuplicatedKeywordIsListedOnce()
    {
        var sp = new Data.SubProgram { KeywordIndices = { 0, 0, 1 } };
        var table = new Dictionary<int, string> { [0] = "A", [1] = "B" };

        CollectionAssert.AreEqual(new[] { "A", "B" }, VariantPlan.KeywordsOf(sp, table));
    }

    [Test]
    public void NoSubProgramMeansNoKeywords()
    {
        CollectionAssert.IsEmpty(VariantPlan.KeywordsOf(null, new Dictionary<int, string>()));
    }

    [Test]
    public void VariantKeyIsOrderIndependent()
    {
        Assert.That(VariantPlan.Key(new[] { "A", "B" }), Is.EqualTo(VariantPlan.Key(new[] { "B", "A" })));
        Assert.That(VariantPlan.Key(new[] { "A", "B" }), Is.EqualTo("A,B"));
    }
}
