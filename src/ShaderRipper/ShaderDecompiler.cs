using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Data;
using ShaderRipper.Emit;
using ShaderRipper.Game;

namespace ShaderRipper
{
    /// <summary>
    /// One payload a shader carries: its platform, and the compressed bytes Unity
    /// stored for that backend.
    ///
    /// This is the only shape a host has to produce.  AssetRipper hands over the
    /// platform id plus <c>m_CompressedBlob</c> and the per-platform offset/length
    /// tables exactly as it read them; nothing about the internal AST is exposed.
    /// </summary>
    public sealed class ShaderPayload
    {
        /// <summary><c>SerializedShader.m_Platforms</c> entry, e.g. 4 for d3d11.</summary>
        public int Platform;

        /// <summary>Whole <c>m_CompressedBlob</c> for the shader.</summary>
        public byte[] Blob = Array.Empty<byte>();

        /// <summary>Slot offsets for this platform (<c>m_Offsets[i][slot]</c>).</summary>
        public uint[] Offsets = Array.Empty<uint>();

        /// <summary>Compressed length of each slot.</summary>
        public uint[] CompressedLengths = Array.Empty<uint>();

        /// <summary>Decompressed length of each slot.</summary>
        public uint[] DecompressedLengths = Array.Empty<uint>();

        /// <summary>
        /// When true, <see cref="Blob"/> is a plain concatenation of already
        /// decompressed program containers rather than Unity's slot-table layout.
        /// A host that holds the decompressed programs (AssetRipper does) uses this;
        /// the library then carves containers by their own headers.
        /// </summary>
        public bool RawPrograms;
    }

    /// <summary>A material property, as the ShaderLab <c>Properties</c> block needs it.</summary>
    public sealed class ShaderProperty
    {
        public string Name = "";
        public string Description = "";

        /// <summary>Unity's <c>ShaderPropertyFlags</c> bit set.</summary>
        public int Flags;

        /// <summary>Unity's <c>SerializedPropertyType</c> code (0 Color, 1 Vector, 2 Float, 3 Range, 4 TexEnv, 5 Int).</summary>
        public int Type;

        public float[] DefaultValue = new float[4];
        public string DefaultTexture = "";
        public int DefaultTextureDim;
    }

    /// <summary>Everything the translator needs that is not in the shader blob itself.</summary>
    public sealed class ShaderModel
    {
        /// <summary>The shader's own name, e.g. <c>CameraFilterPack/Color_GrayScale</c>.</summary>
        public string Name = "";

        public string Fallback = "";
        public string CustomEditor = "";

        /// <summary>The shader's <c>m_KeywordNames</c> table, indexed by keyword id.</summary>
        public List<string> KeywordNames = new();
        /// <summary>Serialized keyword flags, parallel to KeywordNames; bit 0 marks local scope.</summary>
        public List<int> KeywordFlags = new();

        public List<ShaderProperty> Properties = new();

        /// <summary>One entry per platform slot the shader carries.</summary>
        public List<ShaderPayload> Payloads = new();

        /// <summary>
        /// The ShaderLab structure: sub-shaders, their tags, and the passes with
        /// their render state.  A host that omits this still gets a file, but one
        /// with a single synthesised sub-shader and no state.
        /// </summary>
        public List<ShaderSubShader> SubShaders = new();
    }

    /// <summary>One <c>SubShader</c> block.</summary>
    public sealed class ShaderSubShader
    {
        public int LOD;

        /// <summary><c>m_Tags</c>: Queue, RenderType, RenderPipeline, ...</summary>
        public List<(string k, string v)> Tags = new();

        public List<ShaderPass> Passes = new();
    }

    /// <summary>
    /// One <c>Pass</c> block: its name, tags, render state and the reflection the
    /// constant-buffer analysis needs.
    /// </summary>
    public sealed class ShaderPass
    {
        /// <summary><c>m_State.m_Name</c>, e.g. <c>Default</c>.</summary>
        public string Name = "";

        public List<(string k, string v)> Tags = new();

        /// <summary>
        /// Render-state statements already written as ShaderLab, for a host that
        /// cannot supply the numeric state below.  When empty the state is built
        /// from those fields, which is the accurate path: a state driven by a
        /// material property only survives as a name, not as a number.
        /// </summary>
        public List<string> StateLines = new();

