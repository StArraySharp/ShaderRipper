using System;
using System.Collections.Generic;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// One shader an export wrote, as the post-export hooks see it.
    ///
    /// This is a value snapshot rather than a live reference: the export is finished by
    /// the time a hook runs, and handing out the mutable objects the exporter used
    /// would let a hook change state that has already been written to disk.
    /// </summary>
    public sealed class ExportedShader
    {
        /// <summary>Shader name as the asset spells it (<c>UI/Pause Planets</c>).</summary>
        public string Name { get; init; }

        /// <summary>Emitted file, relative to the export root (<c>Assets/Shader/X.shader</c>).</summary>
        public string Path { get; init; }

        /// <summary>The asset's Properties block, in declaration order.</summary>
        public IReadOnlyList<ExportedProperty> Properties { get; init; }
    }

    /// <summary>One entry of a shader's Properties block.</summary>
    public sealed class ExportedProperty
    {
        /// <summary>Property name, including the leading underscore where it has one.</summary>
        public string Name { get; init; }

        /// <summary>
        /// Unity's property type id: 0 Color, 1 Vector, 2 Float, 3 Range, 4 Texture.
        /// Kept as the raw number because that is what the asset stores and what a
        /// reader has to branch on.
        /// </summary>
        public int Type { get; init; }

        /// <summary>Declared default, four components (a scalar uses the first).</summary>
        public float[] DefaultValue { get; init; }

        /// <summary>Default texture for a Texture property (<c>white</c>, <c>black</c>).</summary>
        public string DefaultTexture { get; init; }
    }

    /// <summary>
    /// One material the game shipped, with the property values it saved.
    ///
    /// This is the same data the export writes to its own material index; it is handed
    /// to hooks as well so a hook can build its own view without re-reading the game
    /// data, which it has no access to.
    /// </summary>
    public sealed class ExportedMaterial
    {
        /// <summary>Material asset name.</summary>
        public string Name { get; init; }

        /// <summary>Suggested project path for the material.</summary>
        public string AssetPath { get; init; }

        /// <summary>Shader name the material binds, or null when unresolved.</summary>
        public string Shader { get; init; }

        /// <summary>Emitted <c>.shader</c> file, or null when that shader was not exported.</summary>
        public string ShaderFile { get; init; }

        /// <summary>Float and Range properties.</summary>
        public IReadOnlyDictionary<string, float> Floats { get; init; }

        /// <summary>Color and Vector properties, four components each.</summary>
        public IReadOnlyDictionary<string, float[]> Colors { get; init; }

        /// <summary>Texture properties: property name to asset path or <c>fileId:pathId</c>.</summary>
        public IReadOnlyDictionary<string, string> Textures { get; init; }

        /// <summary>
        /// True when the game shipped no material and this entry was built from the
        /// shader's Properties block: the values are Unity's defaults, not the game's.
        /// </summary>
        public bool Synthetic { get; init; }
    }

    /// <summary>
    /// What an export produced, handed to every <see cref="IExportHook"/> when it
    /// finishes.
    ///
    /// A hook that wants to write extra files has to register their names first
    /// (through <see cref="IPluginRegistry.AddExportArtifact"/>): the export directory
    /// refuses to overwrite anything it did not produce, and a file a plugin writes
    /// without registering would make the next run look foreign.
    /// </summary>
    public sealed class ExportCompleted
    {
        /// <summary>The directory the export wrote to.</summary>
        public string OutputDirectory { get; init; }

        /// <summary>Every shader the run emitted, in write order.</summary>
        public IReadOnlyList<ExportedShader> Shaders { get; init; }

        /// <summary>
        /// Every material the run indexed -- the game's own plus the synthetic entries
        /// built for shaders that ship none.  A hook that has to instantiate something
        /// to render works from this list.
        /// </summary>
        public IReadOnlyList<ExportedMaterial> Materials { get; init; }

        /// <summary>Shaders seen but not written (built-ins, failures), for a summary.</summary>
        public int Skipped { get; init; }

        /// <summary>Programs the run translated.</summary>
        public int Translated { get; init; }

        /// <summary>True when the run was cancelled, so a hook should not write.</summary>
        public bool Cancelled { get; init; }
    }

    /// <summary>
    /// Runs after an export finishes.
    ///
    /// This is the extension point for anything derived from the output as a whole --
    /// a test manifest, a report, an index of what was produced.  It is deliberately
    /// not an <see cref="Emit.IEmitterPass"/>: a pass sees one program's text at a time
    /// and has no idea which shaders the run covered.
    ///
    /// Hooks run in registration order, on the export's own thread, after every file
    /// is written.  One that throws is skipped and the export still succeeds: a broken
    /// plugin must not turn a finished export into a failed one.
    /// </summary>
    public interface IExportHook
    {
        /// <summary>Short name for diagnostics.</summary>
        string Name { get; }

        /// <summary>
        /// Called once per export.  <paramref name="context"/> is a snapshot; a hook
        /// that writes files must use <see cref="ExportCompleted.OutputDirectory"/>.
        /// </summary>
        void ExportFinished(ExportCompleted context);
    }
}
