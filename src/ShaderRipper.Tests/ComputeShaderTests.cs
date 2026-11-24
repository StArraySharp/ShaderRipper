using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShaderRipper.Ast;
using ShaderRipper.Data;
using ShaderRipper.Emit;
using ShaderRipper.Plugin.Compute;

namespace ShaderRipper.Tests;

/// <summary>
/// Compute shaders are a separate payload shape: a kernel's entry point is a system
/// value list, not an appdata/v2f pair, and the thread-group size is a declaration
/// rather than a signature.  Both used to be dropped, which produced a function no
/// compiler accepts.
/// </summary>
[TestFixture]
public class ComputeShaderTests
{
    [Test]
    public void ReflectedMembersKeepPackOffsetsAndIntegerBits()
    {
        var buffer = new AssetConstantBuffer { Size = 32 };
        buffer.Members.Add(new AssetCbMember { Name = "Count", Offset = 0, Type = 5, Dim = 1, RowCount = 1 });
        buffer.Members.Add(new AssetCbMember { Name = "Scale", Offset = 4, Type = 0, Dim = 2, RowCount = 1 });
        var (hlsl, restored) = RestoreBuffer("float3 v = cb0[0].yzx;", buffer);

        Assert.That(restored, Is.True);
        Assert.That(hlsl, Does.Contain("uint Count : packoffset(c0.x);"));
        Assert.That(hlsl, Does.Contain("float2 Scale : packoffset(c0.y);"));
        Assert.That(hlsl, Does.Contain("float3(Scale.x, Scale.y, asfloat(Count))"));
        Assert.That(hlsl, Does.Not.Contain("cb0["));
    }

    [Test]
    public void ReflectedArrayUsesItsElementAndSwizzle()
    {
        var buffer = new AssetConstantBuffer { Size = 32 };
        buffer.Members.Add(new AssetCbMember { Name = "Offsets", Type = 2, Dim = 4, RowCount = 1, ArraySize = 2 });
        var (hlsl, restored) = RestoreBuffer("float2 v = cb0[1].zw;", buffer);

        Assert.That(restored, Is.True);
        Assert.That(hlsl, Does.Contain("int4 Offsets[2] : packoffset(c0.x);"));
        Assert.That(hlsl, Does.Contain("asfloat(Offsets[1].zw)"));
    }

    [TestCase("cb0[0].y")]
    [TestCase("cb0[index].x")]
    public void UnresolvedReadKeepsTheRawBufferAndOtherReads(string unresolved)
    {
        var buffer = new AssetConstantBuffer { Size = 32 };
        buffer.Members.Add(new AssetCbMember { Name = "Count", Type = 5, Dim = 1, RowCount = 1 });
        var (hlsl, restored) = RestoreBuffer($"float v = cb0[0].x + {unresolved};", buffer);

        Assert.That(restored, Is.False);
        Assert.That(hlsl, Does.Contain("float4 cb0[2];"));
        Assert.That(hlsl, Does.Contain($"cb0[0].x + {unresolved}"));
    }

    private static (string Hlsl, bool Restored) RestoreBuffer(string body, AssetConstantBuffer buffer) =>
        ComputeBufferReflection.Apply("cbuffer CB0 : register(b0)\n{\n    float4 cb0[2];\n};\n" + body,
                                      new Dictionary<int, AssetConstantBuffer> { [0] = buffer });

    private const string Simple = """
cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_raw t0
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.x, t0.xyxx
store_raw u0.x, vThreadID.x, r0.x
ret
""";

    [Test]
    public void BufferInfoReportsStructuredElementsAndRawBytes()
    {
        var m = Module.Read("""
cs_5_0
dcl_resource_structured t1, 64
dcl_uav_raw u0
dcl_temps 1
dcl_thread_group 1, 1, 1
bufinfo_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r0.xy, t1.xyzw
bufinfo r0.z, u0
ret
""");
        var result = HlslEmitter.Emit(m);

        Assert.That(m.ParseErrors, Is.Zero);
        Assert.That(result.Missing, Is.Empty);
        Assert.That(result.Hlsl, Does.Contain("t1.GetDimensions(_bufCount0);")
            .And.Contain("r0.xy = asfloat(_bufCount0 / 64u);")
            .And.Contain("u0.GetDimensions(_bufCount1);")
            .And.Contain("r0.z = asfloat(_bufCount1);"));
    }

    [Test]
    public void GroupMemoryFencePreservesTheExecutionSyncFlag()
    {
        var result = HlslEmitter.Emit(Module.Read("""
cs_5_0
dcl_tgsm_structured g0, 8, 8
dcl_input vThreadIDInGroup.x
dcl_temps 1
dcl_thread_group 8, 1, 1
store_structured g0.xy, vThreadIDInGroup.x, l(0), r0.xyxx
sync_g
sync_g_t
ld_structured r0.xy, vThreadIDInGroup.x, l(0), g0.xyxx
ret
"""));
        Assert.That(result.Missing, Is.Empty);
        Assert.That(result.Hlsl, Does.Contain("groupshared uint g0[16];")
            .And.Contain("g0[(svThreadIDInGroup.x * 2u + (0u >> 2u)) + 1u] = asuint(r0.y);")
            .And.Contain("GroupMemoryBarrier();")
            .And.Contain("GroupMemoryBarrierWithGroupSync();")
            .And.Contain("r0.xy = asfloat(uint2(g0["));
    }

