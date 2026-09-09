using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Ninjins_EDDS_ImageSet_Toolkit.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Ninjins_EDDS_ImageSet_Toolkit;

public partial class ImageSetPage : UserControl
{
	private readonly ObservableCollection<ImageSetCell> _cells = new ObservableCollection<ImageSetCell>();
	private ImageSetCell? _picked;
	private bool _applying;
	private bool _busy;
	private bool _ready;
	private bool _painting;
	private bool _sizing;
	private bool _folderLayout;
	private int _gridCols = 4;
	private int _gridRows = 4;
	private int _cellSize = 256;
	private int _atlasW = 1024;
	private int _atlasH = 1024;

	public ImageSetPage()
	{
		InitializeComponent();
	}

	public void OpenGrid()
	{
		if (_ready)
		{
			return;
		}

		_ready = true;
		if (QuadList != null)
		{
			QuadList.ItemsSource = _cells;
		}

		RebuildGrid();
		ShowCppSnippet();
	}

	public void AddDropped(IEnumerable<string> paths)
	{
		OpenGrid();
		List<string> dropped = new List<string>(paths);
		bool fromFolder = false;
		string folderName = "";
		foreach (string path in dropped)
		{
			if (Directory.Exists(path))
			{
				fromFolder = true;
				folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				break;
			}
		}

		List<string> files = RasterFiles(dropped);
		if (fromFolder || files.Count > 1)
		{
			if (!fromFolder && files.Count > 0)
			{
				string? parent = Path.GetDirectoryName(files[0]);
				if (!string.IsNullOrEmpty(parent))
				{
					folderName = Path.GetFileName(parent);
				}
			}

			_ = LoadFolderIcons(files, folderName);
			return;
		}

		if (files.Count == 1 && _picked != null)
		{
			_ = PutFileInCell(_picked, files[0]);
		}
	}

