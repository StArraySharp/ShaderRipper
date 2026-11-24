using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ShaderRipper.Data;

namespace ShaderRipper.Tests;

/// <summary>
/// The RDEF chunk rebuilt from the names the game keeps elsewhere.
///
/// The reader (HLSLcc, fxc's disassembler) walks the tables by offset, so a test
/// that only checked "bytes were written" would pass on a chunk it cannot read.
/// These parse the chunk back the way a reader does -- header, resource table,
/// constant-buffer table, member records, type records -- and check the names,
/// registers and offsets it finds.
/// </summary>
[TestFixture]
public class DxbcReflectionTests
{
    [Test]
    public void ReadTextureNamesKeepsTextureRegistersAndSkipsSamplers()
    {
        var sample = Sample();
        var names = DxbcReflection.ReadTextureNames(DxbcContainer.Wrap("RDEF", sample.Build("ps", "ps_5_0")));
        Assert.That(names, Has.Count.EqualTo(1));
        Assert.That(names[0], Is.EqualTo("_MainTex"));
    }

    // ---- a container to complete -------------------------------------------

    private static byte[] Container(params (string code, byte[] payload)[] chunks)
    {
        int table = 32 + 4 * chunks.Length;
        int total = table + chunks.Sum(c => 8 + c.payload.Length);
        var data = new byte[total];
        Put(data, 0, 0x43425844);
        Put(data, 20, 1);
        Put(data, 24, (uint)total);
        Put(data, 28, (uint)chunks.Length);

        int at = table;
        for (int k = 0; k < chunks.Length; k++)
        {
            Put(data, 32 + 4 * k, (uint)at);
            for (int i = 0; i < 4; i++) data[at + i] = (byte)chunks[k].code[i];
            Put(data, at + 4, (uint)chunks[k].payload.Length);
            Buffer.BlockCopy(chunks[k].payload, 0, data, at + 8, chunks[k].payload.Length);
            at += 8 + chunks[k].payload.Length;
        }
        return data;
    }

    private static void Put(byte[] data, int at, uint value)
    {
        data[at] = (byte)value;
        data[at + 1] = (byte)(value >> 8);
        data[at + 2] = (byte)(value >> 16);
        data[at + 3] = (byte)(value >> 24);
    }

    private static uint U32(byte[] data, int at) => BitConverter.ToUInt32(data, at);

    private static DxbcReflection Sample()
    {
        var refl = new DxbcReflection();

        var material = new DxbcReflection.ConstantBuffer { Name = "UnityPerMaterial", Size = 48 };
        material.Members.Add(new DxbcReflection.Member
        {
            Name = "_Color", Type = "float4", Offset = 0, Size = 16,
        });
        material.Members.Add(new DxbcReflection.Member
        {
            Name = "_MainTex_ST", Type = "float4", Offset = 16, Size = 16,
        });
        material.Members.Add(new DxbcReflection.Member
        {
            Name = "_Clip", Type = "float", Offset = 32, Size = 4,
        });
        refl.Buffers.Add(material);

        var camera = new DxbcReflection.ConstantBuffer { Name = "UnityPerCamera", Size = 64, Register = 1 };
        camera.Members.Add(new DxbcReflection.Member
        {
            Name = "unity_MatrixVP", Type = "float4x4", Offset = 0, Size = 64, Rows = 4,
            IsArray = true, Matrix = true,
        });
        refl.Buffers.Add(camera);

        refl.Bindings.Add(new DxbcReflection.Binding
        {
            Name = "_MainTex", Register = 0, Kind = DxbcReflection.BindingKind.Texture, Dimension = 4,
        });
        refl.Bindings.Add(new DxbcReflection.Binding
        {
            Name = "sampler_MainTex", Register = 0, Kind = DxbcReflection.BindingKind.Sampler,
        });
        return refl;
    }

