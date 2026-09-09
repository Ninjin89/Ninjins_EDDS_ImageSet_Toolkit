using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

/// <summary>
/// DDS header layout used by Enfusion EDDS. Flags match Workbench files
/// and github.com/woozymasta/edds makeDDSHeader.
/// </summary>
internal static class DdsLayout
{
	public const uint Magic = 0x20534444;
	public const uint HeaderBytes = 124;
	public const uint EnfusionMark = 0x31464E45; // ENF1
	public const uint FourCcDx10 = 0x30315844; // DX10

	public const uint FlagCaps = 0x1;
	public const uint FlagHeight = 0x2;
	public const uint FlagWidth = 0x4;
	public const uint FlagPitch = 0x8;
	public const uint FlagPixelFormat = 0x1000;
	public const uint FlagMipCount = 0x20000;
	public const uint FlagLinearSize = 0x80000;

	public const uint CapsComplex = 0x8;
	public const uint CapsTexture = 0x1000;
	public const uint CapsMipmap = 0x400000;

	public const uint PfAlphaPixels = 0x1;
	public const uint PfFourCc = 0x4;
	public const uint PfRgb = 0x40;
	public const uint PfLuminance = 0x20000;
	public const uint PfAlpha = 0x2;

	public static uint FourCc(char a, char b, char c, char d)
	{
		return (uint)a | ((uint)b << 8) | ((uint)c << 16) | ((uint)d << 24);
	}

	public static string FourCcText(uint value)
	{
		Span<byte> raw = stackalloc byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
		return Encoding.ASCII.GetString(raw);
	}
}

internal sealed class DdsHeader
{
	public uint Flags;
	public uint Height;
	public uint Width;
	public uint PitchOrLinearSize;
	public uint Depth;
	public uint MipMapCount;
	public uint[] Reserved1 = new uint[11];
	public uint PixelFlags;
	public uint FourCc;
	public uint RgbBitCount;
	public uint RMask;
	public uint GMask;
	public uint BMask;
	public uint AMask;
	public uint Caps;
	public uint Caps2;
	public uint Caps3;
	public uint Caps4;
	public uint DxgiFormat;
	public bool HasDx10;

	public static DdsHeader Read(BinaryReader reader)
	{
		uint magic = reader.ReadUInt32();
		if (magic != DdsLayout.Magic)
		{
			throw new InvalidDataException("Not a DDS/EDDS file (missing DDS magic).");
		}

		uint size = reader.ReadUInt32();
		if (size != DdsLayout.HeaderBytes)
		{
			throw new InvalidDataException($"Unexpected DDS header size {size}.");
		}

		DdsHeader header = new DdsHeader();
		header.Flags = reader.ReadUInt32();
		header.Height = reader.ReadUInt32();
		header.Width = reader.ReadUInt32();
		header.PitchOrLinearSize = reader.ReadUInt32();
		header.Depth = reader.ReadUInt32();
		header.MipMapCount = reader.ReadUInt32();
		for (int i = 0; i < 11; i++)
		{
			header.Reserved1[i] = reader.ReadUInt32();
		}

		uint pfSize = reader.ReadUInt32();
		if (pfSize != 32)
		{
			throw new InvalidDataException($"Unexpected pixel format size {pfSize}.");
		}

		header.PixelFlags = reader.ReadUInt32();
		header.FourCc = reader.ReadUInt32();
		header.RgbBitCount = reader.ReadUInt32();
		header.RMask = reader.ReadUInt32();
		header.GMask = reader.ReadUInt32();
		header.BMask = reader.ReadUInt32();
		header.AMask = reader.ReadUInt32();
		header.Caps = reader.ReadUInt32();
		header.Caps2 = reader.ReadUInt32();
		header.Caps3 = reader.ReadUInt32();
		header.Caps4 = reader.ReadUInt32();
		reader.ReadUInt32(); // dwReserved2

		if (header.FourCc == DdsLayout.FourCcDx10)
		{
			header.HasDx10 = true;
			header.DxgiFormat = reader.ReadUInt32();
			uint dimension = reader.ReadUInt32();
			uint misc = reader.ReadUInt32();
			uint arraySize = reader.ReadUInt32();
			reader.ReadUInt32();
			if (dimension != 3)
			{
				throw new InvalidDataException($"Unsupported DX10 resource dimension {dimension}.");
			}
			if (arraySize != 1)
			{
				throw new InvalidDataException($"Unsupported DX10 array size {arraySize}.");
			}
			if ((misc & 0x4) != 0)
			{
				throw new InvalidDataException("Cubemap textures are not supported.");
			}
		}

		if ((header.Caps2 & 0x200) != 0)
		{
			throw new InvalidDataException("Cubemap textures are not supported.");
		}

		return header;
	}

