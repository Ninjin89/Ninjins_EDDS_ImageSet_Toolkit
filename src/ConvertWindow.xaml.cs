using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Ninjins_EDDS_ImageSet_Toolkit.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace Ninjins_EDDS_ImageSet_Toolkit;

public partial class ConvertWindow : Window
{
	private readonly FileRows _rows = new FileRows();
	private bool _applyingPreset;
	private bool _busy;
	private int _workPage;

	public ConvertWindow()
	{
		InitializeComponent();
		_applyingPreset = true;
		FileList.ItemsSource = _rows;
		_rows.CollectionChanged += OnRowsChanged;
		Log("Ready. EDDS becomes PNG, PNG becomes EDDS. Imageset builds a Workbench grid.");
		ConvertHelp.ShowSource(ConvertGuide.Steps(), "convert.txt", "guide");
		ShowWorkPage(0);
		Loaded += (_, _) => _applyingPreset = false;
	}

	public void AddPaths(IEnumerable<string> paths)
	{
		foreach (string path in paths)
		{
			AddPath(path);
		}

		RefreshOutputs();
	}

	private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		string queued;
		if (_rows.Count == 0)
		{
			queued = "Drop files to begin.";
		}
		else
		{
			queued = _rows.Count + " file(s) queued.";
		}

