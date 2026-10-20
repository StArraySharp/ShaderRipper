using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderRipper
{
    /// <summary>
    /// Unity's <c>SerializedShader.m_Platforms</c> ids.
    ///
    /// The numbers are <c>UnityEditor.Rendering.ShaderCompilerPlatform</c>, read
    /// straight out of the editor (see DumpPlatforms.cs in the verify project) —
    /// they are NOT the C#-side <c>ShaderCompilerPlatform</c> ordering, which is
    /// why d3d11 shows up as 4 and Vulkan as 18.
    ///
    /// The value matters because <c>m_Offsets</c> is indexed by the same order as
    /// <c>m_Platforms</c>: decompressing the wrong slot yields another backend's
    /// payload (DXBC vs SPIR-V vs GLSL), and the bytes then look like noise.
    /// </summary>
    internal sealed class PlatformInfo
    {
        public int Id;
        public string Name = "";
        public string Format = "";      // "dxbc" | "glsl" | "metal" | "spirv" | "console"
        public bool HasSource;          // true when the payload is recompilable text
        public override string ToString() => $"{Name}({Id})";
    }

    internal static class Platforms
    {
        private static readonly PlatformInfo[] Table =
        {
            new() { Id = 4,  Name = "d3d11",      Format = "dxbc" },
            new() { Id = 5,  Name = "gles20",     Format = "glsl",  HasSource = true },
            new() { Id = 9,  Name = "gles3",      Format = "glsl",  HasSource = true },
            new() { Id = 11, Name = "ps4",        Format = "console" },
            new() { Id = 12, Name = "xboxone",    Format = "dxbc" },
            new() { Id = 14, Name = "metal",      Format = "metal", HasSource = true },
            new() { Id = 15, Name = "openglcore", Format = "glsl",  HasSource = true },
            new() { Id = 18, Name = "vulkan",     Format = "spirv" },
            new() { Id = 19, Name = "switch",     Format = "console" },
            new() { Id = 20, Name = "xboxone-d3d12", Format = "dxbc" },
            new() { Id = 21, Name = "gamecore-xboxone", Format = "dxbc" },
            new() { Id = 22, Name = "gamecore-xboxseries", Format = "dxbc" },
            new() { Id = 23, Name = "ps5",        Format = "console" },
            new() { Id = 24, Name = "ps5-nggc",   Format = "console" },
            new() { Id = 25, Name = "gamecore",   Format = "dxbc" },
            new() { Id = 26, Name = "webgpu",     Format = "wgsl" },
            new() { Id = 27, Name = "switch2",    Format = "console" },
        };

        private static readonly Dictionary<int, PlatformInfo> ById =
            Table.ToDictionary(p => p.Id);

        public static PlatformInfo Get(int id) =>
            ById.TryGetValue(id, out var p) ? p
            : new PlatformInfo { Id = id, Name = "unknown" + id, Format = "unknown" };

        /// <summary>Accept both the numeric id and the name ("vulkan", "gles3").</summary>
        public static int? ByName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (int.TryParse(s, out int id)) return id;
            foreach (var p in Table)
                if (string.Equals(p.Name, s, StringComparison.OrdinalIgnoreCase)) return p.Id;
            return null;
        }

        public static string Describe(int id)
        {
            var p = Get(id);
            return p.Format == "unknown" ? p.Name : $"{p.Name}/{p.Format}";
        }

        /// <summary>
        /// Pick the platform whose payload should drive the emitted program.
        ///
        /// D3D11 wins when present because it is the only backend this tool can
        /// turn into Cg.  Falling back to a source platform (GLSL/Metal) is still
        /// useful: those payloads ARE the original shader source, so the output
        /// is better than a transliteration.
        /// </summary>
        public static int PickMain(IReadOnlyList<uint> ids, int? forced)
        {
            if (forced.HasValue)
            {
                for (int i = 0; i < ids.Count; i++)
                    if ((int)ids[i] == forced.Value) return i;
                return -1;
            }
            // 1) d3d11, 2) any dxbc, 3) GLSL source, 4) Metal, 5) Vulkan, 6) first
            int[] order = { 4, 12, 20, 21, 22, 25, 9, 5, 15, 14, 18 };
            foreach (int want in order)
                for (int i = 0; i < ids.Count; i++)
                    if ((int)ids[i] == want) return i;
            return ids.Count > 0 ? 0 : -1;
        }

        /// <summary>Platforms worth also dumping as reference, newest first.</summary>
        public static IEnumerable<int> OtherIndices(IReadOnlyList<uint> ids, int main)
        {
            for (int i = 0; i < ids.Count; i++)
                if (i != main) yield return i;
        }
    }
}
