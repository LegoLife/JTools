using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace JTail;

/// <summary>Log pane colors (custom, theme-following, or a preset) and font size, with a live preview.</summary>
public partial class LogAppearanceWindow : Window
{
    private const double MinContrastDelta = 0.15;

    private LogThemeStore? _themeStore;
    private Action? _onColorsChanged;
    private Action<double>? _onFontSizeChanged;
    private bool _updatingControls;

    public LogAppearanceWindow()
    {
        InitializeComponent();
    }

    public LogAppearanceWindow(LogThemeStore themeStore, Action onColorsChanged, Action<double> onFontSizeChanged) : this()
    {
        _themeStore = themeStore;
        _onColorsChanged = onColorsChanged;
        _onFontSizeChanged = onFontSizeChanged;

        ShowCurrentValues();

        BackgroundPicker.ColorChanged += (_, _) => OnColorPicked();
        TextPicker.ColorChanged += (_, _) => OnColorPicked();
        FontSizeUpDown.ValueChanged += (_, _) => OnFontSizePicked();
    }

    /// <summary>Shows the stored values; colors fall back to the app theme's log colors when none are set.</summary>
    private void ShowCurrentValues()
    {
        // Updating the controls raises their change events; don't let that save the theme's colors as custom ones.
        _updatingControls = true;
        BackgroundPicker.Color = _themeStore!.Background ?? ThemeColor("JLogBackgroundColor", Colors.Black);
        TextPicker.Color = _themeStore.Text ?? ThemeColor("JLogTextColor", Colors.White);
        FontSizeUpDown.Value = (decimal)_themeStore.FontSize;
        _updatingControls = false;

        UpdatePreview();
    }

    private Color ThemeColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color ? color : fallback;

    private void OnColorPicked()
    {
        if (_updatingControls)
        {
            return;
        }

        _themeStore!.Update(BackgroundPicker.Color, TextPicker.Color, _themeStore.FontSize);
        _onColorsChanged!();
        UpdatePreview();
    }

    private void OnFontSizePicked()
    {
        if (_updatingControls)
        {
            return;
        }

        var fontSize = (double)(FontSizeUpDown.Value ?? (decimal)LogThemeStore.DefaultFontSize);
        _themeStore!.Update(_themeStore.Background, _themeStore.Text, fontSize);
        _onFontSizeChanged!(fontSize);
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        PreviewBorder.Background = new SolidColorBrush(BackgroundPicker.Color);
        PreviewText.Foreground = new SolidColorBrush(TextPicker.Color);
        PreviewText.FontSize = _themeStore!.FontSize;

        var delta = Math.Abs(RelativeLuminance(BackgroundPicker.Color) - RelativeLuminance(TextPicker.Color));
        ContrastWarning.IsVisible = delta < MinContrastDelta;
    }

    private static double RelativeLuminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

    private void OnThemePresetClick(object? sender, RoutedEventArgs e) => SetColors(null, null);

    private void OnMatrixPresetClick(object? sender, RoutedEventArgs e) =>
        SetColors(LogThemeStore.MatrixBackground, LogThemeStore.MatrixText);

    private void SetColors(Color? background, Color? text)
    {
        _themeStore!.Update(background, text, _themeStore.FontSize);
        ShowCurrentValues();
        _onColorsChanged!();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _themeStore!.ResetToDefault();
        ShowCurrentValues();
        _onColorsChanged!();
        _onFontSizeChanged!(_themeStore.FontSize);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
