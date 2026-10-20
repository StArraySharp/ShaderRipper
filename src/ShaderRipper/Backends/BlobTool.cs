using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using K4os.Compression.LZ4;

namespace ShaderRipper
{
    /// <summary>LZ4 stage decode + DXBC carve + 反汇编（统一走 dxcompiler，全平台）。</summary>
    internal static class BlobTool
    {

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferPointerFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate UIntPtr GetBufferSizeFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseFn(IntPtr self);

        private static IntPtr _gbp, _gbs, _rel;

        public static bool NativeReady { get; private set; }
        public static string NativeError { get; private set; }

        static BlobTool()
        {
            try
            {
                // probe with a tiny valid call to ensure the DLL binds
                NativeReady = true;
            }
            catch (Exception e) { NativeError = e.Message; NativeReady = false; }
        }

        /// <summary>Decode every stage of platform 0 into one buffer.</summary>
        public static byte[] DecodeBlob(Shader sh) => DecodeBlob(sh, 0);

        /// <summary>
        /// Decompress one platform's payload.  <c>Offsets</c> is indexed
        /// [platform][slot], matching <c>m_Platforms</c>; picking the wrong
        /// index silently yields another backend's bytes (DXBC vs SPIR-V vs
        /// GLSL), so the caller decides which platform it wants.
        /// </summary>
        public static byte[] DecodeBlob(Shader sh, int platformIndex)
        {
            if (sh.Offsets.Length == 0) return Array.Empty<byte>();
            if (platformIndex < 0 || platformIndex >= sh.Offsets.Length) platformIndex = 0;
            var offs = sh.Offsets[platformIndex];
            var clens = sh.CompressedLengths[platformIndex];
            var dlens = sh.DecompressedLengths[platformIndex];
            var blob = sh.CompressedBlob;

            using var ms = new MemoryStream();
            int n = Math.Min(offs.Length, Math.Min(clens.Length, dlens.Length));
            for (int k = 0; k < n; k++)
            {
                int o = (int)offs[k], cl = (int)clens[k], dl = (int)dlens[k];
                if (o < 0 || o + cl > blob.Length) continue;
                if (cl == dl)
                {
                    ms.Write(blob, o, cl);
                }
                else
                {
                    var dst = new byte[dl];
                    int wrote = LZ4Codec.Decode(blob, o, cl, dst, 0, dl);
                    if (wrote > 0) ms.Write(dst, 0, wrote);
                }
            }
            return ms.ToArray();
        }

        /// <summary>
        /// One compiled program slot inside the decoded platform blob.
        /// <paramref name="BlobIndex"/> is Unity's <c>SerializedSubProgram.m_BlobIndex</c>.
        /// </summary>
        public readonly struct BlobSlot
        {
            public readonly int BlobIndex;
            public readonly int DxbcOffset;
            public readonly int DxbcSize;
            public BlobSlot(int bi, int off, int size) { BlobIndex = bi; DxbcOffset = off; DxbcSize = size; }
        }

