using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JTools;

public partial class MainWindow : Window
{
    private const string AppName = "JTools";

    private bool _closingForExit;

    /// <summary>Raised when a tool asks to exit the whole app (e.g. JTail's File → Exit).</summary>
    public event EventHandler? ExitRequested;

    public MainWindow()
    {
        InitializeComponent();

        TailView.TitleChanged += (_, _) => UpdateTitle();
        TailView.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        ShowTool(TailView);
    }

    private JToolsSettings? _settings;

    public void AttachExpander(ExpanderHost expander) => ExpanderView.Attach(expander);

    public void AttachSettings(JToolsSettings settings)
    {
        _settings = settings;
        UpdateThemeChecks();
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

    private void OnTailNavClick(object? sender, RoutedEventArgs e) => ShowTool(TailView);

    private void OnExpanderNavClick(object? sender, RoutedEventArgs e) => ShowTool(ExpanderView);

    private void ShowTool(Control tool)
    {
        // Tools stay in the visual tree while hidden, so JTail keeps tailing in the background.
        TailView.IsVisible = tool == TailView;
        ExpanderView.IsVisible = tool == ExpanderView;
        TailNavButton.Classes.Set("selected", tool == TailView);
        ExpanderNavButton.Classes.Set("selected", tool == ExpanderView);
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var toolTitle = TailView.IsVisible ? TailView.Title : "Text Expander";
        Title = $"{AppName} — {toolTitle}";
    }

    /// <summary>Closes the window for real (instead of hiding it to the tray).</summary>
    public void CloseForExit()
    {
        _closingForExit = true;
        TailView.StopTailing();
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