	public void Write(BinaryWriter writer)
	{
		writer.Write(DdsLayout.Magic);
		writer.Write(DdsLayout.HeaderBytes);
		writer.Write(Flags);
		writer.Write(Height);
		writer.Write(Width);
		writer.Write(PitchOrLinearSize);
		writer.Write(Depth);
		writer.Write(MipMapCount);
		for (int i = 0; i < 11; i++)
		{
			writer.Write(Reserved1[i]);
		}

		writer.Write(32u);
		writer.Write(PixelFlags);
		writer.Write(FourCc);
		writer.Write(RgbBitCount);
		writer.Write(RMask);
		writer.Write(GMask);
		writer.Write(BMask);
		writer.Write(AMask);
		writer.Write(Caps);
		writer.Write(Caps2);
		writer.Write(Caps3);
		writer.Write(Caps4);
		writer.Write(0u);
	}

	public static DdsHeader ForOutput(int width, int height, int mipCount, PixelPack pack)
	{
		DdsHeader header = new DdsHeader();
		header.Flags = DdsLayout.FlagCaps | DdsLayout.FlagHeight | DdsLayout.FlagWidth | DdsLayout.FlagPixelFormat;
		header.Height = (uint)height;
		header.Width = (uint)width;
		header.Depth = 0;
		header.MipMapCount = (uint)Math.Max(1, mipCount);
		header.Reserved1[1] = DdsLayout.EnfusionMark;
		header.Caps = DdsLayout.CapsTexture;

		if (header.MipMapCount > 1)
		{
			header.Flags |= DdsLayout.FlagMipCount;
			header.Caps |= DdsLayout.CapsComplex | DdsLayout.CapsMipmap;
		}

		switch (pack)
		{
			case PixelPack.Dxt1:
				header.Flags |= DdsLayout.FlagLinearSize;
				header.PitchOrLinearSize = (uint)MipBytes.ForPack(pack, width, height);
				header.PixelFlags = DdsLayout.PfFourCc;
				header.FourCc = DdsLayout.FourCc('D', 'X', 'T', '1');
				break;
			case PixelPack.Dxt3:
				header.Flags |= DdsLayout.FlagLinearSize;
				header.PitchOrLinearSize = (uint)MipBytes.ForPack(pack, width, height);
				header.PixelFlags = DdsLayout.PfFourCc;
				header.FourCc = DdsLayout.FourCc('D', 'X', 'T', '3');
				break;
			case PixelPack.Dxt5:
				header.Flags |= DdsLayout.FlagLinearSize;
				header.PitchOrLinearSize = (uint)MipBytes.ForPack(pack, width, height);
				header.PixelFlags = DdsLayout.PfFourCc;
				header.FourCc = DdsLayout.FourCc('D', 'X', 'T', '5');
				break;
			case PixelPack.Bc4:
				header.Flags |= DdsLayout.FlagLinearSize;
				header.PitchOrLinearSize = (uint)MipBytes.ForPack(pack, width, height);
				header.PixelFlags = DdsLayout.PfFourCc;
				header.FourCc = DdsLayout.FourCc('A', 'T', 'I', '1');
				break;
			case PixelPack.Bc5:
				header.Flags |= DdsLayout.FlagLinearSize;
				header.PitchOrLinearSize = (uint)MipBytes.ForPack(pack, width, height);
				header.PixelFlags = DdsLayout.PfFourCc;
				header.FourCc = DdsLayout.FourCc('A', 'T', 'I', '2');
				break;
			case PixelPack.Rgba8:
				header.Flags |= DdsLayout.FlagPitch;
				header.PitchOrLinearSize = (uint)(width * 4);
				header.PixelFlags = DdsLayout.PfRgb | DdsLayout.PfAlphaPixels;
				header.RgbBitCount = 32;
				header.RMask = 0x000000FFu;
				header.GMask = 0x0000FF00u;
				header.BMask = 0x00FF0000u;
				header.AMask = 0xFF000000u;
				break;
			case PixelPack.Bgra8:
				header.Flags |= DdsLayout.FlagPitch;
				header.PitchOrLinearSize = (uint)(width * 4);
				header.PixelFlags = DdsLayout.PfRgb | DdsLayout.PfAlphaPixels;
				header.RgbBitCount = 32;
				header.RMask = 0x00FF0000u;
				header.GMask = 0x0000FF00u;
				header.BMask = 0x000000FFu;
				header.AMask = 0xFF000000u;
				break;
			default:
				throw new InvalidDataException($"Cannot write pixel pack {pack}.");
		}

		return header;
	}

