using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShaderRipper.Ast;
using ShaderRipper.Data;
using ShaderRipper.Emit;

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

        Assert.That(hlsl, Does.Contain("r0.xy = t0.Load2("));
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
