using System.Reflection;
using System.Runtime.InteropServices;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Native libraries are embedded in the main assembly
    /// (<c>NativeLibs/*.dll</c> as <c>ShaderRipper.Native.&lt;file&gt;</c>) and
    /// extracted to the temp directory on first use, with <c>SetDllDirectory</c>
    /// pointing at them so <c>DllImport</c> resolves.
    ///
    /// Windows only: there is no d3dcompiler elsewhere, and that path is guarded.
    /// </summary>
    internal static class NativeLibLoader
    {
        private static bool _done;
        private static readonly object Lock = new();

        /// <summary>Where the native libraries are extracted; null when unusable.</summary>
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
        /// Extract the embedded resource <c>ShaderRipper.Native.&lt;fileName&gt;</c> and
        /// return its full path, or null when the resource is missing.
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
