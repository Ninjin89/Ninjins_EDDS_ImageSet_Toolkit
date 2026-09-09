using System.Text;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal sealed class ImageSetEntry
{
	public string Name = "";
	public int X;
	public int Y;
	public int Width;
	public int Height;
}

internal static class ImageSetFile
{
	public static void Write(string path, string setName, int width, int height, string texturePath, int mpix, IReadOnlyList<ImageSetEntry> entries)
	{
		string? folder = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(folder))
		{
			Directory.CreateDirectory(folder);
		}

		StringBuilder text = new StringBuilder();
		text.AppendLine("ImageSetClass {");
		text.AppendLine(" Name \"" + Escape(setName) + "\"");
		text.AppendLine(" RefSize " + width + " " + height);
		text.AppendLine(" Textures {");
		text.AppendLine("  ImageSetTextureClass {");
		text.AppendLine("   mpix " + mpix);
		text.AppendLine("   path \"" + Escape(texturePath) + "\"");
		text.AppendLine("  }");
		text.AppendLine(" }");
		text.AppendLine(" Images {");
		foreach (ImageSetEntry entry in entries)
		{
			text.AppendLine("  ImageSetDefClass " + entry.Name + " {");
			text.AppendLine("   Name \"" + Escape(entry.Name) + "\"");
			text.AppendLine("   Pos " + entry.X + " " + entry.Y);
			text.AppendLine("   Size " + entry.Width + " " + entry.Height);
			text.AppendLine("   Flags 0");
			text.AppendLine("  }");
		}

		text.AppendLine(" }");
		text.AppendLine(" Groups {");
		text.AppendLine(" }");
		text.AppendLine("}");
		File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
	}

	public static string ClassName(string raw)
	{
		string name = Path.GetFileNameWithoutExtension(raw);
		StringBuilder clean = new StringBuilder();
		foreach (char c in name)
		{
			if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
			{
				clean.Append(c);
			}
			else if (c == ' ' || c == '-' || c == '.')
			{
				clean.Append('_');
			}
		}

		if (clean.Length == 0)
		{
			return "image";
		}

		return clean.ToString();
	}

	public static string TextureRef(string prefix, string setName)
	{
		string p = (prefix ?? "").Trim().Replace('\\', '/').Trim('/');
		if (p.Length == 0)
		{
			return setName + ".edds";
		}

		return p + "/" + setName + ".edds";
	}

	private static string Escape(string value)
	{
		return value.Replace("\\", "/").Replace("\"", "");
	}
}
