using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace JTail;

/// <summary>
/// JTail's complete UI (toolbar, file tabs, log views, status bar) as a control, so it can be hosted
/// by JTail's own window or embedded in another app.
/// </summary>
public partial class JTailView : UserControl
{
    public const string DefaultTitle = "JTail";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>How long typing in the find or filter box pauses before searching, so a big log isn't searched per keystroke.</summary>
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly RecentFilesStore _recentFiles = RecentFilesStore.Load();
    private readonly LogThemeStore _logTheme = LogThemeStore.Load();
    private readonly HighlightRulesStore _highlightRules = HighlightRulesStore.Load();
    private readonly List<LogTabItem> _tabs = [];

    private readonly DispatcherTimer _findTimer = new() { Interval = SearchDelay };
    private readonly DispatcherTimer _filterTimer = new() { Interval = SearchDelay };
    private DispatcherTimer? _pollTimer;
    private LogTabItem? _selectedTab;
    private bool _loadingSearchBar;

    /// <summary>Window title describing the current state, e.g. "JTail - app.log".</summary>
    public string Title { get; private set; } = DefaultTitle;

    /// <summary>Raised when <see cref="Title"/> changes, so the hosting window can update its title.</summary>
    public event EventHandler? TitleChanged;

    /// <summary>Raised when the user chooses Exit from the toolbar's "More" menu; the host decides what exiting means.</summary>
    public event EventHandler? ExitRequested;

    public JTailView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        RebuildRecentFilesMenu();

        _findTimer.Tick += (_, _) => ApplyFind();
        _filterTimer.Tick += (_, _) => ApplyFilter();
        FindBox.TextChanged += (_, _) => Restart(_findTimer);
        FindMatchCaseToggle.IsCheckedChanged += (_, _) => Restart(_findTimer);
        FindRegexToggle.IsCheckedChanged += (_, _) => Restart(_findTimer);
        FilterBox.TextChanged += (_, _) => Restart(_filterTimer);
        FilterMatchCaseToggle.IsCheckedChanged += (_, _) => Restart(_filterTimer);
        FilterRegexToggle.IsCheckedChanged += (_, _) => Restart(_filterTimer);

