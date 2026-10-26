using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper.Antlr.Data
{
    /// <summary>
    /// Unity 内建着色器索引。
    ///
    /// 判别“这个 shader 是引擎自带的，还是游戏自己写的”不能靠名字前缀猜，
    /// 也不能只看来源文件——玩家包里内建 shader 会被复制进 resources.assets。
    /// 唯一不靠猜的证据是引擎自己的内建资源清单：用 Unity 编辑器
    /// （<c>_uproj/Assets/Editor/DumpBuiltinShaders.cs</c>）把
    /// <c>Resources/unity_builtin_extra</c> 里的 Shader 名全导出来，存成
    /// <c>data/unity_builtin_shaders.txt</c>（当前 6000.3.10f1，206 条）。
    ///
    /// 编辑器升级后重跑一次导出即可；文件是纯文本，也可以人工追加。
    /// </summary>
    internal static class BuiltinIndex
    {
        private const string FileName = "unity_builtin_shaders.txt";

        private static HashSet<string> _names;

        /// <summary>实际加载到的索引文件（null = 没加载）。</summary>
        public static string ResolvedPath { get; private set; }

        /// <summary>索引里的名字数量，-1 表示没加载。</summary>
        public static int Count => _names?.Count ?? -1;

        public static int Load(string explicitPath = null)
        {
            string path = explicitPath ?? Paths.Find(Path.Combine("data", FileName));
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return -1;

            _names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path))
            {
                string n = line.Trim();
                if (n.Length > 0 && !n.StartsWith("#", StringComparison.Ordinal)) _names.Add(n);
            }
            ResolvedPath = path;
            return _names.Count;
        }

        public static bool Contains(string name) =>
            !string.IsNullOrEmpty(name) && _names != null && _names.Contains(name);
    }
}