    private static byte[] ChunkOf(byte[] container, string code)
    {
        for (int k = 0; k < (int)U32(container, 28); k++)
        {
            int at = (int)U32(container, 32 + 4 * k);
            if (Encoding.ASCII.GetString(container, at, 4) == code)
            {
                var bytes = new byte[8 + (int)U32(container, at + 4)];
                Buffer.BlockCopy(container, at, bytes, 0, bytes.Length);
                return bytes;
            }
        }
        return null;
    }

    private static string Name(byte[] body, uint offset)
    {
        Assert.That(offset, Is.LessThan((uint)body.Length), "a name offset points past the chunk");
        int at = (int)offset;
        int end = Array.IndexOf(body, (byte)0, at);
        Assert.That(end, Is.GreaterThan(at), "a name is not terminated");
        return Encoding.UTF8.GetString(body, at, end - at);
    }

    // ---- tests -------------------------------------------------------------

    [Test]
    public void TheChunkGoesFirstAndLeavesTheProgramAlone()
    {
        var shex = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var isgn = new byte[] { 9, 9, 9, 9 };
        var container = Container(("ISGN", isgn), ("SHEX", shex));

        var patched = Sample().Complete(container, "ps", "ps_5_0");

        Assert.That(patched, Is.Not.Null);
        Assert.That(U32(patched, 24), Is.EqualTo((uint)patched.Length),
                    "the container's size field has to match the bytes written");
        Assert.That(Encoding.ASCII.GetString(ChunkOf(patched, "RDEF"), 0, 4), Is.EqualTo("RDEF"));

        for (int k = 0; k < (int)U32(patched, 28); k++)
        {
            int at = (int)U32(patched, 32 + 4 * k);
            Assert.That(at % 4, Is.Zero, "DXBC chunk offsets are DWORD-aligned");
            string code = Encoding.ASCII.GetString(patched, at, 4);
            if (code == "RDEF") continue;
            var expected = code == "ISGN" ? isgn : shex;
            var got = new byte[expected.Length];
            Buffer.BlockCopy(patched, at + 8, got, 0, got.Length);
            Assert.That(got, Is.EqualTo(expected), $"{code} has to survive the splice");
        }
    }

    [Test]
    public void EveryDeclaredNameAndRegisterReadsBack()
    {
        var container = Sample().Complete(Container(("SHEX", new byte[16])), "ps", "ps_5_0");
        var chunk = ChunkOf(container, "RDEF");
        var body = new byte[chunk.Length - 8];
        Buffer.BlockCopy(chunk, 8, body, 0, body.Length);

        Assert.That(U32(body, 8), Is.EqualTo(4u), "two textures/samplers and two buffers are bound");
        Assert.That(U32(body, 12), Is.EqualTo(60u), "the tables start right after the fixed header");
        Assert.That(U32(body, 0), Is.EqualTo(2u));

        // Bound resources: name, type, dimension, register.
        var seen = new List<(string name, uint type, uint register)>();
        for (int i = 0; i < 4; i++)
        {
            int at = 60 + 32 * i;
            seen.Add((Name(body, U32(body, at)), U32(body, at + 4), U32(body, at + 20)));
        }
        Assert.That(seen, Does.Contain(("_MainTex", 2u, 0u)));
        Assert.That(seen, Does.Contain(("sampler_MainTex", 3u, 0u)));
        Assert.That(seen, Does.Contain(("UnityPerMaterial", 0u, 0u)));
        Assert.That(seen, Does.Contain(("UnityPerCamera", 0u, 1u)));

        // Constant buffers: members at their byte offsets, in the order a reader walks.
        int cbTable = (int)U32(body, 4);
        for (int i = 0; i < 2; i++)
        {
            int at = cbTable + 24 * i;
            string cbName = Name(body, U32(body, at));
            int count = (int)U32(body, at + 4);
            int members = (int)U32(body, at + 8);
            if (cbName == "UnityPerMaterial")
            {
                Assert.That(count, Is.EqualTo(5));
                Assert.That(Name(body, U32(body, members)), Is.EqualTo("_Color"));
                Assert.That(U32(body, members + 4), Is.EqualTo(0u));
                Assert.That(Name(body, U32(body, members + 40)), Is.EqualTo("_MainTex_ST"));
                Assert.That(U32(body, members + 44), Is.EqualTo(16u));
                Assert.That(Name(body, U32(body, members + 80)), Is.EqualTo("_Clip"));
                Assert.That(U32(body, members + 84), Is.EqualTo(32u));
                Assert.That(U32(body, members + 88), Is.EqualTo(4u), "_Clip is one float");
                Assert.That(U32(body, members + 124), Is.EqualTo(36u), "padding starts after _Clip");
                Assert.That(U32(body, members + 128), Is.EqualTo(4u));
                Assert.That(U32(body, members + 164), Is.EqualTo(40u), "vec2 padding stays aligned");
                Assert.That(U32(body, members + 168), Is.EqualTo(8u));
            }
            else
            {
                Assert.That(cbName, Is.EqualTo("UnityPerCamera"));
                Assert.That(count, Is.EqualTo(1));
                // class 3 (matrix columns), float, 4x4, four elements.
                int type = (int)U32(body, members + 16);
                Assert.That(U32(body, type) & 0xFFFF, Is.EqualTo(3u));
                Assert.That(U32(body, type) >> 16, Is.EqualTo(3u));
                Assert.That(U32(body, type + 4) & 0xFFFF, Is.EqualTo(4u));
                Assert.That(U32(body, type + 4) >> 16, Is.EqualTo(4u));
                Assert.That(U32(body, type + 8), Is.EqualTo(4u), "a matrix is four rows");
            }
        }
    }

