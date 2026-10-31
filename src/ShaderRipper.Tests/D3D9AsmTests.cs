using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// The d3d9 disassembler reads a bare token stream, so these tests build the
/// tokens by hand from the field layout in <c>d3d9types.h</c> and check the
/// exact text fxc's old spelling would produce.  There is no compiler to
/// round-trip against (d3dcompiler only accepts DXBC), which is precisely why
/// the encoding is asserted here byte for byte.
/// </summary>
[TestFixture]
public class D3D9AsmTests
{
    private const uint End = 0x0000FFFF;

    // ---- token builders ------------------------------------------------------

    /// <summary>Assemble words into a little-endian byte stream.</summary>
    private static byte[] Stream(params uint[] words)
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

    /// <summary>Version token: kind 0xFFFE = vertex, 0xFFFF = pixel.</summary>
    private static uint Version(bool vertex, int major, int minor) =>
        (uint)(((vertex ? 0xFFFE : 0xFFFF) << 16) | (major << 8) | minor);

    /// <summary>Instruction token: opcode, opcode-specific control, operand count.</summary>
    private static uint Instr(int opcode, int operands, int control = 0) =>
        (uint)(opcode | (control << 16) | (operands << 24));

    /// <summary>
    /// Operand token with bit 31 set.  The register type is split across two
    /// fields: bits 28-30 hold the low three bits, bits 11-12 the high two.
    /// </summary>
    private static uint Operand(int type, int num) =>
        0x80000000u
        | (uint)(type & 0x7) << 28
        | (uint)((type >> 3) & 0x3) << 11
        | (uint)(num & 0x7FF);

    /// <summary>A source operand: bits 16-23 are the swizzle, bits 24-27 the modifier.</summary>
    private static uint Src(int type, int num, int swizzle = 0xE4, int modifier = 0) =>
        Operand(type, num) | (uint)(swizzle & 0xFF) << 16 | (uint)(modifier & 0xF) << 24;

    /// <summary>A destination operand: bits 16-19 are the write mask, bits 20-23 the modifier.</summary>
    private static uint Dst(int type, int num, int mask = 0xF, int modifier = 0) =>
        Operand(type, num) | (uint)(mask & 0xF) << 16 | (uint)(modifier & 0xF) << 20;

    private static string Disassemble(byte[] bytes) => D3D9Asm.Disassemble(bytes, 0, bytes.Length);

    // ---- happy paths ---------------------------------------------------------

    [Test]
    public void PixelShaderVersionAndOneMov()
    {
        // ps_3_0 / mov oC0, c0 / END
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(2, 0), End);

