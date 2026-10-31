using System;
using System.IO;
using NUnit.Framework;
using ShaderRipper.Game;

namespace ShaderRipper.Tests;

/// <summary>
/// The incremental cache decides whether a shader is translated again, so getting
/// it wrong is either wasted work (never reuse) or a silently stale directory
/// (always reuse).  Both failure modes are invisible without a test.
/// </summary>
[TestFixture]
public class ExportCacheTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ShaderRipperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Write(string rel, string text)
    {
        string full = Path.Combine(_dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    private static string Profile => ExportCache.ProfileOf("jobs=1");

    [Test]
    public void ACachedOutputIsCurrentAfterItIsRecorded()
    {
        Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);

        Assert.That(cache.IsCurrent("Assets/A.shader", "src|1|2|3", _dir), Is.False,
                    "nothing has been recorded yet");

        cache.Record("Assets/A.shader", "src|1|2|3", _dir);
        Assert.That(cache.IsCurrent("Assets/A.shader", "src|1|2|3", _dir), Is.True);
    }

    [Test]
    public void ADifferentInputKeyInvalidatesTheEntry()
    {
        Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1|2|3", _dir);

        Assert.That(cache.IsCurrent("Assets/A.shader", "src|9|9|9", _dir), Is.False);
    }

    /// <summary>
    /// The input file is unchanged but somebody edited the output.  Reusing would
    /// discard their work without a word, so the hash has to be checked rather
    /// than trusted from the last run.
    /// </summary>
    [Test]
    public void AnEditedOutputIsNotCurrent()
    {
        Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1|2|3", _dir);
        File.WriteAllText(Path.Combine(_dir, "Assets/A.shader"), "hand edited");

        Assert.That(cache.IsCurrent("Assets/A.shader", "src|1|2|3", _dir), Is.False);
    }

    [Test]
    public void ADeletedOutputIsNotCurrent()
    {
        string full = Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1|2|3", _dir);
        File.Delete(full);

        Assert.That(cache.IsCurrent("Assets/A.shader", "src|1|2|3", _dir), Is.False);
    }

    [Test]
    public void AnUnreadableInputKeyIsNeverCurrent()
    {
        Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1|2|3", _dir);

        Assert.That(cache.IsCurrent("Assets/A.shader", null, _dir), Is.False);
    }

    [Test]
    public void EntriesSurviveASaveAndReload()
    {
        Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1|2|3", _dir);
        cache.Save();

        var again = ExportCache.Load(_dir, Profile);
        Assert.That(again.IsCurrent("Assets/A.shader", "src|1|2|3", _dir), Is.True);
    }

    /// <summary>
    /// A rebuilt library emits different text for the same input, so its cache is
    /// not usable: the profile carries the module version id.
    /// </summary>
    [Test]
    public void ADifferentProfileDiscardsTheCache()
    {
        Write("Assets/A.shader", "shader A");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1|2|3", _dir);
        cache.Save();

        var other = ExportCache.Load(_dir, ExportCache.ProfileOf("jobs=4"));
        Assert.That(other.IsCurrent("Assets/A.shader", "src|1|2|3", _dir), Is.False);
    }

    [Test]
    public void ACorruptCacheFileStartsEmptyRatherThanThrowing()
    {
        File.WriteAllText(Path.Combine(_dir, ".shaderripper-cache.json"), "{ not json");
        Assert.DoesNotThrow(() => ExportCache.Load(_dir, Profile));
    }

    /// <summary>An entry whose file is gone is dropped, so the file cannot grow forever.</summary>
    [Test]
    public void PruneDropsEntriesThatAreNoLongerProduced()
    {
        Write("Assets/A.shader", "shader A");
        Write("Assets/B.shader", "shader B");
        var cache = ExportCache.Load(_dir, Profile);
        cache.Record("Assets/A.shader", "src|1", _dir);
        cache.Record("Assets/B.shader", "src|2", _dir);
        cache.Prune(_dir, new[] { "Assets/A.shader" });
        cache.Save();

        var again = ExportCache.Load(_dir, Profile);
        Assert.That(again.IsCurrent("Assets/A.shader", "src|1", _dir), Is.True);
        Assert.That(again.IsCurrent("Assets/B.shader", "src|2", _dir), Is.False);
    }

    /// <summary>A cache with no entries is deleted rather than left as an empty file.</summary>
    [Test]
    public void AnEmptyCacheRemovesTheFile()
    {
        var cache = ExportCache.Load(_dir, Profile);
        cache.Save();
        Assert.That(File.Exists(Path.Combine(_dir, ".shaderripper-cache.json")), Is.False);
    }

    /// <summary>
    /// The source key is built from the file's identity, so touching the input
    /// (a rebuild) has to produce a different one.
    /// </summary>
    [Test]
    public void TheSourceKeyChangesWhenTheInputIsRewritten()
    {
        string input = Path.Combine(_dir, "in.assets");
        File.WriteAllText(input, "one");
        string first = ExportCache.SourceKey(input, 7);

        File.WriteAllText(input, "one and a half");
        File.SetLastWriteTimeUtc(input, DateTime.UtcNow.AddMinutes(1));
        string second = ExportCache.SourceKey(input, 7);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void TheSourceKeyOfAMissingFileIsNull()
    {
        Assert.That(ExportCache.SourceKey(Path.Combine(_dir, "absent.assets"), 1), Is.Null);
    }
}