    [Test]
    public void EachTypeRecordSpellsTheMemberItDescribes()
    {
        var refl = new DxbcReflection();
        var cb = new DxbcReflection.ConstantBuffer { Name = "B", Size = 64 };
        cb.Members.Add(new DxbcReflection.Member { Name = "a", Type = "float3", Offset = 0, Size = 12 });
        cb.Members.Add(new DxbcReflection.Member { Name = "b", Type = "uint2", Offset = 16, Size = 8 });
        cb.Members.Add(new DxbcReflection.Member { Name = "c", Type = "int", Offset = 32, Size = 4 });
        cb.Members.Add(new DxbcReflection.Member { Name = "d", Type = "bool", Offset = 48, Size = 4 });
        refl.Buffers.Add(cb);

        var chunk = ChunkOf(refl.Complete(Container(("SHEX", new byte[8])), "vs", "vs_5_0"), "RDEF");
        var body = new byte[chunk.Length - 8];
        Buffer.BlockCopy(chunk, 8, body, 0, body.Length);

        int buffer = (int)U32(body, 4);
        int members = (int)U32(body, buffer + 8);
        var types = new Dictionary<string, (int cls, int type, int rows, int cols)>();
        for (int i = 0; i < U32(body, buffer + 4); i++)
        {
            int member = members + 40 * i;
            int type = (int)U32(body, member + 16);
            types.Add(Name(body, U32(body, member)), ((int)(U32(body, type) & 0xFFFF), (int)(U32(body, type) >> 16),
                (int)(U32(body, type + 4) & 0xFFFF), (int)(U32(body, type + 4) >> 16)));
        }

        Assert.That(types["a"], Is.EqualTo((1, 3, 1, 3)), "float3 is a three-lane float vector");
        Assert.That(types["b"], Is.EqualTo((1, 19, 1, 2)), "uint2 is a two-lane unsigned vector");
        Assert.That(types["c"], Is.EqualTo((0, 2, 1, 1)), "int is a signed scalar");
        Assert.That(types["d"], Is.EqualTo((0, 1, 1, 1)), "bool is a scalar of its own type");
    }