        // ---- render state ----
        //
        // Each state is a number plus, when the source drove it from a material
        // property, that property's name.  A non-empty name wins: the number is only
        // what the property happened to hold at build time, so emitting it would
        // freeze a tunable state.

        public float ZClip = 1, ZTest = 4, ZWrite = 1, Culling = 2;
        public string ZClipName = "", ZTestName = "", ZWriteName = "", CullingName = "";

        public float OffsetFactor, OffsetUnits, AlphaToMask;
        public string OffsetFactorName = "", OffsetUnitsName = "", AlphaToMaskName = "";

        public float StencilRef, StencilReadMask = 255, StencilWriteMask = 255;
        public string StencilRefName = "", StencilReadMaskName = "", StencilWriteMaskName = "";

        public int FogMode;
        public bool Lighting;
        public float FogDensity, FogStart, FogEnd;

        public bool RtSeparateBlend;
        public List<ShaderBlendState> Blends = new();
        public List<ShaderStencilOp> StencilOps = new();

        /// <summary>The pass's <c>m_NameIndices</c> keys, so constant buffers can be named.</summary>
        public List<string> NameIndices = new();

        /// <summary>Sub-program state: blob index and the keywords it was compiled with.</summary>
        public List<ShaderSubProgramInfo> SubPrograms = new();
    }

    /// <summary>One render target's blend state.</summary>
    public sealed class ShaderBlendState
    {
        public float SrcBlend = 1, DestBlend, BlendOp, ColMask = 15;
        public float SrcBlendAlpha = 1, DestBlendAlpha, BlendOpAlpha;
        public string SrcBlendName = "", DestBlendName = "";
        public string SrcBlendAlphaName = "", DestBlendAlphaName = "";
        public string BlendOpName = "", BlendOpAlphaName = "";
        public string ColMaskName = "";
    }

    /// <summary>One stencil operation set (combined, front or back).</summary>
    public sealed class ShaderStencilOp
    {
        public float Comp, PassOp, Fail, ZFail;
        public string CompName = "", PassName = "", FailName = "", ZFailName = "";
    }

    /// <summary>One compiled program of a pass.</summary>
    public sealed class ShaderSubProgramInfo
    {
        public int BlobIndex;
        public int GpuProgramType;

        /// <summary>
        /// Index into the payload's separate parameter-blob table, when the host can
        /// tell.  It is what selects the program's own constant-buffer layout, and it
        /// is not the same number as <see cref="BlobIndex"/>.
        /// </summary>
        public int ParameterBlobIndex = -1;

        /// <summary>Indices into <see cref="ShaderModel.KeywordNames"/>.</summary>
        public List<int> KeywordIndices = new();

        /// <summary>
        /// The program's texture bindings (<c>m_TextureParams</c>).  Without them a
        /// <c>tN</c> has no name and the export has to guess one from the order the
        /// material's properties are declared in.
        /// </summary>
        public List<ShaderTextureParam> Textures = new();

        /// <summary>
        /// The program's constant-buffer layout (<c>m_ConstantBuffers</c>): the
        /// authoritative name and byte offset of every member, so a row of
        /// <c>$Globals</c> is named rather than emitted as <c>_cb0_rowK</c>.
        /// </summary>
        public List<ShaderConstantBufferInfo> ConstantBuffers = new();
    }

    /// <summary>One texture binding of a program.</summary>
    public sealed class ShaderTextureParam
    {
        public string Name = "";

        /// <summary>The <c>tN</c> register.</summary>
        public int Index;

        /// <summary>The <c>sN</c> register, or -1 when the program does not say.</summary>
        public int Sampler = -1;
    }

    /// <summary>One constant buffer of a program.</summary>
    public sealed class ShaderConstantBufferInfo
    {
        /// <summary>The buffer's name, <c>$Globals</c> for the material's own block.</summary>
        public string Name = "";

        /// <summary>Bytes the compiled program actually uses.</summary>
        public int Size;

        public List<ShaderConstantBufferMember> Members = new();
    }

    /// <summary>One member of a constant buffer.</summary>
    public sealed class ShaderConstantBufferMember
    {
        public string Name = "";

        /// <summary>Byte offset inside the buffer, the value ShaderLab's
        /// <c>packoffset</c> is derived from.</summary>
        public int Offset;

