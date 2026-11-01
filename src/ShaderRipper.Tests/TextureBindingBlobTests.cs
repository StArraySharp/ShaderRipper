using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// Unity stores texture bindings in each platform's parameter blob, but how much
/// it records depends on the backend: D3D11 omits any slot the runtime can derive
/// by convention (measured: 20 of 398 ADOFAI shaders), while Vulkan must write
/// every <c>layout(binding = N)</c> and so records 395 of 398.
///
/// The two encodings differ only in what the second int means, so these tests pin
/// both down.  They matter because a DXBC-only build has no other source for a
/// sampler name: UnityShaderCompiler consumes the RDEF and then strips it, so the
/// parameter blob is all that is left of the reflection.
/// </summary>
[TestFixture]
public class TextureBindingBlobTests
{
    private const uint Magic = 0x0C0A75BAu;
    private const int HeaderLen = 4 + 12;      // slot count + one slot entry

    private static byte[] Le(params uint[] words)
    {
        var b = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
        {
            b[i * 4] = (byte)words[i];
            b[i * 4 + 1] = (byte)(words[i] >> 8);
            b[i * 4 + 2] = (byte)(words[i] >> 16);
            b[i * 4 + 3] = (byte)(words[i] >> 24);
        }
        return b;
    }

    /// <summary>Append a length-prefixed, 4-byte-aligned identifier.</summary>
    private static void Name(List<byte> into, string name)
    {
        into.AddRange(Le((uint)name.Length));
        into.AddRange(System.Text.Encoding.ASCII.GetBytes(name));
        while (into.Count % 4 != 0) into.Add(0);
    }

    /// <summary>
    /// Build a one-slot payload: magic, record count, then the records.
    /// </summary>
    private static byte[] Payload(uint recordCount, Action<List<byte>> records, out int size)
    {
        var body = new List<byte>();
        body.AddRange(Le(Magic, recordCount));
        records(body);

        var file = new List<byte>();
        int off = HeaderLen;
        file.AddRange(Le(1u));                        // one slot
        file.AddRange(Le((uint)off, (uint)body.Count, 0u));
        file.AddRange(body);
        size = body.Count;
        return file.ToArray();
    }

    [Test]
    public void DxbcRecordNamesTheTextureRegister()
    {
        // [m_NameIndex, m_Index = t#, m_SamplerIndex, marker 4]
        var blob = Payload(2, b =>
        {
            Name(b, "_PlanetTex");
            b.AddRange(Le(0u, 0u, 1u, 4u));
            Name(b, "_AlphaTex");
            b.AddRange(Le(0u, 1u, 0u, 4u));
        }, out _);

        var table = BlobTool.ParseBlobTextureParams(blob);

        Assert.That(table, Has.Count.EqualTo(1));
        Assert.That(table[0][0], Is.EqualTo("_PlanetTex"));
        Assert.That(table[0][1], Is.EqualTo("_AlphaTex"));
    }

    [Test]
    public void VulkanRecordPacksTheBindingIntoTheLowByte()
    {
        // [m_NameIndex, 0x08000000 | binding, sampler, marker 4]
        var blob = Payload(2, b =>
        {
            Name(b, "_PlanetTex");
            b.AddRange(Le(0u, 0x08000000u, 0xFFFFFFFFu, 4u));
            Name(b, "_AlphaTex");
            b.AddRange(Le(0u, 0x08000001u, 0xFFFFFFFFu, 4u));
        }, out _);

        var table = BlobTool.ParseVulkanTextureParams(blob);

        Assert.That(table, Has.Count.EqualTo(1));
        Assert.That(table[0][0], Is.EqualTo("_PlanetTex"));
        Assert.That(table[0][1], Is.EqualTo("_AlphaTex"));
    }

    [Test]
    public void ConstantBufferRecordsAreNotMistakenForTextures()
    {
        // A scalar uniform ends in marker 0 and a cbuffer header has no marker at
        // all; neither must land in the texture table.
        var blob = Payload(2, b =>
        {
            Name(b, "_Color");
            b.AddRange(Le(0u, 1u, 4u, 0u));
            Name(b, "$Globals");
            b.AddRange(Le(64u, 0u, 0u, 0u));
        }, out _);

        Assert.That(BlobTool.ParseBlobTextureParams(blob), Is.Empty);
        Assert.That(BlobTool.ParseVulkanTextureParams(blob), Is.Empty);
    }

    [Test]
    public void VulkanRecordWithoutTheDescriptorSetMarkerIsIgnored()
    {
        // binding 0 but no 0x08 prefix: this is the DXBC shape, not the Vulkan one.
        var blob = Payload(1, b =>
        {
            Name(b, "_MainTex");
            b.AddRange(Le(0u, 0u, 1u, 4u));
        }, out _);

        Assert.That(BlobTool.ParseVulkanTextureParams(blob), Is.Empty);
    }
}
