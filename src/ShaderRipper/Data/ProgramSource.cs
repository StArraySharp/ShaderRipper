using System;
using System.Collections.Generic;
using System.Text;

namespace ShaderRipper.Data
{
    /// <summary>One compiled program pulled out of a platform payload.</summary>
    internal sealed class ExtractedProgram
    {
        public int BlobIndex;
        public BlobFormat Format;
        public int Offset;
        public int Length;
        /// <summary>Recompilable source (GLSL / Metal).  Empty for binary formats.</summary>
        public string Text = "";
        /// <summary>Metal entry point, when the archive names one.</summary>
        public string Entry = "";
        public string Note = "";
    }

    /// <summary>
    /// Pull programs out of a decompressed platform payload.
    ///
    /// Every slot starts with a per-program header (0x0C0A75BA) followed by the
    /// constant-buffer name tables, so the payload never starts at offset 0 and its
    /// backend is recognised by its own signature: a DXBC container for d3d11,
    /// HLSLcc text starting at "#version" for gles/gl, a 0xF00DCAFE archive for
    /// metal, and SMOL-V-compressed SPIR-V (no magic number) for vulkan.
    /// </summary>
    internal static class ProgramSource
    {
        public static List<(int off, int size)> SlotRanges(byte[] b)
        {
            var res = new List<(int, int)>();
            if (b == null || b.Length < 8) return res;
            int cnt = BitConverter.ToInt32(b, 0);
            if (cnt <= 0 || cnt >= 8192 || 4L + (long)cnt * 12 > b.Length) return res;
            for (int e = 0; e < cnt; e++)
            {
                int off = BitConverter.ToInt32(b, 4 + e * 12);
                int size = BitConverter.ToInt32(b, 4 + e * 12 + 4);
                if (off < 0 || size <= 0 || (long)off + size > b.Length) continue;
                res.Add((off, size));
            }
            return res;
        }

        public static List<ExtractedProgram> Extract(byte[] payload)
        {
            var res = new List<ExtractedProgram>();
            var slots = SlotRanges(payload);
            for (int i = 0; i < slots.Count; i++)
            {
                var (off, size) = slots[i];
                var (fmt, at) = BlobFormatDetect.FindProgram(payload, off, off + size);
                if (fmt == BlobFormat.Unknown) continue;

                var p = new ExtractedProgram { BlobIndex = i, Format = fmt, Offset = at };
                switch (fmt)
                {
                    case BlobFormat.Dxbc:
                        p.Length = DxbcLength(payload, at, off + size);
                        break;

                    case BlobFormat.Glsl:
                        p.Text = ReadText(payload, at, off + size);
                        p.Length = p.Text.Length;
                        break;

                    case BlobFormat.Metal:
                        if (BlobFormatDetect.MetalSplit(payload, at, out var entry, out var src))
                        {
                            p.Entry = entry;
                            p.Text = ReadText(payload, src, off + size);
                            p.Length = p.Text.Length;
                        }
                        break;

                    case BlobFormat.SpirV:
                        p.Length = SpirVLength(payload, at, off + size);
                        break;

                    case BlobFormat.D3d9:
                        int te = BlobFormatDetect.D3d9TokenEnd(payload, at + 4, off + size);
                        p.Length = te > 0 ? te - at : off + size - at;
                        break;
                }
                res.Add(p);
            }
            return res;
        }

        /// <summary>Does this payload look like SMOL-V compressed SPIR-V?</summary>
        public static bool LooksSmolV(byte[] b)
        {
            if (b == null || b.Length < 64) return false;
            var needle = Encoding.ASCII.GetBytes("GLSL.std");
            return IndexOf(b, needle, 0) >= 0;
        }

        private static int IndexOf(byte[] hay, byte[] needle, int from)
        {
            for (int i = from; i + needle.Length <= hay.Length; i++)
            {
                int k = 0;
                while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return i;
            }
            return -1;
        }

        private static int DxbcLength(byte[] b, int at, int end)
        {
            if (at + 0x1C > end) return end - at;
            int total = BitConverter.ToInt32(b, at + 0x18);
            if (total <= 0x40 || (long)at + total > end) return end - at;
            return total;
        }

        /// <summary>SPIR-V has no total length; walk the instruction stream.</summary>
        private static int SpirVLength(byte[] b, int at, int end)
        {
            int p = at + 20;
            while (p + 4 <= end)
            {
                uint w = BitConverter.ToUInt32(b, p);
                int words = (int)(w >> 16);
                if (words <= 0 || p + words * 4 > end) break;
                p += words * 4;
            }
            return p - at;
        }

        /// <summary>Read source text up to the first NUL run that pads the slot.</summary>
        private static string ReadText(byte[] b, int at, int end)
        {
            int e = Math.Min(end, b.Length);
            // The slot holds the source followed by alignment padding and a trailing
            // marker byte, so the text does not run to `end`.  A NUL is never part of
            // GLSL or Metal source and it is what the padding starts with; the rest is
            // control characters (`\0\0\x19` on the GLES payloads of some builds).
            for (int i = at; i < e; i++)
                if (b[i] == 0) { e = i; break; }
            while (e > at && (b[e - 1] < 0x20 || b[e - 1] == 0xFF)) e--;
            return Encoding.UTF8.GetString(b, at, e - at);
        }
    }
}
