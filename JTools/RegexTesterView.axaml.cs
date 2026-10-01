using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace JTools;

/// <summary>Write a .NET regex, test it against sample text, and see every match and capture group.</summary>
public partial class RegexTesterView : UserControl
{
    private static readonly TimeSpan RunDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShowRunningAfter = TimeSpan.FromMilliseconds(300);

    private readonly RegexTesterState _state = RegexTesterState.Load();
    private readonly MatchHighlightRenderer _matchRenderer;
    private readonly PatternErrorRenderer _errorRenderer;
    private readonly DispatcherTimer _runTimer;
    private readonly DispatcherTimer _saveTimer;

    /// <summary>Bumped for every run; a background run whose number is stale gives up, and its result is ignored.</summary>
    private int _generation;

    private RegexRunResult.Success? _results;
    private string[] _groupHeaders = [];

    /// <summary>Grid column last clicked; a group column outlines that group instead of the whole match.</summary>
    private int _clickedColumn;
    private bool _syncingSelection;

    public RegexTesterView()
    {
        InitializeComponent();

        _matchRenderer = new MatchHighlightRenderer(this);
        _errorRenderer = new PatternErrorRenderer(this);
        SampleEditor.TextArea.TextView.BackgroundRenderers.Add(_matchRenderer);
        PatternEditor.TextArea.TextView.BackgroundRenderers.Add(_errorRenderer);

        foreach (var editor in new[] { PatternEditor, SampleEditor })
        {
            editor.Bind(TemplatedControl.ForegroundProperty, this.GetResourceObservable("JTextBrush"));
            editor.TextArea.Bind(TemplatedControl.ForegroundProperty, this.GetResourceObservable("JTextBrush"));
        }
        ActualThemeVariantChanged += (_, _) => RedrawHighlights();

        // Breathing room between the line numbers' divider and the text.
        if (SampleEditor.TextArea.LeftMargins.LastOrDefault() is { } divider)
        {
            divider.Margin = new Thickness(4, 0, 6, 0);
        }

        PatternEditor.Document = new TextDocument(_state.Pattern);
        SampleEditor.Document = new TextDocument(_state.SampleText);
        IgnoreCaseToggle.IsChecked = _state.IgnoreCase;
        MultilineToggle.IsChecked = _state.Multiline;
        SinglelineToggle.IsChecked = _state.Singleline;
        IgnoreWhitespaceToggle.IsChecked = _state.IgnorePatternWhitespace;
        ExplicitCaptureToggle.IsChecked = _state.ExplicitCapture;
        UpdateWatermark();

        _runTimer = new DispatcherTimer { Interval = RunDelay };
        _runTimer.Tick += (_, _) =>
        {
            _runTimer.Stop();
            StartRun();
        };
        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => SaveState();

        PatternEditor.TextChanged += (_, _) =>
        {
            UpdateWatermark();
            OnInputChanged();
        };
        SampleEditor.TextChanged += (_, _) => OnInputChanged();
        foreach (var toggle in new[] { IgnoreCaseToggle, MultilineToggle, SinglelineToggle, IgnoreWhitespaceToggle, ExplicitCaptureToggle })
        {
            toggle.IsCheckedChanged += (_, _) => OnInputChanged();
        }

        // Without x, the pattern is one logical line: Enter does nothing (AvaloniaEdit would insert a newline).
        PatternEditor.TextArea.AddHandler(KeyDownEvent, OnPatternKeyDown, RoutingStrategies.Tunnel);

        SampleEditor.TextArea.Caret.PositionChanged += (_, _) => SelectRowAtCaret();
        ResultsList.SelectionChanged += OnResultsSelectionChanged;
        ResultsList.AddHandler(PointerPressedEvent, OnTablePointerPressed, RoutingStrategies.Tunnel);
        TableScroller.SizeChanged += (_, _) => FitTableWidth();
        BuildTable([]);

        StartRun();
    }

    private RegexOptions CurrentOptions() =>
        (IgnoreCaseToggle.IsChecked == true ? RegexOptions.IgnoreCase : 0) |
        (MultilineToggle.IsChecked == true ? RegexOptions.Multiline : 0) |
        (SinglelineToggle.IsChecked == true ? RegexOptions.Singleline : 0) |
        (IgnoreWhitespaceToggle.IsChecked == true ? RegexOptions.IgnorePatternWhitespace : 0) |
        (ExplicitCaptureToggle.IsChecked == true ? RegexOptions.ExplicitCapture : 0);

