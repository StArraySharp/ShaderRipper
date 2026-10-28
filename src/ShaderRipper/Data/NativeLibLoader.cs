using System.Reflection;
using System.Runtime.InteropServices;

namespace ShaderRipper.Data
{
    /// <summary>
    /// 原生库随主程序集内嵌（NativeLibs/D3Dcompiler_47.dll → 资源
    /// "ShaderRipper.Native.D3Dcompiler_47.dll"），首次使用时解压到
    /// %TEMP%\ShaderRipper\native\ 并 SetDllDirectory，让 DllImport 能找到。
    /// 只在 Windows 上做 —— Linux 没有 d3dcompiler，这条链路整个被守卫跳过。
    /// </summary>
    internal static class NativeLibLoader
    {
        private static bool _done;
        private static readonly object Lock = new();

        /// <summary>原生库解压目录（%TEMP%\ShaderRipper\native），不可用时为 null。</summary>
        private static string NativeDir
        {
            get
            {
                if (_nativeDir == null)
                {
                    try
                    {
                        _nativeDir = Path.Combine(Path.GetTempPath(), "ShaderRipper", "native");
                        Directory.CreateDirectory(_nativeDir);
                    }
                    catch { }
                }
                return _nativeDir;
            }
        }
        private static string _nativeDir;

        /// <summary>
        /// 通用解压：把嵌入资源 <c>ShaderRipper.Native.&lt;fileName&gt;</c> 解压到
        /// 原生库目录并返回完整路径；资源缺失或解压失败返回 null。
        /// </summary>
        public static string ExtractNative(string fileName)
        {
            string dir = NativeDir;
            if (dir == null || string.IsNullOrEmpty(fileName)) return null;
            try
            {
                string target = Path.Combine(dir, fileName);
                var asm = typeof(NativeLibLoader).Assembly;
                using var src = asm.GetManifestResourceStream("ShaderRipper.Native." + fileName);
                if (src == null) return null;
                if (!File.Exists(target) || new FileInfo(target).Length != src.Length)
                {
                    using var dst = File.Create(target);
                    src.CopyTo(dst);
                }
                return target;
            }
            catch { return null; }
        }

        public static void EnsureD3DCompiler()
        {
            if (_done || !OperatingSystem.IsWindows()) return;
            lock (Lock)
            {
                if (_done) return;
                try
                {
                    string target = ExtractNative("D3Dcompiler_47.dll");
                    if (target != null && NativeDir != null)
                        SetDllDirectory(NativeDir);
                }
                catch {  }
                _done = true;
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }
}