		ProgressLabel.Text = queued;
		if (_workPage == 0)
		{
			StatusHint.Text = queued;
		}
	}

	private void OnNavConvert(object sender, RoutedEventArgs e)
	{
		ShowWorkPage(0);
	}

	private void OnNavImageSet(object sender, RoutedEventArgs e)
	{
		ShowWorkPage(1);
	}

	private void ShowWorkPage(int index)
	{
		_workPage = index;
		bool imageset = index == 1;
		if (imageset)
		{
			ConvertPage.Visibility = Visibility.Collapsed;
			ImageSetView.Visibility = Visibility.Visible;
			NavConvert.Tag = null;
			NavImageSet.Tag = "Active";
		}
		else
		{
			ConvertPage.Visibility = Visibility.Visible;
			ImageSetView.Visibility = Visibility.Collapsed;
			NavConvert.Tag = "Active";
			NavImageSet.Tag = null;
		}
		if (imageset)
		{
			SubtitleBlock.Text = "Build a Workbench imageset. Drop PNG icons into cells, name the quads, then write .edds and .imageset.";
			PagePill.Text = "Imageset";
			StatusHint.Text = "Imageset grid";
			ImageSetView.OpenGrid();
		}
		else
		{
			SubtitleBlock.Text = "EDDS to PNG, PNG to EDDS. Drag files or folders in. No extra tools required.";
			PagePill.Text = "PNG / EDDS";
			if (_rows.Count == 0)
			{
				StatusHint.Text = "Idle";
			}
			else
			{
				StatusHint.Text = _rows.Count + " file(s) queued.";
			}
		}
	}

	private void OnDropZoneClick(object sender, MouseButtonEventArgs e)
	{
		OnAddFiles(sender, e);
	}

	private void OnAddFiles(object sender, RoutedEventArgs e)
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = "Add textures",
			Multiselect = true,
			Filter = "Textures|*.png;*.tga;*.bmp;*.tif;*.tiff;*.jpg;*.jpeg;*.dds;*.edds|All files|*.*"
		};

		if (dialog.ShowDialog(this) == true)
		{
			AddPaths(dialog.FileNames);
		}
	}

	private void OnAddFolder(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog dialog = new OpenFolderDialog
		{
			Title = "Add a folder of textures"
		};

		if (dialog.ShowDialog(this) == true)
		{
			AddPath(dialog.FolderName);
			RefreshOutputs();
		}
	}

	private void OnBrowseOutput(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog dialog = new OpenFolderDialog
		{
			Title = "Output folder"
		};

		if (dialog.ShowDialog(this) == true)
		{
			OutputFolderBox.Text = dialog.FolderName;
		}
	}

	private void OnOutputFolderChanged(object sender, TextChangedEventArgs e)
	{
		RefreshOutputs();
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnDrop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return;
		}

		string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (_workPage == 1)
		{
			ImageSetView.AddDropped(dropped);
			e.Handled = true;
			return;
		}

		AddPaths(dropped);
	}

	private void OnClear(object sender, RoutedEventArgs e)
	{
		_rows.Clear();
		PreviewImage.Source = null;
		PreviewHint.Text = "Select a file to preview";
		Log("List cleared.");
	}

	private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
	{
		if (PresetBox == null || FormatBox == null || MipBox == null)
		{
			return;
		}

		_applyingPreset = true;
		int index = PresetBox.SelectedIndex;
		if (index == 0)
		{
			FormatBox.SelectedIndex = 0;
			MipBox.SelectedIndex = 0;
		}
		else if (index == 1)
		{
			FormatBox.SelectedIndex = 1;
			MipBox.SelectedIndex = 1;
		}
		else if (index == 2)
		{
			FormatBox.SelectedIndex = 2;
			MipBox.SelectedIndex = 1;
		}

		_applyingPreset = false;
	}

	private void OnCustomChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_applyingPreset || PresetBox == null)
		{
			return;
		}

		PresetBox.SelectedIndex = 3;
	}

	private void OnQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (QualityLabel == null)
		{
			return;
		}

		QualityLabel.Content = "DXT quality  " + (int)QualitySlider.Value;
	}

	private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (FileList.SelectedItem is not FileRow row)
		{
			return;
		}

		await ShowPreview(row.SourcePath);
	}

	private async void OnConvert(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}

		if (_rows.Count == 0)
		{
			Log("Add files first.");
			return;
		}

		WriteSettings settings = CurrentSettings();
		bool overwrite = OverwriteBox.IsChecked == true;
		_busy = true;
		ConvertButton.IsEnabled = false;
		Progress.Value = 0;

		int done = 0;
		int failed = 0;
		string? lastOutputFolder = null;

		foreach (FileRow row in _rows.ToList())
		{
			row.Status = "Working";
			row.Detail = "";
			try
			{
				if (!overwrite && File.Exists(row.OutputPath))
				{
					row.Status = "Skipped";
					row.Detail = "Already exists";
				}
				else
				{
					string source = row.SourcePath;
					string dest = row.OutputPath;
					await Task.Run(() => PictureConvert.ConvertFile(source, dest, settings));
					row.Status = "Done";
					row.Detail = Path.GetFileName(dest);
					lastOutputFolder = Path.GetDirectoryName(dest);
					done++;
				}
			}
			catch (Exception ex)
			{
				row.Status = "Failed";
				row.Detail = ex.Message;
				failed++;
				Log("Failed " + row.SourceName + " - " + ex.Message);
			}

			Progress.Value = (done + failed) / (double)_rows.Count;
		}

		_busy = false;
		ConvertButton.IsEnabled = true;
		ProgressLabel.Text = done + " converted, " + failed + " failed.";
		Log(ProgressLabel.Text);
		OpenOutputIfAsked(lastOutputFolder);
	}

	private void OpenOutputIfAsked(string? lastOutputFolder)
	{
		if (OpenFolderBox.IsChecked == true && !string.IsNullOrEmpty(lastOutputFolder) && Directory.Exists(lastOutputFolder))
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = lastOutputFolder,
				UseShellExecute = true
			});
		}
	}

	private WriteSettings CurrentSettings()
	{
		PixelPack pack = PixelPack.Bgra8;
		if (FormatBox.SelectedItem is ComboBoxItem formatItem && formatItem.Tag is string formatTag)
		{
			pack = formatTag switch
			{
				"Dxt1" => PixelPack.Dxt1,
				"Dxt5" => PixelPack.Dxt5,
				_ => PixelPack.Bgra8
			};
		}

		int maxMips = 1;
		if (MipBox.SelectedItem is ComboBoxItem mipItem && mipItem.Tag is string mipTag)
		{
			maxMips = int.Parse(mipTag);
		}

		BlockStore store = BlockStore.Lz4;
		if (StoreBox.SelectedItem is ComboBoxItem storeItem && storeItem.Tag is string storeTag && storeTag == "Copy")
		{
			store = BlockStore.Copy;
		}

		return new WriteSettings
		{
			Pack = pack,
			MaxMips = maxMips,
			Store = store,
			Quality = (int)QualitySlider.Value
		};
	}

	private void AddPath(string path)
	{
		if (Directory.Exists(path))
		{
			foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
			{
				AddFile(file);
			}

			return;
		}

		AddFile(path);
	}

	private void AddFile(string path)
	{
		if (!PictureConvert.IsSupported(path))
		{
			return;
		}

		string full = Path.GetFullPath(path);
		foreach (FileRow existing in _rows)
		{
			if (string.Equals(existing.SourcePath, full, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}

		_rows.Add(new FileRow(full, OutputFolder()));
	}

	private void RefreshOutputs()
	{
		if (OutputFolderBox == null)
		{
			return;
		}

		string? folder = OutputFolder();
		foreach (FileRow row in _rows)
		{
			row.Retarget(folder);
		}
	}

	private string? OutputFolder()
	{
		string text = OutputFolderBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}

		return text;
	}

	private async Task ShowPreview(string path)
	{
		PreviewHint.Text = "Loading preview...";
		try
		{
			(byte[] pngBytes, string summary) = await Task.Run(() =>
			{
				using Image<Rgba32> image = PictureConvert.LoadRgba(path, out PictureInfo info);
				using MemoryStream memory = new MemoryStream();
				image.Save(memory, new PngEncoder());
				string text = info.Width + "x" + info.Height + "  " + info.PackLabel + "  " + info.MipCount + " mips";
				return (memory.ToArray(), text);
			});

			BitmapImage bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.StreamSource = new MemoryStream(pngBytes);
			bitmap.EndInit();
			bitmap.Freeze();

			PreviewImage.Source = bitmap;
			PreviewHint.Text = "";
			if (FileList.SelectedItem is FileRow row)
			{
				row.Detail = summary;
			}
		}
		catch (Exception ex)
		{
			PreviewImage.Source = null;
			PreviewHint.Text = "Preview failed";
			Log("Preview failed - " + ex.Message);
		}
	}

	private void Log(string message)
	{
		if (LogBox == null)
		{
			return;
		}

		string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine;
		LogBox.AppendText(line);
		LogBox.ScrollToEnd();
	}
}
