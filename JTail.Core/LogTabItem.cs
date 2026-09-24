using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace JTail;

/// <summary>
/// A single tab tailing one file: owns its <see cref="LogTailer"/>, text buffer, and scroll state,
/// plus the header (file name + close button) and content (log view) controls that represent it.
/// </summary>
public sealed class LogTabItem
{
    private readonly Border _headerBorder;

    public string FilePath { get; }

    public LogTailer Tailer { get; }

    public TextEditor Editor { get; }

    public Control Header => _headerBorder;

    public Control Content => Editor;

    public event EventHandler? CloseRequested;

    public event EventHandler? SelectRequested;

    /// <param name="background">Custom log background, or null to follow the app theme.</param>
    /// <param name="text">Custom log text color, or null to follow the app theme.</param>
    public LogTabItem(
        string path,
        LogTailer tailer,
        string initialText,
        Color? background,
        Color? text,
        double fontSize,
        HighlightRulesStore highlightRules)
    {
        FilePath = path;
        Tailer = tailer;

        Editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = false,
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas,Cascadia Mono,Menlo,monospace"),
            FontSize = fontSize,
            Padding = new Thickness(8),
            Document = new TextDocument(initialText),
            IsVisible = false
        };
        Editor.TextArea.TextView.LineTransformers.Add(new LineHighlightColorizer(highlightRules));
        ApplyTheme(background, text);

        // Header colors come from the "logTab" styles in JTailView.axaml, so they follow the app theme.
        var closeButton = new Button
        {
            Content = "×",
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "logTabClose" }
        };
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        headerPanel.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(path),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "logTabTitle" }
        });
        headerPanel.Children.Add(closeButton);

        _headerBorder = new Border
        {
            Child = headerPanel,
            Cursor = new Cursor(StandardCursorType.Hand),
            Classes = { "logTab" }
        };
        _headerBorder.PointerPressed += OnHeaderPointerPressed;
        RefreshFileSize();

        SetSelected(false);
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e) =>
        SelectRequested?.Invoke(this, EventArgs.Empty);

    public void SetSelected(bool isSelected)
    {
        _headerBorder.Classes.Set("selected", isSelected);
        Editor.IsVisible = isSelected;
    }

    private IDisposable? _backgroundBinding;
    private IDisposable? _foregroundBinding;
    private IDisposable? _textAreaForegroundBinding;

    /// <summary>Applies custom log colors; a null color follows the app theme (and updates when it switches).</summary>
    public void ApplyTheme(Color? background, Color? text)
    {
        SetBrush(Editor, TemplatedControl.BackgroundProperty, background, "JLogBackgroundBrush", ref _backgroundBinding);
        SetBrush(Editor, TemplatedControl.ForegroundProperty, text, "JLogTextBrush", ref _foregroundBinding);
        SetBrush(Editor.TextArea, TemplatedControl.ForegroundProperty, text, "JLogTextBrush", ref _textAreaForegroundBinding);
    }

    private static void SetBrush(StyledElement target, AvaloniaProperty property, Color? custom, string themeResourceKey,
        ref IDisposable? themeBinding)
    {
        themeBinding?.Dispose();
        themeBinding = null;

        if (custom is { } color)
        {
            target.SetValue(property, new SolidColorBrush(color));
        }
        else
        {
            themeBinding = target.Bind(property, target.GetResourceObservable(themeResourceKey));
        }
    }

    public void ApplyFontSize(double fontSize)
    {
        Editor.FontSize = fontSize;
    }

    public void RefreshHighlights() => Editor.TextArea.TextView.Redraw();

    public void AppendText(string text) => Editor.Document.Insert(Editor.Document.TextLength, text);

    public void RefreshFileSize() => ToolTip.SetTip(_headerBorder, $"{FilePath} ({FormatFileSize(Tailer.Length)})");

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.#} {units[unitIndex]}";
    }

    private const int AutoScrollVisibleLines = 100;

    public void TrimToLastLines()
    {
        var document = Editor.Document;
        if (document.LineCount <= AutoScrollVisibleLines)
        {
            return;
        }

        var firstKeptLine = document.GetLineByNumber(document.LineCount - AutoScrollVisibleLines + 1);
        document.Remove(0, firstKeptLine.Offset);
    }

    public void ScrollToEnd()
    {
        Editor.CaretOffset = Editor.Document.TextLength;
        Editor.TextArea.Caret.BringCaretToView();
    }
}
