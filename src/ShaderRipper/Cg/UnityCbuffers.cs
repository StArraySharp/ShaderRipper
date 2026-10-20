using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Unity's constant-buffer layout for the built-in pipeline, transcribed from
    /// the shipped <c>CGIncludes/UnityShaderVariables.cginc</c>.  Row numbers are
    /// 16-byte float4 slots, which is how the disassembly indexes them.
    ///
    /// d3d11 assigns <c>b</c> registers by walking the declared cbuffers in that
    /// include order and skipping the ones the program never touches, with fxc's
    /// implicit <c>$Globals</c> (material properties) first.  The layout is used
    /// here to *identify* a cbuffer from the rows the program actually reads, and
    /// only after the shader's own name table confirms a member name.
    /// </summary>
    internal static class UnityCbuffers
    {
        public sealed class Member
        {
            public string Cb;
            public string Name;
            public int Row;
            public int Rows = 1;
            public int Col;
            public int Span = 4;
            public bool IsMatrix;
            public bool IsScalar;
            public bool IsVector;

            public bool Covers(int r) => r >= Row && r < Row + Rows;

            /// <summary>Whole row, starting at its first component.</summary>
            public bool FillsRow => !IsMatrix && Col == 0 && Span == 4;

            public bool CoversLane(int r, int c) =>
                r >= Row && r < Row + Rows && c >= Col && c < Col + Span;
        }

        public static readonly List<Member> Members = Build();

        /// <summary>
        /// Buffers the emitted <c>#include "UnityCG.cginc"</c> does NOT bring in, so
        /// the CGPROGRAM block has to declare their members itself.  UnitySprites.cginc
        /// is included explicitly by the sprite shaders, not by UnityCG.cginc.
        /// </summary>
        public static readonly HashSet<string> SelfDeclared =
            new(StringComparer.Ordinal) { "UnityPerDrawSprite" };

        /// <summary>
        /// Global names UnityCG.cginc already declares (unconditionally or under a
        /// keyword).  Re-declaring them collides; the include's copy is the one that
        /// must survive, because it carries the correct cbuffer membership.
        /// </summary>
        public static readonly HashSet<string> IncludeDeclared =
            new(StringComparer.Ordinal)
        {
            "unity_Lightmap_HDR", "unity_Lightmap", "unity_LightmapInd",
            "unity_ShadowMask", "unity_DynamicLightmap", "unity_DynamicDirectionality",
            "unity_DynamicNormal", "unity_LightmapST", "unity_DynamicLightmapST",
            "glstate_matrix_projection", "glstate_matrix_modelview",
            "glstate_matrix_mvp", "glstate_matrix_transpose_modelview",
            "glstate_matrix_view",
        };

        /// <summary>Declared type of the component run a member occupies.</summary>
        public static string TypeOf(Member m) =>
            m.IsMatrix ? "float4x4" : m.Span switch
            {
                1 => "float", 2 => "float2", 3 => "float3", _ => "float4"
            };

        private static List<Member> Build()
        {
            var m = new List<Member>();

            void Add(string cb, string name, int row, int rows = 1, bool mat = false,
                     bool scalar = false, bool vec = false, int col = 0, int span = 0) =>
                m.Add(new Member
                {
                    Cb = cb, Name = name, Row = row, Rows = rows,
                    IsMatrix = mat, IsScalar = scalar, IsVector = vec,
                    Col = col,
                    Span = span > 0 ? span : mat ? 4 : scalar ? 1 : vec ? 3 : 4
                });

            Add("UnityBillboardPerBatch", "unity_BillboardInfo", 0, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardSize", 1, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[0]", 2, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[1]", 3, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[2]", 4, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[3]", 5, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[4]", 6, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[5]", 7, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[6]", 8, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[7]", 9, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[8]", 10, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[9]", 11, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[10]", 12, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[11]", 13, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[12]", 14, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[13]", 15, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[14]", 16, col: 0, span: 4);
            Add("UnityBillboardPerBatch", "unity_BillboardImageTexCoords[15]", 17, col: 0, span: 4);

            Add("UnityBillboardPerCamera", "unity_BillboardNormal", 0, col: 0, span: 3, vec: true);
            Add("UnityBillboardPerCamera", "unity_BillboardTangent", 1, col: 0, span: 3, vec: true);
            Add("UnityBillboardPerCamera", "unity_BillboardCameraParams", 2, col: 0, span: 4);

            Add("UnityFog", "unity_FogColor", 0, col: 0, span: 4);
            Add("UnityFog", "unity_FogParams", 1, col: 0, span: 4);

            Add("UnityLighting", "_WorldSpaceLightPos0", 0, col: 0, span: 4);
            Add("UnityLighting", "_LightPositionRange", 1, col: 0, span: 4);
            Add("UnityLighting", "_LightProjectionParams", 2, col: 0, span: 4);
            Add("UnityLighting", "unity_4LightPosX0", 3, col: 0, span: 4);
            Add("UnityLighting", "unity_4LightPosY0", 4, col: 0, span: 4);
            Add("UnityLighting", "unity_4LightPosZ0", 5, col: 0, span: 4);
            Add("UnityLighting", "unity_4LightAtten0", 6, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[0]", 7, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[1]", 8, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[2]", 9, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[3]", 10, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[4]", 11, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[5]", 12, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[6]", 13, col: 0, span: 4);
            Add("UnityLighting", "unity_LightColor[7]", 14, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[0]", 15, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[1]", 16, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[2]", 17, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[3]", 18, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[4]", 19, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[5]", 20, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[6]", 21, col: 0, span: 4);
            Add("UnityLighting", "unity_LightPosition[7]", 22, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[0]", 23, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[1]", 24, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[2]", 25, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[3]", 26, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[4]", 27, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[5]", 28, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[6]", 29, col: 0, span: 4);
            Add("UnityLighting", "unity_LightAtten[7]", 30, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[0]", 31, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[1]", 32, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[2]", 33, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[3]", 34, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[4]", 35, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[5]", 36, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[6]", 37, col: 0, span: 4);
            Add("UnityLighting", "unity_SpotDirection[7]", 38, col: 0, span: 4);
            Add("UnityLighting", "unity_SHAr", 39, col: 0, span: 4);
            Add("UnityLighting", "unity_SHAg", 40, col: 0, span: 4);
            Add("UnityLighting", "unity_SHAb", 41, col: 0, span: 4);
            Add("UnityLighting", "unity_SHBr", 42, col: 0, span: 4);
            Add("UnityLighting", "unity_SHBg", 43, col: 0, span: 4);
            Add("UnityLighting", "unity_SHBb", 44, col: 0, span: 4);
            Add("UnityLighting", "unity_SHC", 45, col: 0, span: 4);
            Add("UnityLighting", "unity_OcclusionMaskSelector", 46, col: 0, span: 4);
            Add("UnityLighting", "unity_ProbesOcclusion", 47, col: 0, span: 4);

            Add("UnityLightmaps", "unity_LightmapST", 0, col: 0, span: 4);
            Add("UnityLightmaps", "unity_DynamicLightmapST", 1, col: 0, span: 4);

            Add("UnityMetaPass", "unity_MetaVertexControl", 0, col: 0, span: 4);
            Add("UnityMetaPass", "unity_MetaFragmentControl", 1, col: 0, span: 4);
            Add("UnityMetaPass", "unity_VisualizationMode", 2, scalar: true, col: 0);

            Add("UnityPerCamera", "_Time", 0, col: 0, span: 4);
            Add("UnityPerCamera", "_SinTime", 1, col: 0, span: 4);
            Add("UnityPerCamera", "_CosTime", 2, col: 0, span: 4);
            Add("UnityPerCamera", "unity_DeltaTime", 3, col: 0, span: 4);
            Add("UnityPerCamera", "_WorldSpaceCameraPos", 4, col: 0, span: 3, vec: true);
            Add("UnityPerCamera", "_ProjectionParams", 5, col: 0, span: 4);
            Add("UnityPerCamera", "_ScreenParams", 6, col: 0, span: 4);
            Add("UnityPerCamera", "_ZBufferParams", 7, col: 0, span: 4);
            Add("UnityPerCamera", "unity_OrthoParams", 8, col: 0, span: 4);

            Add("UnityPerCamera2", "_CameraToWorld", 0, 4, true);

            Add("UnityPerCameraRare", "unity_CameraWorldClipPlanes[0]", 0, col: 0, span: 4);
            Add("UnityPerCameraRare", "unity_CameraWorldClipPlanes[1]", 1, col: 0, span: 4);
            Add("UnityPerCameraRare", "unity_CameraWorldClipPlanes[2]", 2, col: 0, span: 4);
            Add("UnityPerCameraRare", "unity_CameraWorldClipPlanes[3]", 3, col: 0, span: 4);
            Add("UnityPerCameraRare", "unity_CameraWorldClipPlanes[4]", 4, col: 0, span: 4);
            Add("UnityPerCameraRare", "unity_CameraWorldClipPlanes[5]", 5, col: 0, span: 4);
            Add("UnityPerCameraRare", "unity_CameraProjection", 6, 4, true);
            Add("UnityPerCameraRare", "unity_CameraInvProjection", 10, 4, true);
            Add("UnityPerCameraRare", "unity_WorldToCamera", 14, 4, true);
            Add("UnityPerCameraRare", "unity_CameraToWorld", 18, 4, true);

            Add("UnityPerDraw", "unity_ObjectToWorld", 0, 4, true);
            Add("UnityPerDraw", "unity_WorldToObject", 4, 4, true);
            Add("UnityPerDraw", "unity_LODFade", 8, col: 0, span: 4);
            Add("UnityPerDraw", "unity_WorldTransformParams", 9, col: 0, span: 4);
            Add("UnityPerDraw", "unity_RenderingLayer", 10, col: 0, span: 4);

            Add("UnityPerDrawRare", "glstate_matrix_transpose_modelview0", 0, 4, true);

            Add("UnityPerDrawSprite", "_RendererColor", 0, col: 0, span: 4);
            Add("UnityPerDrawSprite", "_Flip", 1, col: 0, span: 2);
            Add("UnityPerDrawSprite", "_EnableExternalAlpha", 1, scalar: true, col: 2);

            Add("UnityPerFrame", "glstate_lightmodel_ambient", 0, col: 0, span: 4);
            Add("UnityPerFrame", "unity_AmbientSky", 1, col: 0, span: 4);
            Add("UnityPerFrame", "unity_AmbientEquator", 2, col: 0, span: 4);
            Add("UnityPerFrame", "unity_AmbientGround", 3, col: 0, span: 4);
            Add("UnityPerFrame", "unity_IndirectSpecColor", 4, col: 0, span: 4);
            Add("UnityPerFrame", "glstate_matrix_projection", 5, 4, true);
            Add("UnityPerFrame", "unity_MatrixV", 9, 4, true);
            Add("UnityPerFrame", "unity_MatrixInvV", 13, 4, true);
            Add("UnityPerFrame", "unity_MatrixVP", 17, 4, true);
            Add("UnityPerFrame", "unity_StereoEyeIndex", 21, scalar: true, col: 0);
            Add("UnityPerFrame", "unity_ShadowColor", 22, col: 0, span: 4);

            Add("UnityReflectionProbes", "unity_SpecCube0_BoxMax", 0, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube0_BoxMin", 1, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube0_ProbePosition", 2, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube0_HDR", 3, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube1_BoxMax", 4, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube1_BoxMin", 5, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube1_ProbePosition", 6, col: 0, span: 4);
            Add("UnityReflectionProbes", "unity_SpecCube1_HDR", 7, col: 0, span: 4);

            Add("UnityShadows", "unity_ShadowSplitSpheres[0]", 0, col: 0, span: 4);
            Add("UnityShadows", "unity_ShadowSplitSpheres[1]", 1, col: 0, span: 4);
            Add("UnityShadows", "unity_ShadowSplitSpheres[2]", 2, col: 0, span: 4);
            Add("UnityShadows", "unity_ShadowSplitSpheres[3]", 3, col: 0, span: 4);
            Add("UnityShadows", "unity_ShadowSplitSqRadii", 4, col: 0, span: 4);
            Add("UnityShadows", "unity_LightShadowBias", 5, col: 0, span: 4);
            Add("UnityShadows", "_LightSplitsNear", 6, col: 0, span: 4);
            Add("UnityShadows", "_LightSplitsFar", 7, col: 0, span: 4);
            Add("UnityShadows", "unity_WorldToShadow[0]", 8, 4, true);
            Add("UnityShadows", "unity_WorldToShadow[1]", 12, 4, true);
            Add("UnityShadows", "unity_WorldToShadow[2]", 16, 4, true);
            Add("UnityShadows", "unity_WorldToShadow[3]", 20, 4, true);
            Add("UnityShadows", "_LightShadowData", 24, col: 0, span: 4);
            Add("UnityShadows", "unity_ShadowFadeCenterAndType", 25, col: 0, span: 4);

            Add("UnityTerrain", "_WavingTint", 0, col: 0, span: 4);
            Add("UnityTerrain", "_WaveAndDistance", 1, col: 0, span: 4);
            Add("UnityTerrain", "_CameraPosition", 2, col: 0, span: 4);
            Add("UnityTerrain", "_TreeInstanceColor", 3, col: 0, span: 4);
            Add("UnityTerrain", "_TreeInstanceScale", 4, col: 0, span: 4);
            Add("UnityTerrain", "_TerrainEngineBendTree", 5, 4, true);
            Add("UnityTerrain", "_SquashPlaneNormal", 9, col: 0, span: 4);
            Add("UnityTerrain", "_SquashAmount", 10, scalar: true, col: 0);
            Add("UnityTerrain", "_TreeBillboardCameraRight", 10, col: 1, span: 3, vec: true);
            Add("UnityTerrain", "_TreeBillboardCameraUp", 11, col: 0, span: 4);
            Add("UnityTerrain", "_TreeBillboardCameraFront", 12, col: 0, span: 4);
            Add("UnityTerrain", "_TreeBillboardCameraPos", 13, col: 0, span: 4);
            Add("UnityTerrain", "_TreeBillboardDistances", 14, col: 0, span: 4);

            return m;
        }

        public static IEnumerable<Member> ForCb(string cb) =>
            Members.Where(m => m.Cb == cb);

        public static Member Find(string cb, int row) =>
            Members.FirstOrDefault(m => m.Cb == cb && m.Covers(row));

        /// <summary>Distinct cbuffer names known to the layout table.</summary>
        public static IEnumerable<string> CbNames() => Members.Select(m => m.Cb).Distinct();

        /// <summary>How many float4 rows this cbuffer occupies (0 for an unknown name).</summary>
        public static int RowCount(string cb)
        {
            int n = 0;
            foreach (var m in Members)
                if (m.Cb == cb) n = Math.Max(n, m.Row + m.Rows);
            return n;
        }

        /// <summary>The member occupying one exact component of a row.</summary>
        public static Member FindLane(string cb, int row, int col) =>
            Members.FirstOrDefault(m => m.Cb == cb && m.CoversLane(row, col));

        /// <summary>More than one member shares this row.</summary>
        public static bool RowIsShared(string cb, int row) =>
            Members.Count(m => m.Cb == cb && m.Covers(row)) > 1;

        /// <summary>
        /// Does the variable at <paramref name="row"/> of <paramref name="cb"/> have
        /// a name this pass actually references?  This is the evidence gate: a
        /// cbuffer is only renamed when the shader's own name table lists a member
        /// that the disassembly reads at that exact slot.
        /// </summary>
        public static bool Confirmed(string cb, IEnumerable<int> usedRows,
                                     ICollection<string> nameTable, out int hits)
        {
            hits = 0;
            foreach (int r in usedRows)
            {
                var m = Find(cb, r);
                if (m != null && nameTable != null && nameTable.Contains(m.Name)) hits++;
            }
            return hits > 0;
        }
    }
}
