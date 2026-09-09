using System.Windows;
using Ninjins_EDDS_ImageSet_Toolkit.Core;

namespace Ninjins_EDDS_ImageSet_Toolkit;

public partial class App : Application
{
	private void OnStart(object sender, StartupEventArgs e)
	{
		if (e.Args.Length > 0 && string.Equals(e.Args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
		{
			int code = SelfTest.Run(e.Args);
			Shutdown(code);
			return;
		}

		ConvertWindow window = new ConvertWindow();
		window.AddPaths(e.Args);
		window.Show();
	}
}
