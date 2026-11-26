using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using AssetRipper.Primitives;
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
        private const uint FlagBlockDataPadding = 0x200;

        private enum Compression { None = 0, Lzma = 1, Lz4 = 2, Lz4Hc = 3, Lzham = 4 }
        private readonly record struct Block(int Raw, int Stored, Compression Codec);

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
            if (!IsBundleBytes(buf) || buf.Length < 12 || buf[7] != 0)
                throw new InvalidDataException("not a UnityFS bundle");

            int p = 8;
            uint version = U32(buf, ref p);
            CStr(buf, ref p);
            string revision = CStr(buf, ref p);
            if (buf.Length - p < 20) throw new InvalidDataException("truncated UnityFS header");
            ulong size = U64(buf, ref p);
            uint compressedInfoSize = U32(buf, ref p);
            uint uncompressedInfoSize = U32(buf, ref p);
            uint flags = U32(buf, ref p);

            if (size > (ulong)buf.Length || size < (ulong)p ||
                compressedInfoSize > size - (ulong)p || uncompressedInfoSize > Array.MaxLength)
                throw new InvalidDataException("invalid UnityFS bundle or block table size");

            var compression = (Compression)(flags & 0x3F);
            bool atEnd = (flags & FlagBlocksInfoAtEnd) != 0;
            bool hasEngineVersion = UnityVersion.TryParse(revision, out var engine, out _);
            bool alignHeader = version >= 7 || (hasEngineVersion && engine.Major == 2019 &&
                engine >= new UnityVersion(2019, 4, 15));
            bool alignData = (flags & FlagBlockDataPadding) != 0 &&
                (!hasEngineVersion || UsesBlockPaddingFlag(engine));

            InvalidDataException error = null;
            long footer = atEnd ? (long)size - compressedInfoSize : -1;
            foreach (long start in atEnd ? new[] { footer } : AlignmentCandidates(p, alignHeader))
            {
                if (start + compressedInfoSize > (long)size) continue;
                try
                {
                    int tableStart = (int)start;
                    int tableEnd = tableStart + (int)compressedInfoSize;
                    byte[] info = Decompress(Slice(buf, tableStart, (int)compressedInfoSize),
                                             (int)uncompressedInfoSize, compression);
                    return ReadLayout(buf, info, atEnd ? p : tableEnd, atEnd ? tableStart : (int)size,
                                      atEnd, alignData || (atEnd && alignHeader));
                }
                catch (InvalidDataException e) { error ??= e; }
            }
            throw new InvalidDataException("invalid UnityFS block table or block data", error);
        }

        private static List<Node> ReadLayout(byte[] buf, byte[] info, int dataStart, int dataEnd,
                                             bool infoAtEnd, bool alignData)
        {
            if (info.Length < 24) throw new InvalidDataException("truncated UnityFS block table");
            int q = 16;
            uint blockCount = U32(info, ref q);
            if (blockCount > (info.Length - 24) / 10) throw new InvalidDataException("invalid UnityFS block count");
            var blocks = new Block[(int)blockCount];
            long rawSize = 0;
            long storedSize = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                uint raw = U32(info, ref q);
                uint stored = U32(info, ref q);
                var compression = (Compression)(U16(info, ref q) & 0x3F);
                rawSize += raw;
                storedSize += stored;
                if (rawSize > Array.MaxLength || storedSize > dataEnd - dataStart)
                    throw new InvalidDataException("invalid UnityFS block sizes");
                if (compression == Compression.None && raw != stored)
                    throw new InvalidDataException("invalid uncompressed UnityFS block size");
                blocks[i] = new Block((int)raw, (int)stored, compression);
            }

            uint nodeCount = U32(info, ref q);
            if (nodeCount > (info.Length - q) / 21) throw new InvalidDataException("invalid UnityFS node count");
            var nodes = new List<(int Offset, int Length, string Path)>((int)nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                if (info.Length - q < 21) throw new InvalidDataException("truncated UnityFS node");
                ulong off = U64(info, ref q);
                ulong len = U64(info, ref q);
                U32(info, ref q);
                string path = CStr(info, ref q);
                if (off > (ulong)rawSize || len > (ulong)rawSize - off)
                    throw new InvalidDataException("UnityFS node exceeds the block stream");
                if (len > 0) nodes.Add(((int)off, (int)len, path));
            }
            if (info.AsSpan(q).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("unexpected data at the end of the UnityFS block table");

            // With a leading table, the declared bundle end can identify padding
            // even when a legacy writer omitted the data padding flag.
            if (!infoAtEnd && dataEnd - storedSize == Align16(dataStart))
                alignData = true;

            InvalidDataException error = null;
            foreach (long start in AlignmentCandidates(dataStart, alignData))
            {
                long end = start + storedSize;
                if (end > dataEnd || !IsPadding(buf, dataStart, (int)start) ||
                    !IsPadding(buf, (int)end, dataEnd))
                    continue;
                try
                {
                    byte[] stream = ReadBlocks(buf, blocks, (int)start, (int)rawSize);
                    var result = new List<Node>(nodes.Count);
                    foreach (var (offset, length, path) in nodes)
                        result.Add(new Node(path, Slice(stream, offset, length)));
                    return result;
                }
                catch (InvalidDataException e) { error ??= e; }
            }
            throw new InvalidDataException("UnityFS block data not found", error);
        }

        private static byte[] ReadBlocks(byte[] buf, Block[] blocks, int source, int rawSize)
        {
            var stream = new byte[rawSize];
            int dest = 0;
            foreach (var block in blocks)
            {
                if (block.Codec == Compression.None)
                    Buffer.BlockCopy(buf, source, stream, dest, block.Raw);
                else
                {
                    byte[] data = Decompress(Slice(buf, source, block.Stored), block.Raw, block.Codec);
                    Buffer.BlockCopy(data, 0, stream, dest, block.Raw);
                }
                source += block.Stored;
                dest += block.Raw;
            }
            return stream;
        }

        /// <summary>
        /// Serialized files inside a bundle, with nested containers expanded.
        ///
        /// A Unity 6 build can put a whole UnityFS container inside a node of
        /// another one, so a bare <c>CAB-&lt;hash&gt;</c> node may itself be a
        /// bundle.  Reading that node as a SerializedFile desynchronises the type
        /// table immediately.
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
        internal static IEnumerable<(string path, byte[] data)> SerializedFiles(
            byte[] buf, string prefix, int depth = 0, Action<string, Exception> onError = null)
        {
            List<Node> nodes;
            try { nodes = Read(buf); }
            catch (Exception e)
            {
                onError?.Invoke(prefix, e);
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
                    foreach (var inner in SerializedFiles(n.Data, path, depth + 1, onError))
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

        // Older Unity releases used this bit for asset bundle encryption.
        private static bool UsesBlockPaddingFlag(UnityVersion version) => version.Major switch
        {
            < 2020 => false,
            2020 => version >= new UnityVersion(2020, 3, 34),
            2021 => version >= new UnityVersion(2021, 3, 2),
            2022 => version >= new UnityVersion(2022, 1, 1),
            _ => true,
        };

        private static IEnumerable<long> AlignmentCandidates(int offset, bool preferAligned)
        {
            long aligned = Align16(offset);
            yield return preferAligned ? aligned : offset;
            if (aligned != offset) yield return preferAligned ? offset : aligned;
        }

        private static long Align16(int offset) => (offset + 15L) & ~15L;

        private static bool IsPadding(byte[] buffer, int start, int end) =>
            buffer.AsSpan(start, end - start).IndexOfAnyExcept((byte)0) < 0;

        private static byte[] Decompress(byte[] src, int rawSize, Compression c)
        {
            switch (c)
            {
                case Compression.None:
                    if (src.Length != rawSize) throw new InvalidDataException("invalid uncompressed UnityFS table size");
                    return src;
                case Compression.Lz4:
                case Compression.Lz4Hc:
                    {
                        var dst = new byte[rawSize];
                        int got = LZ4Codec.Decode(src, 0, src.Length, dst, 0, rawSize);
                        if (got != rawSize) throw new InvalidDataException("LZ4 block decode failed");
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

        private static byte[] DecodeLzma(byte[] src, int rawSize)
        {
            // UnityFS prefixes the raw LZMA1 stream with five property bytes.
            const int propertySize = 5;
            if (src.Length < propertySize + 5 || src[0] >= 225 ||
                BinaryPrimitives.ReadUInt32LittleEndian(src.AsSpan(1)) > 1u << 27)
                throw new InvalidDataException("Invalid LZMA block properties");
            if (src[propertySize] != 0) throw new InvalidDataException("Invalid LZMA range stream");
            using var input = new MemoryStream(src, propertySize, src.Length - propertySize, writable: false);
            using var output = new MemoryStream(rawSize);
            var decoder = new SevenZip.Compression.LZMA.Decoder();
            try
            {
                decoder.SetDecoderProperties(src[..propertySize]);
                decoder.Code(input, output, input.Length, rawSize, null);
            }
            catch (ApplicationException e)
            {
                throw new InvalidDataException("Invalid LZMA block", e);
            }
            if (output.Length != rawSize)
                throw new InvalidDataException($"LZMA decode produced {output.Length}/{rawSize} bytes");
            return output.ToArray();
        }

        private static byte[] Slice(byte[] b, int off, int len)
        {
            if (off < 0 || len < 0 || len > b.Length - off)
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
            int end = Array.IndexOf(b, (byte)0, p);
            if (end < 0) throw new InvalidDataException("unterminated UnityFS string");
            string value = System.Text.Encoding.UTF8.GetString(b, p, end - p);
            p = end + 1;
            return value;
        }
        private static uint BE(byte[] b, int o) =>
            (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
    }
}