        /// <summary>Unity's <c>ShaderParamType</c>.</summary>
        public int Type;

        /// <summary>Components in one element (1 for a scalar, 4 for a float4).</summary>
        public int Dim = 4;

        public int ArraySize = 1;

        /// <summary>Rows a matrix occupies; 1 for everything else.</summary>
        public int RowCount = 1;

        public bool IsMatrix;
    }

    /// <summary>What one translation produced.</summary>
    public sealed class ShaderResult
    {
        /// <summary>A complete ShaderLab file, or null when nothing was produced.</summary>
        public string ShaderLab;

        /// <summary>Programs seen, disassembled and translated.</summary>
        public int Programs, Disassembled, Translated, Skipped;

        /// <summary>Diagnostics worth surfacing; empty on a clean run.</summary>
        public List<string> Notes = new();

        public bool Ok => !string.IsNullOrEmpty(ShaderLab);
    }

    /// <summary>Tally of a whole-directory export.</summary>
    public sealed class ExportStats
    {
        public int Files, Shaders, Programs, Disassembled, Translated, Skipped, Failed;

        /// <summary>Shaders actually written; lower than <see cref="Shaders"/> when one
        /// failed the ShaderLab well-formedness check.</summary>
        public int Emitted;

        /// <summary>Unity's own shaders left out.</summary>
        public int BuiltinSkipped;

        public List<string> Notes = new();
    }

    /// <summary>
    /// The embedding API: translate one shader, or a whole game directory.
    ///
    /// AssetRipper (and anything else that already has the shader bytes in memory)
    /// uses <see cref="Translate"/>; a host that just wants a tree on disk uses
    /// <see cref="Export"/>.  Neither goes through the command line.
    /// </summary>
    public static class ShaderDecompiler
    {
        /// <summary>
        /// Translate one shader into ShaderLab.
        ///
        /// Everything is driven by what the host passes in: the platform payloads,
        /// the Properties block, the keyword table and each pass's name table.  The
        /// result carries the file text plus the counts, so a caller can log or
        /// reject it without touching the internals.
        /// </summary>
        public static ShaderResult Translate(ShaderModel model)
        {
            var result = new ShaderResult();
            if (model == null || string.IsNullOrEmpty(model.Name))
            {
                result.Notes.Add("shader has no name");
                return result;
            }

            var sh = ToInternalShader(model);
            if (sh == null)
            {
                result.Notes.Add("no usable payload (dxbc or glsl)");
                return result;
            }

            var stats = new GameExport.Stats();
            try
            {
                string text = GameExport.TranslateOne(sh, model.Name, stats);
                result.Programs = stats.Programs;
                result.Disassembled = stats.Disassembled;
                result.Translated = stats.Translated;
                result.Skipped = stats.Skipped;
                result.Notes.AddRange(stats.Notes);
                result.ShaderLab = text;
            }
            catch (Exception e)
            {
                result.Notes.Add($"translate failed: {e.Message}");
                if (Environment.GetEnvironmentVariable("SHADERRIP_TRACE") == "1")
                    Console.Error.WriteLine($"[Translate] {model.Name}: {e}");
            }
            return result;
        }

        /// <summary>
        /// Translate the shader's DXBC/GLSL payload into HLSL only, with no
        /// ShaderLab wrapper -- one entry per program, in blob order.
        /// </summary>
        public static List<(int blobIndex, string stage, string hlsl)> TranslatePrograms(ShaderModel model)
        {
            var list = new List<(int, string, string)>();
            var sh = ToInternalShader(model);
            if (sh == null) return list;

            var stats = new GameExport.Stats();
            try { GameExport.TranslateProgramsOf(sh, stats, list); }
            catch { /* the caller gets whatever succeeded */ }
            return list;
        }

        /// <summary>
        /// Export a whole game data directory to disk.  Thin wrapper over the same
        /// path the CLI uses, for a host that has files rather than bytes.
        /// </summary>
        public static ExportStats Export(string dataDir, string outDir,
            bool shaderLab = true, bool origAsm = false, bool dedup = true,
            bool builtin = false, bool programs = false,
            IReadOnlyList<string> files = null, bool overwrite = false, int jobs = 1,
            string only = null)
        {
            var s = GameExport.Run(dataDir, outDir, only, shaderLab, origAsm, dedup, builtin,
                                   programs, files, overwrite, jobs);
            return new ExportStats
            {
                Files = s.Files, Shaders = s.Shaders, Programs = s.Programs,
                Disassembled = s.Disassembled, Translated = s.Translated,
                Skipped = s.Skipped, Failed = s.Failed, Emitted = s.Emitted,
                BuiltinSkipped = s.BuiltinSkipped,
                Notes = s.Notes,
            };
        }

