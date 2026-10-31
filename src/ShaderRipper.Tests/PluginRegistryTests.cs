using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShaderRipper.Plugins;
using ShaderRipper.Plugins.Containers;
using ShaderRipper.Plugins.Emit;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Tests;

/// <summary>
/// The registry is the contract between a plugin and the pipeline, so its ordering
/// and override rules are pinned here.
///
/// Priority exists because "last registration wins" produces results that depend on
/// load order, which cannot be printed, tested, or reasoned about.  These tests are
/// what make the printed order meaningful.
/// </summary>
[TestFixture]
public class PluginRegistryTests
{
    private static PlatformDescriptor Desc(int id, string name, string format = "dxbc") =>
        new() { Id = id, Name = name, Format = format, HasTranslator = true };

    private sealed class FakePass : IEmitterPass
    {
        public string Name { get; }
        public FakePass(string name) => Name = name;
        public string Apply(string text, bool macros) => text + Name;
    }

    private sealed class FakeProvider : IContainerProvider
    {
        public string Name { get; }
        public FakeProvider(string name) => Name = name;
        public bool CanHandle(ContainerRequest request) => false;
        public IEnumerable<ContainerStream> Read(ContainerRequest request) =>
            Array.Empty<ContainerStream>();
    }

    [Test]
    public void LowerPriorityRunsFirst()
    {
        var registry = new PluginRegistry();
        registry.AddEmitterPass(new FakePass("late"), priority: 200);
        registry.AddEmitterPass(new FakePass("early"), priority: 50);

        var order = registry.EmitterPasses.Select(p => p.Name).ToArray();
        CollectionAssert.AreEqual(new[] { "early", "late" }, order);
    }

    [Test]
    public void EqualPriorityKeepsInsertionOrder()
    {
        var registry = new PluginRegistry();
        registry.AddEmitterPass(new FakePass("first"));
        registry.AddEmitterPass(new FakePass("second"));

        var order = registry.EmitterPasses.Select(p => p.Name).ToArray();
        CollectionAssert.AreEqual(new[] { "first", "second" }, order);
    }

    [Test]
    public void ProvidersAreOrderedByPriority()
    {
        var registry = new PluginRegistry();
        registry.AddContainerProvider(new FakeProvider("b"), priority: 20);
        registry.AddContainerProvider(new FakeProvider("a"), priority: 10);

        Assert.That(registry.ContainerProviders[0], Is.TypeOf<FakeProvider>());
        Assert.That(((FakeProvider)registry.ContainerProviders[0]).Name, Is.EqualTo("a"));
    }

    [Test]
    public void APluginReplacesABuiltinPlatformAndSaysSo()
    {
        var registry = new PluginRegistry();
        registry.CurrentOwner = "builtin";
        registry.AddPlatform(Desc(14, "metal", "metal"), null, null, PluginRegistry.BuiltinPriority);

        registry.CurrentOwner = "Metal (MSL)";
        registry.AddPlatform(Desc(14, "metal", "metal"), null, new NoopTranslator(), 100);

        var entry = registry.PlatformFor(14);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.Priority, Is.EqualTo(100));
        Assert.That(entry.Replaced, Is.True);
        Assert.That(entry.ReplacedName, Is.EqualTo("metal"));
        Assert.That(entry.Translator, Is.Not.Null);
    }

    /// <summary>
    /// Replacement is the opposite of execution order: since execution asks the
    /// lowest number first, an entry that registered later has to win the slot or it
    /// would never be reachable.
    /// </summary>
    [Test]
    public void ALaterRegistrationKeepsTheSlot()
    {
        var registry = new PluginRegistry();
        registry.CurrentOwner = "first";
        registry.AddPlatform(Desc(14, "metal", "metal"), null, new NoopTranslator(), 100);

        registry.CurrentOwner = "second";
        registry.AddPlatform(Desc(14, "metal2", "metal"), null, new NoopTranslator(), 200);

        var entry = registry.PlatformFor(14);
        Assert.That(entry.Descriptor.Name, Is.EqualTo("metal2"));
    }

    /// <summary>
    /// The reverse direction is refused and logged, so a late low-priority attempt
    /// cannot silently take a slot away from the entry that already holds it.
    /// </summary>
    [Test]
    public void AnEarlierRegistrationIsNotDisplacedByAHigherNumber()
    {
        var registry = new PluginRegistry();
        registry.CurrentOwner = "plugin";
        registry.AddPlatform(Desc(14, "metal", "metal"), null, new NoopTranslator(), 100);

        registry.CurrentOwner = "builtin";
        registry.AddPlatform(Desc(14, "metal", "metal"), null, null, PluginRegistry.BuiltinPriority);

        var entry = registry.PlatformFor(14);
        Assert.That(entry.Priority, Is.EqualTo(100));
        Assert.That(entry.Translator, Is.Not.Null);
    }

    [Test]
    public void RegistrationAfterCloseThrows()
    {
        var registry = new PluginRegistry();
        registry.Close();

        Assert.Throws<InvalidOperationException>(
            () => registry.AddEmitterPass(new FakePass("x")));
        Assert.Throws<InvalidOperationException>(
            () => registry.AddContainerProvider(new FakeProvider("x")));
        Assert.Throws<InvalidOperationException>(
            () => registry.AddPlatform(Desc(99, "new"), null));
    }

    [Test]
    public void AnUnknownPlatformHasNoEntry()
    {
        var registry = new PluginRegistry();
        Assert.That(registry.PlatformFor(7777), Is.Null);
        Assert.That(registry.HasTranslator(7777), Is.False);
    }

    [Test]
    public void APlatformWithoutATranslatorIsStillRegistered()
    {
        var registry = new PluginRegistry();
        registry.AddPlatform(Desc(2, "d3d9", "d3d9"), null, null);

        Assert.That(registry.PlatformFor(2), Is.Not.Null);
        Assert.That(registry.HasTranslator(2), Is.False);
    }

    [Test]
    public void DescribeReportsEveryCapability()
    {
        var registry = new PluginRegistry();
        registry.CurrentOwner = "builtin";
        registry.AddPlatform(Desc(4, "d3d11"), null, null, PluginRegistry.BuiltinPriority);
        registry.AddEmitterPass(new FakePass("matrix-chain"), PluginRegistry.BuiltinPriority);

        var lines = registry.Describe().ToList();
        Assert.That(lines.Any(l => l.Contains("d3d11(4)")), Is.True);
        Assert.That(lines.Any(l => l.Contains("matrix-chain")), Is.True);
    }

    private sealed class NoopTranslator : IProgramTranslator
    {
        public bool CanTranslate(TranslationRequest request) => true;
        public TranslationResult Translate(TranslationRequest request) =>
            new() { Hlsl = "// noop", Stage = "ps" };
    }
}
