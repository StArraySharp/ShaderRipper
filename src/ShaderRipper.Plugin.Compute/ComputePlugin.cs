using ShaderRipper.Plugins;

namespace ShaderRipper.Plugin.Compute;

public sealed class ComputePlugin : IShaderRipperPlugin
{
    public string Name => "ComputeShader";
    public string Description => "Reads Unity compute shaders and exports DXBC kernels as .compute files.";
    public PluginApiVersion ApiVersion => PluginApiVersion.Current;

    public void Register(IPluginRegistry registry) =>
        ((PluginRegistry)registry).AddShaderAssetReader(72, ComputeShaderReader.Parse);
}
