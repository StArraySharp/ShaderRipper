using System.Reflection;
using System.Runtime.InteropServices;

namespace ShaderRipper
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

        public static void EnsureD3DCompiler()
        {
            if (_done || !OperatingSystem.IsWindows()) return;
            lock (Lock)
            {
                if (_done) return;
                try
                {
                    string dir = Path.Combine(Path.GetTempPath(), "ShaderRipper", "native");
                    Directory.CreateDirectory(dir);
                    string target = Path.Combine(dir, "D3Dcompiler_47.dll");

                    var asm = typeof(NativeLibLoader).Assembly;
                    using var src = asm.GetManifestResourceStream("ShaderRipper.Native.D3Dcompiler_47.dll");
                    if (src == null) return;                       // 资源缺失（不该发生）
                    if (!File.Exists(target) || new FileInfo(target).Length != src.Length)
                    {                                                 // 版本/大小变化才覆写，避免并发读写冲突
                        using var dst = File.Create(target);
                        src.CopyTo(dst);
                    }
                    SetDllDirectory(dir);
                }
                catch { /* 解压失败就让 DllImport 走系统搜索路径 */ }
                _done = true;
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }
}
