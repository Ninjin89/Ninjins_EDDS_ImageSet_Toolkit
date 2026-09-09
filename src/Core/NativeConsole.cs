using System.Runtime.InteropServices;

namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

internal static class NativeConsole
{
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AllocConsole();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AttachConsole(int processId);

	public static void Open()
	{
		if (!AttachConsole(-1))
		{
			AllocConsole();
		}
	}
}
