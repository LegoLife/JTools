using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using TextExpander;

namespace JTools;

/// <summary>
/// Runs from the system tray: closing the window only hides it, so text expansion keeps working.
/// The app exits from the tray menu or JTail's File → Exit.
/// </summary>
public partial class App : Application
{
    private readonly StartupManager _startup = new("JTools", Program.StartInTrayArgument);
    private readonly JToolsSettings _settings = JToolsSettings.Load();

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private ExpanderHost? _expander;
    private MainWindow? _window;
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
            _window.AttachExpander(_expander);
            _window.AttachSettings(_settings);
            _window.ExitRequested += (_, _) => Exit();

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
        var openItem = new NativeMenuItem("Open JTools");
        openItem.Click += (_, _) => ShowMainWindow();

        var expansionItem = new NativeMenuItem("Text expansion enabled")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = expander.Enabled,
            IsEnabled = expander.IsAvailable,
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

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://JTools/Assets/icon.ico"))),
            ToolTipText = "JTools",
            Menu = new NativeMenu
            {
                openItem,
                new NativeMenuItemSeparator(),
                expansionItem,
                startupItem,
                new NativeMenuItemSeparator(),
                exitItem,
            },
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

    private void Exit()
    {
        _window?.CloseForExit();
        _expander?.Dispose();
        _trayIcon?.Dispose();
        _desktop?.Shutdown();
    }
}
