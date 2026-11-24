using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Read and rebuild the DXBC chunk container.
    ///
    /// A container is a fixed header, a table of chunk offsets and the chunks
    /// themselves -- a four-character code, a byte size, then the payload.  The one
    /// operation this tool needs is putting back a chunk Unity omits, so the writer
    /// keeps every chunk it was given, in the order it was given, and only rewrites
    /// the table.
    /// </summary>
    internal static class DxbcContainer
    {
        private const uint Magic = 0x43425844;       // 'DXBC'
        private const int HeaderBytes = 32;          // magic, checksum, unknown, size, count

        /// <summary>Does this start a DXBC container?</summary>
        public static bool IsContainer(byte[] data) =>
            data != null && data.Length >= HeaderBytes && BitConverter.ToUInt32(data, 0) == Magic;

        /// <summary>
        /// One chunk by code, header included, or null when the container does not
        /// carry it.  The returned bytes are a copy, so the caller can hand them to
        /// another container.
        /// </summary>
        public static byte[] Chunk(byte[] container, string fourCc)
        {
            var chunks = ReadChunks(container);
            if (chunks == null) return null;
            foreach (var chunk in chunks)
                if (chunk.Code == fourCc) return chunk.Bytes;
            return null;
        }

        /// <summary>Is this chunk one the container already carries?</summary>
        public static bool HasChunk(byte[] container, string fourCc) => Chunk(container, fourCc) != null;

        // Reflection can change; the instructions, signatures and other program
        // data must still belong to the same Unity blob.
        public static bool HasSameProgram(byte[] original, byte[] restored)
        {
            var before = ReadChunks(original);
            var after = ReadChunks(restored);
            if (before == null || after == null) return false;
            before.RemoveAll(chunk => chunk.Code == "RDEF");
            after.RemoveAll(chunk => chunk.Code == "RDEF");
            return before.Count == after.Count && before.Any(chunk => chunk.Code is "SHDR" or "SHEX" or "DXIL")
                && before.Zip(after).All(pair => pair.First.Bytes.AsSpan().SequenceEqual(pair.Second.Bytes));
        }

        /// <summary>Wrap a payload as a chunk: four-character code, size, payload.</summary>
        public static byte[] Wrap(string fourCc, byte[] payload)
        {
            var bytes = new byte[8 + payload.Length];
            for (int i = 0; i < 4; i++) bytes[i] = (byte)fourCc[i];
            WriteUInt(bytes, 4, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, bytes, 8, payload.Length);
            return bytes;
        }

        /// <summary>
        /// <paramref name="container"/> with <paramref name="chunk"/> (code, size and
        /// payload) as its first chunk, replacing any chunk that carries the same
        /// code.
        ///
        /// Null when either argument is not a well-formed container, so a caller
        /// can keep the bytes it already has instead of writing something broken.
        /// </summary>
        public static byte[] WithFirstChunk(byte[] container, byte[] chunk)
        {
            if (chunk == null || chunk.Length < 8) return null;
            var existing = ReadChunks(container);
            if (existing == null) return null;

            string code = FourCc(chunk, 0);
            var chunks = new List<byte[]> { chunk };
            foreach (var old in existing)
                if (old.Code != code) chunks.Add(old.Bytes);

            int count = chunks.Count;
            long total = HeaderBytes + 4L * count;
            foreach (var c in chunks) total += c.Length;
            if (total > Array.MaxLength) return null;

            var outp = new byte[total];
            WriteUInt(outp, 0, Magic);
            WriteUInt(outp, 20, BitConverter.ToUInt32(container, 20));
            WriteUInt(outp, 24, (uint)total);
            WriteUInt(outp, 28, (uint)count);

            int at = HeaderBytes + 4 * count;
            for (int k = 0; k < count; k++)
            {
                WriteUInt(outp, HeaderBytes + 4 * k, (uint)at);
                Buffer.BlockCopy(chunks[k], 0, outp, at, chunks[k].Length);
                at += chunks[k].Length;
            }
            DxbcChecksum.Update(outp);
            return outp;
        }

        private readonly struct RawChunk
        {
            public readonly string Code;
            public readonly byte[] Bytes;
            public RawChunk(string code, byte[] bytes) { Code = code; Bytes = bytes; }
        }

        /// <summary>
        /// The container's chunks, header and payload, or null when the table does
        /// not describe chunks that fit inside the container.
        /// </summary>
        private static List<RawChunk> ReadChunks(byte[] container)
        {
            if (!IsContainer(container)) return null;
            int length = BitConverter.ToInt32(container, 24);
            int count = BitConverter.ToInt32(container, 28);
            if (count <= 0 || count > 64) return null;
            int table = HeaderBytes + 4 * count;
            if (length < table || length > container.Length) return null;

            var chunks = new List<RawChunk>(count);
            for (int k = 0; k < count; k++)
            {
                int at = BitConverter.ToInt32(container, HeaderBytes + 4 * k);
                if (at < table || at > length - 8) return null;
                int size = BitConverter.ToInt32(container, at + 4);
                if (size < 0 || (long)at + 8 + size > length) return null;
                var bytes = new byte[8 + size];
                Buffer.BlockCopy(container, at, bytes, 0, bytes.Length);
                chunks.Add(new RawChunk(FourCc(container, at), bytes));
            }
            return chunks;
        }

        private static string FourCc(byte[] data, int at) =>
            new(new[] { (char)data[at], (char)data[at + 1], (char)data[at + 2], (char)data[at + 3] });

        private static void WriteUInt(byte[] data, int at, uint value)
        {
            data[at] = (byte)value;
            data[at + 1] = (byte)(value >> 8);
            data[at + 2] = (byte)(value >> 16);
            data[at + 3] = (byte)(value >> 24);
        }
    }
}
