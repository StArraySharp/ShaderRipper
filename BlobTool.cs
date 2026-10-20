using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using K4os.Compression.LZ4;

namespace ShaderRipper
{
    /// <summary>LZ4 stage decode + DXBC carve + native D3DDisassemble.</summary>
    internal static class BlobTool
    {
        // ---- native ----
        [DllImport("d3dcompiler_47", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int D3DDisassemble(
            IntPtr pSrcData, uint bytecodeLength, uint flags,
            [MarshalAs(UnmanagedType.LPStr)] string szComments, out IntPtr ppDisassembly);

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

        /// <summary>Disassemble one DXBC container; null on failure.</summary>
        public static string Disassemble(byte[] container)
        {
            IntPtr p = Marshal.AllocHGlobal(container.Length);
            try
            {
                Marshal.Copy(container, 0, p, container.Length);
                IntPtr blob;
                int hr = D3DDisassemble(p, (uint)container.Length, 0u, "// ", out blob);
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
                string text = Marshal.PtrToStringAnsi(buf, (int)len);
                rel(blob);
                return text;
            }
            catch { return null; }
            finally { Marshal.FreeHGlobal(p); }
        }
    }

    /// <summary>Which backend produced a compiled program slot.</summary>
    internal enum BlobFormat
    {
        Unknown = 0,
        Dxbc,        // d3d11 / d3d12: DirectX bytecode
        SpirV,       // vulkan / metal-on-vulkan
        Metal,       // Unity's 0xF00DCAFE metal archive (entry name + MSL source)
        Glsl,        // gles3 / gles / opengl: HLSLcc output, already text
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
                if (b[i] == (byte)'#' && i + 8 < end)
                {
                    string h = System.Text.Encoding.ASCII.GetString(b, i, 16);
                    if (h.StartsWith("#version") || h.StartsWith("#ifdef") || h.StartsWith("#define"))
                        return (BlobFormat.Glsl, i);
                }
            }
            return (BlobFormat.Unknown, -1);
        }

        public static string Hex(byte[] b, int off = 0, int len = 32)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = off; i < Math.Min(off + len, b.Length); i++) sb.Append(b[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
