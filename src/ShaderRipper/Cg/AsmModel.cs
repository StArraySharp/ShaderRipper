using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShaderRipper.Cg
{
    /// <summary>One operand of a DXBC instruction (register + swizzle + modifiers).</summary>
    internal sealed class Operand
    {
        public string Kind;
        public int Index;
        public int ArrayIndex = -1;
        public string ArrayExpr;
        public string Swizzle = "";
        public bool Negate;
        public float[] Imm;
        public string Raw;

        public override string ToString() => Raw;
    }

    internal sealed class Instruction
    {
        public string Opcode;

        /// <summary>Opcode including its parenthesised annotations, e.g.
        /// "sample_indexable(texture2d)(float,float,float,float)".</summary>
        public string OpcodeFull;
        public bool Saturate;
        public List<Operand> Operands = new List<Operand>();
        public bool IsLabel;
        public string LabelTarget;
        public int Line;
        public string Raw;
    }
}
