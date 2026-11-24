using System;
using System.Linq;
using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// DXBC container rewriting.  The chunk table has to describe the bytes it was
/// written for and every chunk that was already there has to survive unchanged --
/// a table off by one entry gives a container no disassembler or HLSLcc will read,
/// and a dropped chunk loses the code itself.
/// </summary>
[TestFixture]
public class DxbcContainerTests
{
    /// <summary>A container of the given chunks, written independently of the code under test.</summary>
    private static byte[] Build(params (string code, byte[] payload)[] chunks)
    {
        int table = 32 + 4 * chunks.Length;
        int total = table + chunks.Sum(c => 8 + c.payload.Length);
        var data = new byte[total];
        Magic(data, 0, 0x43425844);
        Magic(data, 20, 1);
        Magic(data, 24, (uint)total);
        Magic(data, 28, (uint)chunks.Length);

        int at = table;
        for (int k = 0; k < chunks.Length; k++)
        {
            Magic(data, 32 + 4 * k, (uint)at);
            for (int i = 0; i < 4; i++) data[at + i] = (byte)chunks[k].code[i];
            Magic(data, at + 4, (uint)chunks[k].payload.Length);
            Buffer.BlockCopy(chunks[k].payload, 0, data, at + 8, chunks[k].payload.Length);
            at += 8 + chunks[k].payload.Length;
        }
        return data;
    }

    private static void Magic(byte[] data, int at, uint value)
    {
        data[at] = (byte)value;
        data[at + 1] = (byte)(value >> 8);
        data[at + 2] = (byte)(value >> 16);
        data[at + 3] = (byte)(value >> 24);
    }

    private static (string code, byte[] payload)[] Chunks(byte[] container)
    {
        int count = BitConverter.ToInt32(container, 28);
        var chunks = new (string, byte[])[count];
        for (int k = 0; k < count; k++)
        {
            int at = BitConverter.ToInt32(container, 32 + 4 * k);
            string code = new(new[] { (char)container[at], (char)container[at + 1],
                                      (char)container[at + 2], (char)container[at + 3] });
            int size = BitConverter.ToInt32(container, at + 4);
            var payload = new byte[size];
            Buffer.BlockCopy(container, at + 8, payload, 0, size);
            chunks[k] = (code, payload);
        }
        return chunks;
    }

    private static byte[] Payload(params byte[] values) => values;

    [Test]
    public void RdefGoesFirstAndEveryOtherChunkSurvives()
    {
        var original = Build(("ISGN", Payload(1, 2, 3, 4)),
                             ("OSGN", Payload(5, 6, 7, 8)),
                             ("SHEX", Payload(9, 10, 11, 12, 13, 14, 15, 16)));
        var rdef = Build(("RDEF", Payload(0xAA, 0xBB)));

        // A chunk is code, size and payload exactly as it sits in its own container.
        var chunk = DxbcContainer.Chunk(rdef, "RDEF");
        var patched = DxbcContainer.WithFirstChunk(original, chunk);

        Assert.That(patched, Is.Not.Null);
        Assert.That(BitConverter.ToUInt32(patched, 0), Is.EqualTo(0x43425844u));
        Assert.That(BitConverter.ToInt32(patched, 24), Is.EqualTo(patched.Length),
                    "the size field has to match the bytes written");
        Assert.That(BitConverter.ToUInt32(patched, 28), Is.EqualTo(4u),
                    "the table has to hold one offset per chunk");

        var chunks = Chunks(patched);
        Assert.That(chunks.Select(c => c.code), Is.EqualTo(new[] { "RDEF", "ISGN", "OSGN", "SHEX" }));
        Assert.That(chunks[0].payload, Is.EqualTo(new byte[] { 0xAA, 0xBB }));
        Assert.That(chunks[1].payload, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        Assert.That(chunks[3].payload, Is.EqualTo(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }));
        Assert.That(patched.Skip(4).Take(16).Any(value => value != 0), Is.True,
                    "the modified container must carry a new checksum");
        Assert.That(DxbcContainer.HasSameProgram(original, patched), Is.True);
        Assert.That(DxbcContainer.HasSameProgram(patched,
            Build(("ISGN", Payload(4, 3, 2, 1)), ("OSGN", Payload(5, 6, 7, 8)),
                  ("SHEX", Payload(9, 10, 11, 12, 13, 14, 15, 16)))), Is.False,
            "identical instructions do not make different signatures interchangeable");
    }

    [Test]
    public void AnExistingRdefIsReplacedNotDuplicated()
    {
        var original = Build(("RDEF", Payload(1)), ("SHEX", Payload(2, 3)));
        var replacement = DxbcContainer.Chunk(Build(("RDEF", Payload(9, 9, 9))), "RDEF");

        var patched = DxbcContainer.WithFirstChunk(original, replacement);

        var chunks = Chunks(patched);
        Assert.That(chunks.Select(c => c.code), Is.EqualTo(new[] { "RDEF", "SHEX" }));
        Assert.That(chunks[0].payload, Is.EqualTo(new byte[] { 9, 9, 9 }));
        Assert.That(chunks[1].payload, Is.EqualTo(new byte[] { 2, 3 }));
    }

    [Test]
    public void ChunkLookupReturnsWhatTheContainerCarries()
    {
        var container = Build(("RDEF", Payload(7, 8, 9)), ("SHEX", Payload(1)));

        Assert.That(DxbcContainer.HasChunk(container, "RDEF"), Is.True);
        Assert.That(DxbcContainer.HasChunk(container, "STAT"), Is.False);
        Assert.That(DxbcContainer.Chunk(container, "STAT"), Is.Null);
        var chunk = DxbcContainer.Chunk(container, "RDEF");
        Assert.That(chunk.Length, Is.EqualTo(8 + 3));
        Assert.That(new string(chunk.Take(4).Select(b => (char)b).ToArray()), Is.EqualTo("RDEF"));
    }

    [Test]
    public void AMalformedContainerIsRejectedInsteadOfRewritten()
    {
        Assert.That(DxbcContainer.IsContainer(null), Is.False);
        Assert.That(DxbcContainer.IsContainer(new byte[8]), Is.False);
        Assert.That(DxbcContainer.WithFirstChunk(new byte[64], new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }),
                    Is.Null);

        // Offsets past the end of the data cannot be copied out faithfully.
        var broken = Build(("SHEX", Payload(1, 2, 3, 4)));
        Magic(broken, 32, 0x7FFF);
        Assert.That(DxbcContainer.WithFirstChunk(broken, DxbcContainer.Chunk(Build(("RDEF", Payload(1))), "RDEF")),
                    Is.Null);

        // A chunk shorter than its own header is not a chunk.
        Assert.That(DxbcContainer.WithFirstChunk(Build(("SHEX", Payload(1))), new byte[4]), Is.Null);
    }
}
