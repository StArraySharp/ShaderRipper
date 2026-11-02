using System;
using ShaderRipper.Plugins;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// SPIR-V backend: gives the tool a Vulkan slot it can read and translate.
    ///
    /// Unity's Vulkan payload is SMOL-V, a compressed form of SPIR-V.  It is worth
    /// reading for two reasons beyond coverage: the payload keeps
    /// <c>layout(binding = N)</c> for every resource (the Vulkan spec requires it),
    /// so texture and cbuffer registers survive where the d3d11 slot drops them; and
    /// SPIR-V is a fully typed IR, so the translation is a real decompile rather than
    /// a transliteration of an instruction listing.
    ///
    /// The work is split the way the API expects: <see cref="SmolvSlotReader"/> knows
    /// how Unity packs programs into a payload, and <see cref="SpirvTranslator"/>
    /// knows how to turn one program into HLSL.  Neither reads game data.
    /// </summary>
    public sealed class SpirVPlugin : IShaderRipperPlugin
    {
        public string Name => "SPIR-V (Vulkan)";

        public string Description =>
            "Reads Unity's SMOL-V payloads and translates them to HLSL via SPIRV-Cross";

        public PluginApiVersion ApiVersion => PluginApiVersion.Current;

        public void Register(IPluginRegistry registry)
        {
            // The descriptor overrides nothing: id 18 is in the built-in table already,
            // but with no reader and no translator, which is exactly the gap this
            // fills.  Priority is irrelevant for a backend (the id is the key), so the
            // default is left alone.
            registry.AddPlatform(
                new PlatformDescriptor
                {
                    Id = 18,
                    Name = "vulkan",
                    Format = "spirv",
                    HasSource = false,
                    HasTranslator = true,
                    OverridesBuiltin = true,
                },
                new SmolvSlotReader(),
                new SpirvTranslator());
        }
    }
}
