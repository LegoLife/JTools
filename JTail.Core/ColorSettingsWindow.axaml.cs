using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace JTail;

public partial class ColorSettingsWindow : Window
{
    private const double MinContrastDelta = 0.15;

    private LogThemeStore? _themeStore;
    private Action? _onThemeChanged;
    private bool _resetting;

    public ColorSettingsWindow()
    {
        InitializeComponent();
    }

    public ColorSettingsWindow(LogThemeStore themeStore, Action onThemeChanged) : this()
    {
        _themeStore = themeStore;
        _onThemeChanged = onThemeChanged;

        ShowCurrentColors();

        BackgroundPicker.ColorChanged += (_, _) => ApplyChange();
        TextPicker.ColorChanged += (_, _) => ApplyChange();
    }

    /// <summary>Shows the custom colors, or the app theme's log colors when none are set.</summary>
    private void ShowCurrentColors()
    {
        BackgroundPicker.Color = _themeStore!.Background ?? ThemeColor("JLogBackgroundColor", Colors.Black);
        TextPicker.Color = _themeStore.Text ?? ThemeColor("JLogTextColor", Colors.LimeGreen);
        UpdateContrastWarning();
    }

    private Color ThemeColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color ? color : fallback;

    private void ApplyChange()
    {
        if (_resetting)
        {
            return;
        }

        _themeStore!.Update(BackgroundPicker.Color, TextPicker.Color, _themeStore.FontSize);
        _onThemeChanged!();
        UpdateContrastWarning();
    }

    private void UpdateContrastWarning()
    {
        var delta = Math.Abs(RelativeLuminance(BackgroundPicker.Color) - RelativeLuminance(TextPicker.Color));
        ContrastWarningText.IsVisible = delta < MinContrastDelta;
    }

    private static double RelativeLuminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _themeStore!.ResetToDefault();

        // Updating the pickers raises ColorChanged; don't let that save the theme's colors as custom ones.
        _resetting = true;
        ShowCurrentColors();
        _resetting = false;

        _onThemeChanged!();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
