using System;
using System.Buffers.Binary;
using System.Numerics;

namespace ShaderRipper.Data;

internal static class DxbcChecksum
{
	// MD5's round constants and rotation schedule (RFC 1321).
	private static ReadOnlySpan<uint> RoundConstants =>
	[
		0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee, 0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
		0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be, 0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
		0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa, 0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
		0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed, 0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
		0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c, 0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
		0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05, 0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
		0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039, 0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
		0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1, 0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
	];

	private static ReadOnlySpan<byte> Rotations => [7, 12, 17, 22, 5, 9, 14, 20, 4, 11, 16, 23, 6, 10, 15, 21];

	public static void Update(Span<byte> container)
	{
		ReadOnlySpan<byte> data = container[20..]; // The FourCC and checksum are outside the hash.
		uint length = (uint)data.Length;
		Span<uint> state = stackalloc uint[] { 0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476 };
		while (data.Length >= 64)
		{
			Compress(state, data[..64]);
			data = data[64..];
		}

		// DXBC uses MD5's compression function with a different final block layout.
		Span<byte> tail = stackalloc byte[64];
		tail.Clear();
		if (data.Length >= 56)
		{
			data.CopyTo(tail);
			tail[data.Length] = 0x80;
			Compress(state, tail);
			tail.Clear();
		}
		else
		{
			data.CopyTo(tail[4..]);
			tail[4 + data.Length] = 0x80;
		}
		BinaryPrimitives.WriteUInt32LittleEndian(tail, length << 3);
		BinaryPrimitives.WriteUInt32LittleEndian(tail[60..], (length << 1) | 1);
		Compress(state, tail);
		for (int i = 0; i < state.Length; i++)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(container[(4 + i * 4)..], state[i]);
		}
	}

	private static void Compress(Span<uint> state, ReadOnlySpan<byte> block)
	{
		uint a = state[0], b = state[1], c = state[2], d = state[3];
		ReadOnlySpan<uint> constants = RoundConstants;
		ReadOnlySpan<byte> rotations = Rotations;
		unchecked
		{
			for (int i = 0; i < 64; i++)
			{
				(uint mix, int index) = (i / 16) switch
				{
					0 => ((b & c) | (~b & d), i),
					1 => ((d & b) | (~d & c), (5 * i + 1) % 16),
					2 => (b ^ c ^ d, (3 * i + 5) % 16),
					_ => (c ^ (b | ~d), (7 * i) % 16),
				};
				uint word = BinaryPrimitives.ReadUInt32LittleEndian(block[(index * 4)..]);
				uint next = b + BitOperations.RotateLeft(a + mix + constants[i] + word, rotations[(i / 16) * 4 + i % 4]);
				(a, b, c, d) = (d, next, b, c);
			}
			state[0] += a;
			state[1] += b;
			state[2] += c;
			state[3] += d;
		}
	}
}