    [Test]
    public void AContainerThatCannotTakeReflectionIsLeftAlone()
    {
        Assert.That(Sample().Complete(new byte[64], "ps", "ps_5_0"), Is.Null, "not a DXBC container");
        Assert.That(new DxbcReflection().Complete(Container(("SHEX", new byte[8])), "ps", "ps_5_0"), Is.Null,
                    "nothing was declared, so there is nothing to describe");
    }

    [Test]
    public void AnUnnamedRowBecomesAPlaceholderSoLaterOffsetsDoNotMove()
    {
        var refl = new DxbcReflection();
        var cb = new DxbcReflection.ConstantBuffer { Name = "UnityPerDraw", Size = 48, Register = 1 };
        cb.Members.Add(new DxbcReflection.Member { Name = "a", Type = "float4", Offset = 0, Size = 16 });
        // Row 1 holds nothing this program reads; row 2 is named.
        cb.Members.Add(new DxbcReflection.Member { Name = "c", Type = "float4", Offset = 32, Size = 16 });
        DxbcReflection.FillGaps(cb);
        refl.Buffers.Add(cb);

        Assert.That(cb.Members.Select(m => m.Name), Is.EqualTo(new[] { "a", "_cb1_1", "c" }));
        Assert.That(cb.Members.Select(m => m.Offset), Is.EqualTo(new[] { 0, 16, 32 }));
    }

    [Test]
    public void PackedAndTrailingGapsCoverEveryByteWithoutRenamingMembers()
    {
        var buffer = new DxbcReflection.ConstantBuffer { Name = "Material", Size = 32 };
        var tint = new DxbcReflection.Member { Name = "_Tint", Type = "float2", Offset = 4, Size = 8 };
        buffer.Members.Add(tint);
        DxbcReflection.CoverBufferLanes(buffer);

        Assert.That(buffer.Members.Select(member => (member.Offset, member.Size)),
            Is.EqualTo(new[] { (0, 4), (4, 8), (12, 4), (16, 16) }));
        Assert.That(buffer.Members[1], Is.SameAs(tint));
    }

    [Test]
    public void StructuredBindingDescribesTheCompiledElementStride()
    {
        var reflection = new DxbcReflection();
        reflection.Bindings.Add(new DxbcReflection.Binding
        {
            Name = "instances", Register = 3, StructureStride = 56,
        });
        byte[] body = reflection.Build("vs", "vs_4_0");
        int binding = (int)U32(body, 12);
        int buffer = (int)U32(body, 4);
        int element = (int)U32(body, buffer + 8);
        int type = (int)U32(body, element + 16);
        int fields = (int)U32(body, type + 12);
        Assert.Multiple(() =>
        {
            Assert.That(U32(body, binding + 4), Is.EqualTo(5u));
            Assert.That(U32(body, binding + 16), Is.EqualTo(56u));
            Assert.That(U32(body, binding + 20), Is.EqualTo(3u));
            Assert.That(U32(body, buffer + 20), Is.EqualTo(3u));
            Assert.That(Name(body, U32(body, element)), Is.EqualTo("$Element"));
            Assert.That(U32(body, type + 8) >> 16, Is.EqualTo(14u));
            Assert.That(U32(body, fields + 13 * 12 + 8), Is.EqualTo(52u));
        });
    }