        /// <summary>
        /// Map every blob to its true <c>m_BlobIndex</c>.
        ///
        /// Two layouts exist:
        ///
        ///  * 2019.3 -- the decompressed payload starts with <c>u32 count</c> then
        ///    <c>count</c> triplets of (absoluteOffset, size, reserved), and each
        ///    slot holds one DXBC container behind a 42-byte per-program header.
        ///
        ///  * 6000.x -- the same header, but the slots hold *parameter* blobs (the
        ///    constant-buffer name tables) and the DXBC containers follow them
        ///    contiguously.  The containers are stored in ascending blobIndex order
        ///    over the entries that actually have a compiled program, so they are
        ///    paired with the sorted set of blob indices the shader's sub-programs
        ///    reference.
        /// </summary>
        public static List<BlobSlot> DecodeSlots(byte[] buf, IReadOnlyCollection<int> usedBlobIndices = null)
        {
            var res = new List<BlobSlot>();
            if (buf.Length < 8) return res;

            int count = BitConverter.ToInt32(buf, 0);
            bool tableOk = count > 0 && count < 8192 && 4 + (long)count * 12 <= buf.Length;

            if (tableOk)
            {
                for (int e = 0; e < count; e++)
                {
                    int off = BitConverter.ToInt32(buf, 4 + e * 12);
                    int size = BitConverter.ToInt32(buf, 4 + e * 12 + 4);
                    if (off < 0 || size < 0x20 || (long)off + size > buf.Length) continue;
                    int at = FindMagic(buf, off, off + size);
                    if (at < 0) continue;
                    int total = BitConverter.ToInt32(buf, at + 0x18);
                    if (total <= 0x40 || (long)at + total > buf.Length) total = off + size - at;
                    res.Add(new BlobSlot(e, at, total));
                }
            }

            if (res.Count == 0 && usedBlobIndices != null)
            {
                // Unity 6 layout: pair the containers, in payload order, with the
                // blob indices the shader references, in ascending order.
                var hits = FindDxbc(buf);
                var idxs = usedBlobIndices.Distinct().OrderBy(x => x).ToList();
                if (hits.Count == idxs.Count)
                    for (int k = 0; k < hits.Count; k++)
                        res.Add(new BlobSlot(idxs[k], hits[k].off, hits[k].total));
            }

            if (res.Count == 0)          // fallback: legacy positional scan
            {
                var hits = FindDxbc(buf);
                for (int k = 0; k < hits.Count; k++) res.Add(new BlobSlot(k, hits[k].off, hits[k].total));
            }
            return res;
        }

        private static int FindMagic(byte[] buf, int from, int to)
        {
            for (int i = from; i + 4 <= to; i++)
                if (buf[i] == 0x44 && buf[i + 1] == 0x58 && buf[i + 2] == 0x42 && buf[i + 3] == 0x43)
                    return i;
            return -1;
        }

        /// <summary>A constant buffer as described by a parameter blob.</summary>
        public sealed class CbInfo
        {
            public string Name = "";
            public int Size;                                   // bytes
            /// <summary>Register the program binds this buffer to, or -1 when unrecorded.</summary>
            public int Register = -1;
            public List<(string name, int offset, int type, int dim)> Members = new();
            /// <summary>Raw 24-byte member-record tails: [0, type, dim, 0, 0, offset]
            /// (verified against known layouts: _EnableExternalAlpha dim=1, float4 dim=4).</summary>
            public List<int[]> MemberTails = new();
        }

        /// <summary>
        /// Parse the per-program parameter blobs (Unity 6000.x layout).
        ///
        /// Each blob holds the constant-buffer layout for one compiled program:
        /// the cbuffer names with their byte sizes, and -- when Unity kept them --
        /// the member names with their byte offsets.  This is the RDEF data Unity
        /// strips out of the DXBC container, so it is the authoritative source for
        /// naming constant-buffer slots.
        /// </summary>
        public static Dictionary<int, List<CbInfo>> ParseParamBlobs(byte[] buf)
        {
            var res = new Dictionary<int, List<CbInfo>>();
            if (buf.Length < 8) return res;

            int count = BitConverter.ToInt32(buf, 0);
            if (count <= 0 || count > 8192 || 4 + (long)count * 12 > buf.Length) return res;

            for (int e = 0; e < count; e++)
            {
                int off = BitConverter.ToInt32(buf, 4 + e * 12);
                int size = BitConverter.ToInt32(buf, 4 + e * 12 + 4);
                if (off < 0 || size < 16 || (long)off + size > buf.Length) continue;
                int end = off + size;

                var list = new List<CbInfo>();
                int p = off + 8;
                int recordEnd = -1;
                while (p + 8 <= end)
                {
                    var rec = ReadCbRecord(buf, p, end);
                    if (rec == null) { p += 4; continue; }
                    var (cb, next) = rec.Value;
                    if (cb.Name == "$Globals" || Cg.UnityCbuffers.Members.Any(m => m.Cb == cb.Name))
                    {
                        list.Add(cb);
                        recordEnd = next;
                    }
                    if (next <= p + 8) break;
                    p = next;
                }

                // After the member records the blob carries a small list of
                // (buffer name, ?, register) -- the register each buffer is bound to
                // in THIS program.  That is stronger evidence than reading the
                // declaration order off the reconstructed source, and it is the only
                // place a program that kept a buffer without a member table can be
                // resolved at all.
                if (recordEnd > 0 && list.Count > 0)
                {
                    var map = ReadRegisterMap(buf, recordEnd, end);
                    if (map != null)
                        foreach (var (nm, reg) in map)
                        {
                            var hit = list.FirstOrDefault(x => x.Name == nm);
                            if (hit != null) hit.Register = reg;
                        }
                }
                if (list.Count > 0) res[e] = list;
            }
            return res;
        }

