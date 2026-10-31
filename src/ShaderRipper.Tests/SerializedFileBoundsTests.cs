using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// A SerializedFile's header fields are attacker-controlled in the sense that they
/// are just bytes: a truncated or foreign container can put any 32-bit value where
/// the counts live.  Every count is used to size an allocation, so a bad one used
/// to ask for gigabytes before a single element had been validated -- the failure
/// mode was an OutOfMemoryException, which says nothing about what went wrong.
///
/// These tests build minimal files with a deliberately corrupt count and assert the
/// reader refuses them with a diagnosable message instead.
/// </summary>
[TestFixture]
public class SerializedFileBoundsTests
{
    /// <summary>Build a v21 serialized-file header with the given type/object counts.</summary>
    private static byte[] Build(int typeCount, int objectCount, int typeEntries = 1,
                                int objectEntries = 1, int metadataSize = 0x400)
    {
        var body = new MemoryStream();
        void BE32(uint v)
        {
            body.WriteByte((byte)(v >> 24));
            body.WriteByte((byte)(v >> 16));
            body.WriteByte((byte)(v >> 8));
            body.WriteByte((byte)v);
        }
        void LE32(int v)
        {
            body.WriteByte((byte)v);
            body.WriteByte((byte)(v >> 8));
            body.WriteByte((byte)(v >> 16));
            body.WriteByte((byte)(v >> 24));
        }
        void LE16(short v)
        {
            body.WriteByte((byte)v);
            body.WriteByte((byte)(v >> 8));
        }

        BE32((uint)metadataSize);        // 0x00 metadataSize
        BE32(0x800);                     // 0x04 fileSize
        BE32(21);                        // 0x08 version
        BE32(0x400);                     // 0x0c dataOffset
        LE32(0);                         // 0x10 endianness; the cursor starts at 0x14
        // Verified against a real Unity 2019.3 file: the version string begins
        // immediately at 0x14, and targetPlatform / enableTypeTree / typeCount
        // follow its NUL terminator.
        foreach (char c in "2019.4.34f1") body.WriteByte((byte)c);
        body.WriteByte(0);
        LE32(19);                        // targetPlatform
        body.WriteByte(0);               // enableTypeTree = false
        LE32(typeCount);
        for (int t = 0; t < typeEntries; t++)
        {
            LE32(115);                   // classId
            body.WriteByte(0);           // stripped
            LE16(-1);                    // scriptId
            body.Write(new byte[16], 0, 16);
        }
        LE32(objectCount);
        for (int o = 0; o < objectEntries; o++)
        {
            body.WriteByte(0);           // align
            LE32(1);                     // pathId (v<14 uses i32; v21 uses i64 -- see below)
        }

        var array = body.ToArray();
        return array;
    }

    private static Func<string, int, TypeNode> NoSchema => (_, _) => null;

    [Test]
    public void AnImplausibleObjectCountIsRejectedWithoutAllocatingIt()
    {
        // 1.4 billion: enough that `new List<>(count)` would have thrown OOM.
        var bytes = Build(typeCount: 1, objectCount: 1460844575);

        var ex = Assert.Throws<InvalidDataException>(
            () => SerializedFile.Load(bytes, NoSchema));

        Assert.That(ex.Message, Does.Contain("implausible object count"));
        Assert.That(ex.Message, Does.Contain("1460844575"));
    }

    [Test]
    public void AnImplausibleTypeCountIsRejectedWithoutAllocatingIt()
    {
        var bytes = Build(typeCount: 200_000_000, objectCount: 0);

        var ex = Assert.Throws<InvalidDataException>(
            () => SerializedFile.Load(bytes, NoSchema));

        Assert.That(ex.Message, Does.Contain("implausible type count"));
    }

    [Test]
    public void ANegativeObjectCountIsRejected()
    {
        var bytes = Build(typeCount: 1, objectCount: -5);
        var ex = Assert.Throws<InvalidDataException>(
            () => SerializedFile.Load(bytes, NoSchema));
        Assert.That(ex.Message, Does.Contain("implausible object count"));
    }

    [Test]
    public void ANegativeTypeCountIsRejected()
    {
        var bytes = Build(typeCount: -1, objectCount: 0);
        var ex = Assert.Throws<InvalidDataException>(
            () => SerializedFile.Load(bytes, NoSchema));
        Assert.That(ex.Message, Does.Contain("implausible type count"));
    }

    /// <summary>
    /// A count that is merely large but still fits the metadata is not rejected:
    /// the check is against the bytes that are actually left, not a magic ceiling.
    /// </summary>
    [Test]
    public void ACountThatFitsTheMetadataIsNotRejected()
    {
        var bytes = Build(typeCount: 1, objectCount: 0, metadataSize: 0x400);
        Assert.DoesNotThrow(() => SerializedFile.Load(bytes, NoSchema));
    }
}
