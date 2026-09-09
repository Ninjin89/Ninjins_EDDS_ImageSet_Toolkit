using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal static class SelfTest
{
	public static int Run(string[] args)
	{
		try
		{
			NativeConsole.Open();
		}
		catch
		{
		}

		string corpus = args.Length > 1 ? args[1] : @"D:\REFERENCES\edds\testdata\corpus";
		string outDir = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "Ninjins_EDDS_ImageSet_Toolkit_selftest");
		Directory.CreateDirectory(outDir);

		List<string> failures = new List<string>();
		void Check(string name, Action body)
		{
			try
			{
				body();
				Console.WriteLine("ok  " + name);
			}
			catch (Exception ex)
			{
				failures.Add(name + ": " + ex.Message);
				Console.WriteLine("FAIL  " + name + "  " + ex.Message);
			}
		}

		string bgraEdds = Path.Combine(corpus, "mip-grid-256.edds");
		string dxtEdds = Path.Combine(corpus, "mip-grid-256-DXTCompression.edds");
		string sourcePng = Path.Combine(corpus, "mip-grid-256.png");

		Check("read Workbench BGRA8 EDDS", () =>
		{
			using Image<Rgba32> image = PictureConvert.LoadRgba(bgraEdds, out PictureInfo info);
			if (info.Width != 256 || info.Height != 256)
			{
				throw new InvalidDataException($"size {info.Width}x{info.Height}");
			}

			if (info.Pack != PixelPack.Bgra8)
			{
				throw new InvalidDataException("pack " + info.Pack);
			}

			image.SaveAsPng(Path.Combine(outDir, "from-bgra.png"));
		});

		Check("read Workbench DXT5 EDDS", () =>
		{
			using Image<Rgba32> image = PictureConvert.LoadRgba(dxtEdds, out PictureInfo info);
			if (info.Pack != PixelPack.Dxt5)
			{
				throw new InvalidDataException("pack " + info.Pack);
			}

			image.SaveAsPng(Path.Combine(outDir, "from-dxt5.png"));
		});

		Check("read Workbench BC7 EDDS", () =>
		{
			string bc7 = Path.Combine(corpus, "mip-grid-256-ColorHQCompression.edds");
			using Image<Rgba32> image = PictureConvert.LoadRgba(bc7, out PictureInfo info);
			if (info.Pack != PixelPack.Bc7)
			{
				throw new InvalidDataException("pack " + info.Pack);
			}

			image.SaveAsPng(Path.Combine(outDir, "from-bc7.png"));
		});

		Check("PNG to BGRA8 EDDS and back", () =>
		{
			using Image<Rgba32> source = Image.Load<Rgba32>(sourcePng);
			string eddsPath = Path.Combine(outDir, "roundtrip-bgra.edds");
			PictureConvert.SaveEdds(source, eddsPath, new WriteSettings
			{
				Pack = PixelPack.Bgra8,
				MaxMips = 1,
				Store = BlockStore.Lz4,
				Quality = 8
			});

			using Image<Rgba32> round = PictureConvert.LoadRgba(eddsPath, out PictureInfo info);
			if (info.Pack != PixelPack.Bgra8)
			{
				throw new InvalidDataException("pack " + info.Pack);
			}

			AssertClose(source, round, 0);
			round.SaveAsPng(Path.Combine(outDir, "roundtrip-bgra.png"));
		});

		Check("PNG to DXT5 EDDS and back", () =>
		{
			using Image<Rgba32> source = Image.Load<Rgba32>(sourcePng);
			string eddsPath = Path.Combine(outDir, "roundtrip-dxt5.edds");
			PictureConvert.SaveEdds(source, eddsPath, new WriteSettings
			{
				Pack = PixelPack.Dxt5,
				MaxMips = 1,
				Store = BlockStore.Lz4,
				Quality = 8
			});

			using Image<Rgba32> round = PictureConvert.LoadRgba(eddsPath, out PictureInfo info);
			if (info.Pack != PixelPack.Dxt5)
			{
				throw new InvalidDataException("pack " + info.Pack);
			}

			AssertClose(source, round, 40);
			round.SaveAsPng(Path.Combine(outDir, "roundtrip-dxt5.png"));
		});

		Check("pack PNGs into imageset + EDDS", () =>
		{
			string packDir = Path.Combine(outDir, "imageset-pack");
			Directory.CreateDirectory(packDir);
			using (Image<Rgba32> red = new Image<Rgba32>(16, 16, new Rgba32(220, 40, 40, 255)))
			{
				red.SaveAsPng(Path.Combine(packDir, "health.png"));
			}

			using (Image<Rgba32> blue = new Image<Rgba32>(16, 16, new Rgba32(40, 80, 220, 255)))
			{
				blue.SaveAsPng(Path.Combine(packDir, "stamina.png"));
			}

			ImageSetBuildResult packed = ImageSetBuild.Pack(
				new[] { Path.Combine(packDir, "health.png"), Path.Combine(packDir, "stamina.png") },
				packDir,
				new ImageSetSettings
				{
					Name = "hud_icons",
					TexturePath = "MyMod/gui/imagesets",
					Gap = 2
				},
				new WriteSettings
				{
					Pack = PixelPack.Bgra8,
					MaxMips = 1,
					Store = BlockStore.Lz4,
					Quality = 8
				},
				true);

			string text = File.ReadAllText(packed.ImageSetPath);
			if (!text.Contains("ImageSetClass {") || !text.Contains("Name \"hud_icons\"") || !text.Contains("ImageSetDefClass health") || !text.Contains("ImageSetDefClass stamina") || !text.Contains("path \"MyMod/gui/imagesets/hud_icons.edds\"") || !text.Contains("mpix 0"))
			{
				throw new InvalidDataException("imageset text missing expected fields");
			}

			using Image<Rgba32> atlas = PictureConvert.LoadRgba(packed.EddsPath, out PictureInfo info);
			if (info.Pack != PixelPack.Bgra8 || packed.ImageCount != 2)
			{
				throw new InvalidDataException("atlas pack " + info.Pack + " count " + packed.ImageCount);
			}
		});

		Check("slice a grid PNG into imageset", () =>
		{
			string sliceDir = Path.Combine(outDir, "imageset-slice");
			Directory.CreateDirectory(sliceDir);
			string sheetPath = Path.Combine(sliceDir, "sheet.png");
			using (Image<Rgba32> sheet = new Image<Rgba32>(64, 32))
			{
				for (int y = 0; y < 32; y++)
				{
					for (int x = 0; x < 64; x++)
					{
						if (x < 32)
						{
							sheet[x, y] = new Rgba32(255, 0, 0, 255);
						}
						else
						{
							sheet[x, y] = new Rgba32(0, 255, 0, 255);
						}
					}
				}

				sheet.SaveAsPng(sheetPath);
			}

			ImageSetBuildResult sliced = ImageSetBuild.Pack(
				new[] { sheetPath },
				sliceDir,
				new ImageSetSettings
				{
					Name = "sheet_set",
					GridCell = 32
				},
				new WriteSettings
				{
					Pack = PixelPack.Bgra8,
					MaxMips = 1,
					Store = BlockStore.Lz4,
					Quality = 8
				},
				true);

			string text = File.ReadAllText(sliced.ImageSetPath);
			if (sliced.ImageCount != 2 || !text.Contains("Size 32 32") || !text.Contains("Pos 32 0"))
			{
				throw new InvalidDataException("slice expected 2 cells of 32, got " + sliced.ImageCount);
			}
		});

		Check("visual grid compose with white overlay", () =>
		{
			string gridDir = Path.Combine(outDir, "imageset-grid");
			Directory.CreateDirectory(gridDir);
			string redPath = Path.Combine(gridDir, "pills.png");
			using (Image<Rgba32> red = new Image<Rgba32>(40, 40, new Rgba32(200, 30, 30, 200)))
			{
				red.SaveAsPng(redPath);
			}

			ImageSetBuildResult written = ImageSetBuild.WriteGrid(
				new[]
				{
					new ImageSetSlot { Name = "pills", SourcePath = redPath, Col = 0, Row = 0 }
				},
				gridDir,
				"items",
				"Ninjins_Core/gui/imagesets",
				1,
				1,
				128,
				8,
				true,
				new WriteSettings
				{
					Pack = PixelPack.Bgra8,
					MaxMips = 1,
					Store = BlockStore.Lz4,
					Quality = 8
				},
				true);

			string text = File.ReadAllText(written.ImageSetPath);
			if (!text.Contains("Name \"items\"") || !text.Contains("Size 128 128") || !text.Contains("path \"Ninjins_Core/gui/imagesets/items.edds\""))
			{
				throw new InvalidDataException("grid imageset missing expected fields");
			}

			using Image<Rgba32> atlas = PictureConvert.LoadRgba(written.EddsPath, out _);
			Rgba32 center = atlas[64, 64];
			if (center.R < 250 || center.G < 250 || center.B < 250 || center.A < 190)
			{
				throw new InvalidDataException("white overlay expected near-white RGB with alpha, got " + center.R + "," + center.G + "," + center.B + "," + center.A);
			}
		});

		Check("auto grid size from icon count", () =>
		{
			ImageSetLayout twenty = ImageSetGridSize.FromIcons(20, 64);
			if (twenty.Cols != 5 || twenty.Rows != 4 || twenty.Cell != 64)
			{
				throw new InvalidDataException("20x64 expected 5x4 cells of 64, got " + twenty.Cols + "x" + twenty.Rows + " cell " + twenty.Cell);
			}

			ImageSetLayout four = ImageSetGridSize.FromIcons(4, 256);
			if (four.Cols != 2 || four.Rows != 2 || four.Cell != 256 || four.AtlasWidth != 512)
			{
				throw new InvalidDataException("4x256 expected 2x2 on 512, got " + four.Cols + "x" + four.Rows + " atlas " + four.AtlasWidth);
			}
		});

		Check("config.cpp imageset snippet", () =>
		{
			string cpp = ImageSetCppSnippet.ModSnippet("Ninjins_Core/gui/imagesets", "items", "backpack_outline");
			if (!cpp.Contains("class imageSets") || !cpp.Contains("files[]=") || !cpp.Contains("\"Ninjins_Core/gui/imagesets/items.imageset\"") || !cpp.Contains("icon=\"set:items image:backpack_outline\";") || !cpp.Contains("ghostIcon=\"set:items image:backpack_outline\";"))
			{
				throw new InvalidDataException("cpp snippet missing Ninjins_Core imageset paste block");
			}
		});

		Check("png edds convert guide", () =>
		{
			string guide = ConvertGuide.Steps();
			if (!guide.Contains("Drop .edds or .dds") || !guide.Contains("Convert writes a .png") || !guide.Contains("Drop .png, .tga, .bmp or .tiff") || !guide.Contains("Convert writes a .edds") || guide.Contains("—") || guide.Contains("–"))
			{
				throw new InvalidDataException("convert guide missing steps or has dash characters");
			}
		});

		string report = Path.Combine(outDir, "selftest-result.txt");
		File.WriteAllLines(report, failures.Count == 0
			? new[] { "PASS" }
			: failures);
		Console.WriteLine(failures.Count == 0 ? "PASS" : "FAILED " + failures.Count);
		return failures.Count == 0 ? 0 : 1;
	}

	private static void AssertClose(Image<Rgba32> a, Image<Rgba32> b, int maxAverageDelta)
	{
		if (a.Width != b.Width || a.Height != b.Height)
		{
			throw new InvalidDataException($"size {a.Width}x{a.Height} vs {b.Width}x{b.Height}");
		}

		long total = 0;
		for (int y = 0; y < a.Height; y++)
		{
			for (int x = 0; x < a.Width; x++)
			{
				Rgba32 pa = a[x, y];
				Rgba32 pb = b[x, y];
				total += Math.Abs(pa.R - pb.R);
				total += Math.Abs(pa.G - pb.G);
				total += Math.Abs(pa.B - pb.B);
				total += Math.Abs(pa.A - pb.A);
			}
		}

		int average = (int)(total / (double)(a.Width * a.Height * 4));
		if (average > maxAverageDelta)
		{
			throw new InvalidDataException($"average channel delta {average} > {maxAverageDelta}");
		}
	}
}
