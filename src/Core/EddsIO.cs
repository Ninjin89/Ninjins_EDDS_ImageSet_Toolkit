using System.IO;
using System.Text;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

/// <summary>
/// EDDS container: DDS header, then COPY/LZ4 block table (smallest mip first),
/// then matching block bodies. Layout taken from github.com/woozymasta/edds.
/// </summary>
internal static class EddsIO
{
	public static (byte[] LargestMip, PictureInfo Info) ReadLargestMip(string path)
	{
		using FileStream stream = File.OpenRead(path);
		using BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
		DdsHeader header = DdsHeader.Read(reader);
		PixelPack pack = header.DetectPack();
		if (pack == PixelPack.Unknown)
		{
			throw new InvalidDataException("Unsupported DDS/EDDS pixel format.");
		}

		int mipCount = header.MipLevels();
		if (mipCount < 1 || mipCount > 32)
		{
			throw new InvalidDataException($"Unsupported mip count {mipCount}.");
		}

		int width = (int)header.Width;
		int height = (int)header.Height;
		PictureInfo info = new PictureInfo
		{
			Width = width,
			Height = height,
			MipCount = mipCount,
			Pack = pack,
			PackLabel = PackLabel(pack)
		};

		if (!HasBlockTable(stream))
		{
			byte[] leftover = reader.ReadBytes((int)(stream.Length - stream.Position));
			int expected = MipBytes.ForPack(pack, width, height);
			byte[] raw = InflateLegacy(leftover, expected);
			return (raw, info);
		}

		List<(string Magic, int Size)> table = ReadTable(reader, mipCount);
		byte[] largest = Array.Empty<byte>();
		for (int i = 0; i < mipCount; i++)
		{
			int mipLevel = mipCount - i - 1;
			byte[] body = reader.ReadBytes(table[i].Size);
			if (mipLevel != 0)
			{
				continue;
			}

			int expected = MipBytes.ForPack(pack, width, height);
			largest = Lz4Chunks.Inflate(table[i].Magic, body, expected);
			if (largest.Length != expected)
			{
				throw new InvalidDataException($"Largest mip is {largest.Length} bytes, expected {expected}.");
			}
		}

		if (largest.Length == 0)
		{
			throw new InvalidDataException("EDDS file has no largest mip body.");
		}

		return (largest, info);
	}

	public static void Write(string path, IReadOnlyList<byte[]> mipsLargestFirst, int width, int height, PixelPack pack, BlockStore store)
	{
		if (mipsLargestFirst.Count == 0)
		{
			throw new InvalidDataException("No mip payloads to write.");
		}

		List<Lz4Chunks.StoredBlock> blocks = new List<Lz4Chunks.StoredBlock>(mipsLargestFirst.Count);
		for (int i = 0; i < mipsLargestFirst.Count; i++)
		{
			int mipW = MipBytes.Dimension(width, i);
			int mipH = MipBytes.Dimension(height, i);
			int expected = MipBytes.ForPack(pack, mipW, mipH);
			byte[] mip = mipsLargestFirst[i];
			if (mip.Length != expected)
			{
				throw new InvalidDataException($"Mip {i} is {mip.Length} bytes, expected {expected} for {mipW}x{mipH} {pack}.");
			}

			blocks.Add(Lz4Chunks.Store(mip, store));
		}

		DdsHeader header = DdsHeader.ForOutput(width, height, blocks.Count, pack);
		string fullPath = Path.GetFullPath(path);
		string? folder = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(folder))
		{
			Directory.CreateDirectory(folder);
		}

		string tempPath = fullPath + ".tmp";
		try
		{
			using (FileStream stream = File.Create(tempPath))
			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
			{
				header.Write(writer);

				for (int i = blocks.Count - 1; i >= 0; i--)
				{
					writer.Write(Encoding.ASCII.GetBytes(blocks[i].Magic));
					writer.Write(blocks[i].TableSize);
				}

				for (int i = blocks.Count - 1; i >= 0; i--)
				{
					writer.Write(blocks[i].Body);
				}
			}

			if (File.Exists(fullPath))
			{
				File.Delete(fullPath);
			}

			File.Move(tempPath, fullPath);
		}
		catch
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}

			throw;
		}
	}

	public static string PackLabel(PixelPack pack)
	{
		return pack switch
		{
			PixelPack.Bgra8 => "BGRA8",
			PixelPack.Rgba8 => "RGBA8",
			PixelPack.Dxt1 => "DXT1",
			PixelPack.Dxt3 => "DXT3",
			PixelPack.Dxt5 => "DXT5",
			PixelPack.Bc4 => "BC4",
			PixelPack.Bc5 => "BC5",
			PixelPack.Bc7 => "BC7",
			PixelPack.R8 => "R8",
			PixelPack.Rg8 => "RG8",
			PixelPack.A8 => "A8",
			_ => "Unknown"
		};
	}

	private static bool HasBlockTable(Stream stream)
	{
		long pos = stream.Position;
		Span<byte> magic = stackalloc byte[4];
		int read = stream.Read(magic);
		stream.Position = pos;
		if (read < 4)
		{
			return false;
		}

		string text = Encoding.ASCII.GetString(magic);
		return text == Lz4Chunks.CopyMagic || text == Lz4Chunks.Lz4Magic;
	}

	private static List<(string Magic, int Size)> ReadTable(BinaryReader reader, int mipCount)
	{
		List<(string Magic, int Size)> table = new List<(string Magic, int Size)>(mipCount);
		for (int i = 0; i < mipCount; i++)
		{
			string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
			int size = reader.ReadInt32();
			if (magic != Lz4Chunks.CopyMagic && magic != Lz4Chunks.Lz4Magic)
			{
				throw new InvalidDataException($"Unknown EDDS block magic '{magic}'.");
			}

			if (size < 0)
			{
				throw new InvalidDataException($"Negative EDDS block size {size}.");
			}

			table.Add((magic, size));
		}

		return table;
	}

	private static byte[] InflateLegacy(byte[] leftover, int expected)
	{
		try
		{
			return Lz4Chunks.Inflate(Lz4Chunks.Lz4Magic, leftover, expected);
		}
		catch
		{
			if (leftover.Length == expected)
			{
				return leftover;
			}

			throw;
		}
	}
}
