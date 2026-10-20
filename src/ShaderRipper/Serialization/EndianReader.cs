using System;
using System.Text;

namespace ShaderRipper
{
    /// <summary>Little-endian cursor over a byte[].</summary>
    internal sealed class EndianReader
    {
        private readonly byte[] _b;
        private int _p;
        private readonly int _end;

        public EndianReader(byte[] b, int start, int end)
        {
            _b = b; _p = start; _end = end;
        }

        public int Position => _p;
        public int Remaining => _end - _p;

        /// <summary>SerializedFile version this reader is operating against.</summary>
        public int Version = 21;

        public string ReadCString()
        {
            int s = _p;
            while (_p < _end && _b[_p] != 0) _p++;
            string v = Encoding.UTF8.GetString(_b, s, _p - s);
            if (_p < _end) _p++;
            return v;
        }

        public byte ReadByte() => _b[_p++];
        public short ReadI16() { short v = BitConverter.ToInt16(_b, _p); _p += 2; return v; }
        public ushort ReadU16() { ushort v = BitConverter.ToUInt16(_b, _p); _p += 2; return v; }
        public int ReadI32() { int v = BitConverter.ToInt32(_b, _p); _p += 4; return v; }
        public uint ReadU32() { uint v = BitConverter.ToUInt32(_b, _p); _p += 4; return v; }
        public long ReadI64() { long v = BitConverter.ToInt64(_b, _p); _p += 8; return v; }
        public float ReadF32() { float v = BitConverter.ToSingle(_b, _p); _p += 4; return v; }

        public void Skip(int n) => _p += n;

        public void Align4()
        {
            int r = _p % 4;
            if (r != 0) _p += 4 - r;
        }

        /// <summary>Unity "string": int32 length + bytes, then align to 4.</summary>
        public string ReadAlignedString()
        {
            int len = ReadI32();
            if (len < 0 || _p + len > _end) throw new InvalidOperationException(
                $"bad string len {len} at {_p - 4}");
            string s = Encoding.UTF8.GetString(_b, _p, len);
            _p += len;
            Align4();
            return s;
        }

        public uint[] ReadU32Array()
        {
            int n = ReadI32();
            var a = new uint[n];
            for (int i = 0; i < n; i++) a[i] = ReadU32();
            return a;
        }

        public uint[][] ReadU32Array2D()
        {
            int outer = ReadI32();
            var a = new uint[outer][];
            for (int i = 0; i < outer; i++) a[i] = ReadU32Array();
            return a;
        }

        public byte[] ReadByteArray()
        {
            int n = ReadI32();
            if (n < 0 || _p + n > _end) throw new InvalidOperationException(
                $"bad byte array len {n} at {_p - 4}");
            var a = new byte[n];
            Array.Copy(_b, _p, a, 0, n);
            _p += n;
            Align4();
            return a;
        }

        public byte[] ReadRaw(int n)
        {
            var a = new byte[n];
            Array.Copy(_b, _p, a, 0, n);
            _p += n;
            return a;
        }
    }
}
