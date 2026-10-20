using System;
using System.Collections.Generic;
using System.IO;
using K4os.Compression.LZ4;

namespace ShaderRipper
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

        private enum Compression { None = 0, Lzma = 1, Lz4 = 2, Lz4Hc = 3 }

        internal readonly struct Node
        {
            public readonly string Path;
            public readonly byte[] Data;
            public Node(string path, byte[] data) { Path = path; Data = data; }
        }

        /// <summary>Nodes of a bundle, in the order the bundle lists them.</summary>
        public static List<Node> Read(string path) => Read(File.ReadAllBytes(path));

        public static List<Node> Read(byte[] buf)
        {
            if (buf.Length < 32 || buf[0] != 'U' || buf[1] != 'n' || buf[2] != 'i' ||
                buf[3] != 't' || buf[4] != 'y' || buf[5] != 'F' || buf[6] != 'S')
                throw new InvalidDataException("not a UnityFS bundle");

            int p = 8;
            uint version = U32(buf, ref p);
            CStr(buf, ref p);                                  // unityVersion
            CStr(buf, ref p);                                  // unityRevision
            long size = (long)U64(buf, ref p);
            uint compressedInfoSize = U32(buf, ref p);
            uint uncompressedInfoSize = U32(buf, ref p);
            uint flags = U32(buf, ref p);

            var compression = (Compression)(flags & 0x3F);
            long infoStart = (flags & FlagBlocksInfoAtEnd) != 0
                ? size - compressedInfoSize
                : p;

            // Where the block table begins is not fully pinned down across Unity
            // versions: 6000.3 bundles carry extra header bytes after `flags`
            // (0x243 has the 0x200 archive bit set) and put the table 14 bytes past
            // the declared end.  Rather than guess, try the declared position and a
            // short window after it, and accept only a decode that yields exactly
            // the declared uncompressed size.
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
                catch { /* try the next offset */ }
            }
            if (info == null)
                throw new InvalidDataException("block table not found");

            int q = 16;                                        // hash
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
                U32(info, ref q);                              // node flags
                nodes[i] = (off, len, CStr(info, ref q));
            }

            // decompress every block into one contiguous stream, then slice the nodes out
            long total = 0;
            foreach (var b in blocks) total += b.raw;
            var stream = new byte[total];

            // Block data does not always begin immediately after the table either:
            // 6000.3 bundles leave a short interstitial, so locate it by requiring the
            // first block to decode to exactly its declared size.
            long sp = -1;
            if (blocks.Length > 0)
            {
                long basePos = infoAt + compressedInfoSize;
                for (int shift = 0; shift <= 160 && sp < 0; shift++)
                {
                    long cand = basePos + shift;
                    if (cand + blocks[0].comp > buf.Length) break;
                    if (blocks[0].comp == blocks[0].raw) { sp = cand; break; }
                    try
                    {
                        var first = Slice(buf, (int)cand, (int)blocks[0].comp);
                        var d = Decompress(first, (int)blocks[0].raw,
                                           (Compression)(blocks[0].flags & 0x3F));
                        if (d.Length == blocks[0].raw) sp = cand;
                    }
                    catch { /* try the next offset */ }
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

        /// <summary>Serialized files inside a bundle (the nodes that are not raw resources).</summary>
        public static IEnumerable<(string path, byte[] data)> SerializedFiles(string bundlePath)
        {
            foreach (var n in Read(bundlePath))
                if (n.Path.EndsWith(".assets", StringComparison.OrdinalIgnoreCase) ||
                    n.Path.IndexOf(".assets.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!n.Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) &&
                     !n.Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)))
                    yield return (n.Path, n.Data);
        }

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
                default:
                    throw new NotSupportedException(
                        $"bundle compression {c} is not supported (only LZ4/none)");
            }
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
            p++;                                               // NUL
            return s2;
        }
        private static uint BE(byte[] b, int o) =>
            (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
    }
}

