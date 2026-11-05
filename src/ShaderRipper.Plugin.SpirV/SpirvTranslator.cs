using System;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// Turns one SMOL-V slot into HLSL.
    ///
    /// The slot holds both stages of one program, so the request's
    /// <see cref="TranslationRequest.Stage"/> selects which module to translate; the
    /// slot reader emitted one descriptor per stage for exactly this reason.
    /// </summary>
    internal sealed class SpirvTranslator : IProgramTranslator
    {
        public bool CanTranslate(TranslationRequest request) =>
            request.Payload.Length >= 8 && Payloads.TryReadSlots(request.Payload.Span, out _);

        public TranslationResult Translate(TranslationRequest request)
        {
            if (!Payloads.TryReadSlots(request.Payload.Span, out var slots))
                return Unsupported("payload has no slot table");

            SpirvPayload blob = SpirvPayload.Read(request.Payload, slots, request.BlobIndex);
            if (blob == null) return Unsupported("no SMOL-V module in this payload slot");

            bool vertex = !string.Equals(request.Stage, "ps", StringComparison.OrdinalIgnoreCase);
            byte[] module = vertex ? blob.Vertex : blob.Fragment;
            if (module == null)
                return Unsupported(vertex ? "payload has no vertex module"
                                          : "payload has no fragment module");

            string hlsl = SpirvCross.Compile(module, blob, vertex,
                                             $"b{request.BlobIndex}", out string error,
                                             request.VertexAttributes,
                                             request.VertexInterpolators);
            if (hlsl == null) return Unsupported("SPIRV-Cross: " + error);

            return new TranslationResult
            {
                Hlsl = hlsl,
                Stage = vertex ? "vs" : "ps",
                // The translation declares Texture2D/SamplerState and calls .Sample,
                // and asks SPIRV-Cross for model 5.0.
                Target = 50,
                // SPIRV-Cross emits a self-contained program: it declares the cbuffers
                // and structs it needs and never calls into UnityCG.
                WantsMacros = false,
                Notes = blob.Notes,
                // The module Unity actually shipped.  SMOL-V is a byte-level
                // re-encoding of SPIR-V, so what the plugin read is not what a
                // disassembler wants; the text below is the decoded module rendered
                // the way `spirv-dis` prints it, which is the form a reader can check
                // the translation against.
                Original = SpirvDisassemble.Describe(module),
                OriginalKind = "SPIR-V",
            };
        }

        private static TranslationResult Unsupported(string why) => new()
        {
            Unsupported = true,
            Notes = new[] { why },
        };
    }
}
