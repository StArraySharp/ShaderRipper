using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper
{
    /// <summary>
    /// 唯一的路径解析点。
    ///
    /// 以前每个数据文件各自写死一串 <c>..\..\..\data</c> 候选，游戏目录更是直接写死成
    /// 本机的 Steam 路径——换台机器、或者把 exe 拷到别处就跑不起来。这里改成：
    ///
    ///   1. 显式参数（<c>--data</c>/<c>--out</c>/<c>--root</c>/<c>--names</c>…）；
    ///   2. 环境变量 <c>SHADERRIP_DATA</c> / <c>SHADERRIP_OUT</c> / <c>SHADERRIP_ROOT</c>；
    ///   3. 从可执行文件目录、再从当前工作目录**向上找标记**（含 <c>schema/</c> 或
    ///      <c>data/</c> 的目录，或者 <c>ShaderRipper.csproj</c> 所在的仓库目录）。
    ///
    /// 全都找不到就返回 null，由调用方明确报错——不再"猜一个看起来像"的路径。
    /// </summary>
    internal static class Paths
    {
        private static string _root;

        /// <summary>资源根目录（含 <c>schema/</c> 与 <c>data/</c>），可能为 null。</summary>
        public static string Root => _root ??= Discover();

        /// <summary>显式指定资源根；传 null/不存在则回退自动发现。</summary>
        public static void SetRoot(string dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                _root = Path.GetFullPath(dir);
        }

        /// <summary>
        /// 找一个资源文件（相对路径形如 <c>data/unity_builtin_shaders.txt</c>）。
        /// 顺序：exe 同级（发布后的布局）→ 资源根 → null。
        /// </summary>
        public static string Find(string relative)
        {
            string exeDir = AppContext.BaseDirectory;
            var probes = new List<string> { Path.Combine(exeDir, relative) };
            if (Root != null) probes.Add(Path.Combine(Root, relative));
            foreach (var p in probes)
                if (File.Exists(p)) return Path.GetFullPath(p);
            return null;
        }

        /// <summary>找一个资源目录（如 <c>data/tmp_layouts</c>）。</summary>
        public static string FindDir(string relative)
        {
            string exeDir = AppContext.BaseDirectory;
            var probes = new List<string> { Path.Combine(exeDir, relative) };
            if (Root != null) probes.Add(Path.Combine(Root, relative));
            foreach (var p in probes)
                if (Directory.Exists(p)) return Path.GetFullPath(p);
            return null;
        }

        /// <summary>
        /// 缓存目录：工作目录下的 <c>.temp</c>（诊断 dump、解码中间产物等）。
        /// 不存在时自动创建；创建失败（只读盘等）返回 null，调用方自行吞掉。
        /// </summary>
        public static string TempDir
        {
            get
            {
                if (_tempDir != null && Directory.Exists(_tempDir)) return _tempDir;
                try
                {
                    string d = Path.GetFullPath(".temp");
                    Directory.CreateDirectory(d);
                    return _tempDir = d;
                }
                catch { return null; }
            }
        }
        private static string _tempDir;


        /// <summary>
        /// schema 文件：<paramref name="explicitPath"/> 可以是文件，也可以是目录。
        /// </summary>
        public static string Schema(string fileName, string explicitPath = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string p = Directory.Exists(explicitPath)
                    ? Path.Combine(explicitPath, fileName)
                    : explicitPath;
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            return Find(Path.Combine("schema", fileName));
        }

        private static string Discover()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                for (int depth = 0; dir != null && depth < 8; depth++, dir = dir.Parent)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "schema")) ||
                        Directory.Exists(Path.Combine(dir.FullName, "data")) ||
                        File.Exists(Path.Combine(dir.FullName, "ShaderRipper.csproj")))
                        return dir.FullName;
                }
            }
            return null;
        }
    }
}
