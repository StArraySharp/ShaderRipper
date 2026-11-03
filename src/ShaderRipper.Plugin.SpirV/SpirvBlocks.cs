using System;
using System.Collections.Generic;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// One constant buffer as the SPIR-V module declares it: which descriptor the
    /// runtime binds it to, and where each member starts in bytes.
    ///
    /// SPIRV-Cross names the block and its members after SPIR-V ids, which say nothing
    /// about what they hold.  The payload's parameter slot does -- it keeps the name
    /// Unity compiled from and the byte offset of every member -- but it is keyed by
    /// the descriptor binding, so the two tables are joined here, through the module.
    /// </summary>
    internal sealed class SpirvBlock
    {
        /// <summary>Result id of the <c>OpVariable</c> that declares the block.</summary>
        public int Variable;

        /// <summary>Result id of the <c>OpTypeStruct</c> the variable points at.</summary>
        public int Struct;

        /// <summary>Descriptor set the runtime binds this block to, or -1.</summary>
        public int Set = -1;

        /// <summary>Binding within <see cref="Set"/>, or -1.</summary>
        public int Binding = -1;

        /// <summary>Member index to byte offset, as <c>OpMemberDecorate MemberOffset</c> gives it.</summary>
        public readonly Dictionary<int, int> MemberOffsets = new();

        /// <summary>How many members the block struct declares.</summary>
        public int MemberCount;
    }

    /// <summary>
    /// The constant buffers a SPIR-V module declares, read straight out of its
    /// instructions.
    ///
    /// Only three decorations matter: <c>DescriptorSet</c> and <c>Binding</c> on the
    /// variable (which is what the payload's register map is keyed by) and
    /// <c>MemberOffset</c> on the struct (which is what the payload's member table is
    /// keyed by).  Unity compiles with an explicit layout, so every member carries one.
    /// </summary>
    internal static class SpirvBlocks
    {
        private const int OpTypeStruct = 30;
        private const int OpTypePointer = 32;
        private const int OpVariable = 59;
        private const int OpDecorate = 71;
        private const int OpMemberDecorate = 72;

        private const int DecorationBinding = 33;
        private const int DecorationDescriptorSet = 34;
        private const int DecorationMemberOffset = 35;

        /// <summary>Uniform storage class, the one a cbuffer uses.</summary>
        private const int StorageClassUniform = 2;

        /// <summary>Every uniform block in the module, keyed by the variable's result id.</summary>
        public static Dictionary<int, SpirvBlock> Read(byte[] spirv)
        {
            var result = new Dictionary<int, SpirvBlock>();
            if (spirv == null || spirv.Length < 20) return result;

            uint[] words = new uint[spirv.Length / 4];
            Buffer.BlockCopy(spirv, 0, words, 0, spirv.Length);

            var pointers = new Dictionary<int, int>();          // pointer id -> pointee id
            var structMembers = new Dictionary<int, int>();     // struct id -> member count
            var variables = new Dictionary<int, int>();         // variable id -> pointee id
            var sets = new Dictionary<int, int>();
            var bindings = new Dictionary<int, int>();
            var offsets = new Dictionary<(int, int), int>();

            int at = 5;   // past the five-word header
            while (at < words.Length)
            {
                int op = (int)(words[at] & 0xFFFF);
                int count = (int)(words[at] >> 16);
                if (count <= 0 || at + count > words.Length) break;

                switch (op)
                {
                    case OpTypePointer when count >= 4:
                        pointers[(int)words[at + 1]] = (int)words[at + 3];
                        break;

                    case OpTypeStruct when count >= 2:
                        structMembers[(int)words[at + 1]] = count - 2;
                        break;

                    case OpVariable when count >= 4 && (int)words[at + 3] == StorageClassUniform:
                        variables[(int)words[at + 2]] = (int)words[at + 1];
                        break;

                    case OpDecorate when count >= 4:
                    {
                        int target = (int)words[at + 1];
                        int decoration = (int)words[at + 2];
                        if (decoration == DecorationDescriptorSet) sets[target] = (int)words[at + 3];
                        else if (decoration == DecorationBinding) bindings[target] = (int)words[at + 3];
                        break;
                    }

                    case OpMemberDecorate when count >= 5:
                        if ((int)words[at + 3] == DecorationMemberOffset)
                            offsets[((int)words[at + 1], (int)words[at + 2])] = (int)words[at + 4];
                        break;
                }

                at += count;
            }

            foreach (var kv in variables)
            {
                if (!pointers.TryGetValue(kv.Value, out int pointee)) continue;
                if (!structMembers.TryGetValue(pointee, out int members)) continue;

                var block = new SpirvBlock
                {
                    Variable = kv.Key,
                    Struct = pointee,
                    MemberCount = members,
                };
                if (sets.TryGetValue(kv.Key, out int set)) block.Set = set;
                if (bindings.TryGetValue(kv.Key, out int binding)) block.Binding = binding;
                for (int i = 0; i < members; i++)
                    if (offsets.TryGetValue((pointee, i), out int off)) block.MemberOffsets[i] = off;
                result[kv.Key] = block;
            }
            return result;
        }
    }
}
