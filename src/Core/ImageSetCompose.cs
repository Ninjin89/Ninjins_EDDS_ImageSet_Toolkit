using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal sealed class ImageSetSlot
{
	public string Name = "";
	public string SourcePath = "";
	public int Col;
	public int Row;
	public byte[]? PngBytes;
}

internal sealed class ImageSetComposeResult
{
	public Image<Rgba32> Atlas = null!;
	public List<ImageSetEntry> Entries = new List<ImageSetEntry>();
}

internal static class ImageSetCompose
{
	public static void TintWhiteKeepAlpha(Image<Rgba32> image)
	{
		image.ProcessPixelRows(accessor =>
		{
			for (int y = 0; y < accessor.Height; y++)
			{
				Span<Rgba32> row = accessor.GetRowSpan(y);
				for (int x = 0; x < row.Length; x++)
				{
					row[x] = new Rgba32(255, 255, 255, row[x].A);
				}
			}
		});
	}

	public static ImageSetComposeResult FromGrid(IReadOnlyList<ImageSetSlot> slots, int cols, int rows, int cell, int inset, bool whiteOverlay)
	{
		if (cols < 1 || rows < 1 || cell < 1)
		{
			throw new InvalidDataException("Grid cols, rows, and cell size must be at least 1.");
		}

		if (inset < 0)
		{
			inset = 0;
		}

		if (inset * 2 >= cell)
		{
			inset = 0;
		}

		int atlasW = ImageSetGridSize.AtlasPow2(cols * cell);
		int atlasH = ImageSetGridSize.AtlasPow2(rows * cell);
		Image<Rgba32> atlas = new Image<Rgba32>(atlasW, atlasH);
		List<ImageSetEntry> entries = new List<ImageSetEntry>();
		HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ImageSetSlot slot in slots)
		{
			if (slot.Col < 0 || slot.Row < 0 || slot.Col >= cols || slot.Row >= rows)
			{
				continue;
			}

			using Image<Rgba32>? source = LoadSlot(slot);
			if (source == null)
			{
				continue;
			}

			if (whiteOverlay)
			{
				TintWhiteKeepAlpha(source);
			}

			int destW = cell - inset * 2;
			int destH = cell - inset * 2;
			int drawW = source.Width;
			int drawH = source.Height;
			if (drawW > destW || drawH > destH)
			{
				float scale = Math.Min(destW / (float)drawW, destH / (float)drawH);
				drawW = Math.Max(1, (int)Math.Round(drawW * scale));
				drawH = Math.Max(1, (int)Math.Round(drawH * scale));
			}

			int originX = slot.Col * cell + inset + (destW - drawW) / 2;
			int originY = slot.Row * cell + inset + (destH - drawH) / 2;
			using Image<Rgba32> placed = source.Clone(ctx => ctx.Resize(drawW, drawH, KnownResamplers.Box));
			atlas.Mutate(ctx => ctx.DrawImage(placed, new Point(originX, originY), 1f));

			string name = UniqueName(usedNames, slot.Name);
			entries.Add(new ImageSetEntry
			{
				Name = name,
				X = slot.Col * cell,
				Y = slot.Row * cell,
				Width = cell,
				Height = cell
			});
		}

		if (entries.Count == 0)
		{
			atlas.Dispose();
			throw new InvalidDataException("Drop icons onto the grid first.");
		}

		return new ImageSetComposeResult { Atlas = atlas, Entries = entries };
	}

	public static List<ImageSetSlot> SliceSheet(string path, int canvas, int cell)
	{
		using Image<Rgba32> sheet = PictureConvert.LoadRgba(path, out _);
		if (sheet.Width != canvas || sheet.Height != canvas)
		{
			throw new InvalidDataException("Sheet is " + sheet.Width + "x" + sheet.Height + ", grid canvas is " + canvas + "x" + canvas + ".");
		}

		if (canvas % cell != 0)
		{
			throw new InvalidDataException("Canvas " + canvas + " must divide evenly by cell " + cell + ".");
		}

		int cols = canvas / cell;
		string prefix = ImageSetFile.ClassName(Path.GetFileNameWithoutExtension(path));
		List<ImageSetSlot> slots = new List<ImageSetSlot>();
		int index = 0;
		for (int row = 0; row < cols; row++)
		{
			for (int col = 0; col < cols; col++)
			{
				using Image<Rgba32> crop = sheet.Clone(ctx => ctx.Crop(new Rectangle(col * cell, row * cell, cell, cell)));
				using MemoryStream memory = new MemoryStream();
				crop.SaveAsPng(memory);
				slots.Add(new ImageSetSlot
				{
					Name = prefix + "_" + index,
					Col = col,
					Row = row,
					PngBytes = memory.ToArray()
				});
				index++;
			}
		}

		return slots;
	}

	public static Image<Rgba32>? LoadSlot(ImageSetSlot slot)
	{
		if (slot.PngBytes != null && slot.PngBytes.Length > 0)
		{
			return Image.Load<Rgba32>(slot.PngBytes);
		}

		if (!string.IsNullOrWhiteSpace(slot.SourcePath) && File.Exists(slot.SourcePath))
		{
			return PictureConvert.LoadRgba(slot.SourcePath, out _);
		}

		return null;
	}

	private static string UniqueName(HashSet<string> used, string raw)
	{
		string name = ImageSetFile.ClassName(raw);
		string candidate = name;
		int n = 2;
		while (!used.Add(candidate))
		{
			candidate = name + "_" + n;
			n++;
		}

		return candidate;
	}
}
