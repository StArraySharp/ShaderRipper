using ShaderRipper.Plugins;
using ShaderRipper.Plugins.Containers;

namespace ShaderRipper.Plugin.XorBundle;

public sealed class XorBundlePlugin : IShaderRipperPlugin, IContainerProvider
{
    private const int HeaderSize = 128;
    private static readonly byte[] Key = Convert.FromHexString(
        "7924442538375EE8B8A2E78886E6ACB8E8BF983634E9B985E9BB843D2D296572");

    public string Name => "UnityFS XOR header (128 bytes)";
    public string Description => "Unwraps UnityFS bundles with a repeating XOR header.";
    public PluginApiVersion ApiVersion => PluginApiVersion.Current;

    public void Register(IPluginRegistry registry) => registry.AddContainerProvider(this);

    public bool CanHandle(ContainerRequest request)
    {
        if (request.Path == null || request.Head.Length < 16) return false;
        var head = request.Head.Span;
        var magic = "UnityFS\0"u8;
        for (int i = 0; i < magic.Length; i++)
            if ((head[i] ^ Key[i]) != magic[i]) return false;
        return true;
    }

    public IEnumerable<ContainerStream> Read(ContainerRequest request)
    {
        byte[] bytes = File.ReadAllBytes(request.Path);
        int end = Math.Min(bytes.Length, HeaderSize);
        for (int i = 0; i < end; i++) bytes[i] ^= Key[i % Key.Length];
        if (!bytes.AsSpan().StartsWith("UnityFS\0"u8))
            throw new InvalidDataException("The decoded header is not UnityFS.");
        yield return new ContainerStream
        {
            Name = Path.GetFileName(request.Path),
            Content = new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true),
        };
    }
}
