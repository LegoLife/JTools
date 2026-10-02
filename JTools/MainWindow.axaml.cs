using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace JTools;

public partial class MainWindow : Window
{
    private const string AppName = "JTools";

    /// <summary>Every tool, in side bar order.</summary>
    private static readonly Tool[] AllTools = [Tool.Tail, Tool.Expander, Tool.Regex];

    private static readonly FilePickerFileType DataFileType = new("JTools data") { Patterns = ["*.json"] };

    private bool _closingForExit;
    private JToolsSettings? _settings;
    private ExpanderHost? _expander;
    private DataBundle? _data;
    private Tool _shownTool = Tool.Tail;

    /// <summary>Raised when a tool asks to exit the whole app (e.g. JTail's File → Exit).</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Raised when the user confirms an import, once their current data is backed up. The app writes the
    /// sections after every tool has saved and stopped, then restarts.
    /// </summary>
    public event EventHandler<IReadOnlyDictionary<string, JsonNode>>? ImportConfirmed;

    public MainWindow()
    {
        InitializeComponent();

        TailView.TitleChanged += (_, _) => UpdateTitle();
        TailView.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Connects the window to the app's settings, expander and data, and starts the tools that are turned on.</summary>
    public void Attach(JToolsSettings settings, ExpanderHost expander, DataBundle data)
    {
        _settings = settings;
        _expander = expander;
        _data = data;

        // A hand-edited settings file could turn everything off, leaving the window empty.
        if (!AllTools.Any(settings.IsEnabled))
        {
            settings.DisabledTools.Clear();
        }

        ExpanderView.Attach(expander);
        if (settings.IsEnabled(Tool.Expander))
        {
            expander.Start();
        }

        UpdateThemeChecks();
        UpdateToolStates();
        ShowTool(settings.IsEnabled(settings.LastTool) ? settings.LastTool : AllTools.First(settings.IsEnabled));
    }

    private void OnThemeItemClick(object? sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        _settings.Theme =
            sender == LightThemeItem ? AppTheme.Light :
            sender == SystemThemeItem ? AppTheme.System :
            AppTheme.Dark;
        _settings.Save();
        _settings.ApplyTheme();
        UpdateThemeChecks();
    }

    private void UpdateThemeChecks()
    {
        DarkThemeItem.IsChecked = _settings?.Theme == AppTheme.Dark;
        LightThemeItem.IsChecked = _settings?.Theme == AppTheme.Light;
        SystemThemeItem.IsChecked = _settings?.Theme == AppTheme.System;
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_data is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export JTools Data",
            SuggestedFileName = $"jtools-export-{DateTime.Now:yyyy-MM-dd}.json",
            DefaultExtension = "json",
            FileTypeChoices = [DataFileType],
        });
        if (file is null)
        {
            return;
        }

