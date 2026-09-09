using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal sealed class AtlasSprite
{
	public string Name = "";
	public string SourcePath = "";
	public int Width;
	public int Height;
	public int X;
	public int Y;
	public Image<Rgba32>? Picture;
}

internal sealed class AtlasResult
{
	public Image<Rgba32> Atlas = null!;
	public List<ImageSetEntry> Entries = new List<ImageSetEntry>();
}

internal static class AtlasPack
{
	public const int MinSize = 256;
	public const int MaxSize = 4096;

	public static AtlasResult Pack(List<AtlasSprite> sprites, int gap)
	{
		if (sprites.Count == 0)
		{
			throw new InvalidDataException("No images to pack.");
		}

		if (gap < 0)
		{
			gap = 0;
		}

		List<AtlasSprite> ordered = new List<AtlasSprite>(sprites);
		bool sameSize = true;
		int firstW = sprites[0].Width;
		int firstH = sprites[0].Height;
		foreach (AtlasSprite sprite in sprites)
		{
			if (sprite.Width != firstW || sprite.Height != firstH)
			{
				sameSize = false;
				break;
			}
		}

		int usedW;
		int usedH;
		if (sameSize)
		{
			int cols = (int)Math.Ceiling(Math.Sqrt(ordered.Count));
			if (cols < 1)
			{
				cols = 1;
			}

			for (int i = 0; i < ordered.Count; i++)
			{
				AtlasSprite sprite = ordered[i];
				if (sprite.Width > MaxSize || sprite.Height > MaxSize)
				{
					throw new InvalidDataException(sprite.Name + " is larger than " + MaxSize + "px.");
				}

				int col = i % cols;
				int row = i / cols;
				sprite.X = col * (firstW + gap);
				sprite.Y = row * (firstH + gap);
			}

			int rows = (ordered.Count + cols - 1) / cols;
			usedW = cols * firstW + (cols - 1) * gap;
			usedH = rows * firstH + (rows - 1) * gap;
		}
		else
		{
			ordered.Sort((a, b) => b.Height.CompareTo(a.Height));
			int x = 0;
			int y = 0;
			int rowHeight = 0;
			usedW = 0;
			usedH = 0;
			foreach (AtlasSprite sprite in ordered)
			{
				if (sprite.Width > MaxSize || sprite.Height > MaxSize)
				{
					throw new InvalidDataException(sprite.Name + " is larger than " + MaxSize + "px.");
				}

				if (x > 0 && x + sprite.Width > MaxSize)
				{
					x = 0;
					y += rowHeight + gap;
					rowHeight = 0;
				}

				sprite.X = x;
				sprite.Y = y;
				x += sprite.Width + gap;
				if (sprite.Height > rowHeight)
				{
					rowHeight = sprite.Height;
				}

				if (x - gap > usedW)
				{
					usedW = x - gap;
				}

				int bottom = y + sprite.Height;
				if (bottom > usedH)
				{
					usedH = bottom;
				}
			}
		}

		int canvasW = CanvasSize(usedW);
		int canvasH = CanvasSize(usedH);
		if (canvasW > MaxSize || canvasH > MaxSize)
		{
			throw new InvalidDataException("Icons do not fit on a " + MaxSize + " atlas. Use fewer or smaller PNGs.");
		}

		Image<Rgba32> atlas = new Image<Rgba32>(canvasW, canvasH);
		List<ImageSetEntry> entries = new List<ImageSetEntry>();
		HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (AtlasSprite sprite in ordered)
		{
			if (sprite.Picture == null)
			{
				continue;
			}

			atlas.Mutate(ctx => ctx.DrawImage(sprite.Picture, new Point(sprite.X, sprite.Y), 1f));
			string name = UniqueName(usedNames, sprite.Name);
			entries.Add(new ImageSetEntry
			{
				Name = name,
				X = sprite.X,
				Y = sprite.Y,
				Width = sprite.Width,
				Height = sprite.Height
			});
		}

		return new AtlasResult { Atlas = atlas, Entries = entries };
	}

	public static AtlasResult SliceGrid(Image<Rgba32> sheet, int cell, string namePrefix)
	{
		if (cell < 1)
		{
			throw new InvalidDataException("Grid cell size must be at least 1.");
		}

		if (sheet.Width % cell != 0 || sheet.Height % cell != 0)
		{
			throw new InvalidDataException("PNG size " + sheet.Width + "x" + sheet.Height + " is not a multiple of cell " + cell + ".");
		}

		int cols = sheet.Width / cell;
		int rows = sheet.Height / cell;
		List<ImageSetEntry> entries = new List<ImageSetEntry>();
		int index = 0;
		for (int row = 0; row < rows; row++)
		{
			for (int col = 0; col < cols; col++)
			{
				string name = ImageSetFile.ClassName(namePrefix + "_" + index);
				entries.Add(new ImageSetEntry
				{
					Name = name,
					X = col * cell,
					Y = row * cell,
					Width = cell,
					Height = cell
				});
				index++;
			}
		}

		int canvasW = NextPow2(sheet.Width);
		int canvasH = NextPow2(sheet.Height);
		if (canvasW > MaxSize || canvasH > MaxSize)
		{
			throw new InvalidDataException("Sheet is larger than " + MaxSize + "px.");
		}

		Image<Rgba32> atlas = sheet;
		if (canvasW != sheet.Width || canvasH != sheet.Height)
		{
			atlas = new Image<Rgba32>(canvasW, canvasH);
			atlas.Mutate(ctx => ctx.DrawImage(sheet, new Point(0, 0), 1f));
		}

		return new AtlasResult { Atlas = atlas, Entries = entries };
	}

	public static int CanvasSize(int used)
	{
		int size = NextPow2(used);
		if (size < MinSize)
		{
			size = MinSize;
		}

		return size;
	}

	public static int NextPow2(int value)
	{
		if (value < 1)
		{
			value = 1;
		}

		int size = 1;
		while (size < value)
		{
			size *= 2;
			if (size > MaxSize)
			{
				return MaxSize + 1;
			}
		}

		return size;
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
