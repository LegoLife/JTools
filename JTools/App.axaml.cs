using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using TextExpander;

namespace JTools;

/// <summary>
/// Runs from the system tray: closing the window only hides it, so text expansion keeps working.
/// Ctrl+Alt+J shows or hides the window from any app. The app exits from the tray menu or JTail's File → Exit.
/// </summary>
public partial class App : Application
{
    private readonly StartupManager _startup = new("JTools", Program.StartInTrayArgument);
    private readonly JToolsSettings _settings = JToolsSettings.Load();

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private ExpanderHost? _expander;
    private MainWindow? _window;
    private GlobalHotkey? _hotkey;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            JTail.ThemeAccent.KeepInSync(this);
            _settings.ApplyTheme();
            _expander = new ExpanderHost();

            _window = new MainWindow();
            _window.Attach(_settings, _expander);
            _window.ExitRequested += (_, _) => Exit();
            _hotkey = GlobalHotkey.TryRegister(_window, ToggleMainWindow);

            CreateTrayIcon(_expander);

            bool startInTray = desktop.Args?.Contains(Program.StartInTrayArgument) == true;
            if (!startInTray)
            {
                ShowMainWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(ExpanderHost expander)
    {
        var openItem = new NativeMenuItem("Open JTools") { Gesture = _hotkey is null ? null : GlobalHotkey.Gesture };
        openItem.Click += (_, _) => ShowMainWindow();

        var expansionItem = new NativeMenuItem("Text expansion enabled")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
        };
        expansionItem.Click += (_, _) => expander.Enabled = !expander.Enabled;
        expander.EnabledChanged += () => expansionItem.IsChecked = expander.Enabled;

        var startupItem = new NativeMenuItem("Start with Windows")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = _startup.IsEnabled,
        };
        startupItem.Click += (_, _) =>
        {
            _startup.IsEnabled = !_startup.IsEnabled;
            startupItem.IsChecked = _startup.IsEnabled;
        };

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => Exit();

        var menu = new NativeMenu
        {
            openItem,
            new NativeMenuItemSeparator(),
            expansionItem,
            startupItem,
            new NativeMenuItemSeparator(),
            exitItem,
        };

        // Say why the shortcut does nothing, where someone would look for it.
        var hotkeyTaken = $"{GlobalHotkey.Gesture} is in use by another app";
        if (_hotkey is null)
        {
            menu.Items.Insert(1, new NativeMenuItem(hotkeyTaken) { IsEnabled = false });
        }

        // The expansion switch only means something while the Expand tool is turned on.
        void UpdateExpansionItem()
        {
            expansionItem.IsChecked = expander.Enabled;
            expansionItem.IsEnabled = expander.IsAvailable;
            bool shown = menu.Items.Contains(expansionItem);
            if (expander.IsStarted && !shown)
            {
                menu.Items.Insert(menu.Items.IndexOf(startupItem), expansionItem);
            }
            else if (!expander.IsStarted && shown)
            {
                menu.Items.Remove(expansionItem);
            }
        }
        expander.StateChanged += UpdateExpansionItem;
        UpdateExpansionItem();

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://JTools/Assets/icon.ico"))),
            ToolTipText = _hotkey is null ? $"JTools ({hotkeyTaken})" : $"JTools ({GlobalHotkey.Gesture})",
            Menu = menu,
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();
        TrayIcon.SetIcons(this, [_trayIcon]);
    }

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }
        _window.Activate();
    }

    /// <summary>
    /// The global hotkey: hides the window if it's in front, otherwise shows it and brings it to the front.
    /// </summary>
    private void ToggleMainWindow()
    {
        if (_window is { IsVisible: true, IsActive: true, WindowState: not WindowState.Minimized })
        {
            _window.Hide();
        }
        else
        {
            ShowMainWindow();
        }
    }

    private void Exit()
    {
        _hotkey?.Dispose();
        _window?.CloseForExit();
        _expander?.Dispose();
        _trayIcon?.Dispose();
        _desktop?.Shutdown();
    }
}