    [TestCase("ps_4_0", 24)]
    [TestCase("ps_5_0", 40)]
    public void AssetBindingsRestorePackedNamesEvenWhenTheEmitterGuessedAnotherBuffer(string profile, int stride)
    {
        var program = new SubProgram();
        var nameTable = new Dictionary<int, string> { [0] = "MaterialParams", [1] = "_Mode", [2] = "_Tint" };
        SerializedFile.ReadConstantBuffers(new Dictionary<string, object>
        {
            ["m_Parameters"] = null,
            ["m_ConstantBuffers"] = new List<object> { new Dictionary<string, object>
            {
                ["m_NameIndex"] = 0, ["m_Size"] = 48,
                ["m_VectorParams"] = new List<object>
                {
                    new Dictionary<string, object> { ["m_NameIndex"] = 1, ["m_Index"] = 4, ["m_Type"] = 2, ["m_Dim"] = 1 },
                    new Dictionary<string, object> { ["m_NameIndex"] = 2, ["m_Index"] = 32, ["m_Type"] = 0, ["m_Dim"] = 4 },
                },
            } },
            ["m_ConstantBufferBindings"] = new List<object> { new Dictionary<string, object>
            {
                ["m_NameIndex"] = 0, ["m_Index"] = 1,
            } },
        }, nameTable, program);

        var reflection = new DxbcReflection();
        var buffer = new DxbcReflection.ConstantBuffer { Name = "UnityPerFrame", Register = 1, Size = 48 };
        buffer.Members.Add(new DxbcReflection.Member { Name = "cb1", Size = 48, IsArray = true, Rows = 3 });
        reflection.Buffers.Add(buffer);
        reflection.ApplyAssetBuffers(program);

        var body = reflection.Build("ps", profile);
        int bufferRecord = (int)U32(body, 4);
        int member = (int)U32(body, bufferRecord + 8);
        int binding = (int)U32(body, 12);
        Assert.Multiple(() =>
        {
            Assert.That(Name(body, U32(body, binding)), Is.EqualTo("MaterialParams"));
            Assert.That(U32(body, binding + 20), Is.EqualTo(1u));
            Assert.That(Name(body, U32(body, member)), Is.EqualTo("_Mode"));
            Assert.That(U32(body, member + 4), Is.EqualTo(4u));
            Assert.That(U32(body, (int)U32(body, member + 16)), Is.EqualTo(2u << 16));
            Assert.That(U32(body, member + stride * 2 + 4), Is.EqualTo(32u));
            Assert.That(Name(body, U32(body, member + stride * 2)), Is.EqualTo("_Tint"));
            Assert.That(U32(body, bufferRecord + 12), Is.EqualTo(48u));
        });
    }

