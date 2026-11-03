using System;
using System.Collections.Generic;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// Enumerates the programs in one decoded Vulkan payload.
    ///
    /// Unity packs a Vulkan payload as a slot table, and each slot is one compiled
    /// program behind a <c>0x0C0A75BA</c> header.  The header's second word is a kind
    /// discriminator: <c>25</c> marks a slot that carries SMOL-V modules, everything
    /// else is a parameter table (the cbuffer and texture reflection Unity also keeps
    /// for d3d11) and holds no code.
    ///
    /// A kind-25 slot holds the vertex and the fragment module of one program back to
    /// back, each introduced by the <c>LOMS</c> magic SMOL-V uses.  Unity pairs them
    /// because the two stages of a pass are compiled together, and the decoder that
    /// splits them needs both halves -- handing it one half fails with "could not find
    /// a vertex entry point".  So one slot yields two descriptors that differ only in
    /// <see cref="ProgramDescriptor.Stage"/>, and the translator returns the module the
    /// requested stage names.
    /// </summary>
    internal sealed class SmolvSlotReader : ISlotReader
    {
        public IReadOnlyList<ProgramDescriptor> Read(ReadOnlyMemory<byte> payload, SlotContext context)
        {
            var list = new List<ProgramDescriptor>();
            if (!Payloads.TryReadSlots(payload.Span, out var slots)) return list;

            for (int e = 0; e < slots.Count; e++)
            {
                var (off, size) = slots[e];
                if (!Payloads.IsSpirVSlot(payload.Span, off)) continue;

                foreach (string stage in Payloads.Stages)
                {
                    list.Add(new ProgramDescriptor
                    {
                        // Unity stores one slot per compiled program and the
                        // sub-program table refers to it by position, the same
                        // convention the d3d11 route relies on.
                        BlobIndex = e,
                        Stage = stage,
                        Offset = 0,
                        // The translator needs the whole payload, not this slot: the
                        // texture names live in the sibling parameter tables.
                        Length = payload.Length,
                        Entry = stage == "vs" ? "vert" : "frag",
                    });
                }
            }
            return list;
        }
    }

    /// <summary>Reading of Unity's slot table and the slot header.</summary>
    internal static class Payloads
    {
        /// <summary>Opens every parameter blob and every SMOL-V slot.</summary>
        internal const uint ParamBlobMagic = 0x0C0A75BAu;

        /// <summary>Slot kind that carries SMOL-V modules rather than a parameter table.</summary>
        internal const int SpirVSlotKind = 25;

        /// <summary>Record kind that marks a texture binding in a parameter slot.</summary>
        internal const int TextureRecordKind = 4;

        /// <summary>The stages one kind-25 slot holds, in the order the decoder returns them.</summary>
        internal static readonly string[] Stages = { "vs", "ps" };

        /// <summary>
        /// Read the <c>u32 count</c> + <c>count * (offset, size, reserved)</c> table that
        /// opens every decoded platform payload, dropping entries that fall outside it.
        /// </summary>
        public static bool TryReadSlots(ReadOnlySpan<byte> payload, out List<(int off, int size)> slots)
        {
            slots = new List<(int, int)>();
            if (payload.Length < 8) return false;

            int count = BitConverter.ToInt32(payload[..4]);
            if (count <= 0 || count > 8192 || 4 + (long)count * 12 > payload.Length) return false;

            for (int e = 0; e < count; e++)
            {
                int at = 4 + e * 12;
                int off = BitConverter.ToInt32(payload.Slice(at, 4));
                int size = BitConverter.ToInt32(payload.Slice(at + 4, 4));
                if (off < 0 || size < 0x10 || (long)off + size > payload.Length) continue;
                slots.Add((off, size));
            }
            return slots.Count > 0;
        }

        /// <summary>True when the slot at <paramref name="off"/> holds SMOL-V modules.</summary>
        public static bool IsSpirVSlot(ReadOnlySpan<byte> payload, int off)
        {
            if (off < 0 || off + 8 > payload.Length) return false;
            if (BitConverter.ToUInt32(payload.Slice(off, 4)) != ParamBlobMagic) return false;
            return BitConverter.ToInt32(payload.Slice(off + 4, 4)) == SpirVSlotKind;
        }
    }
}