        Assert.That(Disassemble(bytes), Is.EqualTo("    ps_3_0\n    mov oC0, c0\n"));
    }

    [Test]
    public void VertexShaderPicksVsPrefixAndThePositionOutput()
    {
        var bytes = Stream(Version(true, 3, 0), Instr(1, 2), Dst(4, 0), Src(2, 0), End);

        Assert.That(Disassemble(bytes), Is.EqualTo("    vs_3_0\n    mov oPos, c0\n"));
    }

    [Test]
    public void MinorVersionSpellingsMatchFxc()
    {
        foreach (var (minor, suffix) in new[] { (0, "_0"), (0x0A, "_a"), (0x0B, "_b") })
        {
            var bytes = Stream(Version(false, 3, minor), Instr(1, 2), Dst(8, 0), Src(2, 0), End);
            Assert.That(Disassemble(bytes), Does.StartWith($"    ps_3{suffix}\n"));
        }
        // 0x0F spells _sw for a vertex shader and _x for a pixel shader.
        var vs = Stream(Version(true, 3, 0x0F), Instr(1, 2), Dst(4, 0), Src(2, 0), End);
        var ps = Stream(Version(false, 3, 0x0F), Instr(1, 2), Dst(8, 0), Src(2, 0), End);
        Assert.That(Disassemble(vs), Does.StartWith("    vs_3_sw\n"));
        Assert.That(Disassemble(ps), Does.StartWith("    ps_3_x\n"));
    }

    [Test]
    public void StageIsReportedAlongsideTheText()
    {
        var ps = Stream(Version(false, 2, 0), Instr(1, 2), Dst(8, 0), Src(2, 0), End);
        var vs = Stream(Version(true, 2, 0), Instr(1, 2), Dst(4, 0), Src(2, 0), End);

        D3D9Asm.Disassemble(ps, 0, ps.Length, out string psStage);
        D3D9Asm.Disassemble(vs, 0, vs.Length, out string vsStage);

        Assert.That(psStage, Is.EqualTo("ps"));
        Assert.That(vsStage, Is.EqualTo("vs"));
    }

    [Test]
    public void TextureDeclarationNamesItsDimension()
    {
        // The declaration parameter is not an operand token (bit 31 clear); its
        // texture-type field sits in bits 27-30.
        var bytes = Stream(Version(false, 3, 0), Instr(31, 2), 2u << 27, Operand(10, 0), End);
        Assert.That(Disassemble(bytes), Is.EqualTo("    ps_3_0\n    dcl_2d s0\n"));
    }

    [Test]
    public void CubeAndVolumeDeclarationsUseTheirOwnKeyword()
    {
        var cube = Stream(Version(false, 3, 0), Instr(31, 2), 3u << 27, Operand(10, 1), End);
        var volume = Stream(Version(false, 3, 0), Instr(31, 2), 4u << 27, Operand(10, 2), End);

        Assert.That(Disassemble(cube), Does.Contain("dcl_cube s1"));
        Assert.That(Disassemble(volume), Does.Contain("dcl_volume s2"));
    }

    [Test]
    public void VertexDeclarationUsesTheUsageAndIndex()
    {
        // vs_3_0 / dcl_position v0: usage 0 in bits 0-3, index 0 in bits 16-19.
        var plain = Stream(Version(true, 3, 0), Instr(31, 2), 0u, Operand(1, 0), End);
        // usage 5 (texcoord) with index 2 -> dcl_texcoord2
        var indexed = Stream(Version(true, 3, 0), Instr(31, 2), 5u | 2u << 16, Operand(1, 1), End);

        Assert.That(Disassemble(plain), Does.Contain("dcl_position v0"));
        Assert.That(Disassemble(indexed), Does.Contain("dcl_texcoord2 v1"));
    }

    [Test]
    public void SamplerTextureLoadKeepsItsThreeOperands()
    {
        var bytes = Stream(Version(false, 3, 0),
            Instr(66, 3), Dst(0, 0), Src(3, 0), Src(10, 0), End);

        Assert.That(Disassemble(bytes), Is.EqualTo("    ps_3_0\n    texld r0, t0, s0\n"));
    }

    [Test]
    public void TexldControlBitsSelectProjectAndBiasVariants()
    {
        var projected = Stream(Version(false, 3, 0),
            Instr(66, 3, control: 1), Dst(0, 0), Src(3, 0), Src(10, 0), End);
        var biased = Stream(Version(false, 3, 0),
            Instr(66, 3, control: 2), Dst(0, 0), Src(3, 0), Src(10, 0), End);

        Assert.That(Disassemble(projected), Does.Contain("texldp r0, t0, s0"));
        Assert.That(Disassemble(biased), Does.Contain("texldb r0, t0, s0"));
    }

    [Test]
    public void ComparisonOpcodesTakeTheirSuffixFromTheControlBits()
    {
        // ifc with comparison code 2 = "eq"; setp with 4 = "lt".
        var ifc = Stream(Version(false, 3, 0), Instr(41, 2, control: 2), Src(0, 0), Src(0, 1), End);
        var setp = Stream(Version(false, 3, 0), Instr(94, 3, control: 4), Dst(0, 0), Src(0, 1), Src(0, 2), End);

        Assert.That(Disassemble(ifc), Does.Contain("if_eq r0, r1"));
        Assert.That(Disassemble(setp), Does.Contain("setp_lt r0, r1, r2"));
    }

    [Test]
    public void WriteMaskBecomesATrailingSwizzle()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0, mask: 0x3), Src(2, 0), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oC0.xy, c0"));
    }

    [Test]
    public void FullWriteMaskIsOmitted()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0, mask: 0xF), Src(2, 0), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oC0, c0"));
    }

    [Test]
    public void DestinationModifiersFollowTheMask()
    {
        // _sat is destination-modifier bit 0.
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0, modifier: 1), Src(2, 0), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oC0_sat, c0"));
    }

    [Test]
    public void SourceSwizzleAndNegationAreEmitted()
    {
        // Replicate x (swizzle 0x00) and negate (modifier 1).
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0),
                           Src(2, 0, swizzle: 0x00, modifier: 1), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oC0, -c0.xxxx"));
    }

    [Test]
    public void DefaultSwizzleIsNotWritten()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(2, 0, swizzle: 0xE4), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oC0, c0\n"));
    }

    [Test]
    public void SourceModifierBiasIsWrittenAsASuffix()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0),
                           Src(2, 0, modifier: 2), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oC0, c0_bias"));
    }

    [Test]
    public void DefEmitsItsFourFloatLiterals()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(81, 5), Dst(2, 0),
                           0x3F800000, 0x40000000, 0x40400000, 0x40800000,   // 1, 2, 3, 4
                           End);
        Assert.That(Disassemble(bytes), Does.Contain("def c0, 1, 2, 3, 4"));
    }

    [Test]
    public void DefiEmitsItsFourIntegerLiterals()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(48, 5), Dst(7, 0),
                           1u, 2u, 3u, 4u, End);
        Assert.That(Disassemble(bytes), Does.Contain("defi i0, 1, 2, 3, 4"));
    }

    [Test]
    public void DefbEmitsTrueOrFalse()
    {
        var on = Stream(Version(false, 3, 0), Instr(47, 2), Dst(14, 0), 1u, End);
        var off = Stream(Version(false, 3, 0), Instr(47, 2), Dst(14, 0), 0u, End);

        Assert.That(Disassemble(on), Does.Contain("defb b0, true"));
        Assert.That(Disassemble(off), Does.Contain("defb b0, false"));
    }

    [Test]
    public void CommentTokensAreSkippedWithoutSurfacing()
    {
        // A comment token: low 16 bits 0xFFFE, high bits the DWORD count.
        const uint comment = 0xFFFEu | 2u << 16;
        var bytes = Stream(Version(false, 3, 0), comment, 0xDEADBEEF, 0xCAFEBABE,
                           Instr(1, 2), Dst(8, 0), Src(2, 0), End);

        Assert.That(Disassemble(bytes), Is.EqualTo("    ps_3_0\n    mov oC0, c0\n"));
    }

    [Test]
    public void ConstantRegisterBanksArrayFromTheirBase()
    {
        // Types 11-13 are the second, third and fourth constant banks; the emitted
        // index is absolute (base + number) because that is what SM3 assembly means.
        Assert.That(Disassemble(Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(11, 5), End)),
            Does.Contain("mov oC0, c2053"));
        Assert.That(Disassemble(Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(12, 5), End)),
            Does.Contain("mov oC0, c4101"));
        Assert.That(Disassemble(Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(13, 5), End)),
            Does.Contain("mov oC0, c6149"));
    }

    [Test]
    public void RelativeAddressingEmitsTheIndexExpression()
    {
        // A source register with bit 13 set consumes a second operand as the index;
        // that index token counts towards the instruction's length field.
        uint relative = Src(2, 0) | 1u << 13;
        var bytes = Stream(Version(true, 3, 0), Instr(1, 3), Dst(4, 0),
                           relative, Src(0, 1), End);
        Assert.That(Disassemble(bytes), Does.Contain("mov oPos, c0[r1]"));
    }

    // ---- rejected input ------------------------------------------------------

    [Test]
    public void MissingEndTokenIsRejected()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(2, 0));
        Assert.That(Disassemble(bytes), Is.Null);
    }

    [Test]
    public void TruncatedInstructionIsRejected()
    {
        // The length field promises two operands but only one follows.
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), End);
        Assert.That(Disassemble(bytes), Is.Null);
    }

    [Test]
    public void OperandTokenWhereAnInstructionBelongsIsRejected()
    {
        var bytes = Stream(Version(false, 3, 0), Dst(8, 0), End);
        Assert.That(Disassemble(bytes), Is.Null);
    }

    [Test]
    public void NonD3d9BytesAreRejected()
    {
        Assert.That(Disassemble(Stream(0x43425844, 0, 0, 0)), Is.Null);
        Assert.That(Disassemble(Array.Empty<byte>()), Is.Null);
        Assert.That(D3D9Asm.Disassemble(null, 0, 0), Is.Null);
    }

    /// <summary>SM1 tokens have no length field, so the token walk cannot read them.</summary>
    [Test]
    public void ShaderModelBelowTwoIsRejected()
    {
        var bytes = Stream(Version(false, 1, 1), Instr(1, 2), Dst(8, 0), Src(2, 0), End);
        Assert.That(Disassemble(bytes), Is.Null);
        Assert.That(BlobFormatDetect.LooksD3d9(bytes, 0, bytes.Length), Is.False);
    }

    [Test]
    public void ShaderModelAboveThreeIsRejected()
    {
        var bytes = Stream(Version(false, 4, 0), Instr(1, 2), Dst(8, 0), Src(2, 0), End);
        Assert.That(Disassemble(bytes), Is.Null);
    }

    // ---- detection -----------------------------------------------------------

    [Test]
    public void DetectionAgreesWithTheDisassembler()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(2, 0), End);

        Assert.That(BlobFormatDetect.Detect(bytes), Is.EqualTo(BlobFormat.D3d9));
        Assert.That(BlobFormatDetect.LooksD3d9(bytes, 0, bytes.Length), Is.True);
        Assert.That(BlobFormatDetect.D3d9TokenEnd(bytes, 4, bytes.Length), Is.EqualTo(bytes.Length));
    }

    [Test]
    public void DetectionRejectsAStreamWithoutEnd()
    {
        var bytes = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(2, 0));
        Assert.That(BlobFormatDetect.LooksD3d9(bytes, 0, bytes.Length), Is.False);
    }

    [Test]
    public void DetectionDoesNotClaimDxbc()
    {
        var dxbc = Stream(0x43425844, 0x00000001, 0, 0);
        Assert.That(BlobFormatDetect.Detect(dxbc), Is.EqualTo(BlobFormat.Dxbc));
    }

    // ---- extraction from a platform payload ----------------------------------

    /// <summary>
    /// The decoded platform blob is a slot table: a DWORD count followed by that
    /// many <c>(offset, size, reserved)</c> triplets.  Every slot then holds a
    /// per-program header before the actual token stream, which is why the
    /// payload is never at offset 0 and the program has to be found by signature.
    /// </summary>
    [Test]
    public void ExtractFindsTheTokenStreamBehindTheSlotHeader()
    {
        var program = Stream(Version(false, 3, 0), Instr(1, 2), Dst(8, 0), Src(2, 0), End);
        const int headerSize = 40;

        // Slot table: one entry, its offset absolute in the payload.  The table
        // itself is four DWORDs, so the header starts at byte 16.
        var bytes = Stream(1, 16, (uint)(headerSize + program.Length), 0)
            .Concat(new byte[headerSize])
            .Concat(program)
            .ToArray();

        var progs = ProgramSource.Extract(bytes);

        Assert.That(progs, Has.Count.EqualTo(1));
        Assert.That(progs[0].Format, Is.EqualTo(BlobFormat.D3d9));
        Assert.That(progs[0].BlobIndex, Is.EqualTo(0));

        string asm = D3D9Asm.Disassemble(bytes, progs[0].Offset, progs[0].Length, out string stage);
        Assert.That(stage, Is.EqualTo("ps"));
        Assert.That(asm, Is.EqualTo("    ps_3_0\n    mov oC0, c0\n"));
    }

    [Test]
    public void ExtractReportsTheLengthOfTheTokenStreamOnly()
    {
        // The slot is padded past the END token; the reported length must stop at
        // it so the disassembler does not walk the padding.
        var program = Stream(Version(true, 2, 0), Instr(1, 2), Dst(4, 0), Src(2, 0), End);
        var padded = program.Concat(new byte[32]).ToArray();

        var bytes = Stream(1, 16, (uint)padded.Length, 0)
            .Concat(new byte[8])
            .Concat(padded)
            .ToArray();

        var progs = ProgramSource.Extract(bytes);

        Assert.That(progs, Has.Count.EqualTo(1));
        Assert.That(progs[0].Length, Is.EqualTo(program.Length));
    }
}
