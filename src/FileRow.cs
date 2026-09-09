using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ninjins_EDDS_ImageSet_Toolkit.Core;

namespace Ninjins_EDDS_ImageSet_Toolkit;

public sealed class FileRow : INotifyPropertyChanged
{
	private string _status = "Ready";
	private string _detail = "";
	private string _direction;
	private string _outputPath = "";
	private string _outputName = "";

	public FileRow(string sourcePath, string? outputFolder)
	{
		SourcePath = sourcePath;
		SourceName = Path.GetFileName(sourcePath);
		_direction = PictureConvert.DirectionLabel(sourcePath);
		Retarget(outputFolder);
	}

	public string SourcePath { get; }
	public string SourceName { get; }

	public string Direction
	{
		get => _direction;
		private set
		{
			_direction = value;
			OnChanged();
		}
	}

	public string OutputPath
	{
		get => _outputPath;
		private set
		{
			_outputPath = value;
			OnChanged();
		}
	}

	public string OutputName
	{
		get => _outputName;
		private set
		{
			_outputName = value;
			OnChanged();
		}
	}

	public string Status
	{
		get => _status;
		set
		{
			_status = value;
			OnChanged();
		}
	}

	public string Detail
	{
		get => _detail;
		set
		{
			_detail = value;
			OnChanged();
		}
	}

	public void Retarget(string? outputFolder, bool packImageset = false, string setName = "icons")
	{
		if (packImageset && PictureConvert.IsRaster(SourcePath))
		{
			string folder = string.IsNullOrWhiteSpace(outputFolder) ? Path.GetDirectoryName(SourcePath) ?? "." : outputFolder;
			Direction = "imageset";
			OutputPath = Path.Combine(folder, setName + ".edds");
			OutputName = setName + ".edds + .imageset";
			return;
		}

		Direction = PictureConvert.DirectionLabel(SourcePath);
		OutputPath = PictureConvert.OutputPath(SourcePath, outputFolder);
		OutputName = Path.GetFileName(OutputPath);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnChanged([CallerMemberName] string? name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}

public sealed class FileRows : ObservableCollection<FileRow>
{
}