        /// <summary>
        /// Map the public model onto the internal one.  The internal types stay
        /// internal so the shape of the parser is free to change.
        /// </summary>
        private static Data.Shader ToInternalShader(ShaderModel model)
        {
            var parsed = new ParsedForm { Name = model.Name, FallbackName = model.Fallback ?? "" };
            parsed.CustomEditorName = model.CustomEditor ?? "";
            parsed.KeywordNames.AddRange(model.KeywordNames ?? new List<string>());
            parsed.KeywordFlags.AddRange(model.KeywordFlags ?? new List<int>());
            foreach (var p in model.Properties ?? new List<ShaderProperty>())
                parsed.Props.Add(new Property
                {
                    Name = p.Name,
                    Description = p.Description,
                    Flags = p.Flags,
                    Type = p.Type,
                    DefValue = p.DefaultValue ?? new float[4],
                    DefTextureName = p.DefaultTexture ?? "",
                    DefTextureDim = p.DefaultTextureDim,
                });

            // The structure the host reports.  Passes carry the reflection the
            // constant-buffer analysis reads; without it the buffers stay cbN and
            // every row keeps its number.
            foreach (var ss in model.SubShaders ?? new List<ShaderSubShader>())
            {
                var sub = new SubShader { LOD = ss.LOD };
                foreach (var (k, v) in ss.Tags ?? new List<(string, string)>())
                    if (!string.IsNullOrEmpty(k)) sub.Tags.Add((k, v));

                foreach (var pi in ss.Passes ?? new List<ShaderPass>())
                {
                    var pass = new Pass
                    {
                        StateName = pi.Name ?? "",
                        StateLines = pi.StateLines ?? new List<string>(),
                        ZClip = pi.ZClip, ZTest = pi.ZTest, ZWrite = pi.ZWrite, Culling = pi.Culling,
                        OffsetFactor = pi.OffsetFactor, OffsetUnits = pi.OffsetUnits,
                        AlphaToMask = pi.AlphaToMask,
                        StencilRef = pi.StencilRef, StencilReadMask = pi.StencilReadMask,
                        StencilWriteMask = pi.StencilWriteMask,
                        FogMode = pi.FogMode, Lighting = pi.Lighting,
                        FogDensity = pi.FogDensity, FogStart = pi.FogStart, FogEnd = pi.FogEnd,
                        RtSeparateBlend = pi.RtSeparateBlend,
                    };
                    pass.ZClipName = pi.ZClipName ?? "";
                    pass.ZTestName = pi.ZTestName ?? "";
                    pass.ZWriteName = pi.ZWriteName ?? "";
                    pass.CullingName = pi.CullingName ?? "";
                    pass.OffsetFactorName = pi.OffsetFactorName ?? "";
                    pass.OffsetUnitsName = pi.OffsetUnitsName ?? "";
                    pass.AlphaToMaskName = pi.AlphaToMaskName ?? "";
                    pass.StencilRefName = pi.StencilRefName ?? "";
                    pass.StencilReadMaskName = pi.StencilReadMaskName ?? "";
                    pass.StencilWriteMaskName = pi.StencilWriteMaskName ?? "";

                    foreach (var b in pi.Blends ?? new List<ShaderBlendState>())
                        pass.Blends.Add(new BlendState
                        {
                            SrcBlend = b.SrcBlend, DestBlend = b.DestBlend,
                            SrcBlendAlpha = b.SrcBlendAlpha, DestBlendAlpha = b.DestBlendAlpha,
                            BlendOp = b.BlendOp, BlendOpAlpha = b.BlendOpAlpha, ColMask = b.ColMask,
                            SrcBlendName = b.SrcBlendName ?? "", DestBlendName = b.DestBlendName ?? "",
                            SrcBlendAlphaName = b.SrcBlendAlphaName ?? "",
                            DestBlendAlphaName = b.DestBlendAlphaName ?? "",
                            BlendOpName = b.BlendOpName ?? "", BlendOpAlphaName = b.BlendOpAlphaName ?? "",
                            ColMaskName = b.ColMaskName ?? "",
                        });
                    foreach (var op in pi.StencilOps ?? new List<ShaderStencilOp>())
                        pass.StencilOps.Add(new StencilOp
                        {
                            Comp = op.Comp, PassOp = op.PassOp, Fail = op.Fail, ZFail = op.ZFail,
                            CompName = op.CompName ?? "", PassName = op.PassName ?? "",
                            FailName = op.FailName ?? "", ZFailName = op.ZFailName ?? "",
                        });
                    foreach (var (k, v) in pi.Tags ?? new List<(string, string)>())
                        if (!string.IsNullOrEmpty(k)) pass.Tags.Add((k, v));
                    foreach (string n in pi.NameIndices ?? new List<string>())
                        if (!string.IsNullOrEmpty(n)) pass.NameIndices[n] = pass.NameIndices.Count;
                    foreach (var sp in pi.SubPrograms ?? new List<ShaderSubProgramInfo>())
                    {
                        var intoSp = new SubProgram
                        {
                            BlobIndex = sp.BlobIndex,
                            GpuProgramType = sp.GpuProgramType,
                            ParameterBlobIndex = sp.ParameterBlobIndex,
                            KeywordIndices = new List<int>(sp.KeywordIndices ?? new List<int>()),
                        };
                        foreach (var t in sp.Textures ?? new List<ShaderTextureParam>())
                            if (!string.IsNullOrEmpty(t.Name))
                                intoSp.TextureParams.Add((t.Name, t.Index, t.Sampler));
                        foreach (var cb in sp.ConstantBuffers ?? new List<ShaderConstantBufferInfo>())
                        {
                            var intoCb = new AssetConstantBuffer { Name = cb.Name ?? "", Size = cb.Size };
                            foreach (var m in cb.Members ?? new List<ShaderConstantBufferMember>())
                                intoCb.Members.Add(new AssetCbMember
                                {
                                    Name = m.Name ?? "", Offset = m.Offset, Type = m.Type,
                                    Dim = m.Dim, ArraySize = m.ArraySize,
                                    RowCount = m.RowCount, IsMatrix = m.IsMatrix,
                                });
                            intoSp.ConstantBuffers.Add(intoCb);
                        }
                        pass.SubPrograms.Add(intoSp);
                    }
                    sub.Passes.Add(pass);
                }
                parsed.SubShaders.Add(sub);
            }

            var sh = new Data.Shader { Name = model.Name, ParsedForm = parsed };

            var usable = (model.Payloads ?? new List<ShaderPayload>())
                         .Where(p => p != null && p.Blob != null && p.Blob.Length > 0 &&
                                     Platforms.Get(p.Platform) != null)
                         .Where(p => Platforms.Get(p.Platform).Format is "dxbc" or "glsl")
                         .ToList();

            // No payload is not the same as nothing to say: a shader compiled without
            // programs still carries its tags, passes and state, and the caller wants
            // that structure written rather than an empty file.
            if (usable.Count == 0 && parsed.SubShaders.Count == 0) return null;
            if (usable.Count == 0) return sh;

            sh.Platforms = usable.Select(p => (uint)p.Platform).ToArray();
            sh.Offsets = usable.Select(p => p.Offsets ?? Array.Empty<uint>()).ToArray();
            sh.CompressedLengths = usable.Select(p => p.CompressedLengths ?? Array.Empty<uint>()).ToArray();
            sh.DecompressedLengths = usable.Select(p => p.DecompressedLengths ?? Array.Empty<uint>()).ToArray();

            // A raw-program payload already holds decompressed containers, so it is
            // kept per platform instead of slicing the shared blob.
            sh.RawPayloads = usable.Select(p => p.RawPrograms ? p.Blob : null).ToArray();

            // One blob per platform in the internal model, so a shader carrying
            // several backends is stored as the first usable one plus its siblings.
            // Unity writes the same bytes for every platform of one build, which is
            // what makes this exact rather than approximate.
            sh.CompressedBlob = usable[0].Blob;
            return sh;
        }
    }
}
