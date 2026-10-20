using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShaderRipper.Cg
{
    /// <summary>One operand of a DXBC instruction (register + swizzle + modifiers).</summary>
    internal sealed class Operand
    {
        public string Kind;        // "r","v","o","cb","x","t","s","icb","l","null"
        public int Index;          // register index (or cb slot)
        public int ArrayIndex = -1; // for x#[n] / cb#[n]; -1 means "dynamic, see ArrayExpr"
        public string ArrayExpr;   // dynamic index expression for x[r1.x+0]
        public string Swizzle = ""; // "xyzx" etc (may include - for negation handled separately)
        public bool Negate;
        public float[] Imm;        // for l(...)
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
        public string LabelTarget;   // for control flow targets
        public int Line;
        public string Raw;
    }
}