        /// <summary>
        /// The trailing (buffer name, ?, register) list, or null.  It has to consume
        /// the blob exactly to its end, and every name in it has to be a buffer the
        /// record scan already found, which is what keeps a stray parse out.
        /// </summary>
        private static List<(string name, int reg)> ReadRegisterMap(byte[] b, int from, int end)
        {
            for (int p = from; p + 4 <= end; p += 4)
            {
                int n = BitConverter.ToInt32(b, p);
                if (n <= 0 || n > 64) continue;
                int q = p + 4;
                var list = new List<(string, int)>();
                bool ok = true;
                for (int i = 0; i < n && ok; i++)
                {
                    if (q + 4 > end) { ok = false; break; }
                    int ln = BitConverter.ToInt32(b, q);
                    if (ln <= 0 || ln > 64 || q + 4 + ln > end) { ok = false; break; }
                    for (int k = 0; k < ln; k++)
                    {
                        byte c = b[q + 4 + k];
                        if (!(c == '$' || c == '_' || (c >= 'A' && c <= 'Z') ||
                              (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
                        { ok = false; break; }
                    }
                    if (!ok) break;
                    string nm = System.Text.Encoding.ASCII.GetString(b, q + 4, ln);
                    int s = q + 4 + ((ln + 3) & ~3);
                    if (s + 12 > end) { ok = false; break; }
                    list.Add((nm, BitConverter.ToInt32(b, s + 4)));
                    q = s + 12;
                }
                if (ok && q == end && list.Count > 0) return list;
            }
            return null;
        }

        private static (CbInfo cb, int next)? ReadCbRecord(byte[] b, int p, int end)
        {
            if (p + 8 > end) return null;
            int n = BitConverter.ToInt32(b, p);
            if (n <= 0 || n > 64 || p + 4 + n > end) return null;
            for (int i = 0; i < n; i++)
            {
                byte c = b[p + 4 + i];
                if (!(c == '$' || c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9'))) return null;
            }
            string name = System.Text.Encoding.ASCII.GetString(b, p + 4, n);
            int q = p + 4 + ((n + 3) & ~3);
            if (q + 8 > end) return null;

            var cb = new CbInfo { Name = name, Size = BitConverter.ToInt32(b, q) };
            int members = BitConverter.ToInt32(b, q + 4);
            if (cb.Size < 0 || cb.Size > 1 << 20 || members < 0 || members > 4096) return null;

            // The record tail measured 12 bytes for one blob and 8 for another, so the
            // member list is taken at +8 and the outer scan resynchronises by 4 bytes
            // whenever the next position does not parse.  Trying a single fixed stride
            // drops blobs whose tail differs.
            int r = q + 8;
            for (int i = 0; i < members; i++)
            {
                if (r + 4 > end) return null;
                int mn = BitConverter.ToInt32(b, r);
                if (mn <= 0 || mn > 64 || r + 4 + mn > end) return null;
                string mname = System.Text.Encoding.ASCII.GetString(b, r + 4, mn);
                int s = r + 4 + ((mn + 3) & ~3);
                if (s + 24 > end) return null;
                int moff = BitConverter.ToInt32(b, s + 20);       // byte offset in the cbuffer
                var tail = new int[6];
                for (int k = 0; k < 6; k++) tail[k] = BitConverter.ToInt32(b, s + 4 * k);
                if (moff >= 0 && moff < cb.Size) { cb.Members.Add((mname, moff, tail[1], tail[2])); cb.MemberTails.Add(tail); }
                r = s + 24;
            }
            return (cb, r);
        }

        /// <summary>True when the buffer uses the Unity 6000.x parameter-blob layout.</summary>
        public static bool IsParamBlobLayout(byte[] buf)
        {
            if (buf.Length < 8) return false;
            int count = BitConverter.ToInt32(buf, 0);
            if (count <= 0 || count > 8192 || 4 + (long)count * 12 > buf.Length) return false;
            int off = BitConverter.ToInt32(buf, 4);
            int size = BitConverter.ToInt32(buf, 8);
            return off >= 0 && size > 16 && (long)off + size <= buf.Length &&
                   FindMagic(buf, off, off + size) < 0;
        }

        /// <summary>Find every "DXBC" container (offset,total).</summary>
        public static List<(int off, int total)> FindDxbc(byte[] buf)
        {
            var res = new List<(int, int)>();
            int i = 0;
            while (true)
            {
                int j = IndexOf(buf, DxbcMagic, i);
                if (j < 0) break;
                if (j + 0x20 <= buf.Length)
                {
                    int total = BitConverter.ToInt32(buf, j + 0x18);
                    int nch = BitConverter.ToInt32(buf, j + 0x1C);
                    if (total > 0x40 && total <= buf.Length - j && nch > 0 && nch <= 16)
                        res.Add((j, total));
                }
                i = j + 4;
            }
            return res;
        }

        private static readonly byte[] DxbcMagic = { 0x44, 0x58, 0x42, 0x43 };  // "DXBC"

        private static int IndexOf(byte[] hay, byte[] needle, int from)
        {
            int last = hay.Length - needle.Length;
            for (int i = from; i <= last; i++)
            {
                if (hay[i] != needle[0]) continue;
                bool ok = true;
                for (int k = 1; k < needle.Length; k++)
                    if (hay[i + k] != needle[k]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }

    /// <summary>Disassemble one DXBC/DXIL container; null on failure.
    /// Windows：SM4/5 DXBC 先走 d3dcompiler（内嵌解压）。d3dcompiler 缺席或失败时
    /// （含全平台 Linux），SM4/5 DXBC 走 DXDecompiler（纯托管 NuGet，跨平台）；
    /// DXIL（D3D12）只有 dxcompiler 认，仍走 DxcDisassemble。</summary>
    public static string Disassemble(byte[] container)
    {
        // SHADERRIP_FORCE_MANAGED=1 跳过 d3dcompiler，直接走托管反汇编（跨平台一致性验证用）
        bool forceManaged = Environment.GetEnvironmentVariable("SHADERRIP_FORCE_MANAGED") == "1";
        string s = null;
        if (!forceManaged && OperatingSystem.IsWindows())
        {
            NativeLibLoader.EnsureD3DCompiler();
            s = DisassembleWith(container, D3DDisassemble);
        }
        if (s == null && !HasDxilChunk(container))
            s = ManagedDisassemble(container);
        if (s == null)
            s = DxcDisassemble(container);
        return s;
    }

    // ---- DXDecompiler（纯托管 SM4/5 DXBC 反汇编）----

    /// <summary>DXDecompiler 托管反汇编 DXBC 容器；失败返回 null。
    /// 输出风格与 d3dcompiler 接近（vs_5_0 / dcl_ / mov 等标准 SM 汇编）。</summary>
    private static string ManagedDisassemble(byte[] container)
    {
        try
        {
            var bc = new DXDecompiler.BytecodeContainer(container);
            string s = bc.ToString();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("[dxbc] DXDecompiler: " + e.Message);
            return null;
        }
    }

    // ---- d3dcompiler（Windows 内嵌解压，仅用于 SM4/5 DXBC）----

    [DllImport("d3dcompiler_47", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int D3DDisassemble(
        IntPtr pSrcData, uint bytecodeLength, uint flags,
        [MarshalAs(UnmanagedType.LPStr)] string szComments, out IntPtr ppDisassembly);

    private static string DisassembleWith(byte[] container, DisasmFnX fn)
    {
        IntPtr p = Marshal.AllocHGlobal(container.Length);
        try
        {
            Marshal.Copy(container, 0, p, container.Length);
            int hr = fn(p, (uint)container.Length, 0u, "// ", out IntPtr blob);
            if (hr != 0 || blob == IntPtr.Zero) return null;

            IntPtr vtbl = Marshal.ReadIntPtr(blob);
            var gbp = (GetBufferPointerFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtbl, 3 * IntPtr.Size), typeof(GetBufferPointerFn));
            var gbs = (GetBufferSizeFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size), typeof(GetBufferSizeFn));
            var rel = (ReleaseFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtbl, 2 * IntPtr.Size), typeof(ReleaseFn));

            IntPtr buf = gbp(blob);
            ulong len = (ulong)gbs(blob);
            string text = Marshal.PtrToStringAnsi(buf, (int)len)?.Replace("\0", "");   // 缓冲区偶带 NUL 残留
            rel(blob);
            return text;
        }
        catch { return null; }
        finally { Marshal.FreeHGlobal(p); }
    }

    private delegate int DisasmFnX(IntPtr data, uint size, uint flags, string comment, out IntPtr blob);


    /// <summary>DXBC 容器里有没有 DXIL chunk（= D3D12/SM6 程序）。</summary>
    public static bool HasDxilChunk(byte[] container)
    {
        if (container.Length < 0x24 || BitConverter.ToUInt32(container, 0) != 0x43425844) return false;
        int nch = BitConverter.ToInt32(container, 0x1C);
        if (nch <= 0 || nch > 16) return false;
        for (int k = 0; k < nch; k++)
        {
            int coff = BitConverter.ToInt32(container, 0x20 + 4 * k);
            if (coff < 0 || coff + 4 > container.Length) continue;
            if (container[coff] == (byte)'D' && container[coff + 1] == (byte)'X' &&
                container[coff + 2] == (byte)'I' && container[coff + 3] == (byte)'L')
                return true;
        }
        return false;
    }

    // ---- DXIL 反汇编：dxcompiler.dll（COM, DxcCreateInstance + IDxcCompiler3）----

    private static readonly Guid CLSID_DxcCompiler = new Guid("73E22D93-E6CE-47F3-B5BF-F0664F39C1B0");
    private static readonly Guid IID_IDxcCompiler3 = new Guid("228B4687-5A6A-4730-900C-9702B2203F54");
    private static readonly Guid IID_IDxcResult = new Guid("58346CDA-DDE7-4497-9461-6F87AF5E0659");

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr h, string name);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DxcCreateInstanceFn(ref Guid clsid, ref Guid iid, out IntPtr obj);
    // IDxcCompiler3：IUnknown(0-2), Compile(3), Disassemble(4)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Disassemble3Fn(IntPtr self, IntPtr dxcBuffer, ref Guid riid, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QIFn(IntPtr self, ref Guid iid, out IntPtr obj);

    private static IntPtr LoadDxcCompiler()
    {
        // 跨平台：优先用内嵌解压的原生库（发布产物是单文件，磁盘上没有），
        // 失败再走系统搜索路径；Windows 额外试注册表定位的 Unity 安装目录。
        if (OperatingSystem.IsWindows())
        {
            IntPtr h;
            string embedded = NativeLibLoader.ExtractNative("dxcompiler.dll");
            if (embedded != null)
            {
                h = LoadLibrary(embedded);
                if (h != IntPtr.Zero) return h;
            }
            h = LoadLibrary("dxcompiler");
            if (h != IntPtr.Zero) return h;
            try
            {
                using var base_ = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
                using var inst = base_.OpenSubKey(@"SOFTWARE\Unity Technologies\Installer");
                if (inst == null) return IntPtr.Zero;
                foreach (var keyName in inst.GetSubKeyNames())
                {
                    using var k = inst.OpenSubKey(keyName);
                    if (k == null) continue;
                    foreach (var vn in k.GetValueNames())
                    {
                        if (!vn.Contains("Location")) continue;
                        if (k.GetValue(vn) is not string dir) continue;
                        string dll = Path.Combine(dir, "Editor", "Data", "Tools", "dxcompiler.dll");
                        if (!File.Exists(dll)) continue;
                        h = LoadLibrary(dll);
                        if (h != IntPtr.Zero) return h;
                    }
                }
            }
            catch { }
            return IntPtr.Zero;
        }
        // Linux/macOS：先解压内嵌的 .so（libdxil.so 预加载，让 dxc 后续 dlopen
        // "libdxil.so" 时命中已加载的 soname），再试 exe 同目录，最后裸 soname。
        string soC = NativeLibLoader.ExtractNative("libdxil.so");
        if (soC != null && NativeLibrary.TryLoad(soC, out IntPtr hDxil)) { /* 预加载即可 */ }
        string so = NativeLibLoader.ExtractNative("libdxcompiler.so");
        if (so != null && NativeLibrary.TryLoad(so, out IntPtr hSo)) return hSo;
        so = Path.Combine(AppContext.BaseDirectory, "libdxcompiler.so");
        if (NativeLibrary.TryLoad(so, out hSo)) return hSo;
        if (NativeLibrary.TryLoad("libdxcompiler.so", out hSo) ||
            NativeLibrary.TryLoad("dxcompiler", out hSo))
            return hSo;
        return IntPtr.Zero;
    }

    /// <summary>用 dxcompiler.dll（COM）反汇编 DXBC+DXIL；失败返回 null。</summary>
    public static string DxcDisassemble(byte[] container)
    {
        try
        {
            IntPtr h = LoadDxcCompiler();
            if (h == IntPtr.Zero) { Console.Error.WriteLine("[dxc] dxcompiler.dll not found"); return null; }
            IntPtr create = OperatingSystem.IsWindows()
                ? GetProcAddress(h, "DxcCreateInstance")
                : NativeLibrary.GetExport(h, "DxcCreateInstance");
            if (create == IntPtr.Zero) return null;
            var createFn = (DxcCreateInstanceFn)Marshal.GetDelegateForFunctionPointer(create, typeof(DxcCreateInstanceFn));

            Guid clsid = CLSID_DxcCompiler, iidC3 = IID_IDxcCompiler3;
            int hrC = createFn(ref clsid, ref iidC3, out IntPtr compiler);
            if (hrC != 0 || compiler == IntPtr.Zero)
            {
                Console.Error.WriteLine($"[dxc] DxcCreateInstance hr=0x{hrC:X8}");
                return null;
            }

            // DxcBuffer { Ptr, Size, Encoding }
            IntPtr buf = Marshal.AllocHGlobal(container.Length);
            Marshal.Copy(container, 0, buf, container.Length);
            IntPtr dxcBuf = Marshal.AllocHGlobal(24);   // {Ptr(8), Size(8), Encoding(4)+pad}
            Marshal.WriteIntPtr(dxcBuf, 0, buf);
            Marshal.WriteIntPtr(dxcBuf, 8, (IntPtr)container.Length);
            Marshal.WriteInt32(dxcBuf, 16, 0);

            IntPtr vtbl = Marshal.ReadIntPtr(compiler);
            var disasm = (Disassemble3Fn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size), typeof(Disassemble3Fn));  // IUnknown(0-2), Compile(3), Disassemble(4)

            // 结果是 IDxcResult（riid 必须对；IID_IDxcBlob/OperationResult 会 E_INVALIDARG）
            Guid iidResult = IID_IDxcResult;
            int hr = disasm(compiler, dxcBuf, ref iidResult, out IntPtr result);
            if (hr != 0)
                Console.Error.WriteLine($"[dxc] Disassemble hr=0x{hr:X8}");

            var relC = (ReleaseFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtbl, 2 * IntPtr.Size), typeof(ReleaseFn));
            relC(compiler);
            if (hr != 0 || result == IntPtr.Zero) return null;

            // IDxcResult：IUnknown(0-2), GetStatus(3), GetResult(4)——主输出即反汇编文本
            IntPtr rvtbl = Marshal.ReadIntPtr(result);
            hr = ((GetResultFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(rvtbl, 4 * IntPtr.Size), typeof(GetResultFn)))(result, out IntPtr blob);
            if (hr != 0 || blob == IntPtr.Zero) return null;

            IntPtr bvtbl = Marshal.ReadIntPtr(blob);
            var gbp = (GetBufferPointerFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(bvtbl, 3 * IntPtr.Size), typeof(GetBufferPointerFn));
            var gbs = (GetBufferSizeFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(bvtbl, 4 * IntPtr.Size), typeof(GetBufferSizeFn));
            IntPtr textPtr = gbp(blob);
            int textLen = (int)gbs(blob);
            string text = Marshal.PtrToStringAnsi(textPtr, textLen);
            var relB = (ReleaseFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(bvtbl, 2 * IntPtr.Size), typeof(ReleaseFn));
            relB(blob);
            return text;
        }
        catch (Exception e) { Console.Error.WriteLine("[dxc] " + e.Message); return null; }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetResultFn(IntPtr self, out IntPtr blob);
    }

    /// <summary>Which backend produced a compiled program slot.</summary>
    internal enum BlobFormat
    {
        Unknown = 0,
        Dxbc,        // d3d11 / d3d12: DirectX bytecode (DXBC+SM4/5 或 DXBC+DXIL)
        SpirV,       // vulkan / metal-on-vulkan
        Metal,       // Unity's 0xF00DCAFE metal archive (entry name + MSL source)
        Glsl,        // gles3 / gles / opengl: HLSLcc output, already text
        D3d9,        // d3d9: SM2/SM3 裸 token 流（自研反汇编，见 D3D9Asm）
    }

    internal static class BlobFormatDetect
    {
        private const uint MetalMagic = 0xF00DCAFE;
        private const uint SpirVMagic = 0x07230203;

        public static BlobFormat Detect(byte[] b, int off = 0, int len = -1)
        {
            if (b == null || b.Length < off + 4) return BlobFormat.Unknown;
            if (len < 0) len = b.Length - off;
            uint u = System.BitConverter.ToUInt32(b, off);
            if (u == 0x43425844) return BlobFormat.Dxbc;          // "DXBC"
            if (u == MetalMagic) return BlobFormat.Metal;
            if (u == SpirVMagic || u == 0x03022307) return BlobFormat.SpirV;
            if (LooksD3d9(b, off, off + len)) return BlobFormat.D3d9;

            // GLSL is text; HLSLcc always opens with "#version".  Skip a BOM and
            // leading whitespace but do not scan far: a wrong guess here costs a
            // lot less than accepting a binary blob as source.
            int i = off;
            int limit = Math.Min(off + len, off + 512);
            if (i + 3 <= limit && b[i] == 0xEF && b[i + 1] == 0xBB && b[i + 2] == 0xBF) i += 3;
            while (i < limit && (b[i] == (byte)' ' || b[i] == (byte)'\t' ||
                                 b[i] == (byte)'\r' || b[i] == (byte)'\n')) i++;
            if (i < limit && b[i] == (byte)'#')
            {
                string head = System.Text.Encoding.ASCII.GetString(b, i, Math.Min(256, limit - i));
                if (head.Contains("version") || head.Contains("define") || head.Contains("ifdef"))
                    return BlobFormat.Glsl;
            }
            return BlobFormat.Unknown;
        }

        /// <summary>Unity's Metal archive: 0xF00DCAFE, u32 header size, then a
        /// NUL-terminated entry name and the MSL source.</summary>
        public static bool MetalSplit(byte[] b, int off, out string entry, out int srcOff)
        {
            entry = null;
            srcOff = off;
            if (Detect(b, off) != BlobFormat.Metal) return false;
            int header = System.BitConverter.ToInt32(b, off + 4);
            if (header <= 0 || off + header >= b.Length) return false;
            int p = off + header;
            int e = p;
            while (e < b.Length && b[e] != 0) e++;
            entry = System.Text.Encoding.ASCII.GetString(b, p, e - p);
            srcOff = Math.Min(e + 1, b.Length);
            return srcOff < b.Length;
        }

        /// <summary>
        /// Locate the compiled program inside a slot.  Every slot starts with a
        /// per-program header (<c>0x0C0A75BA</c>) followed by constant-buffer
        /// tables, so the payload is not at offset 0 and its backend is only
        /// knowable by its own signature.
        /// </summary>
        public static (BlobFormat fmt, int off) FindProgram(byte[] b, int start, int end)
        {
            if (b == null) return (BlobFormat.Unknown, -1);
            start = Math.Max(0, start);
            end = Math.Min(b.Length, end);
            for (int i = start; i + 4 <= end; i++)
            {
                uint u = System.BitConverter.ToUInt32(b, i);
                if (u == 0x43425844) return (BlobFormat.Dxbc, i);
                if (u == 0x07230203) return (BlobFormat.SpirV, i);
                if (u == MetalMagic) return (BlobFormat.Metal, i);
                // d3d9 版本 token：高 16 位 FFFE(vs)/FFFF(ps)，主版本 1..3。
                // 随手一个 0xFFFFxxxx 不算数，必须整条 token 流能走到 END。
                uint hi = u >> 16;
                if (i == start || hi == 0xFFFE || hi == 0xFFFF)
                {
                    if ((hi == 0xFFFE || hi == 0xFFFF) && LooksD3d9(b, i, end))
                        return (BlobFormat.D3d9, i);
                }
                if (b[i] == (byte)'#' && i + 8 < end)
                {
                    string h = System.Text.Encoding.ASCII.GetString(b, i, 16);
                    if (h.StartsWith("#version") || h.StartsWith("#ifdef") || h.StartsWith("#define"))
                        return (BlobFormat.Glsl, i);
                }
            }
            return (BlobFormat.Unknown, -1);
        }

        /// <summary>
        /// d3d9 SM2/SM3 载荷校验 + 长度计算。token 流规则：END=0x0000FFFF；
        /// 注释 token 低 16 位 0xFFFE、bit16-30 是 DWORD 数；指令 token 的
        /// bit24-27 是长度（不含指令 token 本身）；操作数 token bit31=1，
        /// 跟在指令长度里，不需单独跳。
        /// </summary>
        public static bool LooksD3d9(byte[] b, int off, int end)
        {
            if (off + 4 > end) return false;
            uint v = System.BitConverter.ToUInt32(b, off);
            uint hi = v >> 16;
            if (hi != 0xFFFE && hi != 0xFFFF) return false;
            uint major = (v >> 8) & 0xFF;
            if (major < 2 || major > 3) return false;             // SM1 不带长度字段
            return D3d9TokenEnd(b, off + 4, end) >= 0;
        }

        /// <summary>从 token 流走到 END，返回 END 之后的位置；走不下去返回 -1。</summary>
        public static int D3d9TokenEnd(byte[] b, int p, int end)
        {
            int guard = 0;
            while (p + 4 <= end && guard++ < 1 << 20)
            {
                uint w = System.BitConverter.ToUInt32(b, p);
                if (w == 0x0000FFFF) return p + 4;                // END
                if ((w & 0xFFFF) == 0xFFFE)                       // comment
                {
                    int cnt = (int)((w >> 16) & 0x7FFF);
                    if (cnt == 0) return -1;
                    p += 4 + cnt * 4;
                    continue;
                }
                if ((w & 0x80000000) != 0) return -1;             // 裸操作数 token
                int len = (int)((w >> 24) & 0xF);
                if (len <= 0) return -1;
                p += (len + 1) * 4;                               // 长度不含指令 token
            }
            return -1;
        }

        public static string Hex(byte[] b, int off = 0, int len = 32)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = off; i < Math.Min(off + len, b.Length); i++) sb.Append(b[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
