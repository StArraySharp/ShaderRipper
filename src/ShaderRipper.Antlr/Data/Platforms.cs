using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderRipper.Antlr.Data
{
    /// <summary>
    /// Unity's <c>SerializedShader.m_Platforms</c> ids.
    ///
    /// The numbers are <c>UnityEditor.Rendering.ShaderCompilerPlatform</c>, read
    /// straight out of the editor (see DumpPlatforms.cs in the verify project) —
    /// they are NOT the C#-side <c>ShaderCompilerPlatform</c> ordering, which is
    /// why d3d11 shows up as 4 and Vulkan as 18.
    ///
    /// 权威值（本机 Unity 6000.3.10f1 编辑器反射，注册表定位安装目录）：
    ///   0 None, 4 D3D, 5 GLES20, 9 GLES3x, 11 PS4, 12 XboxOneD3D11, 14 Metal,
    ///   15 OpenGLCore, 18 Vulkan, 19 Switch, 20 XboxOneD3D12, 21 GameCoreXboxOne,
    ///   22 GameCoreXboxSeries, 23 PS5, 24 PS5NGGC, 25 GameCore, 26 WebGPU, 27 Switch2.
    /// 现代编辑器枚举里没有 d3d9 / 桌面 GL / 通用 D3D12 —— 它们只存在于旧序列化
    /// 惯例（1=OpenGL、2=D3D9）或与平台 4 共用（D3D12 的 DXIL 载荷仍挂在 id 4 的
    /// 槽位里，按 DXBC 容器内的 DXIL chunk 识别）。
    ///
    /// The value matters because <c>m_Offsets</c> is indexed by the same order as
    /// <c>m_Platforms</c>: decompressing the wrong slot yields another backend's
    /// payload (DXBC vs SPIR-V vs GLSL), and the bytes then look like noise.
    /// </summary>
    internal sealed class PlatformInfo
    {
        public int Id;
        public string Name = "";
        public string Format = "";
        public bool HasSource;
        public override string ToString() => $"{Name}({Id})";
    }

    internal static class Platforms
    {
        private static readonly PlatformInfo[] Table =
        {
            new() { Id = 1,  Name = "opengl",     Format = "glsl",  HasSource = true },
            new() { Id = 2,  Name = "d3d9",       Format = "d3d9" },
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

        /// <summary>Accept both the numeric id and the name ("vulkan", "gles3").
        /// 常用别名：gles→gles3、gles2→gles20、gl→opengl、glcore→openglcore。</summary>
        public static int? ByName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToLowerInvariant();
            if (int.TryParse(s, out int id)) return id;
            s = s switch
            {
                "gles" => "gles3",
                "gles2" => "gles20",
                "gl" => "opengl",
                "glcore" => "openglcore",
                _ => s,
            };
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
            int[] order = { 4, 12, 20, 21, 22, 25, 9, 5, 15, 1, 2, 14, 18 };
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
