using System.Collections.Generic;

namespace ShaderRipper.Data
{
    internal sealed class Shader
    {
        public long PathId;
        public long Offset;
        public uint Size;
        public string Name;
        /// <summary>Container path from the owning bundle node (e.g.
        /// "assets/resources/foo.shader"), or null for loose .assets files.</summary>
        public string ContainerPath;

        /// <summary>Base name of the .assets file this object came from
        /// ("resources.assets", or the GUID-named apk entry) — the key the
        /// Resources map uses.</summary>
        public string SourceFile;
        public ParsedForm ParsedForm;
        public uint[] Platforms = System.Array.Empty<uint>();
        public uint[][] Offsets = System.Array.Empty<uint[]>();
        public uint[][] CompressedLengths = System.Array.Empty<uint[]>();
        public uint[][] DecompressedLengths = System.Array.Empty<uint[]>();
        public byte[] CompressedBlob = System.Array.Empty<byte>();

        /// <summary>
        /// Per platform, a stream of already-decompressed program containers.  A host
        /// that holds the programs individually (AssetRipper) fills this instead of
        /// the blob-plus-slot-table layout, and the exporter carves containers from it
        /// by their own headers.  Null entries mean "use <see cref="CompressedBlob"/>".
        /// </summary>
        public byte[][] RawPayloads = System.Array.Empty<byte[]>();
        public string FallbackName => ParsedForm?.FallbackName ?? "";
    }

    internal sealed class ParsedForm
    {
        public string Name;
        public string FallbackName = "";
        public List<Property> Props = new List<Property>();
        public List<SubShader> SubShaders = new List<SubShader>();

        /// <summary>
        /// <c>SerializedShader.m_KeywordNames</c>: the shader's keyword table.
        /// <c>SerializedSubProgram.m_KeywordIndices</c> indexes into this list.
        /// </summary>
        public List<string> KeywordNames = new List<string>();
        public List<int> KeywordFlags = new List<int>();

        /// <summary><c>SerializedShader.m_CustomEditorName</c>: the ShaderLab
        /// <c>CustomEditor</c> attribute (empty when the shader has none).</summary>
        public string CustomEditorName = "";

        /// <summary><c>SerializedShader.m_Dependencies</c>: <c>UsePass</c>/<c>Fallback</c>
        /// references to other shaders, kept verbatim.</summary>
        public List<string> Dependencies = new List<string>();
    }

    internal sealed class Property
    {
        public string Name = "";
        public string Description = "";
        public int Flags;
        public int Type;
        public float[] DefValue = new float[4];
        public string DefTextureName = "";
        public int DefTextureDim;
    }

    internal sealed class SubShader
    {
        public int LOD;
        public List<(string k, string v)> Tags = new List<(string, string)>();
        public List<Pass> Passes = new List<Pass>();
    }

    internal sealed class Pass
    {
        public string StateName = "";
        public List<(string k, string v)> Tags = new List<(string, string)>();

        /// <summary>
        /// Render-state statements supplied verbatim by a host that already has
        /// them rendered (see <c>ShaderDecompiler.ShaderPass.StateLines</c>).
        ///
        /// When this is non-empty it replaces the lines derived from the numeric
        /// fields below: a host that owns the serialized state object can render it
        /// exactly, which keeps property references (<c>Blend [_SrcBlend] [_DstBlend]</c>)
        /// and every block shape intact without this class mirroring all of them.
        /// </summary>
        public List<string> StateLines = new List<string>();

        /// <summary>
        /// <c>SerializedPass.m_NameIndices</c>: the shader's name table.  It lists
        /// every constant-buffer, uniform and texture name the pass references, but
        /// not the register each one binds to -- the register has to be inferred
        /// from the constant-buffer layout in the disassembly.
        /// </summary>
        public Dictionary<string, int> NameIndices = new Dictionary<string, int>();

        public uint ProgramMask;

        public float ZClip = 1, ZTest = 4, ZWrite = 1, Culling = 2;
        public float OffsetFactor, OffsetUnits, AlphaToMask;
        public float StencilRef, StencilReadMask = 255, StencilWriteMask = 255;
        public string StencilRefName = "", StencilReadMaskName = "", StencilWriteMaskName = "";

        /// <summary>
        /// Property names carried next to the numeric value of each state entry
        /// (<c>SerializedShaderFloatValue.name</c>).  When non-empty the number is
        /// only a fallback and ShaderLab must reference the property instead --
        /// e.g. <c>ZWrite [_ZWrite]</c>, <c>Blend [_SrcBlend] [_DstBlend]</c> --
        /// which is how every surface shader drives its material state.
        /// </summary>
        public string ZClipName = "", ZTestName = "", ZWriteName = "", CullingName = "";
        public string OffsetFactorName = "", OffsetUnitsName = "", AlphaToMaskName = "";
        public int FogMode;
        public bool Lighting;
        public float FogDensity, FogStart, FogEnd;
        public bool RtSeparateBlend;

