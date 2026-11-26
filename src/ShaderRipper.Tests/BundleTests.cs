using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// The UnityFS reader is exercised against bundle bytes assembled here, because
/// the bug it guards against cannot be seen in a log: an unaligned block table
/// decodes into plausible-looking garbage, and every node inside it then fails to
/// parse as a SerializedFile.  One such build turned 357 shaders into failures
/// with a single 3-byte offset.
/// </summary>
[TestFixture]
public class BundleTests
{
    private const uint FlagBlocksInfoAtEnd = 0x80;
    private const uint FlagDataPadding = 0x200;

    // ---- a bundle writer for the tests --------------------------------------

    private sealed class Builder
    {
        public uint Version = 8;
        public string UnityVersion = "2022.3.33f1";
        public bool PadTo16 = true;
        public bool? PadData;
        public bool DataPaddingFlag;
        public bool BlocksInfoAtEnd;
        public bool LzmaBlock;
        public bool LzmaInfo;
        public byte[] EncodedLzmaBlock;
        public List<(string path, byte[] data)> Nodes = new();

        public byte[] Build()
        {
            var nodeStream = new MemoryStream();
            var table = new List<(long off, long len, string path)>();
            foreach (var (path, data) in Nodes)
            {
                table.Add((nodeStream.Position, data.Length, path));
                nodeStream.Write(data, 0, data.Length);
            }
            byte[] blockData = nodeStream.ToArray();
            byte[] stored = EncodedLzmaBlock ?? (LzmaBlock ? EncodeLzma(blockData) : blockData);
            var info = new MemoryStream();
            info.Write(new byte[16], 0, 16);                       // hash, unused
            WriteBE(info, 1);                                      // one block
            WriteBE(info, (uint)blockData.Length);
            WriteBE(info, (uint)stored.Length);
            WriteBE16(info, LzmaBlock || EncodedLzmaBlock != null ? (ushort)0x41 : (ushort)0x40);
            WriteBE(info, (uint)table.Count);
            foreach (var (off, len, path) in table)
            {
                WriteBE64(info, off);
                WriteBE64(info, len);
                WriteBE(info, 0);
                var bytes = System.Text.Encoding.UTF8.GetBytes(path);
                info.Write(bytes, 0, bytes.Length);
                info.WriteByte(0);
            }
            byte[] blockInfo = info.ToArray();
            byte[] storedInfo = LzmaInfo ? EncodeLzma(blockInfo) : blockInfo;

            var head = new MemoryStream();
            head.Write(System.Text.Encoding.ASCII.GetBytes("UnityFS"), 0, 7);
            head.WriteByte(0);
            WriteBE(head, Version);
            WriteCStr(head, "5.x.x");
            WriteCStr(head, UnityVersion);
            long sizePosition = head.Position;
            WriteBE64(head, 0);                                    // patched below
            WriteBE(head, (uint)storedInfo.Length);
            WriteBE(head, (uint)blockInfo.Length);
            WriteBE(head, (BlocksInfoAtEnd ? FlagBlocksInfoAtEnd : 0) |
                          (DataPaddingFlag ? FlagDataPadding : 0) | (LzmaInfo ? 1u : 0u));

            if (BlocksInfoAtEnd)
            {
                // Table last: header, then the block data, then the table.
                if (PadTo16) Pad16(head);
                head.Write(stored, 0, stored.Length);
                if (PadTo16) Pad16(head);
                head.Write(storedInfo);
            }
            else
            {
                // Table first: header, then the table, then the block data.
                if (PadTo16) Pad16(head);
                head.Write(storedInfo);
                if (PadData ?? PadTo16) Pad16(head);
                head.Write(stored);
            }

            var result = head.ToArray();
            long total = result.Length;
            for (int i = 0; i < 8; i++) result[(int)sizePosition + i] = (byte)(total >> (56 - 8 * i));
            return result;
        }

