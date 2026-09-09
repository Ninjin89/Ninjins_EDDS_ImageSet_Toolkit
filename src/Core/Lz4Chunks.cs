using System.IO;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

/// <summary>
/// Enfusion LZ4 chunk-stream used inside EDDS LZ4 blocks.
/// Decode matches edds2png (LZ4ChainDecoder, 64 KiB window).
/// Encode matches github.com/woozymasta/edds (independent 64 KiB chunks,
/// 24-bit size + last-chunk flag, COPY fallback when LZ4 does not shrink).
/// </summary>
internal static class Lz4Chunks
{
	public const int ChunkSize = 64 * 1024;
	public const string CopyMagic = "COPY";
	public const string Lz4Magic = "LZ4 ";

	private const double MinRatio = 1.0 / 0.85;

	internal sealed class StoredBlock
	{
		public string Magic = CopyMagic;
		public int TableSize;
		public byte[] Body = Array.Empty<byte>();
	}

	public static StoredBlock Store(byte[] raw, BlockStore store)
	{
		if (store == BlockStore.Copy || raw.Length < 1024)
		{
			return CopyBlock(raw);
		}

		try
		{
			return Lz4Block(raw);
		}
		catch
		{
			return CopyBlock(raw);
		}
	}

	public static byte[] Inflate(string magic, byte[] body, int expectedSize)
	{
		if (magic == CopyMagic)
		{
			if (body.Length != expectedSize)
			{
				throw new InvalidDataException($"COPY block size {body.Length}, expected {expectedSize}.");
			}

			return body;
		}

		if (magic != Lz4Magic)
		{
			throw new InvalidDataException($"Unknown EDDS block magic '{magic}'.");
		}

		byte[] payload = body;
		int targetSize = expectedSize;
		if (body.Length >= 8)
		{
			int peek = BitConverter.ToInt32(body, 0);
			int firstChunk = body[4] | (body[5] << 8) | (body[6] << 16);
			if ((peek == expectedSize || peek > 0) && firstChunk > 0 && firstChunk < (1 << 20))
			{
				targetSize = peek;
				payload = new byte[body.Length - 4];
				Buffer.BlockCopy(body, 4, payload, 0, payload.Length);
			}
		}

		return InflateChunks(payload, targetSize);
	}

	private static StoredBlock CopyBlock(byte[] raw)
	{
		return new StoredBlock
		{
			Magic = CopyMagic,
			TableSize = raw.Length,
			Body = raw
		};
	}

	private static StoredBlock Lz4Block(byte[] raw)
	{
		MemoryStream chunks = new MemoryStream();
		byte[] compressBuf = new byte[LZ4Codec.MaximumOutputSize(ChunkSize)];

		for (int offset = 0; offset < raw.Length; offset += ChunkSize)
		{
			int remaining = Math.Min(ChunkSize, raw.Length - offset);
			ReadOnlySpan<byte> src = raw.AsSpan(offset, remaining);
			int packed = LZ4Codec.Encode(src, compressBuf.AsSpan(), LZ4Level.L00_FAST);
			bool last = offset + remaining >= raw.Length;

			if (packed <= 0 || (raw.Length / (double)Math.Max(packed, 1)) < MinRatio)
			{
				return CopyBlock(raw);
			}

			if (packed > 0x7FFFFF)
			{
				throw new InvalidDataException("LZ4 chunk is larger than the EDDS 24-bit size field.");
			}

			chunks.WriteByte((byte)packed);
			chunks.WriteByte((byte)(packed >> 8));
			chunks.WriteByte((byte)(packed >> 16));
			chunks.WriteByte(last ? (byte)0x80 : (byte)0x00);
			chunks.Write(compressBuf, 0, packed);
		}

		byte[] stream = chunks.ToArray();
		int total = 4 + stream.Length;
		if (raw.Length / (double)total < MinRatio)
		{
			return CopyBlock(raw);
		}

		byte[] body = new byte[total];
		BitConverter.TryWriteBytes(body.AsSpan(0, 4), raw.Length);
		Buffer.BlockCopy(stream, 0, body, 4, stream.Length);

		return new StoredBlock
		{
			Magic = Lz4Magic,
			TableSize = total,
			Body = body
		};
	}

	private static byte[] InflateChunks(byte[] payload, int targetSize)
	{
		byte[] output = new byte[targetSize];
		int outIndex = 0;
		int offset = 0;
		using LZ4ChainDecoder decoder = new LZ4ChainDecoder(ChunkSize, 0);

		while (true)
		{
			if (payload.Length - offset < 4)
			{
				throw new InvalidDataException("Truncated LZ4 chunk header.");
			}

			int chunkSize = payload[offset] | (payload[offset + 1] << 8) | (payload[offset + 2] << 16);
			byte flags = payload[offset + 3];
			offset += 4;
			if ((flags & ~0x80) != 0)
			{
				throw new InvalidDataException($"Unknown LZ4 chunk flags 0x{flags:X2}.");
			}

			if (chunkSize <= 0 || chunkSize > payload.Length - offset)
			{
				throw new InvalidDataException($"Invalid LZ4 chunk size {chunkSize}.");
			}

			int want = Math.Min(ChunkSize, targetSize - outIndex);
			if (want <= 0)
			{
				throw new InvalidDataException("LZ4 decode wrote past the expected mip size.");
			}

			if (!decoder.DecodeAndDrain(payload.AsSpan(offset, chunkSize), output.AsSpan(outIndex, want), out int decoded))
			{
				throw new InvalidDataException("LZ4 chunk decode failed.");
			}

			outIndex += decoded;
			offset += chunkSize;

			if ((flags & 0x80) != 0)
			{
				break;
			}
		}

		if (outIndex != targetSize)
		{
			throw new InvalidDataException($"LZ4 decoded {outIndex} bytes, expected {targetSize}.");
		}

		if (offset != payload.Length)
		{
			throw new InvalidDataException("LZ4 chunk stream has leftover bytes.");
		}

		return output;
	}
}