	public PixelPack DetectPack()
	{
		if (HasDx10)
		{
			return DxgiToPack(DxgiFormat);
		}

		if ((PixelFlags & DdsLayout.PfFourCc) != 0)
		{
			string code = DdsLayout.FourCcText(FourCc);
			return code switch
			{
				"DXT1" => PixelPack.Dxt1,
				"DXT2" or "DXT3" => PixelPack.Dxt3,
				"DXT4" or "DXT5" => PixelPack.Dxt5,
				"ATI1" or "BC4U" => PixelPack.Bc4,
				"ATI2" or "BC5U" => PixelPack.Bc5,
				_ => PixelPack.Unknown
			};
		}

		if ((PixelFlags & DdsLayout.PfRgb) != 0 && RgbBitCount == 32 && (PixelFlags & DdsLayout.PfAlphaPixels) != 0)
		{
			if (RMask == 0x000000FFu && GMask == 0x0000FF00u && BMask == 0x00FF0000u && AMask == 0xFF000000u)
			{
				return PixelPack.Rgba8;
			}
			if (RMask == 0x00FF0000u && GMask == 0x0000FF00u && BMask == 0x000000FFu && AMask == 0xFF000000u)
			{
				return PixelPack.Bgra8;
			}
		}

		if (RgbBitCount == 8 && RMask == 0x000000FFu && GMask == 0 && BMask == 0 && AMask == 0)
		{
			return PixelPack.R8;
		}

		if ((PixelFlags & DdsLayout.PfAlpha) != 0 && RgbBitCount == 8 && AMask == 0x000000FFu)
		{
			return PixelPack.A8;
		}

		if (RgbBitCount == 16 && RMask == 0x000000FFu && AMask == 0x0000FF00u)
		{
			return PixelPack.Rg8;
		}

		return PixelPack.Unknown;
	}

	private static PixelPack DxgiToPack(uint dxgi)
	{
		return dxgi switch
		{
			71 or 72 => PixelPack.Dxt1,
			74 or 75 => PixelPack.Dxt3,
			77 or 78 => PixelPack.Dxt5,
			80 or 81 => PixelPack.Bc4,
			83 or 84 => PixelPack.Bc5,
			87 or 91 => PixelPack.Bgra8,
			98 or 99 => PixelPack.Bc7,
			28 or 29 => PixelPack.Rgba8,
			49 => PixelPack.Rg8,
			61 => PixelPack.R8,
			65 => PixelPack.A8,
			_ => PixelPack.Unknown
		};
	}

	public int MipLevels()
	{
		if ((Caps & DdsLayout.CapsMipmap) != 0 && MipMapCount > 0)
		{
			return (int)MipMapCount;
		}

		return 1;
	}
}

internal static class MipBytes
{
	public static int Dimension(int baseSize, int level)
	{
		int value = baseSize >> level;
		if (value < 1)
		{
			return 1;
		}

		return value;
	}

	public static int FullChain(int width, int height)
	{
		int count = 1;
		int w = width;
		int h = height;
		while (w > 1 || h > 1)
		{
			count++;
			if (w > 1)
			{
				w /= 2;
			}
			if (h > 1)
			{
				h /= 2;
			}
		}

		return count;
	}

	public static int ForPack(PixelPack pack, int width, int height)
	{
		int blocksW = (width + 3) / 4;
		int blocksH = (height + 3) / 4;
		switch (pack)
		{
			case PixelPack.Dxt1:
			case PixelPack.Bc4:
				return blocksW * blocksH * 8;
			case PixelPack.Dxt3:
			case PixelPack.Dxt5:
			case PixelPack.Bc5:
			case PixelPack.Bc7:
				return blocksW * blocksH * 16;
			case PixelPack.Rgba8:
			case PixelPack.Bgra8:
				return width * height * 4;
			case PixelPack.R8:
			case PixelPack.A8:
				return width * height;
			case PixelPack.Rg8:
				return width * height * 2;
			default:
				throw new InvalidDataException($"Unknown pixel pack {pack}.");
		}
	}
}