        private static byte[] EncodeLzma(byte[] data)
        {
            var encoder = new SevenZip.Compression.LZMA.Encoder();
            using var source = new MemoryStream(data);
            using var compressed = new MemoryStream();
            encoder.WriteCoderProperties(compressed);
            encoder.Code(source, compressed, source.Length, -1, null);
            return compressed.ToArray();
        }

        private static void Pad16(Stream s)
        {
            long pad = (s.Position + 15 & ~15L) - s.Position;
            s.Write(new byte[pad], 0, (int)pad);
        }

        private static void WriteBE(Stream s, uint v)
        {
            for (int i = 3; i >= 0; i--) s.WriteByte((byte)(v >> (8 * i)));
        }

        private static void WriteBE16(Stream s, ushort v)
        {
            s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)v);
        }

        private static void WriteBE64(Stream s, long v)
        {
            for (int i = 7; i >= 0; i--) s.WriteByte((byte)(v >> (8 * i)));
        }

        private static void WriteCStr(Stream s, string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            s.Write(bytes, 0, bytes.Length);
            s.WriteByte(0);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReadsLzmaDataBlocksWithUnityFsPropertiesOnly(bool infoAtEnd)
    {
        byte[] data = Enumerable.Repeat((byte)0xA5, 8192).ToArray();
        var builder = new Builder { LzmaBlock = true, LzmaInfo = true, BlocksInfoAtEnd = infoAtEnd };
        builder.Nodes.Add(("CAB-test", data));
        Assert.That(Bundle.Read(builder.Build()).Single().Data, Is.EqualTo(data));
    }

    [Test]
    public void ReadsCompressedBlockWhenTheStoredAndRawSizesMatch()
    {
        byte[] data = new byte[17];
        var builder = new Builder
        {
            EncodedLzmaBlock = Convert.FromHexString("5D0010000000006D9F497BFFFFF0004000"),
        };
        builder.Nodes.Add(("CAB-test", data));
        Assert.That(Bundle.Read(builder.Build()).Single().Data, Is.EqualTo(data));
    }

    [TestCase(6u, "2019.4.14f1", false)]
    [TestCase(6u, "2019.4.15f1", false)]
    [TestCase(7u, "2019.4.15f1", false)]
    [TestCase(8u, "2022.3.34f1", true)]
    public void HeaderAndDataAlignmentAreIndependent(uint version, string revision, bool padData)
    {
        var builder = new Builder
        {
            Version = version, UnityVersion = revision, PadData = padData, DataPaddingFlag = padData,
        };
        byte[] data = Payload("original uncompressed bytes");
        builder.Nodes.Add(("CAB-test", data));
        Assert.That(Bundle.Read(builder.Build()).Single().Data, Is.EqualTo(data));
    }

    [Test]
    public void UnalignedDataBeforeTheFooterDoesNotReadTheBlockTable()
    {
        var builder = new Builder { Version = 6, UnityVersion = "2019.4.14f1", PadTo16 = false, BlocksInfoAtEnd = true };
        byte[] data = Payload("payload");
        builder.Nodes.Add(("CAB-test", data));
        Assert.That(Bundle.Read(builder.Build()).Single().Data, Is.EqualTo(data));
    }

    [Test]
    public void DeclaredBundleEndResolvesUnflaggedPaddingBeforeRawData()
    {
        var builder = new Builder();
        var data = new byte[32];
        data[0] = 0x12;
        builder.Nodes.Add(("CAB-test", data));
        Assert.That(Bundle.Read(builder.Build()).Single().Data, Is.EqualTo(data));
    }

    private static byte[] Payload(string marker)
    {
        // Any bytes will do; the reader only slices nodes out.
        return System.Text.Encoding.ASCII.GetBytes(marker);
    }

    // ---- the regression ------------------------------------------------------

    /// <summary>
    /// UnityFS v7+ pads the block table and the block data to 16 bytes.  Reading
    /// the data at the position right after the table instead -- three bytes
    /// earlier in the build this came from -- shifts the whole node stream and
    /// every node becomes garbage.
    /// </summary>
    [Test]
    public void BlockDataIsReadAtTheAlignedPosition()
    {
        var builder = new Builder { PadTo16 = true };
        builder.Nodes.Add(("CAB-first", Payload("node one")));
        builder.Nodes.Add(("CAB-second", Payload("node two")));

        var nodes = Bundle.Read(builder.Build());

        Assert.That(nodes, Has.Count.EqualTo(2));
        Assert.That(nodes[0].Path, Is.EqualTo("CAB-first"));
        Assert.That(System.Text.Encoding.ASCII.GetString(nodes[0].Data), Is.EqualTo("node one"));
        Assert.That(nodes[1].Path, Is.EqualTo("CAB-second"));
        Assert.That(System.Text.Encoding.ASCII.GetString(nodes[1].Data), Is.EqualTo("node two"));
    }

    /// <summary>
    /// A pre-v7 writer did not pad, and its bundle must still read: the aligned
    /// position is tried first for every build, but a version that predates the
    /// padding rule is not held to it.
    /// </summary>
    [Test]
    public void UnpaddedBundleStillReads()
    {
        var builder = new Builder { PadTo16 = false, Version = 6 };
        builder.Nodes.Add(("CAB-only", Payload("payload")));

        var nodes = Bundle.Read(builder.Build());

        Assert.That(nodes, Has.Count.EqualTo(1));
        Assert.That(System.Text.Encoding.ASCII.GetString(nodes[0].Data), Is.EqualTo("payload"));
    }

    [Test]
    public void BlocksInfoAtEndLayoutReads()
    {
        var builder = new Builder { PadTo16 = true, BlocksInfoAtEnd = true };
        builder.Nodes.Add(("CAB-end", Payload("at end")));

        var nodes = Bundle.Read(builder.Build());

        Assert.That(nodes, Has.Count.EqualTo(1));
        Assert.That(nodes[0].Path, Is.EqualTo("CAB-end"));
    }

    [Test]
    public void NodeOrderIsPreserved()
    {
        var builder = new Builder();
        for (int i = 0; i < 5; i++) builder.Nodes.Add(($"CAB-{i}", Payload("x" + i)));

        var nodes = Bundle.Read(builder.Build());

        CollectionAssert.AreEqual(new[] { "CAB-0", "CAB-1", "CAB-2", "CAB-3", "CAB-4" },
                                  nodes.Select(n => n.Path).ToArray());
    }

    [Test]
    public void AnEmptyBundleYieldsNoNodes()
    {
        Assert.That(Bundle.Read(new Builder().Build()), Is.Empty);
    }

    // ---- rejection -----------------------------------------------------------

    [Test]
    public void NonBundleBytesAreRejected()
    {
        var notBundle = System.Text.Encoding.ASCII.GetBytes("this is not a UnityFS container at all");
        Assert.Throws<InvalidDataException>(() => Bundle.Read(notBundle));
    }

    [Test]
    public void IsBundleBytesOnlyAcceptsTheMagic()
    {
        Assert.That(Bundle.IsBundleBytes(System.Text.Encoding.ASCII.GetBytes("UnityFS\0")), Is.True);
        Assert.That(Bundle.IsBundleBytes(System.Text.Encoding.ASCII.GetBytes("UnityFS")), Is.True);
        Assert.That(Bundle.IsBundleBytes(System.Text.Encoding.ASCII.GetBytes("UnityFX\0")), Is.False);
        Assert.That(Bundle.IsBundleBytes(null), Is.False);
        Assert.That(Bundle.IsBundleBytes(Array.Empty<byte>()), Is.False);
    }

    [Test]
    public void IsBundleReadsTheMagicFromDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sr-bundle-{Guid.NewGuid():N}.bin");
        try
        {
            var builder = new Builder();
            builder.Nodes.Add(("CAB-x", Payload("x")));
            File.WriteAllBytes(path, builder.Build());
            Assert.That(Bundle.IsBundle(path), Is.True);

            File.WriteAllText(path, "plain text");
            Assert.That(Bundle.IsBundle(path), Is.False);

            File.Delete(path);
            Assert.That(Bundle.IsBundle(path), Is.False);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
