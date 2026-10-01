using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace JTail;

/// <summary>
/// A single tab tailing one file: owns its <see cref="LogTailer"/>, text buffer, scroll state, and find and
/// filter, plus the header (file name + close button) and content (log view) controls that represent it.
/// </summary>
public sealed class LogTabItem
{
    private readonly Border _headerBorder;
    private readonly FindHighlightRenderer _findRenderer;
    private LineSearch? _find;
    private LineFilter? _filter;

    /// <summary>The whole file while a filter hides part of it (the editor then shows only the matching lines).</summary>
    private StringBuilder? _allText;

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
            FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
            FontSize = fontSize,
            Padding = new Thickness(12, 8),
            Document = new TextDocument(initialText),
            IsVisible = false
        };
        // The log is never edited, so there's nothing to undo; otherwise every appended chunk would be kept twice.
        Editor.Document.UndoStack.SizeLimit = 0;
        Editor.TextArea.TextView.LineTransformers.Add(new LineHighlightColorizer(highlightRules));
        _findRenderer = new FindHighlightRenderer(Editor);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_findRenderer);
        ApplyTheme(background, text);

        // Header colors come from the "logTab" styles in JTailView.axaml, so they follow the app theme.
        var closeButton = new Button
        {
            Content = "", // Cancel glyph; the logTabClose style sets the icon font
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "logTabClose" }
        };
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
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

    public void AppendText(string text)
    {
        var document = Editor.Document;
        // Matches on the last line can change once the rest of it arrives, so search again from its start.
        var lastLineStart = document.Lines[^1].Offset;

        if (_filter is not null)
        {
            _allText!.Append(text);
            text = _filter.Append(text);
        }
        if (text.Length == 0)
        {
            return;
        }

        document.Insert(document.TextLength, text);
        if (_find is not null)
        {
            var hits = _findRenderer.Hits;
            while (hits.Count > 0 && hits[^1].Offset >= lastLineStart)
            {
                hits.RemoveAt(hits.Count - 1);
            }
            hits.AddRange(_find.FindAll(document.GetText(lastLineStart, document.TextLength - lastLineStart), lastLineStart));
            if (_findRenderer.Current >= hits.Count)
            {
                _findRenderer.Current = -1;
            }
        }
    }

    /// <summary>The find box's text and options for this tab.</summary>
    public SearchQuery FindQuery { get; private set; } = new();

    /// <summary>Why <see cref="FindQuery"/> isn't a valid regex, or null.</summary>
    public string? FindError { get; private set; }

    public int FindHitCount => _findRenderer.Hits.Count;

    /// <summary>Index of the match last navigated to, or -1.</summary>
    public int CurrentFindHit => _findRenderer.Current;

    /// <summary>The filter box's text and options for this tab.</summary>
    public SearchQuery FilterQuery { get; private set; } = new();

    /// <summary>Why <see cref="FilterQuery"/> isn't a valid regex, or null.</summary>
    public string? FilterError { get; private set; }

    /// <summary>How many lines the filter hides, or null when no filter is applied.</summary>
    public int? HiddenLineCount => _filter?.HiddenLines;

    /// <summary>Highlights every match of <paramref name="query"/> in the lines shown.</summary>
    public void SetFind(SearchQuery query)
    {
        FindQuery = query;
        _find = LineSearch.Create(query, out var error);
        FindError = error;
        Refind();
    }

    private void Refind()
    {
        _findRenderer.Hits = _find?.FindAll(Editor.Document.Text, 0) ?? [];
        _findRenderer.Current = -1;
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    /// <summary>Shows only the lines matching <paramref name="query"/>, or every line if it's empty or invalid.</summary>
    public void SetFilter(SearchQuery query)
    {
        FilterQuery = query;
        var search = LineSearch.Create(query, out var error);
        FilterError = error;
        if (search is null && _allText is null)
        {
            return;
        }

        _allText ??= new StringBuilder(Editor.Document.Text);
        _filter = search is null ? null : new LineFilter(search);
        var allText = _allText.ToString();
        Editor.Document.Text = _filter?.Append(allText) ?? allText;
        if (_filter is null)
        {
            _allText = null;
        }
        Refind();
    }

    /// <summary>
    /// Selects the next (or previous) find match, wrapping around, and scrolls to it. Starts from the caret
    /// unless the current match is still selected.
    /// </summary>
    /// <returns>false if there are no matches.</returns>
    public bool GoToFindHit(bool forward)
    {
        var hits = _findRenderer.Hits;
        if (hits.Count == 0)
        {
            return false;
        }

        int current = _findRenderer.Current;
        bool currentSelected = current >= 0 && current < hits.Count
            && Editor.SelectionStart == hits[current].Offset && Editor.SelectionLength == hits[current].Length;
        int next;
        if (currentSelected)
        {
            next = (current + (forward ? 1 : -1) + hits.Count) % hits.Count;
        }
        else
        {
            // The first match that ends past the caret; the one before it ends at or before the caret.
            int afterCaret = _findRenderer.FirstHitEndingAtOrAfter(Editor.CaretOffset + 1);
            next = forward ? afterCaret % hits.Count : (afterCaret - 1 + hits.Count) % hits.Count;
        }

        _findRenderer.Current = next;
        var hit = hits[next];
        Editor.Select(hit.Offset, hit.Length);
        var location = Editor.Document.GetLocation(hit.Offset);
        Editor.ScrollTo(location.Line, location.Column);
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        return true;
    }

    /// <summary>The file's current size, e.g. "12.4 KB".</summary>
    public string FileSizeText => FormatFileSize(Tailer.Length);

    public void RefreshFileSize() => ToolTip.SetTip(_headerBorder, $"{FilePath} ({FileSizeText})");

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

    public void ScrollToEnd()
    {
        Editor.CaretOffset = Editor.Document.TextLength;
        Editor.TextArea.Caret.BringCaretToView();
    }
}
