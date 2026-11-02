using System;
using System.Collections.Generic;
using System.Text;
using Tesearis.SmolVDecoder;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// Everything one decoded Vulkan payload says about a shader: the SPIR-V modules of
    /// each program, plus the names Unity kept in the sibling parameter slots.
    ///
    /// The names matter because SPIR-V carries none.  Unity compiles with the debug
    /// information stripped -- the only <c>OpName</c> left in a shipping module is an
    /// interpolator -- so a raw SPIRV-Cross run names a texture <c>_13</c>, and the
    /// emitted program would declare <c>Texture2D _13 : register(t0)</c> where the
    /// original said <c>_PlanetTex</c>.  The parameter slots of the same payload keep
    /// the mapping, keyed by the register the d3d11 build would have used, which is the
    /// same number SPIR-V assigns as a binding because both backends consume the one
    /// reflection table UnityShaderCompiler produces.
    ///
    /// Vertex attribute semantics are the one thing the payload does NOT keep.  SPIR-V
    /// records a location and nothing else, and the GLSL build of a different shader
    /// shows the declaration order is per-shader rather than the fixed
    /// <c>POSITION, NORMAL, TANGENT, TEXCOORD0.., COLOR</c> sequence the engine's own
    /// <c>VertexChannel</c> enumeration suggests.  So the first input is named
    /// <c>POSITION</c> (every vertex program has one, at location 0) and the rest keep
    /// the placeholder with a note, rather than being given a semantic that would
    /// silently bind the wrong mesh stream.
    /// </summary>
    internal sealed class SpirvPayload
    {
        /// <summary>Vertex module of the first program, or null when it had none.</summary>
        public byte[] Vertex;

        /// <summary>Fragment module of the first program, or null when it had none.</summary>
        public byte[] Fragment;

        /// <summary>Texture register (<c>t#</c>) to property name.</summary>
        public readonly Dictionary<int, string> Textures = new();

        /// <summary>Diagnostics worth writing into the generated file.</summary>
        public readonly List<string> Notes = new();

        /// <summary>
        /// Read one program's slots: the module slot the descriptor names, plus the
        /// parameter slots that carry the texture names.
        ///
        /// <paramref name="blobIndex"/> is the slot the descriptor came from.  Each
        /// keyword variant of a pass is its own slot with its own pair of modules, so
        /// reading a fixed slot would hand every variant the same program.
        /// </summary>
        public static SpirvPayload Read(ReadOnlyMemory<byte> payload, List<(int off, int size)> slots,
                                        int blobIndex)
        {
            var result = new SpirvPayload();
            ReadOnlySpan<byte> span = payload.Span;

            // Texture names are program-wide: Unity keeps them per slot but every
            // variant of one shader records the same bindings, so reading all of them
            // just makes the table complete when a variant omits a slot the runtime
            // derives by convention.
            foreach (var (off, size) in slots)
                if (!Payloads.IsSpirVSlot(span, off))
                    result.ReadTextureSlot(span.Slice(off, size));

            if (blobIndex < 0 || blobIndex >= slots.Count) return null;
            var (moduleOff, moduleSize) = slots[blobIndex];
            if (!Payloads.IsSpirVSlot(span, moduleOff)) return null;

            if (!SmolV.TryDecodeStages(span.Slice(moduleOff, moduleSize).ToArray(),
                                       out byte[] vs, out byte[] fs, out string error))
            {
                if (!string.IsNullOrEmpty(error)) result.Notes.Add("SMOL-V: " + error);
                return null;
            }

            result.Vertex = vs;
            result.Fragment = fs;
            if (result.Vertex == null && result.Fragment == null) return null;
            return result;
        }

        // ------------------------------------------------------------------
        // Parameter slots
        // ------------------------------------------------------------------

        /// <summary>
        /// Harvest the texture bindings out of one parameter slot.
        ///
        /// The slot is a flat run of records with no index.  A record is a
        /// length-prefixed name followed by four ints; for a texture they are
        /// <c>[nameIndex, register, samplerIndex, 4]</c>, where the register is
        /// <c>0x08000000 | binding</c> because Vulkan packs the descriptor set into the
        /// high byte.  Everything else in the slot is a cbuffer or a scalar whose
        /// name the emitted HLSL derives from the SPIR-V itself, so only texture
        /// records are of interest here.
        /// </summary>
        private void ReadTextureSlot(ReadOnlySpan<byte> slot)
        {
            int at = 0;
            while (at + 8 <= slot.Length)
            {
                if (!TryReadName(slot, at, out string name, out int next)) { at += 4; continue; }
                if (next + 16 > slot.Length) break;

                int nameIndex = BitConverter.ToInt32(slot.Slice(next, 4));
                int packed = BitConverter.ToInt32(slot.Slice(next + 4, 4));
                int kind = BitConverter.ToInt32(slot.Slice(next + 12, 4));

                // A texture binding: kind 4, descriptor set 8 in the high byte, the
                // register in the low one.  The cbuffer records that share this slot
                // are skipped: a b# is a per-stage number, so a name read from here
                // cannot be attached to a buffer without collapsing two unrelated
                // blocks that happen to share the register across stages.
                if (kind == Payloads.TextureRecordKind &&
                    (packed & unchecked((int)0xFF000000)) == 0x08000000)
                {
                    int binding = packed & 0xFF;
                    if (binding < 128 && !Textures.ContainsKey(binding)) Textures[binding] = name;
                }

                at = next + 16;
                _ = nameIndex;
            }
        }

        /// <summary>Read a length-prefixed, 4-byte-aligned identifier.</summary>
        internal static bool TryReadName(ReadOnlySpan<byte> b, int at, out string name, out int next)
        {
            name = null;
            next = at;
            if (at < 0 || at + 4 > b.Length) return false;

            int len = BitConverter.ToInt32(b.Slice(at, 4));
            if (len <= 0 || len > 64 || at + 4 + len > b.Length) return false;

            for (int i = 0; i < len; i++)
            {
                byte c = b[at + 4 + i];
                bool ok = c == '_' || c == '$' || (c >= 'A' && c <= 'Z') ||
                          (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!ok) return false;
            }

            name = Encoding.ASCII.GetString(b.Slice(at + 4, len).ToArray());
            next = at + 4 + ((len + 3) & ~3);
            return true;
        }
    }
}
