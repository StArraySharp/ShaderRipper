using System;
using System.Collections.Generic;

namespace ShaderRipper.Plugins.Platforms
{
    /// <summary>
    /// One row of the backend table: what a <c>m_Platforms</c> id means.
    ///
    /// Ids come from <c>UnityEditor.Rendering.ShaderCompilerPlatform</c> read out of
    /// the editor by reflection, which is why d3d11 is 4 and Vulkan is 18.  A plugin
    /// may also add an id this build has never heard of.
    /// </summary>
    public sealed class PlatformDescriptor
    {
        /// <summary>The numeric id stored in <c>m_Platforms</c>.</summary>
        public int Id { get; init; }

        /// <summary>Name accepted by <c>--platform</c>; lower case, no spaces.</summary>
        public string Name { get; init; } = "";

        /// <summary>Payload format: dxbc / glsl / d3d9 / spirv / metal / wgsl.</summary>
        public string Format { get; init; } = "";

        /// <summary>True when the payload is itself readable source (GLSL, Metal).</summary>
        public bool HasSource { get; init; }

        /// <summary>False when the payload can only be disassembled, not translated.</summary>
        public bool HasTranslator { get; init; }

        /// <summary>True when this descriptor replaces one the build already had.</summary>
        public bool OverridesBuiltin { get; init; }

        /// <summary>Renders as <c>name(id)/format</c>, the form the registry logs.</summary>
        public override string ToString() => $"{Name}({Id})/{Format}";
    }

    /// <summary>One compiled program found inside a decoded platform payload.</summary>
    public sealed class ProgramDescriptor
    {
        /// <summary>Unity's <c>m_BlobIndex</c>; the output file name uses it.</summary>
        public int BlobIndex { get; init; }

        /// <summary>
        /// "vs" / "ps" / "cs", or <c>null</c> when only the translation can tell.
        /// A null stage means <see cref="TranslationResult.Stage"/> decides.
        /// </summary>
        public string Stage { get; init; }

        /// <summary>Offset of the program inside the payload, in bytes.</summary>
        public int Offset { get; init; }

        /// <summary>Length of the program inside the payload, in bytes.</summary>
        public int Length { get; init; }

        /// <summary>Entry point name when the archive records one (Metal does).</summary>
        public string Entry { get; init; }
    }

    /// <summary>Context handed to a slot reader.</summary>
    public sealed class SlotContext
    {
        /// <summary><c>SubProgram.m_BlobIndex</c> values, in slot order.</summary>
        public IReadOnlyList<int> BlobIndices { get; init; }

        /// <summary>The backend this payload belongs to.</summary>
        public PlatformDescriptor Platform { get; init; }
    }

    /// <summary>
    /// Enumerates the compiled programs inside a decoded platform payload.
    ///
    /// This is a property of the *format*, not of the translator: a DXBC slot is a
    /// per-program header followed by cbuffer name tables followed by a container,
    /// a GLSL slot is bare text, a Metal slot is a 0xF00DCAFE archive.  The host has
    /// to know what is in the slot before it can translate anything, so the two are
    /// separate interfaces.
    /// </summary>
    public interface ISlotReader
    {
        /// <summary>
        /// Enumerate the programs in one decoded payload, in the order they appear.
        /// </summary>
        /// <remarks>
        /// A descriptor whose <see cref="ProgramDescriptor.Offset"/> and
        /// <see cref="ProgramDescriptor.Length"/> fall outside <paramref name="payload"/>
        /// is counted as skipped rather than failed, so a truncated slot degrades into a
        /// partial result instead of aborting the shader.
        /// </remarks>
        /// <param name="payload">The decompressed platform slot.</param>
        /// <param name="context">Which backend this is and which blob indices the asset
        /// expects; use it to pair each program with its <c>m_BlobIndex</c>.</param>
        /// <returns>The programs found; empty when the payload holds none.</returns>
        IReadOnlyList<ProgramDescriptor> Read(ReadOnlyMemory<byte> payload, SlotContext context);
    }

    /// <summary>Everything one translation needs, so a translator never reads game data.</summary>
    public sealed class TranslationRequest
    {
        /// <summary>Program bytes, already decompressed out of the platform slot.</summary>
        public ReadOnlyMemory<byte> Payload { get; init; }

        /// <summary>Stage from <see cref="ProgramDescriptor.Stage"/>; may be null.</summary>
        public string Stage { get; init; }

        /// <summary>Unity's <c>m_BlobIndex</c>.</summary>
        public int BlobIndex { get; init; }

        /// <summary>Entry point name, when the format records one.</summary>
        public string Entry { get; init; }

        /// <summary>The shader's property names, in declaration order.</summary>
        public IReadOnlyList<string> PropertyNames { get; init; }

        /// <summary>The backend this program came from.</summary>
        public PlatformDescriptor Platform { get; init; }
    }

    /// <summary>Outcome of translating one program.</summary>
    public sealed class TranslationResult
    {
        /// <summary>HLSL program body; null when nothing was produced.</summary>
        public string Hlsl { get; init; }

        /// <summary>Comment lines to write into the <c>.shader</c>.</summary>
        public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Stage the translation turned out to be.  Overrides
        /// <see cref="ProgramDescriptor.Stage"/> when that was unknown.
        /// </summary>
        public string Stage { get; init; }

        /// <summary>Shader model the body needs, as 40 / 50; 0 keeps the default.</summary>
        public int Target { get; init; }

        /// <summary>
        /// True when <see cref="Hlsl"/> needs the Unity include chain (and so may use
        /// <c>UnityObjectToClipPos</c> and friends).  False for a standalone program.
        /// </summary>
        public bool WantsMacros { get; init; } = true;

        /// <summary>
        /// The translator decided this payload is beyond it -- a newer syntax version
        /// say.  Counted as skipped rather than failed, because the input was valid.
        /// </summary>
        public bool Unsupported { get; init; }
    }

    /// <summary>Turns one compiled program into HLSL.</summary>
    public interface IProgramTranslator
    {
        /// <summary>Cheap check; false means "not my syntax", and the program is skipped.</summary>
        bool CanTranslate(TranslationRequest request);

        /// <summary>Translate.  Never returns a partially valid body: null means failure.</summary>
        TranslationResult Translate(TranslationRequest request);
    }
}
