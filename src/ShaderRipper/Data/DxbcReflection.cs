using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Rebuilds the RDEF chunk Unity strips out of the DXBC it ships.
    ///
    /// The container in a game holds no reflection: fxc wrote the constant-buffer
    /// layout and the resource names, UnityShaderCompiler read them and then removed
    /// the chunk.  None of that information is lost, though -- the same names sit in
    /// the asset's parameter blobs (<see cref="BlobTool.ParseParamBlobs"/>,
    /// <see cref="BlobTool.ParseBlobTextureParams"/>), which is where the caller
    /// resolves them from, together with the registers the disassembly declares.
    ///
    /// So the chunk is written from that data rather than copied from elsewhere.  A
    /// consumer that reads reflection by name -- HLSLcc, fxc's own disassembler --
    /// then sees `UnityPerMaterial { vec4 _Color; }` and `_MainTex` instead of
    /// `_cb0 { vec4 _m0[3]; }` and `_tex0`.
    ///
    /// The word layout below was measured field by field against what
    /// <c>d3dcompiler_47</c> writes for the same program.  The shader-model-4 part
    /// matches the published description of the format (Tim Jones, "Parsing Direct3D
    /// shader bytecode", 2015) field for field; shader model 5 prepends the
    /// <c>RD11</c> block and widens the member and type records, which that write-up
    /// does not cover and which is where the measurements came in:
    /// <code>
    ///   0  u32 constant buffers
    ///   4  u32 constant-buffer table offset
    ///   8  u32 bound resources
    ///  12  u32 bound-resource table offset: 60 for shader model 5, 28 below it
    ///  16  u8 minor, u8 major, u16 shader type
    ///  20  u32 flags (0x100 in every fxc output)
    ///  24  u32 creator string offset
    ///  28  only from shader model 5 on: "RD11", then 60, 24, 32, 40, 36, 12, 0 --
    ///      the record sizes of the reflection layout, kept verbatim
    /// </code>
    /// then, from the table offset on: the bound resources, the name strings, the
    /// constant-buffer table, the member records and the type records.  The record
    /// shapes and their D3D names:
    /// <code>
    ///   binding (32):   D3D11_SHADER_INPUT_BIND_DESC -- name, type, return type,
    ///                   dimension, samples, bind point, bind count, flags
    ///   buffer (24):    D3D11_SHADER_BUFFER_DESC     -- name, member count, member
    ///                   table offset, byte size, flags, type
    ///   member (24/40): D3D11_SHADER_VARIABLE_DESC   -- name, start offset, size,
    ///                   flags, type offset, default value, then (5.0 only) start
    ///                   texture, texture size, start sampler, sampler size
    ///   type (16/36):   D3D11_SHADER_TYPE_DESC       -- class, component type, rows,
    ///                   columns, elements, member count, member table offset, then
    ///                   (5.0 only) parent type offset, two unknown words, a further
    ///                   unknown word, and the type-name offset
    /// </code>
    /// The member and type records are the ones that change with the shader model, and
    /// a reader assumes the size its own version implies: 40 and 36 for a 5.0 chunk,
    /// 24 and 16 below it.
    ///
    /// Every offset in the chunk is relative to the chunk body.
    /// </summary>
    internal sealed class DxbcReflection
    {
        /// <summary>The header every version shares: the four counts and the version word.</summary>
        private const int HeaderBytes = 28;

        /// <summary>
        /// Where the tables start: 60 for a shader model 5 chunk, which carries the
        /// <c>RD11</c> marker and a 28-byte block of record sizes after the header;
        /// 28 for an older chunk, which carries neither.
        /// </summary>
        private static int TableOffset(int shaderModel) => shaderModel >= 5 ? 60 : HeaderBytes;

        /// <summary>Size of one constant-buffer member record.</summary>
        private static int MemberRecordSize(int shaderModel) => shaderModel >= 5 ? 40 : 24;

        /// <summary>Size of one type record.</summary>
        private static int TypeRecordSize(int shaderModel) => shaderModel >= 5 ? 36 : 16;

        /// <summary>D3D_SHADER_VARIABLE_CLASS values this writer emits.</summary>
        private enum VariableClass
        {
            Scalar = 0,
            Vector = 1,
            MatrixColumns = 3,
            Struct = 5,
        }

        /// <summary>D3D_SHADER_VARIABLE_TYPE values this writer emits.</summary>
        private enum VariableType
        {
            Bool = 1,
            Int = 2,
            Float = 3,
            UInt = 19,
        }

        /// <summary>A constant-buffer member: a row, a scalar lane, an array or a matrix.</summary>
        public sealed class Member
        {
            public string Name = "";
            /// <summary>HLSL type spelling (<c>float4</c>, <c>float4x4</c>, <c>uint</c>).</summary>
            public string Type = "float4";
            /// <summary>Byte offset inside the buffer.</summary>
            public int Offset;
            /// <summary>Byte size, array elements included.</summary>
            public int Size;
            /// <summary>Array length when <see cref="IsArray"/>.</summary>
            public int Rows = 1;
            public bool IsArray;
            public bool Matrix;
            public IReadOnlyList<Member> Fields;
        }

        public sealed class ConstantBuffer
        {
            public string Name = "";
            public int Register;
            /// <summary>Declared byte size.</summary>
            public int Size;
            public bool ResourceElement;
            public readonly List<Member> Members = new();
        }

        public enum BindingKind
        {
            Texture,
            Sampler,
            Uav,
        }

        public sealed class Binding
        {
            public string Name = "";
            public int Register;
            public BindingKind Kind;
            /// <summary>D3D_SRV_DIMENSION of the view.</summary>
            public int Dimension = 4;
            /// <summary>Sample count; 0 when the resource is not multisampled.</summary>
            public int Samples;
            /// <summary>
            /// A byte-address buffer (D3D_SIT_BYTEADDRESS) rather than a texture view.
            /// A typed <c>Buffer&lt;float4&gt;</c> is not one: fxc records it as a
            /// texture whose dimension is <c>BUFFER</c>.
            /// </summary>
            public bool ByteAddress;
            public int StructureStride;
        }

        public readonly List<ConstantBuffer> Buffers = new();
        public readonly List<Binding> Bindings = new();

        /// <summary>Anything to write?  A program with no declarations has no reflection.</summary>
        public bool Any => Buffers.Count > 0 || Bindings.Count > 0;

        public static Dictionary<int, string> ReadTextureNames(byte[] rdef)
        {
            var names = new Dictionary<int, string>();
            if (rdef == null || rdef.Length < 8 + HeaderBytes) return names;
            uint count = BitConverter.ToUInt32(rdef, 16);
            uint offset = BitConverter.ToUInt32(rdef, 20);
            bool extended = rdef[25] > 5 || rdef[25] == 5 && rdef[24] >= 1;
            int stride = extended ? 40 : 32;
            if (count == 0 || 8L + offset + stride * (long)count > rdef.Length) return names;
            for (int i = 0; i < count; i++)
            {
                int record = 8 + (int)offset + i * stride;
                uint kind = BitConverter.ToUInt32(rdef, record + 4);
                if (kind is not (2 or 5 or 7)) continue;
                uint register = BitConverter.ToUInt32(rdef, record + 20);
                uint name = BitConverter.ToUInt32(rdef, record);
                if (register >= 128 || 8L + name >= rdef.Length) continue;
                int end = Array.IndexOf(rdef, (byte)0, 8 + (int)name);
                if (end > 8 + name)
                    names.TryAdd((int)register, Encoding.ASCII.GetString(rdef, 8 + (int)name, end - 8 - (int)name));
            }
            return names;
        }

        public void ApplyAssetBuffers(SubProgram program)
        {
            if (program == null) return;

            foreach (var buffer in Buffers)
            {
                bool bound = program.ConstantBufferBindings.TryGetValue(buffer.Register, out string name);
                var layout = program.ConstantBuffers.Find(cb => cb.Name == (bound ? name : buffer.Name));
                if (layout == null || layout.Size <= 0 || (!bound && layout.Size < buffer.Size)) continue;

                var members = new List<Member>();
                foreach (var source in layout.Members.OrderBy(m => m.Offset))
                {
                    if (string.IsNullOrEmpty(source.Name) || source.Offset < 0 || source.Offset % 4 != 0)
                        continue;

                    string scalar = source.Type switch
                    {
                        0 or 1 => "float",
                        2 or 4 => "int",
                        3 => "bool",
                        5 => "uint",
                        _ => null,
                    };
                    if (scalar == null) continue;

                    int width = source.IsMatrix ? source.RowCount : source.Dim;
                    if (width is < 1 or > 4) continue;
                    int elements = Math.Max(1, source.ArraySize);
                    int vectors = source.IsMatrix ? width : 1;
                    long size = ((long)elements * vectors - 1) * 16 + 4 * width;
                    if (source.Offset + size > layout.Size ||
                        (source.IsMatrix || source.ArraySize > 0 ? source.Offset % 16 != 0
                                                                  : source.Offset % 16 + size > 16) ||
                        (members.Count > 0 && End(members[^1]) > source.Offset)) continue;

                    members.Add(new Member
                    {
                        Name = source.Name,
                        Offset = source.Offset,
                        Size = (int)size,
                        Type = source.IsMatrix ? $"{scalar}{width}x{width}"
                             : width == 1 ? scalar : scalar + width,
                        Matrix = source.IsMatrix,
                        IsArray = source.ArraySize > 0,
                        Rows = elements,
                    });
                }
                if (members.Count == 0) continue;

                // Partial layouts can omit members. Keep inferred entries only when
                // they describe the same buffer and don't overlap the asset layout.
                if (layout.Partial && buffer.Name == layout.Name)
                    members.AddRange(buffer.Members.Where(m => members.All(a =>
                        m.Name != a.Name && (End(a) <= m.Offset || End(m) <= a.Offset))));

                buffer.Name = layout.Name;
                buffer.Size = Math.Max(buffer.Size, layout.Size);
                buffer.Members.Clear();
                buffer.Members.AddRange(members);
                FillGaps(buffer);
            }

            static int End(Member m) => m.Offset + m.Size;
        }

        /// <summary>
        /// Put <paramref name="cb"/>'s members in offset order and add a placeholder
        /// for every row no member covers.
        ///
        /// The declaration layer leaves a gap where nothing in this program named a
        /// row (a sibling variant's uniform), and a reader that lays the members out
        /// in order would then place every later member on the wrong row.  A filler
        /// keeps the offsets the same ones the emitted <c>packoffset</c> pins put them
        /// at, and a placeholder costs nothing: it names bytes nothing reads.
        /// </summary>
        public static void FillGaps(ConstantBuffer cb)
        {
            if (cb.Members.Count == 0) return;
            var sorted = new List<Member>(cb.Members);
            sorted.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            var dense = new List<Member>(sorted.Count);
            int cursor = 0;                              // next byte no member covers
            foreach (var m in sorted)
            {
                for (int at = (cursor + 15) & ~15; at + 16 <= m.Offset; at += 16)
                    dense.Add(new Member
                    {
                        Name = $"_cb{cb.Register}_{at / 16}", Type = "float4", Offset = at, Size = 16,
                    });
                dense.Add(m);
                cursor = Math.Max(cursor, m.Offset + m.Size);
            }
            cb.Members.Clear();
            cb.Members.AddRange(dense);
            cb.Size = Math.Max(cb.Size, (cursor + 15) & ~15);
        }

        // A bytecode operand can select an unreflected lane in an otherwise named
        // row. Give it a distinct field without moving or renaming the known fields.
        internal static void CoverBufferLanes(ConstantBuffer buffer)
        {
            var members = buffer.Members.OrderBy(member => member.Offset).ToArray();
            var names = members.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
            buffer.Members.Clear();
            int cursor = 0;
            foreach (var member in members)
            {
                AddPadding(member.Offset);
                buffer.Members.Add(member);
                cursor = Math.Max(cursor, member.Offset + member.Size);
            }
            AddPadding(buffer.Size);

            void AddPadding(int end)
            {
                while (cursor + 4 <= end)
                {
                    int width = Math.Min((end - cursor) / 4, 4 - cursor % 16 / 4);
                    // Keep synthesized vector padding aligned for std140 readers.
                    if (cursor % 16 != 0)
                        width = Math.Min(width, cursor % 8 == 0 ? 2 : 1);
                    string name = $"_cb{buffer.Register}_padding{cursor / 4}";
                    while (!names.Add(name)) name += "_";
                    buffer.Members.Add(new Member
                    {
                        Name = name, Type = width == 1 ? "float" : $"float{width}",
                        Offset = cursor, Size = width * 4,
                    });
                    cursor += width * 4;
                }
            }
        }

        /// <summary>
        /// <paramref name="container"/> with an RDEF built from this reflection as its
        /// first chunk, or null when the container cannot take one (not DXBC, or a
        /// DXIL payload, whose reflection lives in the DXIL metadata).
        /// </summary>
        public byte[] Complete(byte[] container, string stage, string profile)
        {
            if (!Any || !DxbcContainer.IsContainer(container) || BlobTool.HasDxilChunk(container) ||
                ShaderModel(profile) > 5 || (profile?.EndsWith("_5_1", StringComparison.Ordinal) ?? false))
                return null;
            foreach (var buffer in Buffers)
                CoverBufferLanes(buffer);
            return DxbcContainer.WithFirstChunk(container, DxbcContainer.Wrap("RDEF", Build(stage, profile)));
        }

        /// <summary>The RDEF chunk body (the four-character code and size excluded).</summary>
        public byte[] Build(string stage, string profile)
        {
            int shaderModel = ShaderModel(profile);
            int minorVersion = profile != null && profile.EndsWith("_1", StringComparison.Ordinal) ? 1 : 0;
            int tableOffset = TableOffset(shaderModel);

            // Bound resources: the declared textures, samplers and UAVs, then the
            // constant buffers, which fxc lists here too so a consumer can bind by
            // register without walking the buffer table.
            int resourceCount = Bindings.Count + Buffers.Count;
            var buffers = Buffers.Concat(Bindings.Where(binding => binding.StructureStride > 0)
                .Select(StructuredElement)).ToList();

            var strings = new StringTable(tableOffset + 32 * resourceCount);
            var resourceNames = new List<int>();
            foreach (var b in Bindings) resourceNames.Add(strings.Add(b.Name));
            var bufferNames = new List<int>();
            foreach (var cb in buffers) bufferNames.Add(strings.Add(cb.Name));
            var memberNames = new List<List<int>>();
            var typeNames = new List<List<int>>();
            int memberCount = 0;
            int fieldCount = 0;
            foreach (var cb in buffers)
            {
                var names = new List<int>();
                var types = new List<int>();
                foreach (var m in cb.Members)
                {
                    names.Add(strings.Add(m.Name));
                    // The type record's last 5.0 word points at the type spelling;
                    // zero means "unnamed", which a reader accepts but then cannot
                    // tell a matrix from four rows of floats.
                    types.Add(shaderModel >= 5 ? strings.Add(TypeName(m)) : 0);
                    if (m.Fields == null) continue;
                    foreach (var field in m.Fields)
                    {
                        strings.Add(field.Name);
                        if (shaderModel >= 5) strings.Add(TypeName(field));
                    }
                    fieldCount += m.Fields.Count;
                }
                memberNames.Add(names);
                typeNames.Add(types);
                memberCount += cb.Members.Count;
            }

            int stringOffset = strings.Base;
            int bufferTableOffset = stringOffset + strings.Length;
            int memberRecordSize = MemberRecordSize(shaderModel);
            int typeRecordSize = TypeRecordSize(shaderModel);
            int memberOffset = bufferTableOffset + 24 * buffers.Count;
            int typeOffset = memberOffset + memberRecordSize * memberCount;
            int fieldOffset = typeOffset + typeRecordSize * (memberCount + fieldCount);
            string creator = "ShaderRipper (reflection rebuilt from the Unity parameter blobs)";
            int creatorOffset = fieldOffset + 12 * fieldCount;

            int bodySize = (creatorOffset + Encoding.UTF8.GetByteCount(creator) + 4) & ~3;
            var body = new byte[bodySize];
            Put(body, 0, (uint)buffers.Count);
            Put(body, 4, (uint)bufferTableOffset);
            Put(body, 8, (uint)resourceCount);
            Put(body, 12, (uint)tableOffset);
            uint shaderType = stage switch
            {
                "ps" => 0xFFFF,
                "vs" => 0xFFFE,
                "cs" => 0x4353,     // 'C' 'S' -- what fxc writes for a compute program
                "gs" => 0xFFFD,
                "hs" => 0xFFFC,
                "ds" => 0xFFFB,
                _ => 0xFFFF,
            };
            Put(body, 16, (uint)(minorVersion | (shaderModel << 8) | ((int)shaderType << 16)));
            Put(body, 20, 0x100);
            Put(body, 24, (uint)creatorOffset);
            if (tableOffset > HeaderBytes)
            {
                Encoding.ASCII.GetBytes("RD11").CopyTo(body, HeaderBytes);
                // Fixed in every shader-model-5 chunk this was measured against; a
                // reader treats them as the record sizes it is about to walk.
                uint[] preamble = { (uint)tableOffset, 24, 32, 40, 36, 12, 0 };
                for (int i = 0; i < preamble.Length; i++) Put(body, HeaderBytes + 4 + 4 * i, preamble[i]);
            }

            int at = tableOffset;
            for (int i = 0; i < Bindings.Count; i++, at += 32)
            {
                var b = Bindings[i];
                bool byteAddress = b.ByteAddress && b.Kind != BindingKind.Sampler;
                bool structured = b.StructureStride > 0;
                uint inputType = b.Kind switch
                {
                    BindingKind.Sampler => 3u,
                    _ when structured => 5u, // D3D_SIT_STRUCTURED
                    _ => byteAddress ? 7u : 2u,
                };
                // D3D_RETURN_TYPE: UNORM(1) for a comparison sampler's float result is
                // not what fxc records there -- it records 0 for a sampler, FLOAT(5)
                // for a texture view and UINT(6) for a byte-address buffer.
                uint returnType = b.Kind == BindingKind.Sampler ? 0u : byteAddress || structured ? 6u : 5u;
                uint dimension = b.Kind == BindingKind.Sampler ? 0u
                               : byteAddress || structured ? 1u        // D3D_SRV_DIMENSION_BUFFER
                               : (uint)Math.Max(b.Dimension, 1);
                uint samples = structured ? (uint)b.StructureStride
                             : b.Kind == BindingKind.Sampler || byteAddress ? 0u
                             : b.Samples > 0 ? (uint)b.Samples : 0xFFFFFFFFu;
                Put(body, at, (uint)resourceNames[i]);
                Put(body, at + 4, inputType);
                Put(body, at + 8, returnType);
                Put(body, at + 12, dimension);
                Put(body, at + 16, samples);
                Put(body, at + 20, (uint)b.Register);
                Put(body, at + 24, 1u);
                // D3D_SVF_USERPACKED (the declarations name their registers) plus, for
                // a texture view, the "four components" bit pair fxc sets on one.
                Put(body, at + 28, inputType == 2u ? 0xDu : 1u);
            }
            for (int i = 0; i < Buffers.Count; i++, at += 32)
            {
                Put(body, at, (uint)bufferNames[i]);
                Put(body, at + 4, 0u);              // type: D3D_SIT_CBUFFER
                Put(body, at + 20, (uint)Buffers[i].Register);
                Put(body, at + 24, 1u);
                Put(body, at + 28, 1u);             // D3D_SVF_USERPACKED
            }

            strings.CopyTo(body, stringOffset);

            at = bufferTableOffset;
            int memberAt = memberOffset;
            int typeAt = typeOffset;
            int fieldAt = fieldOffset;
            for (int i = 0; i < buffers.Count; i++, at += 24)
            {
                var cb = buffers[i];
                Put(body, at, (uint)bufferNames[i]);
                Put(body, at + 4, (uint)cb.Members.Count);
                Put(body, at + 8, (uint)memberAt);
                Put(body, at + 12, (uint)cb.Size);
                Put(body, at + 16, 0u);             // D3D_SHADER_CBUFFER_FLAGS: none
                Put(body, at + 20, cb.ResourceElement ? 3u : 0u); // RESOURCE_BIND_INFO / CBUFFER

                for (int j = 0; j < cb.Members.Count; j++)
                {
                    var m = cb.Members[j];
                    Put(body, memberAt, (uint)memberNames[i][j]);
                    Put(body, memberAt + 4, (uint)m.Offset);
                    Put(body, memberAt + 8, (uint)m.Size);
                    Put(body, memberAt + 12, 2u);   // D3D_SVF_USED
                    Put(body, memberAt + 16, (uint)typeAt);
                    Put(body, memberAt + 20, 0u);   // no default value
                    if (memberRecordSize > 24)
                    {
                        // A 5.0 record also carries the texture and sampler a member
                        // binds; a constant-buffer member binds none.
                        Put(body, memberAt + 24, 0xFFFFFFFF);
                        Put(body, memberAt + 28, 0u);
                        Put(body, memberAt + 32, 0xFFFFFFFF);
                        Put(body, memberAt + 36, 0u);
                    }

                    WriteType(body, typeAt, m, typeNames[i][j]);
                    typeAt += typeRecordSize;
                    if (m.Fields != null)
                    {
                        Put(body, typeAt - typeRecordSize + 12, (uint)fieldAt);
                        foreach (var field in m.Fields)
                        {
                            Put(body, fieldAt, (uint)strings.Add(field.Name));
                            Put(body, fieldAt + 4, (uint)typeAt);
                            Put(body, fieldAt + 8, (uint)field.Offset);
                            WriteType(body, typeAt, field, shaderModel >= 5 ? strings.Add(TypeName(field)) : 0);
                            typeAt += typeRecordSize;
                            fieldAt += 12;
                        }
                    }
                    memberAt += memberRecordSize;
                }
            }

            Encoding.UTF8.GetBytes(creator).CopyTo(body, creatorOffset);
            return body;
        }

        private static ConstantBuffer StructuredElement(Binding binding)
        {
            // The disassembly records the stride but can omit field types. Reflect
            // the bytecode's addressable words without inventing an asset schema.
            var layout = new ConstantBuffer
            {
                Name = binding.Name, Register = binding.Register,
                Size = binding.StructureStride, ResourceElement = true,
            };
            layout.Members.Add(new Member
            {
                Name = "$Element", Type = "_StructuredElement", Size = layout.Size,
                Fields = Enumerable.Range(0, layout.Size / 4).Select(index => new Member
                {
                    Name = $"word{index}", Type = "uint", Offset = index * 4, Size = 4,
                }).ToArray(),
            });
            return layout;
        }

        /// <summary>
        /// One <c>D3D11_SHADER_TYPE_DESC</c> record.
        ///
        /// The class and component type say what the member is (a scalar, a vector, a
        /// column-major matrix), <c>elements</c> carries an array length, and the 5.0
        /// tail points at the type spelling -- without it a reader sees a matrix as
        /// four rows of a vector, which is the same bytes but tells a consumer far
        /// less.
        /// </summary>
        private static void WriteType(byte[] body, int at, Member m, int nameOffset)
        {
            if (m.Fields != null)
            {
                Put(body, at, (uint)VariableClass.Struct);
                Put(body, at + 4, ((uint)m.Fields.Count << 16) | 1u);
                Put(body, at + 8, (uint)m.Fields.Count << 16);
                if (nameOffset != 0) Put(body, at + 32, (uint)nameOffset);
                return;
            }

            int varClass = (int)VariableClass.Vector;
            int varType = (int)VariableType.Float;
            int rows = 1;
            int columns = 4;
            string ty = m.Type ?? "";

            if (ty == "float")
            {
                varClass = (int)VariableClass.Scalar;
                rows = columns = 1;
            }
            else if (ty.StartsWith("float", StringComparison.Ordinal))
            {
                string tail = ty[5..];
                int cross = tail.IndexOf('x');
                if (m.Matrix || cross >= 0)
                {
                    varClass = (int)VariableClass.MatrixColumns;
                    rows = ParseOr(cross >= 0 ? tail[..cross] : tail, 4);
                    columns = ParseOr(cross >= 0 ? tail[(cross + 1)..] : tail, 4);
                }
                else
                {
                    columns = ParseOr(tail, 4);
                }
            }
            else
            {
                // bool / int / uint, scalar or vector: the component type comes from
                // the prefix, the lane count from the trailing digit.
                (int type, string rest) = ty.StartsWith("bool", StringComparison.Ordinal) ? (1, ty[4..])
                    : ty.StartsWith("int", StringComparison.Ordinal) ? (2, ty[3..])
                    : ty.StartsWith("uint", StringComparison.Ordinal) ? (19, ty[4..])
                    : ((int)VariableType.Float, "");
                varType = type;
                varClass = rest.Length == 0 ? (int)VariableClass.Scalar : (int)VariableClass.Vector;
                rows = 1;
                columns = rest.Length == 0 ? 1 : ParseOr(rest, 4);
            }

            // class (u16), component type (u16), rows (u16), columns (u16)
            Put(body, at, (uint)((varType << 16) | varClass));
            Put(body, at + 4, (uint)((columns << 16) | rows));
            // elements (u16), struct member count (u16).  An array of rows: `float4
            // x[3]` is a 1x4 vector repeated three times.
            Put(body, at + 8, (uint)(m.IsArray ? Math.Max(m.Rows, 1) : 0));
            // The member table offset (at + 12) stays zero: no member of a buffer is a
            // struct this writer invents.
            if (nameOffset != 0) Put(body, at + 32, (uint)nameOffset);
        }

        /// <summary>
        /// The spelling a type record name points at, in the form fxc uses
        /// (<c>float4</c>, <c>float4x4</c>, <c>uint2</c>).
        ///
        /// A matrix is declared as <c>float4x4</c> so that is what it is called; an
        /// array member names its element type, which is what the reader shows.
        /// </summary>
        private static string TypeName(Member m)
        {
            string ty = m.Type ?? "float4";
            if (ty is "float" or "int" or "uint" or "bool") return ty;
            if (m.Matrix && !ty.Contains('x')) return "float4x4";
            return ty;
        }

        private static int ParseOr(string text, int fallback) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0
                ? v : fallback;

        /// <summary>
        /// The major version fxc stamps into the chunk, from the profile the program
        /// was compiled for (<c>ps_5_0</c> is 5).
        /// </summary>
        private static int ShaderModel(string profile)
        {
            if (string.IsNullOrEmpty(profile)) return 5;
            var parts = profile.Split('_');
            return parts.Length > 1 &&
                   int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0
                ? v : 5;
        }

        private static void Put(byte[] body, int at, uint value)
        {
            body[at] = (byte)value;
            body[at + 1] = (byte)(value >> 8);
            body[at + 2] = (byte)(value >> 16);
            body[at + 3] = (byte)(value >> 24);
        }

        /// <summary>
        /// The chunk's string area: every name once, each terminated and padded to
        /// four bytes, addressed by its offset from the chunk body.
        /// </summary>
        private sealed class StringTable
        {
            private readonly List<byte> _bytes = new();
            private readonly Dictionary<string, int> _offsets = new(StringComparer.Ordinal);

            public StringTable(int baseOffset) => Base = baseOffset;

            /// <summary>Offset the table starts at in the chunk body.</summary>
            public int Base { get; }

            public int Length => _bytes.Count;

            public int Add(string name)
            {
                name ??= "";
                if (_offsets.TryGetValue(name, out int known)) return known;
                int at = Base + _bytes.Count;
                _offsets[name] = at;
                _bytes.AddRange(Encoding.UTF8.GetBytes(name));
                _bytes.Add(0);
                while (_bytes.Count % 4 != 0) _bytes.Add(0xAB);
                return at;
            }

            public void CopyTo(byte[] body, int at) => _bytes.CopyTo(0, body, at, _bytes.Count);
        }
    }
}
