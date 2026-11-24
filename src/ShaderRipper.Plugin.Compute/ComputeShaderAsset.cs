using ShaderRipper.Data;
using ShaderRipper.Game;

namespace ShaderRipper.Plugin.Compute;

internal sealed class ComputeShaderAsset : ShaderAsset
{
    public readonly List<ComputeVariant> Variants = new();
    public readonly List<string> KernelNames = new();
    public readonly List<string> GlobalKeywords = new();
    public readonly List<string> LocalKeywords = new();

    public override string OutputExtension => ".compute";

    public override void Dump(TextWriter writer, long pathId)
    {
        writer.WriteLine($"    compute: type={pathId} kernels={KernelNames.Count} variants={Variants.Count}");
        foreach (var variant in Variants)
            writer.WriteLine($"      backend={Platforms.Get(variant.Platform).Name}({variant.Platform}) " +
                             $"level={variant.Level} kernels={variant.Kernels.Count}");
        foreach (string kernel in KernelNames) writer.WriteLine("      kernel " + kernel);
    }

    public override void Export(string name, string rel, string outDir,
                                bool writeAsset, bool origAsm, GameExport.Stats stats) =>
        ComputeExport.Export(this, name, rel, outDir, writeAsset, origAsm, stats);
}

internal sealed class ComputeVariant
{
    public int Platform;
    public int Level;
    public readonly List<ComputeKernel> Kernels = new();
    public readonly List<string> GlobalKeywords = new();
    public readonly List<string> LocalKeywords = new();
}

internal sealed class ComputeKernel
{
    public string Name = "";
    public int[] ThreadGroupSize = Array.Empty<int>();
    public readonly List<ComputeProgram> Programs = new();
}

internal sealed record ComputeProgram(byte[] Code, Dictionary<int, AssetConstantBuffer> ConstantBuffers);
