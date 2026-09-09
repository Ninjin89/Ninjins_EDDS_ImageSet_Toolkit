namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal static class ConvertGuide
{
	public static string Steps()
	{
		return "// PNG / EDDS\r\n" +
			"//\r\n" +
			"// Drop .edds or .dds\r\n" +
			"// Convert writes a .png\r\n" +
			"//\r\n" +
			"// Drop .png, .tga, .bmp or .tiff\r\n" +
			"// Convert writes a .edds\r\n" +
			"// Preset, format, mips and LZ4 apply only then\r\n" +
			"//\r\n" +
			"// Direction column: to PNG or to EDDS\r\n" +
			"// Then click Convert";
	}
}
