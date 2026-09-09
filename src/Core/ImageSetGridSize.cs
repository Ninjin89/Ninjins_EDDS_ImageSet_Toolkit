namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal sealed class ImageSetLayout
{
	public int Cols;
	public int Rows;
	public int Cell;
	public int AtlasWidth;
	public int AtlasHeight;
}

internal static class ImageSetGridSize
{
	public const int MinAtlas = 256;
	public const int MaxAtlas = 4096;

	public static ImageSetLayout FromIcons(int iconCount, int cell)
	{
		int count = iconCount;
		if (count < 1)
		{
			count = 1;
		}

		int cellSize = cell;
		if (cellSize < 16)
		{
			cellSize = 16;
		}

		int cols = (int)Math.Ceiling(Math.Sqrt(count));
		if (cols < 1)
		{
			cols = 1;
		}

		int rows = (count + cols - 1) / cols;
		while (cols * cellSize > MaxAtlas && cols > 1)
		{
			cols--;
			rows = (count + cols - 1) / cols;
		}

		while (rows * cellSize > MaxAtlas && cellSize > 16)
		{
			cellSize /= 2;
			if (cellSize < 16)
			{
				cellSize = 16;
			}

			cols = (int)Math.Ceiling(Math.Sqrt(count));
			rows = (count + cols - 1) / cols;
		}

		int atlasW = AtlasPow2(cols * cellSize);
		int atlasH = AtlasPow2(rows * cellSize);
		return new ImageSetLayout
		{
			Cols = cols,
			Rows = rows,
			Cell = cellSize,
			AtlasWidth = atlasW,
			AtlasHeight = atlasH
		};
	}

	public static int TypicalSide(IReadOnlyList<string> paths)
	{
		List<int> sides = new List<int>();
		foreach (string path in paths)
		{
			try
			{
				SixLabors.ImageSharp.ImageInfo info = SixLabors.ImageSharp.Image.Identify(path);
				int side = info.Width;
				if (info.Height > side)
				{
					side = info.Height;
				}

				if (side > 0)
				{
					sides.Add(side);
				}
			}
			catch
			{
			}
		}

		if (sides.Count == 0)
		{
			return 64;
		}

		sides.Sort();
		return sides[sides.Count / 2];
	}

	public static int AtlasPow2(int used)
	{
		int size = 1;
		if (used < 1)
		{
			used = 1;
		}

		while (size < used)
		{
			size *= 2;
			if (size >= MaxAtlas)
			{
				return MaxAtlas;
			}
		}

		if (size < MinAtlas)
		{
			size = MinAtlas;
		}

		return size;
	}
}