    [TestCase("ps_5_0", 60, 40, 36)]
    [TestCase("ps_4_0", 28, 24, 16)]
    public void TheChunkWalksExactlyAsTheDocumentedStructuresSay(string profile, int tableOffset,
                                                                int memberSize, int typeSize)
    {
        var container = Sample().Complete(Container(("SHEX", new byte[8])), "ps", profile);
        var chunk = ChunkOf(container, "RDEF");
        var body = new byte[chunk.Length - 8];
        Buffer.BlockCopy(chunk, 8, body, 0, body.Length);

        // Header: D3D11_SHADER_DESC counts, then the version word (minor, major,
        // program type -- 0xFFFF is a pixel shader).
        uint cbCount = U32(body, 0), cbOffset = U32(body, 4);
        uint resCount = U32(body, 8), resOffset = U32(body, 12);
        Assert.That(U32(body, 16) & 0xFF, Is.EqualTo(0u), "minor version");
        Assert.That((U32(body, 16) >> 8) & 0xFF, Is.EqualTo((uint)(profile[3] - '0')), "major version");
        Assert.That(U32(body, 16) >> 16, Is.EqualTo(0xFFFFu), "program type: pixel");
        // The string area sits between the bindings and the buffer table, so the
        // buffer table starts after the bindings and is four-byte aligned.
        Assert.That(cbOffset, Is.GreaterThanOrEqualTo((uint)(tableOffset + 32 * resCount)),
                    "buffers follow the bindings and the name strings");
        Assert.That(cbOffset % 4, Is.EqualTo(0u));
        Assert.That(resOffset, Is.EqualTo((uint)tableOffset), "bindings start at the table offset");
        Assert.That(Name(body, U32(body, 24)), Does.StartWith("ShaderRipper"), "creator string");

        // Shader model 5 carries the RD11 marker and the record sizes a reader is
        // about to walk, then the interface slot count.
        if (tableOffset > 28)
        {
            Assert.That(Encoding.ASCII.GetString(body, 28, 4), Is.EqualTo("RD11"));
            Assert.That(U32(body, 32), Is.EqualTo((uint)tableOffset));
            Assert.That(U32(body, 36), Is.EqualTo(24u), "buffer record size");
            Assert.That(U32(body, 40), Is.EqualTo(32u), "binding record size");
            Assert.That(U32(body, 44), Is.EqualTo((uint)memberSize), "member record size");
            Assert.That(U32(body, 48), Is.EqualTo((uint)typeSize), "type record size");
            Assert.That(U32(body, 52), Is.EqualTo(12u));
            Assert.That(U32(body, 56), Is.EqualTo(0u), "interface slot count");
        }

        // Bindings: D3D11_SHADER_INPUT_BIND_DESC.
        var bindingNames = new List<string>();
        for (int i = 0; i < resCount; i++)
        {
            int at = (int)resOffset + 32 * i;
            bindingNames.Add(Name(body, U32(body, at)));
            Assert.That(U32(body, at + 4), Is.AnyOf(0u, 2u, 3u), "cbuffer / texture / sampler");
            Assert.That(U32(body, at + 24), Is.EqualTo(1u), "bind count");
        }
        Assert.That(bindingNames, Does.Contain("_MainTex"));

        // Buffers: D3D11_SHADER_BUFFER_DESC, then its variable and type records.
        for (int i = 0; i < cbCount; i++)
        {
            int at = (int)cbOffset + 24 * i;
            string cbName = Name(body, U32(body, at));
            int vars = (int)U32(body, at + 4);
            int members = (int)U32(body, at + 8);
            Assert.That(U32(body, at + 16), Is.EqualTo(0u), "no cbuffer flags");
            Assert.That(U32(body, at + 20), Is.EqualTo(0u), "cbuffer type, not a texture");
            Assert.That(vars, Is.GreaterThan(0));

            for (int v = 0; v < vars; v++)
            {
                int rec = members + memberSize * v;
                Name(body, U32(body, rec));                                  // member name
                Assert.That(U32(body, rec + 12), Is.EqualTo(2u), "D3D_SVF_USED");
                Assert.That(U32(body, rec + 20), Is.EqualTo(0u), "no default value");
                if (memberSize > 24)
                {
                    Assert.That(U32(body, rec + 24), Is.EqualTo(0xFFFFFFFFu), "no start texture");
                    Assert.That(U32(body, rec + 28), Is.EqualTo(0u));
                    Assert.That(U32(body, rec + 32), Is.EqualTo(0xFFFFFFFFu), "no start sampler");
                    Assert.That(U32(body, rec + 36), Is.EqualTo(0u));
                }

                int type = (int)U32(body, rec + 16);
                Assert.That(U32(body, type) & 0xFFFF, Is.AnyOf(0u, 1u, 3u), "scalar / vector / matrix");
                Assert.That(U32(body, type + 4) & 0xFFFF, Is.GreaterThanOrEqualTo(1u), "rows");
                Assert.That(U32(body, type + 8) & 0xFFFF, Is.GreaterThanOrEqualTo(0u), "elements");
                Assert.That(U32(body, type + 12), Is.EqualTo(0u), "no struct members");
                if (typeSize > 16)
                {
                    Assert.That(U32(body, type + 16), Is.EqualTo(0u), "no parent type");
                    Assert.That(U32(body, type + 20), Is.EqualTo(0u));
                    Assert.That(U32(body, type + 24), Is.AnyOf(0u, 1u));
                    Assert.That(U32(body, type + 28), Is.EqualTo(0u));
                    // The 5.0 tail names the type; it has to land on real text.
                    Assert.That(Name(body, U32(body, type + 32)),
                                Does.Match(@"^(float|float[234]|float4x4|int|uint|bool)$"));
                }
            }
        }
    }
}
