using System;
using System.IO;

namespace ShaderRipper
{
    /// <summary>
    /// 内建资源来源判定。
    ///
    /// Unity 玩家包里有三处“引擎自带”的着色器来源，里面的东西不是游戏自己的
    /// 渲染逻辑，不需要反编译：
    ///
    ///   Resources/unity default resources   — 默认资源
    ///   Resources/unity_builtin_extra       — 内置着色器（Hidden/BlitCopy 等）
    ///   StreamingAssets/aa/*unitybuiltinassets.bundle — Addressables 的
    ///                                        "Built In Data" 包，内建着色器整包
    ///
    /// 只跳过这三个来源，其余文件（resources.assets、游戏的 .bundle）照常处理。
    /// </summary>
    internal static class BuiltinSource
    {
        private const string DefaultResources = "unity default resources";
        private const string BuiltinExtra = "unity_builtin_extra";
        private const string BuiltinBundle = "unitybuiltinassets.bundle";

        /// <summary>True when the path names one of the engine's built-in shader sources.</summary>
        public static bool IsBuiltin(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            string file = Path.GetFileName(path.TrimEnd('/', '\\'));
            if (string.Equals(file, DefaultResources, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(file, BuiltinExtra, StringComparison.OrdinalIgnoreCase)) return true;

            // 包名带哈希前缀（64b0…_unitybuiltinassets.bundle），只能做包含匹配。
            return file.Contains(BuiltinBundle, StringComparison.OrdinalIgnoreCase);
        }
    }
}
