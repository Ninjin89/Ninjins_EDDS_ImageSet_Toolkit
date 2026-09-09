using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

public sealed class ImageSetSettings
{
	public string Name { get; set; } = "icons";
	public string TexturePath { get; set; } = "";
	public int Gap { get; set; } = 2;
	public int GridCell { get; set; }
}

public sealed class ImageSetBuildResult
{
	public string EddsPath { get; init; } = "";
	public string ImageSetPath { get; init; } = "";
	public int Width { get; init; }
	public int Height { get; init; }
	public int ImageCount { get; init; }
}

public static class ImageSetBuild
{
	public static string DefaultSetName(IReadOnlyList<string> sourcePaths)
	{
		foreach (string path in sourcePaths)
		{
			if (!PictureConvert.IsRaster(path))
			{
				continue;
			}

			string? folder = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(folder))
			{
				string fromFolder = ImageSetFile.ClassName(Path.GetFileName(folder));
				if (fromFolder.Length > 0 && fromFolder != "image")
				{
					return fromFolder;
				}
			}

			return ImageSetFile.ClassName(Path.GetFileNameWithoutExtension(path));
		}

		return "icons";
	}

	public static ImageSetBuildResult Pack(IReadOnlyList<string> sourcePaths, string outputFolder, ImageSetSettings setSettings, WriteSettings writeSettings, bool overwrite)
	{
		List<string> rasters = new List<string>();
		foreach (string path in sourcePaths)
		{
			if (PictureConvert.IsRaster(path))
			{
				rasters.Add(path);
			}
		}

		if (rasters.Count == 0)
		{
			throw new InvalidDataException("Add PNG/TGA/BMP/TIFF files to pack an imageset.");
		}

		string requestedName = (setSettings.Name ?? "").Trim();
		string setName;
		if (requestedName.Length == 0)
		{
			setName = DefaultSetName(rasters);
		}
		else
		{
			setName = ImageSetFile.ClassName(requestedName);
		}

		string folder = string.IsNullOrWhiteSpace(outputFolder) ? Path.GetDirectoryName(rasters[0]) ?? "." : outputFolder;
		Directory.CreateDirectory(folder);
		string eddsPath = Path.Combine(folder, setName + ".edds");
		string imagesetPath = Path.Combine(folder, setName + ".imageset");
		if (!overwrite && (File.Exists(eddsPath) || File.Exists(imagesetPath)))
		{
			throw new InvalidDataException("Output already exists. Enable overwrite or pick another folder.");
		}

		List<Image<Rgba32>> loaded = new List<Image<Rgba32>>();
		Image<Rgba32>? extraAtlas = null;
		try
		{
			AtlasResult atlasResult;
			if (rasters.Count == 1 && setSettings.GridCell > 0)
			{
				Image<Rgba32> sheet = PictureConvert.LoadRgba(rasters[0], out _);
				loaded.Add(sheet);
				string prefix = ImageSetFile.ClassName(Path.GetFileNameWithoutExtension(rasters[0]));
				atlasResult = AtlasPack.SliceGrid(sheet, setSettings.GridCell, prefix);
				if (!ReferenceEquals(atlasResult.Atlas, sheet))
				{
					extraAtlas = atlasResult.Atlas;
				}
			}
			else
			{
				List<AtlasSprite> sprites = new List<AtlasSprite>();
				foreach (string path in rasters)
				{
					Image<Rgba32> picture = PictureConvert.LoadRgba(path, out _);
					loaded.Add(picture);
					sprites.Add(new AtlasSprite
					{
						Name = Path.GetFileNameWithoutExtension(path),
						SourcePath = path,
						Width = picture.Width,
						Height = picture.Height,
						Picture = picture
					});
				}

				atlasResult = AtlasPack.Pack(sprites, setSettings.Gap);
				extraAtlas = atlasResult.Atlas;
			}

			int width = atlasResult.Atlas.Width;
			int height = atlasResult.Atlas.Height;
			int count = atlasResult.Entries.Count;
			int mpix = writeSettings.MaxMips == 1 ? 0 : 1;
			string textureRef = ImageSetFile.TextureRef(setSettings.TexturePath, setName);
			PictureConvert.SaveEdds(atlasResult.Atlas, eddsPath, writeSettings);
			ImageSetFile.Write(imagesetPath, setName, width, height, textureRef, mpix, atlasResult.Entries);

			return new ImageSetBuildResult
			{
				EddsPath = eddsPath,
				ImageSetPath = imagesetPath,
				Width = width,
				Height = height,
				ImageCount = count
			};
		}
		finally
		{
			extraAtlas?.Dispose();
			foreach (Image<Rgba32> picture in loaded)
			{
				picture.Dispose();
			}
		}
	}

	internal static ImageSetBuildResult WriteGrid(IReadOnlyList<ImageSetSlot> slots, string outputFolder, string setName, string texturePath, int cols, int rows, int cell, int inset, bool whiteOverlay, WriteSettings writeSettings, bool overwrite)
	{
		string name = ImageSetFile.ClassName(setName);
		if (name.Length == 0 || name == "image")
		{
			name = "icons";
		}

		Directory.CreateDirectory(outputFolder);
		string eddsPath = Path.Combine(outputFolder, name + ".edds");
		string imagesetPath = Path.Combine(outputFolder, name + ".imageset");
		if (!overwrite && (File.Exists(eddsPath) || File.Exists(imagesetPath)))
		{
			throw new InvalidDataException("Output already exists. Enable overwrite or pick another folder.");
		}

		ImageSetComposeResult composed = ImageSetCompose.FromGrid(slots, cols, rows, cell, inset, whiteOverlay);
		try
		{
			int mpix = writeSettings.MaxMips == 1 ? 0 : 1;
			string textureRef = ImageSetFile.TextureRef(texturePath, name);
			PictureConvert.SaveEdds(composed.Atlas, eddsPath, writeSettings);
			ImageSetFile.Write(imagesetPath, name, composed.Atlas.Width, composed.Atlas.Height, textureRef, mpix, composed.Entries);
			return new ImageSetBuildResult
			{
				EddsPath = eddsPath,
				ImageSetPath = imagesetPath,
				Width = composed.Atlas.Width,
				Height = composed.Atlas.Height,
				ImageCount = composed.Entries.Count
			};
		}
		finally
		{
			composed.Atlas.Dispose();
		}
	}
}
