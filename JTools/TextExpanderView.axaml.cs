using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TextExpander;

namespace JTools;

/// <summary>One row in the abbreviation list.</summary>
public sealed class SnippetRow(Snippet snippet)
{
    private const int MaxPreviewLength = 80;

    public Snippet Snippet { get; } = snippet;

    public string Abbreviation => Snippet.Abbreviation;

    public string Preview
    {
        get
        {
            var singleLine = Snippet.Expansion.ReplaceLineEndings(" ⏎ ");
            return singleLine.Length > MaxPreviewLength ? singleLine[..MaxPreviewLength] + "…" : singleLine;
        }
    }
}

/// <summary>Add, edit and delete abbreviations, and turn expansion on or off.</summary>
public partial class TextExpanderView : UserControl
{
    private static readonly TimeSpan DeleteConfirmWindow = TimeSpan.FromSeconds(3);

    private ExpanderHost? _expander;
    private Snippet? _editing;
    private bool _refreshingList;
    private DispatcherTimer? _deleteConfirmTimer;

    public TextExpanderView()
    {
        InitializeComponent();

        SearchBox.TextChanged += (_, _) => RefreshList(_editing);
        SnippetList.SelectionChanged += OnSelectionChanged;
        AbbreviationBox.TextChanged += (_, _) => ClearStatus();
        ExpansionBox.TextChanged += (_, _) => ClearStatus();
    }

    public void Attach(ExpanderHost expander)
    {
        _expander = expander;

        EnabledSwitch.IsChecked = expander.Enabled;
        EnabledSwitch.IsEnabled = expander.IsAvailable;
        EnabledSwitch.IsCheckedChanged += (_, _) => expander.Enabled = EnabledSwitch.IsChecked == true;
        expander.EnabledChanged += () => EnabledSwitch.IsChecked = expander.Enabled;

        // If another process owns expansion it also owns the abbreviation file, so don't edit it here.
        UnavailableBanner.IsVisible = !expander.IsAvailable;
        EditorArea.IsEnabled = expander.IsAvailable;
        SettingsButton.IsEnabled = expander.IsAvailable;

        expander.Settings.Changed += UpdateHint;
        UpdateHint();

        RefreshList(null);
        if (SnippetList.ItemCount > 0)
        {
            SnippetList.SelectedIndex = 0;
        }
        else
        {
            StartNew();
        }
    }

    private List<Snippet> Snippets => _expander!.Store.Snippets;

    private void UpdateHint()
    {
        var settings = _expander!.Settings;
        var howToExpand = settings.Mode == ExpandMode.Immediate
            ? "Type an abbreviation in any app and it expands immediately."
            : $"Type an abbreviation in any app, then press {settings.DescribeTriggerKeys()} to expand it.";
        HintText.Text = howToExpand + " Abbreviations are case-sensitive and can't contain spaces. " +
                        "Expansion is paused while JTools is the active window.";
    }

    private void RefreshList(Snippet? select)
    {
        var filter = SearchBox.Text?.Trim() ?? "";
        var rows = Snippets
            .Where(s => filter.Length == 0
                        || s.Abbreviation.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || s.Expansion.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Abbreviation, StringComparer.OrdinalIgnoreCase)
            .Select(s => new SnippetRow(s))
            .ToList();

        _refreshingList = true;
        SnippetList.ItemsSource = rows;
        SnippetList.SelectedItem = rows.FirstOrDefault(r => r.Snippet == select);
        _refreshingList = false;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingList || SnippetList.SelectedItem is not SnippetRow row)
        {
            return;
        }

        _editing = row.Snippet;
        AbbreviationBox.Text = row.Snippet.Abbreviation;
        ExpansionBox.Text = row.Snippet.Expansion.ReplaceLineEndings(Environment.NewLine);
        DeleteButton.IsEnabled = true;
        ResetDeleteConfirm();
        ClearStatus();
    }

    private void OnNewClick(object? sender, RoutedEventArgs e) => StartNew();

    private void StartNew()
    {
        _refreshingList = true;
        SnippetList.SelectedItem = null;
        _refreshingList = false;

        _editing = null;
        AbbreviationBox.Text = "";
        ExpansionBox.Text = "";
        DeleteButton.IsEnabled = false;
        ResetDeleteConfirm();
        ClearStatus();
        AbbreviationBox.Focus();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveCurrent();

    private void SaveCurrent()
    {
        if (_expander is null || !_expander.IsAvailable)
        {
            return;
        }

        var abbreviation = AbbreviationBox.Text?.Trim() ?? "";
        var expansion = ExpansionBox.Text ?? "";

        string? problem =
            abbreviation.Length == 0 ? "Enter an abbreviation." :
            abbreviation.Any(char.IsWhiteSpace) ? "Abbreviations can't contain spaces." :
            expansion.Length == 0 ? "Enter the text this abbreviation expands to." :
            Snippets.Any(s => s != _editing && s.Abbreviation == abbreviation) ? $"\"{abbreviation}\" is already in use." :
            null;
        if (problem is not null)
        {
            ShowStatus(problem, isWarning: true);
            return;
        }

        if (_editing is null)
        {
            _editing = new Snippet();
            Snippets.Add(_editing);
        }
        _editing.Abbreviation = abbreviation;
        _editing.Expansion = expansion;
        _expander.Store.Save();

        RefreshList(_editing);
        DeleteButton.IsEnabled = true;
        ShowStatus("Saved ✓");
    }

    /// <summary>Delete asks for a second click instead of a popup: "Delete" → "Click again to delete".</summary>
    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_editing is null || _expander is null)
        {
            return;
        }

        if (_deleteConfirmTimer is null)
        {
            DeleteButton.Content = "Click again to delete";
            _deleteConfirmTimer = new DispatcherTimer { Interval = DeleteConfirmWindow };
            _deleteConfirmTimer.Tick += (_, _) => ResetDeleteConfirm();
            _deleteConfirmTimer.Start();
            return;
        }

        ResetDeleteConfirm();
        Snippets.Remove(_editing);
        _expander.Store.Save();
        StartNew();
        RefreshList(null);
        ShowStatus("Deleted");
    }

    private void ResetDeleteConfirm()
    {
        _deleteConfirmTimer?.Stop();
        _deleteConfirmTimer = null;
        DeleteButton.Content = "Delete";
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_expander is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        await new ExpanderSettingsWindow(_expander.Settings).ShowDialog(owner);
    }

    private void ShowStatus(string message, bool isWarning = false)
    {
        StatusText.Text = message;
        StatusText.Classes.Set("warning", isWarning);
    }

    private void ClearStatus() => StatusText.Text = "";

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)
        {
            SaveCurrent();
            e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.N)
        {
            StartNew();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}
