using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// Blob format detection decides which route a payload takes, so a wrong answer
/// either produces garbage or silently skips a translatable program.  Every case
/// here is a Magic number the real decoders rely on.
/// </summary>
[TestFixture]
public class BlobFormatDetectTests
{
    private static byte[] Bytes(params uint[] words)
    {
        var bytes = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
        {
            bytes[i * 4] = (byte)words[i];
            bytes[i * 4 + 1] = (byte)(words[i] >> 8);
            bytes[i * 4 + 2] = (byte)(words[i] >> 16);
            bytes[i * 4 + 3] = (byte)(words[i] >> 24);
        }
        return bytes;
    }

    private static byte[] Ascii(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    [Test]
    public void DxbcMagicIsRecognised()
    {
        Assert.That(BlobFormatDetect.Detect(Bytes(0x43425844)), Is.EqualTo(BlobFormat.Dxbc));
    }

    [Test]
    public void MetalArchiveMagicIsRecognised()
    {
        Assert.That(BlobFormatDetect.Detect(Bytes(0xF00DCAFE, 8)), Is.EqualTo(BlobFormat.Metal));
    }

    [Test]
    public void SpirVMagicIsRecognisedInBothByteOrders()
    {
        Assert.That(BlobFormatDetect.Detect(Bytes(0x07230203)), Is.EqualTo(BlobFormat.SpirV));
        Assert.That(BlobFormatDetect.Detect(Bytes(0x03022307)), Is.EqualTo(BlobFormat.SpirV));
    }

    [Test]
    public void GlslIsRecognisedByItsLeadingDirective()
    {
        Assert.That(BlobFormatDetect.Detect(Ascii("#version 300 es\nvoid main() {}\n")),
            Is.EqualTo(BlobFormat.Glsl));
    }

    [Test]
    public void GlslIsRecognisedPastAUtf8BomAndBlankLines()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Ascii("\n\t\r\n#define FOO 1\n"))
            .ToArray();
        Assert.That(BlobFormatDetect.Detect(bytes), Is.EqualTo(BlobFormat.Glsl));
    }

    [Test]
    public void UnknownPayloadStaysUnknown()
    {
        Assert.That(BlobFormatDetect.Detect(Ascii("not a shader payload at all")),
            Is.EqualTo(BlobFormat.Unknown));
        Assert.That(BlobFormatDetect.Detect(null), Is.EqualTo(BlobFormat.Unknown));
        Assert.That(BlobFormatDetect.Detect(Array.Empty<byte>()), Is.EqualTo(BlobFormat.Unknown));
    }

    [Test]
    public void MetalSplitReturnsTheEntryPointAndSourceOffset()
    {
        // 0xF00DCAFE, then the offset of the entry name relative to the archive
        // start, then a NUL-terminated entry name and the MSL source.
        var head = Bytes(0xF00DCAFE, 8);
        var body = Ascii("main0\0kernel void main0() {}");
        var bytes = head.Concat(body).ToArray();

        Assert.That(BlobFormatDetect.MetalSplit(bytes, 0, out string entry, out int src), Is.True);
        Assert.That(entry, Is.EqualTo("main0"));
        Assert.That(System.Text.Encoding.ASCII.GetString(bytes, src, 20), Does.StartWith("kernel void main0"));
    }

    [Test]
    public void MetalSplitRejectsANonMetalPayload()
    {
        Assert.That(BlobFormatDetect.MetalSplit(Ascii("#version 300 es\n"), 0, out _, out _), Is.False);
    }

    [Test]
    public void FindProgramLocatesThePayloadBehindTheSlotHeader()
    {
        // A real slot starts with a per-program header and constant-buffer tables,
        // so the payload is never at offset 0.
        var bytes = Bytes(0x0C0A75BA, 0, 0, 0)
            .Concat(Bytes(0x43425844))
            .Concat(Bytes(0, 0, 0, 0))
            .ToArray();

        var (fmt, at) = BlobFormatDetect.FindProgram(bytes, 0, bytes.Length);
        Assert.That(fmt, Is.EqualTo(BlobFormat.Dxbc));
        Assert.That(at, Is.EqualTo(16));
    }

    [Test]
    public void FindProgramReturnsUnknownWhenNothingMatches()
    {
        var (fmt, at) = BlobFormatDetect.FindProgram(Ascii("nothing here"), 0, 12);
        Assert.That(fmt, Is.EqualTo(BlobFormat.Unknown));
        Assert.That(at, Is.EqualTo(-1));
    }

    [Test]
    public void FindProgramLocatesGlslBehindPadding()
    {
        var bytes = new byte[] { 0xAA, 0xBB, 0xCC }
            .Concat(Ascii("#version 300 es\n"))
            .ToArray();

        var (fmt, _) = BlobFormatDetect.FindProgram(bytes, 0, bytes.Length);
        Assert.That(fmt, Is.EqualTo(BlobFormat.Glsl));
    }

    [Test]
    public void HexIsLowercaseAndBounded()
    {
        Assert.That(BlobFormatDetect.Hex(new byte[] { 0x00, 0x1F, 0xFF }), Is.EqualTo("001fff"));
        Assert.That(BlobFormatDetect.Hex(new byte[] { 0x00, 0x1F, 0xFF }, 1, 1), Is.EqualTo("1f"));
    }
}
