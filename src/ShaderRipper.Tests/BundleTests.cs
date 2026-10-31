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

    // ---- a bundle writer for the tests --------------------------------------

    private sealed class Builder
    {
        public uint Version = 8;
        public bool PadTo16 = true;
        public bool BlocksInfoAtEnd;
        public bool CompressInfo;
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

            // One block, stored uncompressed: raw == compressed.  That is the case
            // where a misaligned read cannot be detected by trying to decode.
            var info = new MemoryStream();
            info.Write(new byte[16], 0, 16);                       // hash, unused
            WriteBE(info, 1);                                      // one block
            WriteBE(info, (uint)blockData.Length);
            WriteBE(info, (uint)blockData.Length);
            WriteBE16(info, 0x40);                                 // compression flags
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

            var head = new MemoryStream();
            head.Write(System.Text.Encoding.ASCII.GetBytes("UnityFS"), 0, 7);
            head.WriteByte(0);
            WriteBE(head, Version);
            WriteCStr(head, "5.x.x");
            WriteCStr(head, "2022.3.33f1");
            long sizePosition = head.Position;
            WriteBE64(head, 0);                                    // patched below
            WriteBE(head, (uint)blockInfo.Length);
            WriteBE(head, (uint)blockInfo.Length);
            WriteBE(head, BlocksInfoAtEnd ? FlagBlocksInfoAtEnd : 0);

            if (BlocksInfoAtEnd)
            {
                // Table last: header, then the block data, then the table.
                if (PadTo16) Pad16(head);
                head.Write(blockData, 0, blockData.Length);
                if (PadTo16) Pad16(head);
                head.Write(blockInfo, 0, blockInfo.Length);
            }
            else
            {
                // Table first: header, then the table, then the block data.
                long infoAt = PadTo16 ? (head.Position + 15) & ~15L : head.Position;
                head.SetLength(infoAt);
                head.Write(new byte[infoAt - head.Position], 0, (int)(infoAt - head.Position));
                head.Write(blockInfo, 0, blockInfo.Length);

                long dataAt = PadTo16 ? (head.Position + 15) & ~15L : head.Position;
                head.SetLength(dataAt);
                head.Write(new byte[dataAt - head.Position], 0, (int)(dataAt - head.Position));
                head.Write(blockData, 0, blockData.Length);
            }

            var result = head.ToArray();
            long total = result.Length;
            for (int i = 0; i < 8; i++) result[(int)sizePosition + i] = (byte)(total >> (56 - 8 * i));
            return result;
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
