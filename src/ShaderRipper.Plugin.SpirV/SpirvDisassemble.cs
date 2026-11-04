using System;
using System.Collections.Generic;
using System.Text;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// Renders a decoded SPIR-V module as text, for the <c>--embed-original</c>
    /// comment block.
    ///
    /// This is deliberately a dump and not a disassembler: it walks the instruction
    /// stream and prints one line per instruction with the opcode's name and its raw
    /// operands.  A full disassembler would have to model types, constants and the
    /// decoration grammar to name operands the way <c>spirv-dis</c> does, and the
    /// point here is comparison -- "did the translation read the same instructions I
    /// think it did" -- which the opcode stream already answers.
    ///
    /// The module is the one Unity shipped, after SMOL-V decoding: SMOL-V is a
    /// byte-level re-encoding of the same instruction stream, so the decoded form is
    /// the honest representation of what the plugin translated.
    /// </summary>
    internal static class SpirvDisassemble
    {
        /// <summary>
        /// A one-line summary followed by every instruction, one per line, with the
        /// word offset it starts at.
        /// </summary>
        public static string Describe(byte[] module)
        {
            if (module == null || module.Length < 20) return null;

            var sb = new StringBuilder(module.Length * 2);
            sb.Append("; ").Append(module.Length).Append(" bytes, ");

            int count = 0;
            int at = 20;                       // past the five-word header
            while (at + 4 <= module.Length)
            {
                uint word = BitConverter.ToUInt32(module, at);
                int opcode = (int)(word & 0xFFFF);
                int words = (int)(word >> 16);
                if (words == 0 || at + words * 4 > module.Length) break;
                count++;
                at += words * 4;
            }
            sb.Append(count).Append(" instruction(s)\n");

            at = 0;
            while (at + 4 <= module.Length)
            {
                // The five-word header (magic, version, generator, bound, schema) is
                // not an instruction: word 0 there is the magic number, and reading it
                // as an opcode/length pair yields a bogus 1827-word instruction that
                // fails the bounds check below and stops the walk.  The first
                // instruction starts right after the header.
                if (at == 0)
                {
                    sb.Append("0000  ; header\n");
                    at = 20;
                    continue;
                }

                uint word = BitConverter.ToUInt32(module, at);
                int opcode = (int)(word & 0xFFFF);
                int words = (int)(word >> 16);
                if (words == 0 || at + words * 4 > module.Length) break;

                // A word offset, the opcode, and the operands as raw unsigned words.
                // Word 0 of an instruction holds the opcode and the length, so the
                // operands start at word 1.
                sb.Append(at.ToString("X4")).Append("  ");
                sb.Append(Name(opcode).PadRight(26));
                for (int i = 1; i < words; i++)
                {
                    if (i > 1) sb.Append(' ');
                    sb.Append(BitConverter.ToUInt32(module, at + i * 4));
                }
                sb.Append('\n');
                at += words * 4;
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The opcode's mnemonic, for the ones a shader module uses.  Anything else
        /// prints as <c>Op&lt;number&gt;</c>, which is still enough to line the
        /// instruction up with <c>spirv-dis</c> output.
        /// </summary>
        private static string Name(int opcode) => opcode switch
        {
            0 => "OpNop",
            1 => "OpUndef",
            2 => "OpSourceContinued",
            3 => "OpSource",
            4 => "OpSourceExtension",
            5 => "OpName",
            6 => "OpMemberName",
            7 => "OpString",
            8 => "OpLine",
            10 => "OpExtension",
            11 => "OpExtInstImport",
            12 => "OpExtInst",
            14 => "OpMemoryModel",
            15 => "OpEntryPoint",
            16 => "OpExecutionMode",
            17 => "OpCapability",
            19 => "OpTypeVoid",
            20 => "OpTypeBool",
            21 => "OpTypeInt",
            22 => "OpTypeFloat",
            23 => "OpTypeVector",
            24 => "OpTypeMatrix",
            25 => "OpTypeImage",
            26 => "OpTypeSampler",
            27 => "OpTypeSampledImage",
            28 => "OpTypeArray",
            29 => "OpTypeRuntimeArray",
            30 => "OpTypeStruct",
            31 => "OpTypeOpaque",
            32 => "OpTypePointer",
            33 => "OpTypeFunction",
            41 => "OpConstantTrue",
            42 => "OpConstantFalse",
            43 => "OpConstant",
            44 => "OpConstantComposite",
            46 => "OpConstantNull",
            48 => "OpSpecConstantTrue",
            49 => "OpSpecConstantFalse",
            50 => "OpSpecConstant",
            51 => "OpSpecConstantComposite",
            54 => "OpFunction",
            55 => "OpFunctionParameter",
            56 => "OpFunctionEnd",
            57 => "OpFunctionCall",
            59 => "OpVariable",
            61 => "OpLoad",
            62 => "OpStore",
            65 => "OpAccessChain",
            66 => "OpInBoundsAccessChain",
            68 => "OpArrayLength",
            71 => "OpDecorate",
            72 => "OpMemberDecorate",
            73 => "OpDecorationGroup",
            74 => "OpGroupDecorate",
            79 => "OpVectorExtractDynamic",
            80 => "OpVectorInsertDynamic",
            81 => "OpVectorShuffle",
            82 => "OpCompositeConstruct",
            83 => "OpCompositeExtract",
            84 => "OpCompositeInsert",
            86 => "OpTranspose",
            87 => "OpSampledImage",
            88 => "OpImageSampleImplicitLod",
            89 => "OpImageSampleExplicitLod",
            90 => "OpImageSampleDrefImplicitLod",
            91 => "OpImageSampleDrefExplicitLod",
            96 => "OpImageFetch",
            98 => "OpImageWrite",
            99 => "OpImage",
            100 => "OpImageQuerySizeLod",
            102 => "OpImageQueryLod",
            104 => "OpConvertFToU",
            105 => "OpConvertFToS",
            106 => "OpConvertSToF",
            107 => "OpConvertUToF",
            109 => "OpUConvert",
            110 => "OpSConvert",
            111 => "OpFConvert",
            112 => "OpQuantizeToF16",
            113 => "OpConvertPtrToU",
            114 => "OpSatConvertSToU",
            115 => "OpSatConvertUToS",
            116 => "OpConvertUToPtr",
            117 => "OpPtrCastToGeneric",
            118 => "OpGenericCastToPtr",
            119 => "OpGenericCastToPtrExplicit",
            120 => "OpBitcast",
            124 => "OpSNegate",
            126 => "OpFNegate",
            128 => "OpIAdd",
            129 => "OpFAdd",
            130 => "OpISub",
            131 => "OpFSub",
            132 => "OpIMul",
            133 => "OpFMul",
            134 => "OpUDiv",
            135 => "OpSDiv",
            136 => "OpFDiv",
            137 => "OpUMod",
            138 => "OpSRem",
            139 => "OpSMod",
            140 => "OpFRem",
            141 => "OpFMod",
            142 => "OpVectorTimesScalar",
            143 => "OpMatrixTimesScalar",
            144 => "OpVectorTimesMatrix",
            145 => "OpMatrixTimesVector",
            146 => "OpMatrixTimesMatrix",
            147 => "OpOuterProduct",
            148 => "OpDot",
            149 => "OpIAddCarry",
            150 => "OpISubBorrow",
            151 => "OpUMulExtended",
            152 => "OpSMulExtended",
            154 => "OpAny",
            155 => "OpAll",
            156 => "OpIsNan",
            157 => "OpIsInf",
            164 => "OpLogicalEqual",
            165 => "OpLogicalNotEqual",
            166 => "OpLogicalOr",
            167 => "OpLogicalAnd",
            168 => "OpLogicalNot",
            169 => "OpSelect",
            170 => "OpIEqual",
            172 => "OpUGreaterThan",
            173 => "OpSGreaterThan",
            174 => "OpUGreaterThanEqual",
            175 => "OpSGreaterThanEqual",
            176 => "OpULessThan",
            177 => "OpSLessThan",
            178 => "OpULessThanEqual",
            179 => "OpSLessThanEqual",
            180 => "OpFOrdEqual",
            184 => "OpFOrdLessThan",
            186 => "OpFOrdGreaterThan",
            188 => "OpFOrdLessThanEqual",
            190 => "OpFOrdGreaterThanEqual",
            191 => "OpShiftRightLogical",
            192 => "OpShiftRightArithmetic",
            193 => "OpShiftLeftLogical",
            194 => "OpBitwiseOr",
            195 => "OpBitwiseXor",
            196 => "OpBitwiseAnd",
            197 => "OpNot",
            199 => "OpBitFieldInsert",
            200 => "OpBitFieldSExtract",
            201 => "OpBitFieldUExtract",
            202 => "OpBitReverse",
            203 => "OpBitCount",
            207 => "OpDPdx",
            208 => "OpDPdy",
            209 => "OpFwidth",
            210 => "OpDPdxFine",
            211 => "OpDPdyFine",
            212 => "OpFwidthFine",
            213 => "OpDPdxCoarse",
            214 => "OpDPdyCoarse",
            215 => "OpFwidthCoarse",
            218 => "OpEmitVertex",
            219 => "OpEndPrimitive",
            224 => "OpControlBarrier",
            225 => "OpMemoryBarrier",
            227 => "OpAtomicLoad",
            228 => "OpAtomicStore",
            229 => "OpAtomicExchange",
            231 => "OpAtomicIIncrement",
            234 => "OpAtomicCompareExchange",
            235 => "OpAtomicCompareExchangeWeak",
            236 => "OpAtomicIAdd",
            237 => "OpAtomicISub",
            242 => "OpPhi",
            245 => "OpLoopMerge",
            246 => "OpSelectionMerge",
            247 => "OpLabel",
            248 => "OpBranch",
            249 => "OpBranchConditional",
            250 => "OpSwitch",
            251 => "OpKill",
            252 => "OpReturn",
            253 => "OpReturnValue",
            254 => "OpUnreachable",
            255 => "OpLifetimeStart",
            256 => "OpLifetimeStop",
            _ => "Op" + opcode,
        };
    }
}
