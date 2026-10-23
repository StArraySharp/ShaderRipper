using System.Collections.Generic;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// One translation unit of Unity's GLSL payload, in the shape the emitter
    /// needs — independent of how it was read.
    ///
    /// Two readers fill this in: <see cref="GlslToCg"/> scans it line by line
    /// (the original route), and <c>GlslToCgTree</c> parses it with
    /// <c>Antlr/Glsl.g4</c>.  Both produce the same model, so the assembly of
    /// appdata/v2f/vert/frag is written once and the two routes differ only in
    /// how they read, never in what they emit.
    /// </summary>
    internal sealed class GlslBranch
    {
        public List<(string decl, string name)> Uniforms = new();
        public List<GlslIo> Ins = new();
        public List<GlslIo> Varyings = new();
        public List<string> Globals = new();
        public List<string> Locals = new();

        /// <summary>Already-translated Cg/HLSL statements, one per line.</summary>
        public List<string> Body = new();

        public string BodyText = "";
        public string OutputType;
        public string OutputSemantic;
        public bool UsesFragCoord;
    }

    /// <summary>A struct member: appdata attribute, v2f varying or fragment output.</summary>
    internal sealed class GlslIo
    {
        public string Name;
        public string CgType = "float4";
        public string Semantic;
    }
}
