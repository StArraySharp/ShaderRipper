using System;
using System.Collections.Generic;
using System.Text;
using Tesearis.SmolVDecoder;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// One constant buffer as the payload's parameter slot records it.
    ///
    /// Unity keeps this table for its own binding, and it is the only surviving copy
    /// of the names: the SPIR-V module is compiled with its debug information stripped,
    /// so nothing else in the payload says a block was called <c>UnityPerMaterial</c>
    /// or that its first member was <c>_Color</c>.
    /// </summary>
    internal sealed class CBufferRecord
    {
        public string Name;

        /// <summary>Declared size in bytes.</summary>
        public int Size;

        public List<CBufferMember> Members = new();

        /// <summary>Descriptor set the runtime binds this buffer to.</summary>
        public int Set = -1;

        /// <summary>Binding within <see cref="Set"/>.</summary>
        public int Binding = -1;

        /// <summary>True once the slot's register map named this buffer.</summary>
        public bool HasBinding;
    }

    /// <summary>One member of a <see cref="CBufferRecord"/>.</summary>
    internal sealed class CBufferMember
    {
        public string Name;

        /// <summary>Byte offset from the start of the buffer.</summary>
        public int Offset;
    }

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

        /// <summary>
        /// The constant buffers the payload records, with their member names and byte
        /// offsets.  This is the only place those names survive: the SPIR-V module is
        /// compiled with its debug information stripped, so a translated program names
        /// a block <c>_16_18</c> and its members <c>_18_m0</c> where the original said
        /// <c>VGlobals...</c> and <c>unity_ObjectToWorld</c>.
        /// </summary>
        public readonly List<CBufferRecord> Buffers = new();
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
                    result.ReadParamSlot(span.Slice(off, size));

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
        /// Harvest one parameter slot: the texture bindings, and the constant buffers
        /// with the member names and byte offsets the SPIR-V module no longer carries.
        ///
        /// The slot is a flat run of records with no index.  A texture record is a
        /// length-prefixed name followed by four ints -- <c>[nameIndex, register,
        /// samplerIndex, 4]</c>, where the register is <c>0x08000000 | binding</c>
        /// because Vulkan packs the descriptor set into the high byte.  A buffer record
        /// is a name, a byte size and a member count, followed by that many member
        /// records.  A trailing <c>[name, 1, set&lt;&lt;24 | binding]</c> list then maps
        /// the buffers it names to the descriptor each is bound to.
        /// </summary>
        private void ReadParamSlot(ReadOnlySpan<byte> slot)
        {
            // Records of THIS slot only.  The register map at the end names buffers
            // the slot itself declared, and looking the name up across the whole
            // payload would find a same-named buffer from an earlier slot -- already
            // bound, so this slot's copy would never get its descriptor and the
            // record after it would be read as a texture instead.
            var local = new List<CBufferRecord>();

            int at = 0;
            while (at + 8 <= slot.Length)
            {
                if (!TryReadName(slot, at, out string name, out int next)) { at += 4; continue; }

                if (TryReadCBuffer(slot, name, next, out CBufferRecord cb, out int after))
                {
                    local.Add(cb);
                    Buffers.Add(cb);
                    at = after;
                    continue;
                }

                // The trailing register map.  It only names buffers the record scan
                // above already found, which is what tells it apart from a texture
                // record whose name table index happens to be 1.
                //
                // The word packs `tag << 24 | set << 16 | binding`: the high byte
                // distinguishes the record's role (4 for a vertex-stage buffer, 8 for a
                // fragment-stage one or a texture), and the two below it are exactly the
                // DescriptorSet and Binding the module decorates its variable with.
                if (next + 8 <= slot.Length &&
                    BitConverter.ToInt32(slot.Slice(next, 4)) == 1)
                {
                    CBufferRecord known = local.Find(b => b.Name == name);
                    if (known != null)
                    {
                        int packed = BitConverter.ToInt32(slot.Slice(next + 4, 4));
                        known.Set = (packed >> 16) & 0xFF;
                        known.Binding = packed & 0xFF;
                        known.HasBinding = true;
                        at = next + 8;
                        continue;
                    }
                }

                if (next + 16 > slot.Length) break;
                int reg = BitConverter.ToInt32(slot.Slice(next + 4, 4));
                int kind = BitConverter.ToInt32(slot.Slice(next + 12, 4));

                // A texture binding: kind 4, descriptor set 8 in the high byte, the
                // register in the low one.
                if (kind == Payloads.TextureRecordKind &&
                    (reg & unchecked((int)0xFF000000)) == 0x08000000)
                {
                    int binding = reg & 0xFF;
                    if (binding < 128 && !Textures.ContainsKey(binding)) Textures[binding] = name;
                }

                at = next + 16;
            }
        }

        /// <summary>
        /// Read one constant-buffer record: <c>name, byteSize, memberCount</c> then that
        /// many <c>name, [0, type, dim, 0, 0, offset]</c> members.
        ///
        /// Every field is validated before the record is accepted, because the scan
        /// walks the slot word by word and a false positive would consume the records
        /// that follow it.
        /// </summary>
        private static bool TryReadCBuffer(ReadOnlySpan<byte> slot, string name, int next,
                                           out CBufferRecord cb, out int after)
        {
            cb = null;
            after = next;
            if (next + 8 > slot.Length) return false;

            int size = BitConverter.ToInt32(slot.Slice(next, 4));
            int count = BitConverter.ToInt32(slot.Slice(next + 4, 4));
            if (size < 0 || size > 1 << 20 || count < 0 || count > 4096) return false;

            var members = new List<CBufferMember>(count);
            int at = next + 8;
            for (int i = 0; i < count; i++)
            {
                if (!TryReadName(slot, at, out string member, out int mn)) return false;
                if (mn + 24 > slot.Length) return false;
                int offset = BitConverter.ToInt32(slot.Slice(mn + 20, 4));
                if (offset < 0 || offset >= size) return false;
                members.Add(new CBufferMember { Name = member, Offset = offset });
                at = mn + 24;
            }

            cb = new CBufferRecord { Name = name, Size = size, Members = members };
            after = at;
            return true;
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
