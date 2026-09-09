using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

public static class PictureConvert
{
	public static readonly string[] InputExtensions =
	{
		".png", ".tga", ".bmp", ".tiff", ".tif", ".jpg", ".jpeg", ".dds", ".edds"
	};

	public static bool IsSupported(string path)
	{
		string ext = Path.GetExtension(path);
		foreach (string allowed in InputExtensions)
		{
			if (string.Equals(ext, allowed, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	public static bool GoesToPng(string path)
	{
		string ext = Path.GetExtension(path);
		return ext.Equals(".edds", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dds", StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsRaster(string path)
	{
		if (GoesToPng(path))
		{
			return false;
		}

		return IsSupported(path);
	}

	public static string OutputPath(string sourcePath, string? outputFolder)
	{
		string directory = string.IsNullOrWhiteSpace(outputFolder) ? Path.GetDirectoryName(sourcePath) ?? "." : outputFolder;
		string name = Path.GetFileNameWithoutExtension(sourcePath);
		string ext = GoesToPng(sourcePath) ? ".png" : ".edds";
		return Path.Combine(directory, name + ext);
	}

	public static string DirectionLabel(string sourcePath)
	{
		return GoesToPng(sourcePath) ? "to PNG" : "to EDDS";
	}

	public static void ConvertFile(string sourcePath, string destPath, WriteSettings settings)
	{
		if (GoesToPng(sourcePath))
		{
			using Image<Rgba32> image = LoadRgba(sourcePath, out _);
			string? folder = Path.GetDirectoryName(destPath);
			if (!string.IsNullOrEmpty(folder))
			{
				Directory.CreateDirectory(folder);
			}

			image.SaveAsPng(destPath);
			return;
		}

		using Image<Rgba32> source = LoadRgba(sourcePath, out _);
		SaveEdds(source, destPath, settings);
	}

	public static Image<Rgba32> LoadRgba(string path, out PictureInfo info)
	{
		string ext = Path.GetExtension(path).ToLowerInvariant();
		if (ext == ".edds")
		{
			(byte[] mip, PictureInfo eddsInfo) = EddsIO.ReadLargestMip(path);
			info = eddsInfo;
			return DecodeMip(mip, info.Width, info.Height, info.Pack);
		}

		if (ext == ".dds")
		{
			using FileStream stream = File.OpenRead(path);
			BcDecoder decoder = new BcDecoder();
			Image<Rgba32> ddsImage = decoder.DecodeToImageRgba32(stream);
			info = new PictureInfo
			{
				Width = ddsImage.Width,
				Height = ddsImage.Height,
				MipCount = 1,
				Pack = PixelPack.Unknown,
				PackLabel = "DDS"
			};
			return ddsImage;
		}

		Image<Rgba32> image = Image.Load<Rgba32>(path);
		info = new PictureInfo
		{
			Width = image.Width,
			Height = image.Height,
			MipCount = 1,
			Pack = PixelPack.Rgba8,
			PackLabel = ext.Trim('.').ToUpperInvariant()
		};
		return image;
	}

	public static void SaveEdds(Image<Rgba32> image, string path, WriteSettings settings)
	{
		int mipCount = 1;
		int full = MipBytes.FullChain(image.Width, image.Height);
		if (settings.MaxMips <= 0)
		{
			mipCount = full;
		}
		else
		{
			mipCount = Math.Min(settings.MaxMips, full);
		}

		if (mipCount < 1)
		{
			mipCount = 1;
		}

		List<byte[]> mips = EncodeMips(image, settings.Pack, mipCount, settings.Quality);
		EddsIO.Write(path, mips, image.Width, image.Height, settings.Pack, settings.Store);
	}

	private static List<byte[]> EncodeMips(Image<Rgba32> image, PixelPack pack, int mipCount, int quality)
	{
		List<byte[]> mips = new List<byte[]>(mipCount);
		for (int level = 0; level < mipCount; level++)
		{
			int w = MipBytes.Dimension(image.Width, level);
			int h = MipBytes.Dimension(image.Height, level);
			using Image<Rgba32> mipImage = image.Clone(ctx => ctx.Resize(w, h, KnownResamplers.Box));
			mips.Add(EncodeOneMip(mipImage, pack, quality));
		}

		return mips;
	}

	private static byte[] EncodeOneMip(Image<Rgba32> image, PixelPack pack, int quality)
	{
		if (pack == PixelPack.Bgra8)
		{
			byte[] bgra = new byte[image.Width * image.Height * 4];
			image.ProcessPixelRows(accessor =>
			{
				int offset = 0;
				for (int y = 0; y < accessor.Height; y++)
				{
					Span<Rgba32> row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						bgra[offset++] = row[x].B;
						bgra[offset++] = row[x].G;
						bgra[offset++] = row[x].R;
						bgra[offset++] = row[x].A;
					}
				}
			});
			return bgra;
		}

		if (pack == PixelPack.Rgba8)
		{
			byte[] rgba = new byte[image.Width * image.Height * 4];
			image.CopyPixelDataTo(rgba);
			return rgba;
		}

		BcEncoder encoder = new BcEncoder();
		encoder.OutputOptions.GenerateMipMaps = false;
		encoder.OutputOptions.Quality = MapQuality(quality);
		encoder.OutputOptions.Format = pack switch
		{
			PixelPack.Dxt1 => CompressionFormat.Bc1,
			PixelPack.Dxt3 => CompressionFormat.Bc2,
			PixelPack.Dxt5 => CompressionFormat.Bc3,
			PixelPack.Bc4 => CompressionFormat.Bc4,
			PixelPack.Bc5 => CompressionFormat.Bc5,
			PixelPack.Bc7 => CompressionFormat.Bc7,
			_ => throw new InvalidDataException($"Cannot encode pixel pack {pack}.")
		};

		return encoder.EncodeToRawBytes(image)[0];
	}

	private static Image<Rgba32> DecodeMip(byte[] mip, int width, int height, PixelPack pack)
	{
		if (pack == PixelPack.Bgra8)
		{
			Image<Rgba32> bgra = new Image<Rgba32>(width, height);
			bgra.ProcessPixelRows(accessor =>
			{
				int offset = 0;
				for (int y = 0; y < accessor.Height; y++)
				{
					Span<Rgba32> row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						byte b = mip[offset++];
						byte g = mip[offset++];
						byte r = mip[offset++];
						byte a = mip[offset++];
						row[x] = new Rgba32(r, g, b, a);
					}
				}
			});
			return bgra;
		}

		if (pack == PixelPack.Rgba8)
		{
			return Image.LoadPixelData<Rgba32>(mip, width, height);
		}

		if (pack == PixelPack.R8)
		{
			Image<Rgba32> gray = new Image<Rgba32>(width, height);
			gray.ProcessPixelRows(accessor =>
			{
				int offset = 0;
				for (int y = 0; y < accessor.Height; y++)
				{
					Span<Rgba32> row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						byte v = mip[offset++];
						row[x] = new Rgba32(v, v, v, 255);
					}
				}
			});
			return gray;
		}

		if (pack == PixelPack.A8)
		{
			Image<Rgba32> alpha = new Image<Rgba32>(width, height);
			alpha.ProcessPixelRows(accessor =>
			{
				int offset = 0;
				for (int y = 0; y < accessor.Height; y++)
				{
					Span<Rgba32> row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						byte a = mip[offset++];
						row[x] = new Rgba32(255, 255, 255, a);
					}
				}
			});
			return alpha;
		}

		if (pack == PixelPack.Rg8)
		{
			Image<Rgba32> rg = new Image<Rgba32>(width, height);
			rg.ProcessPixelRows(accessor =>
			{
				int offset = 0;
				for (int y = 0; y < accessor.Height; y++)
				{
					Span<Rgba32> row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						byte r = mip[offset++];
						byte g = mip[offset++];
						row[x] = new Rgba32(r, g, 0, 255);
					}
				}
			});
			return rg;
		}

		CompressionFormat format = pack switch
		{
			PixelPack.Dxt1 => CompressionFormat.Bc1,
			PixelPack.Dxt3 => CompressionFormat.Bc2,
			PixelPack.Dxt5 => CompressionFormat.Bc3,
			PixelPack.Bc4 => CompressionFormat.Bc4,
			PixelPack.Bc5 => CompressionFormat.Bc5,
			PixelPack.Bc7 => CompressionFormat.Bc7,
			_ => throw new InvalidDataException($"Cannot decode pixel pack {pack}.")
		};

		BcDecoder decoder = new BcDecoder();
		return decoder.DecodeRawToImageRgba32(mip, width, height, format);
	}

	private static CompressionQuality MapQuality(int quality)
	{
		if (quality <= 0)
		{
			return CompressionQuality.Balanced;
		}

		if (quality <= 3)
		{
			return CompressionQuality.Fast;
		}

		if (quality >= 8)
		{
			return CompressionQuality.BestQuality;
		}

		return CompressionQuality.Balanced;
	}
}