        // The tester saves a second after each edit; export what's on screen.
        RegexView.SaveState();
        try
        {
            await File.WriteAllTextAsync(file.Path.LocalPath, _data.Export(DateTimeOffset.Now));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await MessageWindow.Show(this, "Export Failed", ex.Message);
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (_data is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import JTools Data",
            FileTypeFilter = [DataFileType],
        });
        if (files.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<string, JsonNode> sections;
        try
        {
            sections = _data.Parse(await File.ReadAllTextAsync(files[0].Path.LocalPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await MessageWindow.Show(this, "Can't Import", ex.Message);
            return;
        }

        var message =
            $"This replaces your {_data.Describe(sections.Keys)} with the ones in {files[0].Name}.\n\n" +
            "Your current data is backed up first, to the backups folder in %APPDATA%\\JTools. " +
            "JTools then restarts to load the imported data.";
        if (!await MessageWindow.Confirm(this, "Import JTools Data", message, "Import and Restart"))
        {
            return;
        }

        RegexView.SaveState();
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JTools", "backups");
            Backups.Write(folder, "before-import", _data.Export(DateTimeOffset.Now), DateTime.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await MessageWindow.Show(this, "Can't Import", $"Your current data couldn't be backed up, so nothing was imported.\n\n{ex.Message}");
            return;
        }

        ImportConfirmed?.Invoke(this, sections);
    }

    private void OnToolItemClick(object? sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        var tool = AllTools.First(t => ToolItem(t) == sender);
        SetToolEnabled(tool, !_settings.IsEnabled(tool));
    }

    /// <summary>
    /// Turns a tool on or off. Off means hidden and not running: Tail closes its files, and Expand
    /// removes its keyboard hook and releases the expander lock.
    /// </summary>
    private void SetToolEnabled(Tool tool, bool enabled)
    {
        var settings = _settings!;
        if (enabled)
        {
            settings.DisabledTools.Remove(tool);
        }
        else if (!settings.DisabledTools.Contains(tool))
        {
            settings.DisabledTools.Add(tool);
        }
        settings.Save();

        switch (tool)
        {
            case Tool.Tail when !enabled:
                TailView.CloseAllTabs();
                break;
            case Tool.Expander when enabled:
                _expander?.Start();
                break;
            case Tool.Expander:
                _expander?.Stop();
                break;
        }

        UpdateToolStates();
        if (!enabled && tool == _shownTool)
        {
            ShowTool(AllTools.First(settings.IsEnabled));
        }
    }

    private void UpdateToolStates()
    {
        var settings = _settings!;
        int enabledCount = AllTools.Count(settings.IsEnabled);
        foreach (var tool in AllTools)
        {
            bool enabled = settings.IsEnabled(tool);
            NavButton(tool).IsVisible = enabled;
            ToolItem(tool).IsChecked = enabled;
            // The last tool that's on can't be turned off, or the window would have nothing to show.
            ToolItem(tool).IsEnabled = !enabled || enabledCount > 1;
        }
    }

    private void OnTailNavClick(object? sender, RoutedEventArgs e) => ShowTool(Tool.Tail);

    private void OnExpanderNavClick(object? sender, RoutedEventArgs e) => ShowTool(Tool.Expander);

    private void OnRegexNavClick(object? sender, RoutedEventArgs e) => ShowTool(Tool.Regex);

    private void ShowTool(Tool tool)
    {
        _shownTool = tool;

        // Tools stay in the visual tree while hidden, so JTail keeps tailing in the background.
        foreach (var t in AllTools)
        {
            ToolView(t).IsVisible = t == tool;
            NavButton(t).Classes.Set("selected", t == tool);
        }
        UpdateTitle();

        if (_settings is not null && _settings.LastTool != tool)
        {
            _settings.LastTool = tool;
            _settings.Save();
        }
    }

    private Control ToolView(Tool tool) => tool switch
    {
        Tool.Tail => TailView,
        Tool.Expander => ExpanderView,
        _ => RegexView,
    };

    private Button NavButton(Tool tool) => tool switch
    {
        Tool.Tail => TailNavButton,
        Tool.Expander => ExpanderNavButton,
        _ => RegexNavButton,
    };

    private MenuItem ToolItem(Tool tool) => tool switch
    {
        Tool.Tail => TailToolItem,
        Tool.Expander => ExpanderToolItem,
        _ => RegexToolItem,
    };

    private void UpdateTitle()
    {
        var toolTitle = _shownTool switch
        {
            Tool.Tail => TailView.Title,
            Tool.Regex => "Regex Tester",
            _ => "Text Expander",
        };
        Title = $"{AppName} — {toolTitle}";
    }

    /// <summary>Closes the window for real (instead of hiding it to the tray).</summary>
    public void CloseForExit()
    {
        _closingForExit = true;
        TailView.StopTailing();
        RegexView.SaveState();
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Clicking X hides to the tray so text expansion keeps running; exit from the tray menu.
        if (!_closingForExit && e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