	private void OnGridChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_ready || _sizing)
		{
			return;
		}

		_folderLayout = false;
		RebuildGrid();
	}

	private void OnInsetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (InsetLabel != null && InsetSlider != null)
		{
			InsetLabel.Content = "Icon inset  " + (int)InsetSlider.Value + " px";
		}

		if (!_ready)
		{
			return;
		}

		PaintBoard();
	}

	private void OnWhiteOverlayChanged(object sender, RoutedEventArgs e)
	{
		if (!_ready)
		{
			return;
		}

		_ = RefreshPreviews();
	}

	private void RebuildGrid()
	{
		int cols;
		int rows;
		int cell;
		if (_folderLayout)
		{
			cols = _gridCols;
			rows = _gridRows;
			cell = _cellSize;
		}
		else
		{
			int canvas = ReadTag(CanvasBox, 1024);
			cell = ReadTag(CellBox, 256);
			if (cell > canvas || canvas % cell != 0)
			{
				StatusLabel.Text = "Pick a cell size that divides the canvas (1024 / 256 = 4x4).";
				return;
			}

			cols = canvas / cell;
			rows = cols;
			_gridCols = cols;
			_gridRows = rows;
			_cellSize = cell;
			_atlasW = canvas;
			_atlasH = canvas;
		}

		List<ImageSetCell> previous = new List<ImageSetCell>(_cells);
		_applying = true;
		_cells.Clear();
		for (int row = 0; row < rows; row++)
		{
			for (int col = 0; col < cols; col++)
			{
				ImageSetCell? keep = null;
				foreach (ImageSetCell old in previous)
				{
					if (old.Col == col && old.Row == row)
					{
						keep = old;
						break;
					}
				}

				if (keep == null)
				{
					keep = new ImageSetCell(col, row);
					keep.Name = "quad_" + (row * cols + col);
				}

				_cells.Add(keep);
			}
		}

		_applying = false;

		if (GridLabel != null)
		{
			int inset = InsetSlider == null ? 16 : (int)InsetSlider.Value;
			GridLabel.Text = cols + " x " + rows + " cells, " + cell + " px icons, atlas " + _atlasW + "x" + _atlasH + ", inset " + inset + " px";
		}

		PaintBoard();
		if (_cells.Count > 0)
		{
			Pick(_cells[0]);
		}
	}

	private void PaintBoard()
	{
		if (_painting || CellBoard == null)
		{
			return;
		}

		_painting = true;
		try
		{
			int cols = _gridCols;
			int rows = _gridRows;
			if (cols < 1)
			{
				cols = 1;
			}

			if (rows < 1)
			{
				rows = 1;
			}

			CellBoard.Columns = cols;
			CellBoard.Rows = rows;
			if (BoardFrame != null)
			{
				int longest = cols;
				if (rows > longest)
				{
					longest = rows;
				}

				BoardFrame.Width = 800.0 * cols / longest;
				BoardFrame.Height = 800.0 * rows / longest;
			}

			CellBoard.Children.Clear();
			foreach (ImageSetCell cell in _cells)
			{
				CellBoard.Children.Add(MakeCellUi(cell));
			}
		}
		finally
		{
			_painting = false;
		}
	}

	private Border MakeCellUi(ImageSetCell cell)
	{
		Brush? accent = Application.Current.TryFindResource("AccentBrush") as Brush;
		Brush line;
		if (cell.Selected && accent != null)
		{
			line = accent;
		}
		else
		{
			line = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 49, 64));
		}
		Border border = new Border
		{
			Tag = cell,
			BorderBrush = line,
			BorderThickness = new Thickness(cell.Selected ? 3 : 1),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 0, 0)),
			AllowDrop = true,
			Cursor = Cursors.Hand
		};
		border.MouseLeftButtonUp += OnCellClick;
		border.Drop += OnCellDrop;
		border.DragOver += OnPageDragOver;

		double pad = BoardInsetPad();
		Grid grid = new Grid();
		System.Windows.Controls.Image picture = new System.Windows.Controls.Image
		{
			Source = cell.Preview,
			Stretch = Stretch.Uniform,
			Margin = new Thickness(pad)
		};
		grid.Children.Add(picture);
		if (pad > 0)
		{
			byte alpha;
			if (cell.Selected)
			{
				alpha = 220;
			}
			else
			{
				alpha = 140;
			}

			Brush insetLine;
			if (accent != null)
			{
				insetLine = accent.Clone();
				insetLine.Opacity = alpha / 255.0;
			}
			else
			{
				insetLine = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 95, 126, 230));
			}

			Border insetGuide = new Border
			{
				BorderBrush = insetLine,
				BorderThickness = new Thickness(1),
				Margin = new Thickness(pad),
				IsHitTestVisible = false
			};
			grid.Children.Add(insetGuide);
		}

		TextBlock label = new TextBlock
		{
			Text = cell.Caption,
			Foreground = (Brush)FindResource("TextBrush"),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 11, 13, 17)),
			FontSize = 11,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Padding = new Thickness(2, 2, 2, 3),
			VerticalAlignment = VerticalAlignment.Bottom
		};
		grid.Children.Add(label);
		border.Child = grid;
		return border;
	}

	private void OnCellClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is Border border && border.Tag is ImageSetCell cell)
		{
			Pick(cell);
		}
	}

	private void OnCellDrop(object sender, DragEventArgs e)
	{
		if (sender is not Border border || border.Tag is not ImageSetCell cell)
		{
			return;
		}

		List<string> files = RasterFromDrop(e);
		if (files.Count == 0)
		{
			return;
		}

		e.Handled = true;
		Pick(cell);
		_ = PutFileInCell(cell, files[0]);
		if (files.Count > 1)
		{
			FillEmpty(files.GetRange(1, files.Count - 1));
		}
	}

	private void OnBoardDrop(object sender, DragEventArgs e)
	{
		List<string> files = RasterFromDrop(e);
		if (files.Count == 0)
		{
			return;
		}

		e.Handled = true;
		AddDropped(files);
	}

	private void OnPageDrop(object sender, DragEventArgs e)
	{
		if (e.Handled)
		{
			return;
		}

		List<string> files = RasterFromDrop(e);
		if (files.Count == 0)
		{
			return;
		}

		e.Handled = true;
		AddDropped(files);
	}

	private void OnPageDragOver(object sender, DragEventArgs e)
	{
		e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnQuadListChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_applying || _painting)
		{
			return;
		}

		if (QuadList.SelectedItem is ImageSetCell cell)
		{
			Pick(cell);
		}
	}

	private void OnCellNameChanged(object sender, TextChangedEventArgs e)
	{
		if (_applying || _picked == null)
		{
			return;
		}

		_picked.Name = CellNameBox.Text.Trim();
		PaintBoard();
		ShowCppSnippet(_picked.Name);
	}

	private void OnSnippetFieldsChanged(object sender, TextChangedEventArgs e)
	{
		ShowCppSnippet();
	}

	private void ShowCppSnippet(string? quadName = null)
	{
		if (CppView == null || SetNameBox == null || TexturePathBox == null)
		{
			return;
		}

		string quad = quadName ?? "";
		if (quad.Length == 0)
		{
			foreach (ImageSetCell cell in _cells)
			{
				if (cell.HasPicture && !string.IsNullOrWhiteSpace(cell.Name))
				{
					quad = cell.Name;
					break;
				}
			}
		}

		if (quad.Length == 0 && _picked != null && !string.IsNullOrWhiteSpace(_picked.Name))
		{
			quad = _picked.Name;
		}

		CppView.ShowSource(ImageSetCppSnippet.ModSnippet(TexturePathBox.Text, SetNameBox.Text, quad));
	}

	private void Pick(ImageSetCell cell)
	{
		if (_picked == cell && cell.Selected)
		{
			return;
		}

		_picked = cell;
		foreach (ImageSetCell item in _cells)
		{
			item.Selected = item == cell;
		}

		_applying = true;
		if (CellNameBox != null)
		{
			CellNameBox.Text = cell.Name;
		}

		if (QuadList != null)
		{
			QuadList.SelectedItem = cell;
		}

		_applying = false;
		PaintBoard();
	}

	private async void OnBrowseCell(object sender, RoutedEventArgs e)
	{
		if (_picked == null)
		{
			return;
		}

		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = "PNG for this cell",
			Filter = "Images|*.png;*.tga;*.bmp;*.tif;*.tiff;*.jpg;*.jpeg|All files|*.*"
		};
		if (dialog.ShowDialog(Window.GetWindow(this)) == true)
		{
			await PutFileInCell(_picked, dialog.FileName);
		}
	}

	private void OnClearCell(object sender, RoutedEventArgs e)
	{
		if (_picked == null)
		{
			return;
		}

		_picked.SourcePath = "";
		_picked.PngBytes = null;
		_picked.Preview = null;
		PaintBoard();
	}

	private void OnFillFolder(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog dialog = new OpenFolderDialog
		{
			Title = "Folder of PNG icons"
		};
		if (dialog.ShowDialog(Window.GetWindow(this)) == true)
		{
			string folderName = Path.GetFileName(dialog.FolderName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			_ = LoadFolderIcons(RasterFiles(new[] { dialog.FolderName }), folderName);
		}
	}

	private async void OnLoadSheet(object sender, RoutedEventArgs e)
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = "Photoshop sheet PNG",
			Filter = "PNG|*.png|Images|*.png;*.tga;*.bmp;*.tif;*.tiff|All files|*.*"
		};
		if (dialog.ShowDialog(Window.GetWindow(this)) != true)
		{
			return;
		}

		int canvas = ReadTag(CanvasBox, 1024);
		int cell = ReadTag(CellBox, 256);
		try
		{
			List<ImageSetSlot> slots = await Task.Run(() => ImageSetCompose.SliceSheet(dialog.FileName, canvas, cell));
			for (int i = 0; i < slots.Count && i < _cells.Count; i++)
			{
				ImageSetCell cellUi = _cells[i];
				ImageSetSlot slot = slots[i];
				cellUi.Name = slot.Name;
				cellUi.SourcePath = "";
				cellUi.PngBytes = slot.PngBytes;
			}

			await RefreshPreviews();
			StatusLabel.Text = "Loaded sheet into " + Math.Min(slots.Count, _cells.Count) + " cells. Rename the quads, then write.";
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
		}
	}

	private void OnClearGrid(object sender, RoutedEventArgs e)
	{
		int cols = Columns();
		foreach (ImageSetCell cell in _cells)
		{
			cell.SourcePath = "";
			cell.PngBytes = null;
			cell.Preview = null;
			cell.Name = "quad_" + (cell.Row * cols + cell.Col);
		}

		PaintBoard();
		StatusLabel.Text = "Grid cleared.";
	}

	private void OnBrowseOutput(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog dialog = new OpenFolderDialog
		{
			Title = "Output folder"
		};
		if (dialog.ShowDialog(Window.GetWindow(this)) == true)
		{
			OutputFolderBox.Text = dialog.FolderName;
		}
	}

	private async void OnWrite(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}

		string folder = OutputFolderBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(folder))
		{
			StatusLabel.Text = "Pick an output folder.";
			return;
		}

		List<ImageSetSlot> slots = new List<ImageSetSlot>();
		foreach (ImageSetCell cell in _cells)
		{
			if (!cell.HasPicture)
			{
				continue;
			}

			slots.Add(new ImageSetSlot
			{
				Name = cell.Name,
				SourcePath = cell.SourcePath,
				PngBytes = cell.PngBytes,
				Col = cell.Col,
				Row = cell.Row
			});
		}

		if (slots.Count == 0)
		{
			StatusLabel.Text = "Drop icons onto the grid first.";
			return;
		}

		WriteSettings settings = new WriteSettings
		{
			Pack = PixelPack.Bgra8,
			MaxMips = 1,
			Store = BlockStore.Lz4,
			Quality = 8
		};
		int cellSize = _cellSize;
		int cols = _gridCols;
		int rows = _gridRows;
		int inset = (int)InsetSlider.Value;
		bool white = WhiteOverlayBox.IsChecked == true;
		bool overwrite = OverwriteBox.IsChecked == true;
		string setName = SetNameBox.Text.Trim();
		string texturePath = TexturePathBox.Text.Trim();
		_busy = true;
		WriteButton.IsEnabled = false;
		StatusLabel.Text = "Writing...";
		try
		{
			ImageSetBuildResult result = await Task.Run(() => ImageSetBuild.WriteGrid(slots, folder, setName, texturePath, cols, rows, cellSize, inset, white, settings, overwrite));
			StatusLabel.Text = result.ImageCount + " quads on " + result.Width + "x" + result.Height + ". Copy both files into " + texturePath + ", then paste class imageSets into config.cpp.";
			ShowCppSnippet(slots[0].Name);
			if (OpenFolderBox.IsChecked == true)
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
			}
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
		}

		_busy = false;
		WriteButton.IsEnabled = true;
	}

	private async Task LoadFolderIcons(List<string> files, string folderName)
	{
		OpenGrid();
		if (files.Count == 0)
		{
			StatusLabel.Text = "No PNG/TGA/BMP/TIFF files in that folder.";
			return;
		}

		if (_busy)
		{
			return;
		}

		_busy = true;
		StatusLabel.Text = "Reading " + files.Count + " icons...";
		try
		{
			int typical = await Task.Run(() => ImageSetGridSize.TypicalSide(files));
			ImageSetLayout layout = ImageSetGridSize.FromIcons(files.Count, typical);
			_folderLayout = true;
			_gridCols = layout.Cols;
			_gridRows = layout.Rows;
			_cellSize = layout.Cell;
			_atlasW = layout.AtlasWidth;
			_atlasH = layout.AtlasHeight;
			_sizing = true;
			if (InsetSlider != null)
			{
				InsetSlider.Value = 0;
			}

			_sizing = false;
			RebuildGrid();
			ApplyFolderSetName(folderName);
			int cols = _gridCols;
			foreach (ImageSetCell gridCell in _cells)
			{
				gridCell.SourcePath = "";
				gridCell.PngBytes = null;
				gridCell.Preview = null;
				gridCell.Name = "quad_" + (gridCell.Row * cols + gridCell.Col);
			}

			int placed = 0;
			int limit = files.Count;
			if (limit > _cells.Count)
			{
				limit = _cells.Count;
			}

			for (int i = 0; i < limit; i++)
			{
				await PutFileInCell(_cells[i], files[i], false);
				placed++;
			}

			PaintBoard();
			string leftover = "";
			if (files.Count > _cells.Count)
			{
				leftover = " " + (files.Count - _cells.Count) + " did not fit.";
			}

			StatusLabel.Text = "Loaded " + placed + " icons as " + layout.Cols + "x" + layout.Rows + " at " + layout.Cell + " px (atlas " + layout.AtlasWidth + "x" + layout.AtlasHeight + ")." + leftover;
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
		}

		_busy = false;
	}

	private void ApplyFolderSetName(string folderName)
	{
		if (SetNameBox == null)
		{
			return;
		}

		string current = SetNameBox.Text.Trim();
		if (current.Length > 0 && !string.Equals(current, "items", StringComparison.OrdinalIgnoreCase) && !string.Equals(current, "ninjin_imageset", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		string name = ImageSetFile.ClassName(folderName);
		if (name.Length > 0 && name != "image")
		{
			SetNameBox.Text = name;
		}
	}

	private void SelectTag(ComboBox box, int value)
	{
		if (box == null)
		{
			return;
		}

		string tag = value.ToString();
		foreach (object item in box.Items)
		{
			if (item is ComboBoxItem combo && combo.Tag is string itemTag && itemTag == tag)
			{
				box.SelectedItem = combo;
				return;
			}
		}
	}

	private async void FillEmpty(List<string> files)
	{
		int fileIndex = 0;
		foreach (ImageSetCell cell in _cells)
		{
			if (fileIndex >= files.Count)
			{
				break;
			}

			if (cell.HasPicture)
			{
				continue;
			}

			string path = files[fileIndex];
			fileIndex++;
			await PutFileInCell(cell, path, false);
		}

		PaintBoard();
		if (fileIndex < files.Count)
		{
			StatusLabel.Text = "Grid is full. Increase canvas or use a smaller cell.";
		}
		else if (fileIndex > 0)
		{
			StatusLabel.Text = "Placed " + fileIndex + " icon(s). Rename quads if you want, then write.";
		}
	}

	private async Task PutFileInCell(ImageSetCell cell, string path, bool paint = true)
	{
		if (!PictureConvert.IsRaster(path))
		{
			return;
		}

		cell.SourcePath = Path.GetFullPath(path);
		cell.PngBytes = null;
		if (string.IsNullOrWhiteSpace(cell.Name) || cell.Name.StartsWith("quad_", StringComparison.Ordinal))
		{
			cell.Name = ImageSetFile.ClassName(Path.GetFileNameWithoutExtension(path));
		}

		cell.Preview = await PreviewBitmap(cell);
		if (_picked == cell)
		{
			_applying = true;
			CellNameBox.Text = cell.Name;
			_applying = false;
		}

		if (paint)
		{
			PaintBoard();
		}
	}

	private async Task RefreshPreviews()
	{
		foreach (ImageSetCell cell in _cells)
		{
			if (cell.HasPicture)
			{
				cell.Preview = await PreviewBitmap(cell);
			}
		}

		PaintBoard();
	}

	private async Task<BitmapSource?> PreviewBitmap(ImageSetCell cell)
	{
		ImageSetSlot slot = new ImageSetSlot
		{
			SourcePath = cell.SourcePath,
			PngBytes = cell.PngBytes
		};
		bool white = WhiteOverlayBox.IsChecked == true;
		try
		{
			byte[]? png = await Task.Run(() =>
			{
				using Image<Rgba32>? image = ImageSetCompose.LoadSlot(slot);
				if (image == null)
				{
					return null;
				}

				if (white)
				{
					ImageSetCompose.TintWhiteKeepAlpha(image);
				}

				using MemoryStream memory = new MemoryStream();
				image.Save(memory, new PngEncoder());
				return memory.ToArray();
			});

			if (png == null)
			{
				return null;
			}

			BitmapImage bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.StreamSource = new MemoryStream(png);
			bitmap.EndInit();
			bitmap.Freeze();
			return bitmap;
		}
		catch
		{
			return null;
		}
	}

	private double BoardInsetPad()
	{
		int cell = _cellSize;
		int inset = 0;
		if (InsetSlider != null)
		{
			inset = (int)InsetSlider.Value;
		}

		if (cell < 1 || inset <= 0 || _gridCols < 1)
		{
			return 0;
		}

		double boardSize = 800;
		if (BoardFrame != null && BoardFrame.Width > 0)
		{
			boardSize = BoardFrame.Width;
		}

		double boardCell = boardSize / _gridCols;
		double pad = inset * (boardCell / cell);
		if (pad * 2 >= boardCell - 4)
		{
			return 0;
		}

		return pad;
	}

	private int Columns()
	{
		if (_gridCols < 1)
		{
			return 1;
		}

		return _gridCols;
	}

	private static int ReadTag(ComboBox box, int fallback)
	{
		if (box?.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int value))
		{
			return value;
		}

		return fallback;
	}

	private static List<string> RasterFromDrop(DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return new List<string>();
		}

		string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
		return RasterFiles(dropped);
	}

	private static List<string> RasterFiles(IEnumerable<string> paths)
	{
		List<string> files = new List<string>();
		foreach (string path in paths)
		{
			if (Directory.Exists(path))
			{
				foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
				{
					if (PictureConvert.IsRaster(file))
					{
						files.Add(file);
					}
				}

				continue;
			}

			if (PictureConvert.IsRaster(path))
			{
				files.Add(path);
			}
		}

		files.Sort(StringComparer.OrdinalIgnoreCase);
		return files;
	}
}
