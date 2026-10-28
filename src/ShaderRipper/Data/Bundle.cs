using System;
using System.Collections.Generic;
using System.IO;
using K4os.Compression.LZ4;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Minimal reader for the <c>UnityFS</c> bundle container, enough to pull the
    /// serialized files out of it.  A bundle is a header, a block table and a node
    /// table; the nodes are concatenated into one decompressed stream and each node
    /// is either a <see cref="SerializedFile"/> or a raw resource (<c>.resS</c>).
    /// </summary>
    internal static class Bundle
    {
        private const uint FlagBlocksInfoAtEnd = 0x80;

        private enum Compression { None = 0, Lzma = 1, Lz4 = 2, Lz4Hc = 3, Lzham = 4 }

        internal readonly struct Node
        {
            public readonly string Path;
            public readonly byte[] Data;
            public Node(string path, byte[] data) { Path = path; Data = data; }
        }

        /// <summary>Nodes of a bundle, in the order the bundle lists them.</summary>
        public static List<Node> Read(string path) => Read(File.ReadAllBytes(path));

        /// <summary>
        /// Is this file a UnityFS container?  Decided from the bytes, not the
        /// extension: a game is free to name its archives anything, and a *.bundle
        /// that is really a SerializedFile must be read as one.
        /// </summary>
        public static bool IsBundle(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var head = new byte[8];
                if (fs.Read(head, 0, 8) < 7) return false;
                return IsBundleBytes(head);
            }
            catch { return false; }
        }

        public static List<Node> Read(byte[] buf)
        {
            if (!IsBundleBytes(buf))
                throw new InvalidDataException("not a UnityFS bundle");

            int p = 8;
            uint version = U32(buf, ref p);
            CStr(buf, ref p);
            CStr(buf, ref p);
            long size = (long)U64(buf, ref p);
            uint compressedInfoSize = U32(buf, ref p);
            uint uncompressedInfoSize = U32(buf, ref p);
            uint flags = U32(buf, ref p);

            var compression = (Compression)(flags & 0x3F);
            long infoStart = (flags & FlagBlocksInfoAtEnd) != 0
                ? size - compressedInfoSize
                : p;

            byte[] info = null;
            long infoAt = -1;
            for (int shift = 0; shift <= 64 && info == null; shift++)
            {
                long at = infoStart + shift;
                if (at < 0 || at + compressedInfoSize > buf.Length) break;
                var slice = Slice(buf, (int)at, (int)compressedInfoSize);
                if (compressedInfoSize == uncompressedInfoSize)
                {
                    info = slice;
                    infoAt = at;
                    break;
                }
                try
                {
                    var d = Decompress(slice, (int)uncompressedInfoSize, compression);
                    if (d.Length == uncompressedInfoSize) { info = d; infoAt = at; }
                }
                catch {  }
            }
            if (info == null)
                throw new InvalidDataException("block table not found");

            int q = 16;
            int blockCount = (int)U32(info, ref q);
            var blocks = new (uint raw, uint comp, ushort flags)[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                uint raw = U32(info, ref q);
                uint comp = U32(info, ref q);
                ushort f = (ushort)U16(info, ref q);
                blocks[i] = (raw, comp, f);
            }

            int nodeCount = (int)U32(info, ref q);
            var nodes = new (long off, long len, string path)[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                long off = (long)U64(info, ref q);
                long len = (long)U64(info, ref q);
                U32(info, ref q);
                nodes[i] = (off, len, CStr(info, ref q));
            }

            long total = 0;
            foreach (var b in blocks) total += b.raw;
            var stream = new byte[total];

            long sp = -1;
            if (blocks.Length > 0)
            {
                // Normal layout: block data follows the block table. With the
                // BlocksInfoAtEnd flag (0x80) the data sits right after the header.
                var bases = new List<long>();
                if ((flags & FlagBlocksInfoAtEnd) != 0) bases.Add(p);
                bases.Add(infoAt + compressedInfoSize);
                foreach (var basePos in bases)
                for (int shift = 0; shift <= 160 && sp < 0; shift++)
                {
                    long cand = basePos + shift;
                    if (cand < 0 || cand + blocks[0].comp > buf.Length) break;
                    if (blocks[0].comp == blocks[0].raw) { sp = cand; break; }
                    try
                    {
                        var first = Slice(buf, (int)cand, (int)blocks[0].comp);
                        var d = Decompress(first, (int)blocks[0].raw,
                                           (Compression)(blocks[0].flags & 0x3F));
                        if (d.Length == blocks[0].raw) sp = cand;
                    }
                    catch {  }
                }
            }
            if (sp < 0) throw new InvalidDataException("block data not found");

            int dp = 0;
            foreach (var (raw, comp, bf) in blocks)
            {
                var src = Slice(buf, (int)sp, (int)comp);
                var dst = new byte[raw];
                if (comp == raw) Buffer.BlockCopy(src, 0, dst, 0, (int)raw);
                else
                {
                    var bc = (Compression)(bf & 0x3F);
                    if (bc == Compression.None) Buffer.BlockCopy(src, 0, dst, 0, (int)raw);
                    else if (bc is Compression.Lz4 or Compression.Lz4Hc)
                    {
                        int got = LZ4Codec.Decode(src, 0, src.Length, dst, 0, dst.Length);
                        if (got <= 0) throw new InvalidDataException("LZ4 block decode failed");
                    }
                    else throw new NotSupportedException(
                        $"bundle block compression {bc} is not supported (only LZ4/none)");
                }
                Buffer.BlockCopy(dst, 0, stream, dp, dst.Length);
                dp += dst.Length;
                sp += comp;
            }

            var list = new List<Node>(nodeCount);
            foreach (var (off, len, nodePath) in nodes)
            {
                if (off < 0 || len <= 0 || off + len > stream.Length) continue;
                var data = new byte[len];
                Buffer.BlockCopy(stream, (int)off, data, 0, (int)len);
                list.Add(new Node(nodePath, data));
            }
            return list;
        }

        /// <summary>
        /// Serialized files inside a bundle, with nested containers expanded.
        ///
        /// A Unity 6 build can put a whole UnityFS container inside a node of
        /// another one: the outer bundle lists a bare <c>CAB-&lt;hash&gt;</c> node
        /// whose payload is itself a bundle, and the serialized files that matter
        /// live one level down.  Reading that node as a SerializedFile desynchronises
        /// the type table immediately (the payload starts with "UnityFS", not with a
        /// metadata size), which is exactly the "truncated type table, negative
        /// bytes left" failure this used to report for every such node.
        ///
        /// Nodes ending in <c>.resS</c> / <c>.resource</c> are raw payloads and are
        /// skipped: no shader object has ever lived in one.
        /// </summary>
        public static IEnumerable<(string path, byte[] data)> SerializedFiles(string bundlePath)
        {
            foreach (var (path, data) in SerializedFiles(File.ReadAllBytes(bundlePath), ""))
                yield return (path, data);
        }

        /// <summary>
        /// The same walk over an already-loaded bundle, with a path prefix so a
        /// nested entry still says where it came from.
        ///
        /// Depth is capped: no Unity build nests containers more than a couple of
        /// levels, and a hostile or corrupt file must not send the walk into an
        /// unbounded recursion.
        /// </summary>
        private static IEnumerable<(string path, byte[] data)> SerializedFiles(
            byte[] buf, string prefix, int depth = 0)
        {
            List<Node> nodes;
            try { nodes = Read(buf); }
            catch (Exception e)
            {
                // Silence here used to hide a genuine failure: a bundle that could
                // not be decoded produced zero shaders and a "0 files" summary, with
                // nothing to say why.  A one-line note costs nothing and turns that
                // into a diagnosable report.
                if (Environment.GetEnvironmentVariable("SHADERRIP_BUNDLEDBG") == "1")
                    Console.Error.WriteLine($"[bundle] {prefix}: {e.GetType().Name}: {e.Message}");
                yield break;
            }

            foreach (var n in nodes)
            {
                string path = prefix.Length == 0 ? n.Path : prefix + "/" + n.Path;

                if (IsBundleBytes(n.Data))
                {
                    // Nested container: its own nodes are what the caller wants,
                    // and the container itself carries no objects of its own.
                    if (depth >= 4) continue;
                    foreach (var inner in SerializedFiles(n.Data, path, depth + 1))
                        yield return inner;
                    continue;
                }

                if (n.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                    n.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return (path, n.Data);
            }
        }

        /// <summary>Does this payload start with the UnityFS magic?</summary>
        internal static bool IsBundleBytes(byte[] data) =>
            data != null && data.Length >= 7 &&
            data[0] == 'U' && data[1] == 'n' && data[2] == 'i' &&
            data[3] == 't' && data[4] == 'y' && data[5] == 'F' && data[6] == 'S';

        private static byte[] Decompress(byte[] src, int rawSize, Compression c)
        {
            var dst = new byte[rawSize];
            switch (c)
            {
                case Compression.None:
                    Buffer.BlockCopy(src, 0, dst, 0, Math.Min(src.Length, rawSize));
                    return dst;
                case Compression.Lz4:
                case Compression.Lz4Hc:
                    {
                        int got = LZ4Codec.Decode(src, 0, src.Length, dst, 0, rawSize);
                        if (got <= 0) throw new InvalidDataException("LZ4 blocksInfo decode failed");
                        return dst;
                    }
                case Compression.Lzma:
                    return DecodeLzma(src, rawSize);
                case Compression.Lzham:
                    throw new NotSupportedException(
                        "bundle is LZHAM-compressed — not supported (very rare; LZ4 and LZMA are)");
                default:
                    throw new NotSupportedException(
                        $"bundle compression {(int)c} is not supported (only LZ4/LZMA/none)");
            }
        }

        /// <summary>
        /// UnityFS LZMA blocks use the classic "lzma alone" framing: 5 bytes of
        /// range-coder properties followed by the 8-byte uncompressed size, then
        /// the raw stream. SharpCompress' LzmaStream decodes exactly that.
        /// </summary>
        private static byte[] DecodeLzma(byte[] src, int rawSize)
        {
            if (src.Length < 13) throw new InvalidDataException("LZMA payload too short");
            var props = new byte[5];
            Buffer.BlockCopy(src, 0, props, 0, 5);
            using var input = new MemoryStream(src, 13, src.Length - 13, writable: false);
            using var output = new MemoryStream(rawSize);
            var dec = new SevenZip.Compression.LZMA.Decoder();
            dec.SetDecoderProperties(props);
            dec.Code(input, output, src.Length - 13, rawSize, null);
            if (output.Length != rawSize)
                throw new InvalidDataException($"LZMA decode produced {output.Length}/{rawSize} bytes");
            return output.ToArray();
        }

        private static byte[] Slice(byte[] b, int off, int len)
        {
            if (off < 0 || len < 0 || off + len > b.Length)
                throw new InvalidDataException("bundle offset out of range");
            var r = new byte[len];
            Buffer.BlockCopy(b, off, r, 0, len);
            return r;
        }

        private static uint U32(byte[] b, ref int p) { uint v = BE(b, p); p += 4; return v; }
        private static ushort U16(byte[] b, ref int p)
        {
            ushort v = (ushort)((b[p] << 8) | b[p + 1]); p += 2; return v;
        }
        private static ulong U64(byte[] b, ref int p)
        {
            ulong v = ((ulong)BE(b, p) << 32) | BE(b, p + 4); p += 8; return v;
        }
        private static string CStr(byte[] b, ref int p)
        {
            int s = p;
            while (p < b.Length && b[p] != 0) p++;
            var s2 = System.Text.Encoding.UTF8.GetString(b, s, p - s);
            p++;
            return s2;
        }
        private static uint BE(byte[] b, int o) =>
            (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
    }
}
