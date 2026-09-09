using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Ninjins_EDDS_ImageSet_Toolkit;

public sealed class ImageSetCell : INotifyPropertyChanged
{
	private string _name = "";
	private string _sourcePath = "";
	private BitmapSource? _preview;
	private bool _selected;

	public ImageSetCell(int col, int row)
	{
		Col = col;
		Row = row;
	}

	public int Col { get; }
	public int Row { get; }
	public byte[]? PngBytes { get; set; }

	public string Name
	{
		get => _name;
		set
		{
			_name = value ?? "";
			OnChanged();
			OnChanged(nameof(Caption));
		}
	}

	public string SourcePath
	{
		get => _sourcePath;
		set
		{
			_sourcePath = value ?? "";
			OnChanged();
			OnChanged(nameof(Caption));
		}
	}

	public BitmapSource? Preview
	{
		get => _preview;
		set
		{
			_preview = value;
			OnChanged();
		}
	}

	public bool Selected
	{
		get => _selected;
		set
		{
			_selected = value;
			OnChanged();
		}
	}

	public bool HasPicture
	{
		get
		{
			if (PngBytes != null && PngBytes.Length > 0)
			{
				return true;
			}

			return !string.IsNullOrWhiteSpace(SourcePath);
		}
	}

	public string Caption
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Name))
			{
				return Name;
			}

			if (!string.IsNullOrWhiteSpace(SourcePath))
			{
				return Path.GetFileNameWithoutExtension(SourcePath);
			}

			return "empty";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnChanged([CallerMemberName] string? name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
