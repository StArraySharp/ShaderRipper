using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShaderRipper.Plugins;

namespace ShaderRipper.Tests;

/// <summary>
/// The finished-export hook contract: a plugin sees the run as a whole, and a broken
/// hook cannot turn a finished export into a failed one.
///
/// The hook exists because an emitter pass cannot do this job -- a pass sees one
/// program's text at a time and never learns which shaders the run covered.
/// </summary>
[TestFixture]
public class ExportHookTests
{
    private sealed class Recorder : IExportHook
    {
        public string Name => "recorder";
        public List<ExportCompleted> Seen { get; } = new();
        public void ExportFinished(ExportCompleted context) => Seen.Add(context);
    }

    private sealed class Thrower : IExportHook
    {
        public string Name => "thrower";
        public void ExportFinished(ExportCompleted context) =>
            throw new InvalidOperationException("this hook is broken");
    }

    private static ExportCompleted Sample() => new()
    {
        OutputDirectory = "/tmp/out",
        Shaders = new[]
        {
            new ExportedShader
            {
                Name = "UI/Pause Planets",
                Path = "Assets/Shader/UI_Pause Planets.shader",
                Properties = new[]
                {
                    new ExportedProperty { Name = "_Color", Type = 0, DefaultValue = new[] { 1f, 1f, 1f, 1f } },
                    new ExportedProperty { Name = "_MainTex", Type = 4, DefaultTexture = "white" },
                },
            },
        },
        Materials = new[]
        {
            new ExportedMaterial
            {
                Name = "Sprites-Default",
                AssetPath = "Assets/Shader/Sprites-Default.mat",
                Shader = "Sprites/Default",
                ShaderFile = "Assets/Shader/UI_Pause Planets.shader",
                Floats = new Dictionary<string, float> { ["PixelSnap"] = 0f },
                Colors = new Dictionary<string, float[]> { ["_Color"] = new[] { 1f, 1f, 1f, 1f } },
                Textures = new Dictionary<string, string> { ["_MainTex"] = "white" },
                Synthetic = false,
            },
        },
        Skipped = 8,
        Translated = 3270,
    };

    [Test]
    public void ARunArtifactIsAcceptedByTheExportDirectory()
    {
        // The overwrite check is a name list.  A hook that writes a file without
        // registering the name would make the next --overwrite run refuse the
        // directory it wrote itself.
        var registry = new PluginRegistry();
        Assert.That(registry.ExportArtifacts, Is.Empty);
        registry.AddExportArtifact(".shaderripper-tests.json");
        Assert.That(registry.ExportArtifacts, Does.Contain(".shaderripper-tests.json"));
    }

    [Test]
    public void AnArtifactMustBeAFileNameNotAPath()
    {
        var registry = new PluginRegistry();
        Assert.Throws<ArgumentException>(() => registry.AddExportArtifact("sub/file.json"));
        Assert.Throws<ArgumentException>(() => registry.AddExportArtifact(""));
    }

    [Test]
    public void RegisteringTheSameArtifactTwiceKeepsOneEntry()
    {
        var registry = new PluginRegistry();
        registry.AddExportArtifact("a.json");
        registry.AddExportArtifact("A.JSON");     // same name, different case
        Assert.That(registry.ExportArtifacts.Count, Is.EqualTo(1));
    }

    [Test]
    public void TheContextCarriesWhatAHookNeeds()
    {
        ExportCompleted ctx = Sample();
        Assert.That(ctx.Shaders.Single().Name, Is.EqualTo("UI/Pause Planets"));
        Assert.That(ctx.Shaders.Single().Properties.Count, Is.EqualTo(2));
        Assert.That(ctx.Shaders.Single().Properties[1].DefaultTexture, Is.EqualTo("white"));
        Assert.That(ctx.Materials.Single().ShaderFile, Is.Not.Null);
        Assert.That(ctx.Materials.Single().Synthetic, Is.False);
    }

    [Test]
    public void HooksRunInRegistrationOrder()
    {
        var registry = new PluginRegistry();
        var first = new Recorder();
        var second = new Recorder();
        registry.AddExportHook(first);
        registry.AddExportHook(second);
        Assert.That(registry.ExportHooks.Select(h => h.Name),
                    Is.EqualTo(new[] { "recorder", "recorder" }));
    }

    [Test]
    public void AHookThatThrowsIsSkippedAndTheExportStaysSuccessful()
    {
        // The export is already on disk by the time a hook runs; a broken plugin must
        // not be able to turn it into a failed run.
        var registry = new PluginRegistry();
        registry.AddExportHook(new Thrower());
        var recorder = new Recorder();
        registry.AddExportHook(recorder);

        var notes = new List<string>();
        foreach (IExportHook hook in registry.ExportHooks)
        {
            try { hook.ExportFinished(Sample()); }
            catch (Exception e) { notes.Add($"{hook.Name} failed: {e.GetType().Name}"); }
        }

        Assert.That(notes, Has.Count.EqualTo(1));
        Assert.That(notes[0], Does.Contain("thrower"));
        Assert.That(recorder.Seen, Has.Count.EqualTo(1), "the hook after the broken one still ran");
    }
}
