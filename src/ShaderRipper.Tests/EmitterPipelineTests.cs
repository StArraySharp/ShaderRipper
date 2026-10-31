using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShaderRipper.Plugins;
using ShaderRipper.Plugins.Emit;

namespace ShaderRipper.Tests;

/// <summary>
/// The emitter pipeline runs post-processing passes over every program body.
///
/// Two properties matter more than anything a pass does: a pass that throws must not
/// take the export down, and it must not leave a half-rewritten body behind either.
/// Both are asserted here, because they are the failure modes a third-party pass will
/// actually hit.
/// </summary>
[TestFixture]
public class EmitterPipelineTests
{
    private sealed class RecordingPass : IEmitterPass
    {
        public string Name => "record";
        public readonly List<(string text, bool macros)> Calls = new();
        public string Apply(string text, bool macros)
        {
            Calls.Add((text, macros));
            return text;
        }
    }

    private sealed class ThrowingPass : IEmitterPass
    {
        public string Name => "boom";
        public string Apply(string text, bool macros) =>
            throw new InvalidOperationException("pass exploded");
    }

    private sealed class RewritingPass : IEmitterPass
    {
        private readonly string _from, _to;
        public RewritingPass(string from, string to) { _from = from; _to = to; }
        public string Name => $"rewrite:{_from}";
        public string Apply(string text, bool macros) => text.Replace(_from, _to);
    }

    [TearDown]
    public void Restore() => PluginContext.Install(null);

    [Test]
    public void WithNoRegistryOnlyTheBuiltinPassRuns()
    {
        PluginContext.Install(null);
        Assert.That(EmitterPipeline.Passes.Select(p => p.Name), Is.EqualTo(new[] { "matrix-chain" }));
    }

    [Test]
    public void PassesRunInPriorityOrder()
    {
        var registry = new PluginRegistry();
        registry.AddEmitterPass(new RewritingPass("B", "BB"), priority: 200);
        registry.AddEmitterPass(new RewritingPass("A", "AA"), priority: 100);
        registry.Close();
        PluginContext.Install(registry);

        // Each pass sees the previous pass's output, so a rewrite chained in order is
        // observable in the result.
        string result = EmitterPipeline.Run("A B", macros: true);
        Assert.That(result, Is.EqualTo("AA BB"));
    }

    [Test]
    public void TheMacrosFlagReachesThePass()
    {
        var registry = new PluginRegistry();
        var probe = new RecordingPass();
        registry.AddEmitterPass(probe);
        registry.Close();
        PluginContext.Install(registry);

        EmitterPipeline.Run("x", macros: false);
        EmitterPipeline.Run("y", macros: true);

        Assert.That(probe.Calls, Has.Count.EqualTo(2));
        Assert.That(probe.Calls[0].macros, Is.False);
        Assert.That(probe.Calls[1].macros, Is.True);
    }

    /// <summary>
    /// A throwing pass is skipped and the text keeps the value it had before that
    /// pass -- not a partial rewrite, and not an aborted export.
    /// </summary>
    [Test]
    public void AThrowingPassIsSkippedAndLeavesTheTextIntact()
    {
        var registry = new PluginRegistry();
        registry.AddEmitterPass(new RewritingPass("keep", "KEPT"), priority: 10);
        registry.AddEmitterPass(new ThrowingPass(), priority: 20);
        registry.AddEmitterPass(new RewritingPass("tail", "TAIL"), priority: 30);
        registry.Close();
        PluginContext.Install(registry);

        string result = EmitterPipeline.Run("keep tail", macros: true);
        Assert.That(result, Is.EqualTo("KEPT TAIL"));
    }

    [Test]
    public void APassReturningNullLeavesTheTextUnchanged()
    {
        var registry = new PluginRegistry();
        registry.AddEmitterPass(new NullPass());
        registry.Close();
        PluginContext.Install(registry);

        Assert.That(EmitterPipeline.Run("unchanged", macros: true), Is.EqualTo("unchanged"));
    }

    private sealed class NullPass : IEmitterPass
    {
        public string Name => "null";
        public string Apply(string text, bool macros) => null;
    }
}

/// <summary>
/// <see cref="PluginCall"/> is the guard every plugin invocation goes through, so its
/// two promises -- never propagate, and report each distinct failure once -- are
/// checked directly.
/// </summary>
[TestFixture]
public class PluginCallTests
{
    [SetUp]
    public void Clear() => PluginCall.Reset();

    [Test]
    public void ASuccessfulCallReturnsItsValue()
    {
        var notes = new List<string>();
        int value = PluginCall.Try("test", () => 42, -1, notes);

        Assert.That(value, Is.EqualTo(42));
        Assert.That(notes, Is.Empty);
    }

    [Test]
    public void AThrowingCallReturnsTheFallbackAndRecordsOneNote()
    {
        var notes = new List<string>();
        int value = PluginCall.Try<int>("test", () => throw new InvalidOperationException("bad"), -1, notes);

        Assert.That(value, Is.EqualTo(-1));
        Assert.That(notes, Has.Count.EqualTo(1));
        Assert.That(notes[0], Does.Contain("test"));
        Assert.That(notes[0], Does.Contain("bad"));
    }

    [Test]
    public void TheSameFailureIsReportedOnlyOnce()
    {
        var notes = new List<string>();
        for (int i = 0; i < 5; i++)
            PluginCall.Try<int>("test", () => throw new InvalidOperationException("bad"), -1, notes);

        Assert.That(notes, Has.Count.EqualTo(1), "a plugin failing per program must not flood the log");
    }

    [Test]
    public void DistinctFailuresAreEachReported()
    {
        var notes = new List<string>();
        PluginCall.Try<int>("a", () => throw new InvalidOperationException("one"), -1, notes);
        PluginCall.Try<int>("b", () => throw new InvalidOperationException("two"), -1, notes);

        Assert.That(notes, Has.Count.EqualTo(2));
    }

    [Test]
    public void ResetAllowsTheSameFailureToBeReportedAgain()
    {
        var notes = new List<string>();
        PluginCall.Try<int>("test", () => throw new InvalidOperationException("bad"), -1, notes);
        PluginCall.Reset();
        PluginCall.Try<int>("test", () => throw new InvalidOperationException("bad"), -1, notes);

        Assert.That(notes, Has.Count.EqualTo(2));
    }

    [Test]
    public void AVoidCallReportsSuccess()
    {
        var notes = new List<string>();
        Assert.That(PluginCall.Try("ok", () => { }, notes), Is.True);
        Assert.That(PluginCall.Try("bad", () => throw new Exception("x"), notes), Is.False);
    }
}
