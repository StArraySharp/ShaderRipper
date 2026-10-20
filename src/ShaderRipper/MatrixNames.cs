using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper
{
    /// <summary>
    /// Unity 内置的 float4x4 全局变量名单。
    ///
    /// `$Globals` 是 fxc 给「所有未放进 CBUFFER 的全局 uniform」自动开的容器，
    /// 所以 `unity_WorldToLight` 这类全局矩阵会和材质属性挤在同一个 cbuffer 里。
    /// Unity 的参数块只记录了成员的**起始 offset**，不记录它跨几行——于是矩阵的
    /// 第 1~3 行就成了没有名字的 `cbN[row]`。
    ///
    /// offset 差值本身已经是证据：下一个成员在 +48/+64 字节处，说明这个成员横跨
    /// 3~4 行。再用这张表确认「它确实是个矩阵」而不是一块凑巧的空白，就能把
    /// 后续行安全地命名成 `name[1]`、`name[2]`、`name[3]`。
    ///
    /// 名单来源是 Unity 自己的头文件，不是人工枚举：编辑器安装目录下
    /// `CGIncludes/*.cginc` 里所有 `float4x4 NAME;` 声明。
    /// </summary>
    internal static class MatrixNames
    {
        private const string FileName = "unity_matrix_names.txt";

        private static HashSet<string> _names;

        public static string ResolvedPath { get; private set; }

        public static int Count => _names?.Count ?? -1;

        public static int Load(string explicitPath = null)
        {
            string path = explicitPath ?? Resolve();
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

        private static string Resolve() => Paths.Find(Path.Combine("data", FileName));
    }
}
