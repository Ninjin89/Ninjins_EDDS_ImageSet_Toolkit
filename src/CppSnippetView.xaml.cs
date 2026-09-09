using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ninjins_EDDS_ImageSet_Toolkit;

public partial class CppSnippetView : UserControl
{
	private static readonly Brush KeywordBrush = BrushOf("#569CD6");
	private static readonly Brush TypeBrush = BrushOf("#4EC9B0");
	private static readonly Brush StringBrush = BrushOf("#CE9178");
	private static readonly Brush CommentBrush = BrushOf("#6A9955");
	private static readonly Brush TextBrush = BrushOf("#D4D4D4");
	private string _source = "";

	public CppSnippetView()
	{
		InitializeComponent();
	}

	public void ShowSource(string source)
	{
		ShowSource(source, "config.cpp", "C++");
	}

	public void ShowSource(string source, string fileName, string language)
	{
		_source = source ?? "";
		if (FileLabel != null)
		{
			FileLabel.Text = fileName;
		}

		if (LangLabel != null)
		{
			LangLabel.Text = language;
		}

		CodeBox.Inlines.Clear();
		bool afterClass = false;
		int i = 0;
		while (i < _source.Length)
		{
			char c = _source[i];
			if (c == '/' && i + 1 < _source.Length && _source[i + 1] == '/')
			{
				int end = _source.IndexOf('\n', i);
				if (end < 0)
				{
					end = _source.Length;
				}

				AddRun(_source.Substring(i, end - i), CommentBrush);
				i = end;
				afterClass = false;
				continue;
			}

			if (c == '"')
			{
				int end = i + 1;
				while (end < _source.Length && _source[end] != '"' && _source[end] != '\n')
				{
					end++;
				}

				if (end < _source.Length && _source[end] == '"')
				{
					end++;
				}

				AddRun(_source.Substring(i, end - i), StringBrush);
				i = end;
				afterClass = false;
				continue;
			}

			if (char.IsLetter(c) || c == '_')
			{
				int end = i + 1;
				while (end < _source.Length)
				{
					char n = _source[end];
					if (!char.IsLetterOrDigit(n) && n != '_')
					{
						break;
					}

					end++;
				}

				string word = _source.Substring(i, end - i);
				Brush color = TextBrush;
				if (word == "class")
				{
					color = KeywordBrush;
					afterClass = true;
				}
				else if (afterClass)
				{
					color = TypeBrush;
					afterClass = false;
				}

				AddRun(word, color);
				i = end;
				continue;
			}

			afterClass = false;
			int mark = i + 1;
			while (mark < _source.Length)
			{
				char n = _source[mark];
				if (n == '/' || n == '"' || char.IsLetter(n) || n == '_')
				{
					break;
				}

				mark++;
			}

			AddRun(_source.Substring(i, mark - i), TextBrush);
			i = mark;
		}

		string[] lines = _source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		int last = lines.Length;
		if (last > 0 && lines[last - 1].Length == 0)
		{
			last--;
		}

		if (last < 1)
		{
			last = 1;
		}

		System.Text.StringBuilder numbers = new System.Text.StringBuilder();
		for (int line = 1; line <= last; line++)
		{
			if (line > 1)
			{
				numbers.Append('\n');
			}

			numbers.Append(line);
		}

		LineBox.Text = numbers.ToString();
	}

	private void AddRun(string text, Brush color)
	{
		CodeBox.Inlines.Add(new Run(text) { Foreground = color });
	}

	private void OnCopy(object sender, RoutedEventArgs e)
	{
		if (_source.Length == 0)
		{
			return;
		}

		Clipboard.SetText(_source);
	}

	private static SolidColorBrush BrushOf(string hex)
	{
		SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
		brush.Freeze();
		return brush;
	}
}
