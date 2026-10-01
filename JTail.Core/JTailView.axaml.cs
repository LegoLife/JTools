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

    private readonly RecentFilesStore _recentFiles = RecentFilesStore.Load();
    private readonly LogThemeStore _logTheme = LogThemeStore.Load();
    private readonly HighlightRulesStore _highlightRules = HighlightRulesStore.Load();
    private readonly List<LogTabItem> _tabs = [];

    private DispatcherTimer? _pollTimer;
    private LogTabItem? _selectedTab;

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
        _selectedTab = tab;

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
        FileStatsText.Text = $"{tab.FileSizeText}  ·  {lines:N0} {(lines == 1 ? "line" : "lines")}";
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