        public List<BlendState> Blends = new List<BlendState>();
        public List<StencilOp> StencilOps = new List<StencilOp>();
        public List<SubProgram> SubPrograms = new List<SubProgram>();
    }

    internal sealed class BlendState
    {
        public float SrcBlend = 1, DestBlend, BlendOp, ColMask = 15;
        public float SrcBlendAlpha = 1, DestBlendAlpha, BlendOpAlpha;
        public string SrcBlendName = "", DestBlendName = "";
        public string SrcBlendAlphaName = "", DestBlendAlphaName = "";
        public string BlendOpName = "", BlendOpAlphaName = "";
        public string ColMaskName = "";
    }

    internal sealed class StencilOp
    {
        public float Comp, PassOp, Fail, ZFail;

        /// <summary>Material-property names driving each value (<c>Comp [_StencilComp]</c>).</summary>
        public string CompName = "", PassName = "", FailName = "", ZFailName = "";

        public bool AnyName => CompName != "" || PassName != "" || FailName != "" || ZFailName != "";
    }

    internal sealed class SubProgram
    {
        public string Stage = "";
        public int BlobIndex;
        public int GpuProgramType;

        /// <summary>
        /// <c>m_KeywordIndices</c>: indices into the pass's name table naming the
        /// shader keywords this variant is compiled for.  An empty list is the base
        /// variant; together the variants enumerate the keyword combinations.
        /// </summary>
        public List<int> KeywordIndices = new List<int>();

        /// <summary>
        /// Index into the shader payload's parameter-blob table, which holds this
        /// program's constant-buffer layout.  It comes from
        /// <c>SerializedProgram.m_ParameterBlobIndices[platform][i]</c> and is NOT
        /// the same as <see cref="BlobIndex"/>.
        /// </summary>
        public int ParameterBlobIndex = -1;

        /// <summary>
        /// <c>m_Parameters.m_ConstantBuffers</c>: Unity's own reflection of the
        /// constant buffers this program binds, straight out of the asset.  This is
        /// the authoritative member table -- name and byte offset for every member,
        /// per stage -- so neither the disassembly nor any outside source is needed
        /// to name a buffer.  It is also where the shader inspector's
        /// "Constant Buffer ... { Float _Active at 32 }" preview comes from.
        /// </summary>
        public System.Collections.Generic.List<AssetConstantBuffer> ConstantBuffers
            = new System.Collections.Generic.List<AssetConstantBuffer>();

        /// <summary>
        /// <c>m_Parameters.m_TextureParams</c> (plus the program-wide
        /// <c>m_CommonParameters</c> copy): the asset's own reflection of which
        /// texture occupies which sampler register.  <c>Index</c> is the t# register
        /// the disassembly references, <c>Sampler</c> the s# binding.  This is
        /// the authoritative texture name source -- property declaration order is
        /// NOT the register order.
        /// </summary>
        public System.Collections.Generic.List<(string Name, int Index, int Sampler)> TextureParams
            = new System.Collections.Generic.List<(string, int, int)>();
    }

    /// <summary>One <c>ConstantBuffer</c> entry from a program's parameter reflection.</summary>
    internal sealed class AssetConstantBuffer
    {
        public string Name = "";
        public int Size;
        public bool Partial;
        public System.Collections.Generic.List<AssetCbMember> Members
            = new System.Collections.Generic.List<AssetCbMember>();

        /// <summary>The `$Globals` block, i.e. the material's own properties.</summary>
        public bool IsGlobals => Name == "$Globals";

        /// <summary>Member covering a byte range, or null.</summary>
        public AssetCbMember At(int byteOffset)
        {
            foreach (var m in Members)
                if (m.Offset == byteOffset) return m;
            return null;
        }
    }

    internal sealed class AssetCbMember
    {
        public string Name = "";
        public int Offset;
        public int Type;
        public int Dim;
        public int ArraySize;
        public int RowCount;
        public bool IsMatrix;
    }

    /// <summary>
    /// Minimal <c>Material</c> (ClassID 21) view: identity, the shader it points
    /// at, and the saved property sheet — everything needed to reconcile the
    /// material's values against the shader's property table.
    /// </summary>
    internal sealed class MaterialAsset
    {
        public long PathId;
        public string Name = "";

        /// <summary><c>m_Shader</c> PPtr.  FileID 0 = same serialized file;
        /// anything else refers to an external file (see SerializedFile.Externals).</summary>
        public int ShaderFileId;
        public long ShaderPathId;

        /// <summary><c>m_SavedProperties.m_TexEnvs</c>: name → texture PPtr (fileId, pathId).</summary>
        public readonly List<(string name, int fileId, long pathId)> TexEnvs = new();

        /// <summary><c>m_SavedProperties.m_Floats</c> (includes Range properties).</summary>
        public readonly List<(string name, float value)> Floats = new();

        /// <summary><c>m_SavedProperties.m_Colors</c> (Color/Vector properties).</summary>
        public readonly List<(string name, float r, float g, float b, float a)> Colors = new();
    }
}