    [Test]
    public void DoubleAddReassemblesWordsAndReadsBothPairsBeforeOverwriting()
    {
        var m = Module.Read("""
cs_5_0
dcl_globalFlags enableDoublePrecisionFloatOps
dcl_temps 1
dcl_thread_group 1, 1, 1
dadd r0.xyzw, r0.zwxy, d(1.500000l, 2.000000l)
ret
""");
        var result = HlslEmitter.Emit(m);

        Assert.That(m.ParseErrors, Is.Zero);
        Assert.That(result.Missing, Is.Empty);
        Assert.That(result.Hlsl, Does.Contain("asdouble(asuint(r0.z), asuint(r0.w)) + asdouble(0u, 1073217536u)")
            .And.Contain("asdouble(asuint(r0.x), asuint(r0.y)) + asdouble(0u, 1073741824u)")
            .And.Contain("r0.xyzw = asfloat(uint4(_dadd0lo, _dadd0hi, _dadd2lo, _dadd2hi));"));
    }

    [Test]
    public void TheThreadGroupSizeIsReadFromItsDeclaration()
    {
        var m = Module.Read(Simple);
        Assert.That(m.ThreadGroup, Is.EqualTo(new[] { 64, 1, 1 }));
    }

    [Test]
    public void AGraphicsProgramHasNoThreadGroup()
    {
        var m = Module.Read("""
ps_5_0
dcl_temps 1
mov r0.x, l(1)
ret
""");
        Assert.That(m.ThreadGroup, Is.Empty);
    }

    /// <summary>
    /// The entry point takes one parameter per system value, named after it, with the
    /// SV_* semantic the value stands for.  A `void CSMain()` with the body reading
    /// `svThreadID` is an undeclared identifier (X3004).
    /// </summary>
    [Test]
    public void TheEntryPointDeclaresOneParameterPerSystemValue()
    {
        var hlsl = HlslEmitter.Emit(Module.Read(Simple)).Hlsl;

        Assert.That(hlsl, Does.Contain("void CSMain(uint3 svThreadID : SV_DispatchThreadID)"));
    }

    [Test]
    public void EachSystemValueMapsToItsOwnSemantic()
    {
        var hlsl = HlslEmitter.Emit(Module.Read("""
cs_5_0
dcl_input vThreadGroupID.x
dcl_input vThreadIDInGroup.x
dcl_thread_group 8, 8, 1
imad r0.x, vThreadGroupID.x, l(256), vThreadIDInGroup.x
ret
""")).Hlsl;

        Assert.That(hlsl, Does.Contain("svThreadGroupID : SV_GroupID"));
        Assert.That(hlsl, Does.Contain("svThreadIDInGroup : SV_GroupThreadID"));
        // The body names the parameters, not a `v#` register.
        Assert.That(hlsl, Does.Contain("i0.x = svThreadGroupID.x * 256 + svThreadIDInGroup.x"));
    }

    /// <summary>
    /// `dcl_resource_raw` is an untyped buffer read by byte offset.  Declaring it as a
    /// Texture2D leaves `t0.Load2(...)` with no matching overload, which is every
    /// compute kernel's first instruction.
    /// </summary>
    [Test]
    public void ARawResourceIsDeclaredAsAByteAddressBuffer()
    {
        var hlsl = HlslEmitter.Emit(Module.Read(Simple)).Hlsl;

        Assert.That(hlsl, Does.Contain("ByteAddressBuffer t0 : register(t0);"));
        Assert.That(hlsl, Does.Contain("RWByteAddressBuffer u0;"));
    }

    [Test]
    public void LdRawReadsByByteAddressWithoutAStride()
    {
        var hlsl = HlslEmitter.Emit(Module.Read(Simple)).Hlsl;

        Assert.That(hlsl, Does.Contain("r0.xy = asfloat(t0.Load2(svThreadID.x));"));
        Assert.That(hlsl, Does.Not.Contain("TODO ld_raw"));
    }

    [Test]
    public void StoreRawWritesByByteAddress()
    {
        var hlsl = HlslEmitter.Emit(Module.Read(Simple)).Hlsl;

        Assert.That(hlsl, Does.Contain("u0.Store("));
        Assert.That(hlsl, Does.Not.Contain("TODO store_raw"));
    }

    /// <summary>Every instruction of a compute kernel is translated, not skipped.</summary>
    [Test]
    public void ASimpleKernelTranslatesEveryInstruction()
    {
        var r = HlslEmitter.Emit(Module.Read(Simple));

        Assert.That(r.Todo, Is.Zero, string.Join(", ", r.Missing));
        Assert.That(r.Missing, Is.Empty);
    }

    /// <summary>A kernel returns nothing; a `return value;` does not compile.</summary>
    [Test]
    public void TheKernelBodyHasNoReturnValue()
    {
        var hlsl = HlslEmitter.Emit(Module.Read(Simple)).Hlsl;

        Assert.That(hlsl, Does.Contain("return;"));
        Assert.That(hlsl, Does.Not.Contain("return r"));
    }

    /// <summary>`imad` has a third source operand; dropping it loses the addend.</summary>
    [Test]
    public void ImadKeepsItsAddend()
    {
        var hlsl = HlslEmitter.Emit(Module.Read("""
cs_5_0
dcl_input vThreadID.x
dcl_thread_group 8, 1, 1
imad r0.x, vThreadID.x, l(4), l(7)
ret
""")).Hlsl;

        Assert.That(hlsl, Does.Contain("* 4 + 7"));
    }

    /// <summary>
    /// A system value is already an integer, so it must not be wrapped in
    /// `asint(...)` -- that names a value the entry point does not declare.
    /// </summary>
    [Test]
    public void ASystemValueIsNotReinterpreted()
    {
        var hlsl = HlslEmitter.Emit(Module.Read(Simple)).Hlsl;

        Assert.That(hlsl, Does.Not.Contain("asint(svThreadID"));
    }
}
