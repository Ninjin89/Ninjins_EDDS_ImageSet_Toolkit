namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal static class ImageSetCppSnippet
{
	public static string ModSnippet(string textureFolder, string setName, string quadName)
	{
		string setRef = ImageSetFile.ClassName(setName);
		string folder = (textureFolder ?? "").Trim().Replace('\\', '/').Trim('/');
		if (folder.Length == 0)
		{
			folder = "Ninjins_Core/gui/imagesets";
		}

		string imagesetPath = folder + "/" + setRef + ".imageset";
		string quad = (quadName ?? "").Trim();
		if (quad.Length == 0)
		{
			quad = "quad_name";
		}

		return "// Paste into Ninjins_Core/config.cpp inside class defs\r\n" +
			"class imageSets\r\n" +
			"{\r\n" +
			"\tfiles[]=\r\n" +
			"\t{\r\n" +
			"\t\t\"" + imagesetPath + "\"\r\n" +
			"\t};\r\n" +
			"};\r\n" +
			"\r\n" +
			"// HUD / slot (CfgVehicles or layout)\r\n" +
			"icon=\"set:" + setRef + " image:" + quad + "\";\r\n" +
			"ghostIcon=\"set:" + setRef + " image:" + quad + "\";";
	}
}