    private void OnInputChanged()
    {
        _runTimer.Stop();
        _runTimer.Start();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void UpdateWatermark() => PatternWatermark.IsVisible = PatternEditor.Document.TextLength == 0;

    private void OnPatternKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && IgnoreWhitespaceToggle.IsChecked != true)
        {
            e.Handled = true;
        }
    }

    private void StartRun()
    {
        var generation = ++_generation;
        var pattern = PatternEditor.Text;
        var input = SampleEditor.Text;
        var options = CurrentOptions();

        if (pattern.Length == 0)
        {
            ShowError(null);
            ShowResults(null);
            StatusText.Text = "Enter a pattern to see its matches.";
            return;
        }

        var finished = false;
        DispatcherTimer.RunOnce(() =>
        {
            if (!finished && generation == _generation)
            {
                StatusText.Text = "Running…";
            }
        }, ShowRunningAfter);

        Task.Run(() => RegexRun.Execute(pattern, options, input, () => Volatile.Read(ref _generation) != generation))
            .ContinueWith(task => Dispatcher.UIThread.Post(() =>
            {
                finished = true;
                if (generation == _generation && task.IsCompletedSuccessfully && task.Result is { } result)
                {
                    ApplyResult(result);
                }
            }));
    }

    private void ApplyResult(RegexRunResult result)
    {
        switch (result)
        {
            case RegexRunResult.Invalid invalid:
                // Keep the last good results on screen, faded, so a half-typed pattern doesn't blank everything.
                ShowError(invalid);
                if (_results is null)
                {
                    StatusText.Text = "Fix the pattern to see its matches.";
                }
                break;

            case RegexRunResult.TimedOut:
                ShowError(null);
                ShowResults(null);
                StatusText.Text = $"Timed out after {RegexRun.MatchTimeout.TotalSeconds:0} s. The pattern may be backtracking " +
                                  "catastrophically, e.g. nested quantifiers like (a+)+.";
                break;

            case RegexRunResult.Success success:
                ShowError(null);
                ShowResults(success);
                StatusText.Text = Describe(success);
                break;
        }
    }

    private static string Describe(RegexRunResult.Success results)
    {
        if (results.Spans.Length == 0)
        {
            return "No matches.";
        }

        var elapsed = results.Elapsed.TotalMilliseconds < 1 ? "<1 ms" : $"{results.Elapsed.TotalMilliseconds:0} ms";
        var text = $"{Plural(results.Spans.Length, "match", "matches")} · {Plural(results.GroupHeaders.Length, "group", "groups")} · {elapsed}";
        return results.IsTruncated
            ? text + $" · table shows the first {results.Rows.Count:N0} of {results.Spans.Length:N0} matches"
            : text;
    }

    private static string Plural(int count, string one, string many) => $"{count:N0} {(count == 1 ? one : many)}";

    private void ShowError(RegexRunResult.Invalid? error)
    {
        ErrorText.IsVisible = error is not null;
        ErrorText.Text = error?.Message;
        PatternFrame.Classes.Set("invalid", error is not null);

        // .NET reports the offset just past the problem, so underline the character before it.
        var patternLength = PatternEditor.Document.TextLength;
        _errorRenderer.ErrorIndex = error is { Offset: >= 0 } && patternLength > 0
            ? Math.Clamp(error.Offset - 1, 0, patternLength - 1)
            : null;
        PatternEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

        _matchRenderer.IsDimmed = error is not null;
        TableFrame.Opacity = error is null ? 1 : 0.5;
        RedrawHighlights();
    }

    private void ShowResults(RegexRunResult.Success? results)
    {
        _results = results;
        _matchRenderer.Spans = results?.Spans ?? [];
        _matchRenderer.Outline = null;

        _syncingSelection = true;
        ResultsList.ItemsSource = null;
        if (results is not null && !results.GroupHeaders.SequenceEqual(_groupHeaders))
        {
            BuildTable(results.GroupHeaders);
        }
        ResultsList.ItemsSource = results?.Rows;
        _syncingSelection = false;

        RedrawHighlights();
    }

    private void RedrawHighlights()
    {
        SampleEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        PatternEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    /// <summary>Rebuilds the grid's header and row template for a pattern with a different set of groups.</summary>
    private void BuildTable(string[] groupHeaders)
    {
        _groupHeaders = groupHeaders;
        _clickedColumn = 0;
        TableHeaderHost.Child = MatchTable.CreateHeader(groupHeaders);
        ResultsList.ItemTemplate = new FuncDataTemplate<MatchRow>((_, _) => new MatchRowView(groupHeaders.Length), supportsRecycling: true);
        FitTableWidth();
    }

    /// <summary>Fill the card's width, but never squeeze columns below their minimum; past that, scroll sideways.</summary>
    private void FitTableWidth() =>
        TableFrame.Width = Math.Max(TableScroller.Bounds.Width, MatchTable.MinWidth(_groupHeaders.Length));

    /// <summary>Remember which column was clicked, so the outline shows that group (or the whole match).</summary>
    private void OnTablePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var cell = (e.Source as Visual)?.GetSelfAndVisualAncestors()
            .OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("cell"));
        if (cell?.Tag is int column)
        {
            _clickedColumn = column;
            // Runs after the ListBox has handled the press, so the clicked row is selected by then.
            Dispatcher.UIThread.Post(ShowSelectedOutline);
        }
    }

    private void OnResultsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncingSelection)
        {
            ShowSelectedOutline();
        }
    }

    /// <summary>Table → text: outline the selected match, or the clicked group within it, and scroll it into view.</summary>
    private void ShowSelectedOutline()
    {
        MatchSpan? outline = null;
        if (ResultsList.SelectedItem is MatchRow row)
        {
            var groupIndex = _clickedColumn - MatchTable.FixedColumns;
            if (groupIndex < 0)
            {
                outline = new MatchSpan(row.Index, row.Length);
            }
            else if (groupIndex < row.Groups.Length && row.Groups[groupIndex] is { Participated: true } group)
            {
                outline = new MatchSpan(group.Index, group.Length);
            }
        }

        _matchRenderer.Outline = outline;
        RedrawHighlights();

        if (outline is { } span && span.Index <= SampleEditor.Document.TextLength)
        {
            var location = SampleEditor.Document.GetLocation(span.Index);
            SampleEditor.ScrollTo(location.Line, location.Column);
        }
    }

    /// <summary>Text → table: when the caret lands in a match, select that match's row.</summary>
    private void SelectRowAtCaret()
    {
        if (_results is null)
        {
            return;
        }

        var matchIndex = RegexRun.FindMatchAt(_results.Spans, SampleEditor.CaretOffset);
        if (matchIndex < 0 || matchIndex >= _results.Rows.Count || ResultsList.SelectedIndex == matchIndex)
        {
            return;
        }

        _clickedColumn = 0;
        _syncingSelection = true;
        ResultsList.SelectedIndex = matchIndex;
        _syncingSelection = false;
        ResultsList.ScrollIntoView(matchIndex);

        _matchRenderer.Outline = _results.Spans[matchIndex];
        RedrawHighlights();
    }

    private async void OnCopyCSharpClick(object? sender, RoutedEventArgs e)
    {
        await CopyToClipboard(RegexRun.ToCSharp(PatternEditor.Text, CurrentOptions()));
        CopyCSharpLabel.Text = "Copied ✓";
        DispatcherTimer.RunOnce(() => CopyCSharpLabel.Text = "Copy as C#", TimeSpan.FromSeconds(1.5));
    }

    /// <summary>Copies the full (unescaped, untruncated) value of the cell last clicked in the selected row.</summary>
    private async void OnCopyCellClick(object? sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not MatchRow row)
        {
            return;
        }

        var groupIndex = _clickedColumn - MatchTable.FixedColumns;
        var text = _clickedColumn switch
        {
            0 => row.Number.ToString(),
            1 => row.Index.ToString(),
            2 => row.Value,
            _ when groupIndex < row.Groups.Length => row.Groups[groupIndex].Value,
            _ => "",
        };
        await CopyToClipboard(text);
    }

    private async void OnCopyAllClick(object? sender, RoutedEventArgs e)
    {
        if (_results is null)
        {
            return;
        }

        var tsv = new StringBuilder(MatchRow.TsvHeader(_groupHeaders)).AppendLine();
        foreach (var row in _results.Rows)
        {
            tsv.AppendLine(row.ToTsv());
        }
        await CopyToClipboard(tsv.ToString());
    }

    private async Task CopyToClipboard(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>Saves the pattern, test text and options now (also happens a second after each edit).</summary>
    public void SaveState()
    {
        _saveTimer.Stop();
        _state.Pattern = PatternEditor.Text;
        _state.SampleText = SampleEditor.Text;
        _state.IgnoreCase = IgnoreCaseToggle.IsChecked == true;
        _state.Multiline = MultilineToggle.IsChecked == true;
        _state.Singleline = SinglelineToggle.IsChecked == true;
        _state.IgnorePatternWhitespace = IgnoreWhitespaceToggle.IsChecked == true;
        _state.ExplicitCapture = ExplicitCaptureToggle.IsChecked == true;
        _state.Save();
    }
}