        // Tunnel, so the shortcuts work wherever focus is in the view, including the log itself.
        AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            if (File.Exists(path))
            {
                OpenFile(path);
            }
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e) => await ShowOpenDialog();

    private async void OnEmptyStateClick(object? sender, PointerPressedEventArgs e) => await ShowOpenDialog();

    private void OnExitClick(object? sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void ShowDialog(Window dialog)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    private void OnAppearanceClick(object? sender, RoutedEventArgs e)
    {
        ShowDialog(new LogAppearanceWindow(_logTheme, ApplyLogTheme, ApplyLogFontSize));
    }

    private void ApplyLogTheme()
    {
        foreach (var tab in _tabs)
        {
            tab.ApplyTheme(_logTheme.Background, _logTheme.Text);
        }
    }

    private void ApplyLogFontSize(double fontSize)
    {
        foreach (var tab in _tabs)
        {
            tab.ApplyFontSize(fontSize);
        }
    }

    private void OnHighlightsClick(object? sender, RoutedEventArgs e)
    {
        ShowDialog(new HighlightsWindow(_highlightRules, RefreshHighlights));
    }

    private void RefreshHighlights()
    {
        foreach (var tab in _tabs)
        {
            tab.RefreshHighlights();
        }
    }

    private async Task ShowOpenDialog()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Log File",
            AllowMultiple = true
        });

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            if (File.Exists(path))
            {
                OpenFile(path);
            }
        }
    }

    private void OnRecentFileClick(string path)
    {
        if (File.Exists(path))
        {
            OpenFile(path);
            return;
        }

        _recentFiles.Remove(path);
        RebuildRecentFilesMenu();

        ShowDialog(new Window
        {
            Title = "File Not Found",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Content = new TextBlock
            {
                Text = $"File not found: {path}",
                Margin = new Avalonia.Thickness(16),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        });
    }

    private void OpenFile(string path)
    {
        var existing = _tabs.FirstOrDefault(t => string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectTab(existing);
            return;
        }

        var tailer = LogTailer.Open(path, out var initialText);
        var tab = new LogTabItem(path, tailer, initialText, _logTheme.Background, _logTheme.Text, _logTheme.FontSize, _highlightRules);
        tab.CloseRequested += (_, _) => CloseTab(tab);
        tab.SelectRequested += (_, _) => SelectTab(tab);

        _tabs.Add(tab);
        TabStrip.Children.Add(tab.Header);
        TabContentHost.Children.Add(tab.Content);

        TabsPanel.IsVisible = true;
        EmptyState.IsVisible = false;

        SelectTab(tab);
        Dispatcher.UIThread.Post(tab.ScrollToEnd, DispatcherPriority.Loaded);

        EnsurePollTimerRunning();

        _recentFiles.Add(path);
        RebuildRecentFilesMenu();
    }

    private void SelectTab(LogTabItem tab)
    {
        // Finish a search still waiting for typing to pause, so it lands on the tab it was typed for.
        ApplyPendingSearches();
        _selectedTab = tab;
        LoadSearchBar(tab);

        foreach (var t in _tabs)
        {
            t.SetSelected(t == tab);
        }

        UpdateTitle();
        UpdateStatusBar();
    }

    private void CloseTab(LogTabItem tab)
    {
        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TabStrip.Children.Remove(tab.Header);
        TabContentHost.Children.Remove(tab.Content);

        if (_tabs.Count == 0)
        {
            _pollTimer?.Stop();
            _pollTimer = null;
            _selectedTab = null;
            TabsPanel.IsVisible = false;
            EmptyState.IsVisible = true;
            SetTitle(DefaultTitle);
            UpdateStatusBar();
            return;
        }

        if (_selectedTab == tab)
        {
            SelectTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
        }
    }

    private void RebuildRecentFilesMenu()
    {
        var menu = (MenuFlyout)RecentButton.Flyout!;
        menu.Items.Clear();

        if (_recentFiles.Paths.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(no recent files)", IsEnabled = false });
            return;
        }

        foreach (var path in _recentFiles.Paths)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => OnRecentFileClick(path);
            menu.Items.Add(item);
        }
    }

    private void EnsurePollTimerRunning()
    {
        if (_pollTimer is not null)
        {
            return;
        }

        _pollTimer = new DispatcherTimer
        {
            Interval = PollInterval
        };
        _pollTimer.Tick += (_, _) => PollForChanges();
        _pollTimer.Start();
    }

    private void PollForChanges()
    {
        foreach (var tab in _tabs)
        {
            var appended = tab.Tailer.ReadAppendedText();
            if (string.IsNullOrEmpty(appended))
            {
                continue;
            }

            tab.AppendText(appended);
            tab.RefreshFileSize();
            if (tab == _selectedTab)
            {
                UpdateSearchStatus();
            }

            if (AutoScrollCheckBox.IsChecked == true && tab == _selectedTab)
            {
                tab.ScrollToEnd();
            }

            if (tab == _selectedTab)
            {
                UpdateStatusBar();
            }
        }
    }

    private void OnAutoScrollChanged(object? sender, RoutedEventArgs e)
    {
        if (AutoScrollCheckBox.IsChecked == true)
        {
            _selectedTab?.ScrollToEnd();
        }
    }

    private void UpdateStatusBar()
    {
        var tab = _selectedTab;
        TailingIndicator.IsVisible = tab is not null;
        FileStatsText.IsVisible = tab is not null;

        if (tab is null)
        {
            FilePathText.Text = "No file open";
            return;
        }

        FilePathText.Text = tab.FilePath;
        var document = tab.Editor.Document;
        var lines = document.LineCount - (document.Lines[^1].Length == 0 ? 1 : 0);
        FileStatsText.Text = tab.HiddenLineCount is { } hidden
            ? $"{tab.FileSizeText}  ·  {lines:N0} of {lines + hidden:N0} lines"
            : $"{tab.FileSizeText}  ·  {lines:N0} {(lines == 1 ? "line" : "lines")}";
    }

    private static void Restart(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private void ApplyPendingSearches()
    {
        if (_findTimer.IsEnabled)
        {
            ApplyFind();
        }
        if (_filterTimer.IsEnabled)
        {
            ApplyFilter();
        }
    }

    private void ApplyFind()
    {
        _findTimer.Stop();
        if (_loadingSearchBar || _selectedTab is not { } tab)
        {
            return;
        }

        tab.SetFind(new SearchQuery(FindBox.Text ?? "", FindMatchCaseToggle.IsChecked == true, FindRegexToggle.IsChecked == true));
        UpdateSearchStatus();
    }

    private void ApplyFilter()
    {
        _filterTimer.Stop();
        if (_loadingSearchBar || _selectedTab is not { } tab)
        {
            return;
        }

        tab.SetFilter(new SearchQuery(FilterBox.Text ?? "", FilterMatchCaseToggle.IsChecked == true, FilterRegexToggle.IsChecked == true));
        if (AutoScrollCheckBox.IsChecked == true)
        {
            tab.ScrollToEnd();
        }
        UpdateSearchStatus();
        UpdateStatusBar();
    }

    /// <summary>Shows the tab's own find and filter in the bar.</summary>
    private void LoadSearchBar(LogTabItem tab)
    {
        _loadingSearchBar = true;
        FindBox.Text = tab.FindQuery.Text;
        FindMatchCaseToggle.IsChecked = tab.FindQuery.MatchCase;
        FindRegexToggle.IsChecked = tab.FindQuery.IsRegex;
        FilterBox.Text = tab.FilterQuery.Text;
        FilterMatchCaseToggle.IsChecked = tab.FilterQuery.MatchCase;
        FilterRegexToggle.IsChecked = tab.FilterQuery.IsRegex;
        _loadingSearchBar = false;
        _findTimer.Stop();
        _filterTimer.Stop();
        UpdateSearchStatus();
    }

    private void UpdateSearchStatus()
    {
        var tab = _selectedTab;

        var findError = tab?.FindError;
        FindBox.Classes.Set("invalid", findError is not null);
        ToolTip.SetTip(FindStatusText, findError);
        FindStatusText.Classes.Set("error", findError is not null);
        FindStatusText.Text =
            tab is null || tab.FindQuery.Text.Length == 0 ? "" :
            findError is not null ? "Invalid regex" :
            tab.FindHitCount == 0 ? "No matches" :
            tab.CurrentFindHit >= 0 ? $"{tab.CurrentFindHit + 1:N0} of {tab.FindHitCount:N0}" :
            tab.FindHitCount == 1 ? "1 match" : $"{tab.FindHitCount:N0} matches";

        var filterError = tab?.FilterError;
        FilterBox.Classes.Set("invalid", filterError is not null);
        ToolTip.SetTip(FilterStatusText, filterError);
        FilterStatusText.Classes.Set("error", filterError is not null);
        FilterStatusText.Text =
            filterError is not null ? "Invalid regex" :
            tab?.HiddenLineCount is not { } hidden ? "" :
            hidden == 1 ? "1 line hidden" : $"{hidden:N0} lines hidden";
    }

    private void OnFindPreviousClick(object? sender, RoutedEventArgs e) => GoToFindHit(forward: false);

    private void OnFindNextClick(object? sender, RoutedEventArgs e) => GoToFindHit(forward: true);

    private void GoToFindHit(bool forward)
    {
        if (_findTimer.IsEnabled)
        {
            ApplyFind();
        }

        if (_selectedTab?.GoToFindHit(forward) == true)
        {
            // Going to a match means reading back through the log, so stop jumping to new lines.
            AutoScrollCheckBox.IsChecked = false;
        }
        UpdateSearchStatus();
    }

    /// <summary>Ctrl+F, Ctrl+Shift+F, F3 / Shift+F3, and Enter / Shift+Enter and Escape in the boxes.</summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_selectedTab is null)
        {
            return;
        }

        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool inFindBox = FindBox.IsKeyboardFocusWithin;
        bool inFilterBox = FilterBox.IsKeyboardFocusWithin;

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var box = shift ? FilterBox : FindBox;
            box.Focus();
            box.SelectAll();
        }
        else if (e.Key == Key.F3 || (e.Key == Key.Enter && inFindBox))
        {
            GoToFindHit(forward: !shift);
        }
        else if (e.Key == Key.Escape && (inFindBox || inFilterBox))
        {
            // The first Escape clears the box, the next goes back to the log.
            var box = inFindBox ? FindBox : FilterBox;
            if (!string.IsNullOrEmpty(box.Text))
            {
                box.Text = "";
            }
            else
            {
                _selectedTab.Editor.Focus();
            }
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    private void UpdateTitle()
    {
        SetTitle(_selectedTab is not null
            ? $"{DefaultTitle} - {Path.GetFileName(_selectedTab.FilePath)}"
            : DefaultTitle);
    }

    private void SetTitle(string title)
    {
        Title = title;
        TitleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes every open file, which also stops tailing.</summary>
    public void CloseAllTabs()
    {
        foreach (var tab in _tabs.ToList())
        {
            CloseTab(tab);
        }
    }

    /// <summary>Stops polling files for changes; call when the hosting window closes.</summary>
    public void StopTailing()
    {
        _pollTimer?.Stop();
        _pollTimer = null;
    }
}
