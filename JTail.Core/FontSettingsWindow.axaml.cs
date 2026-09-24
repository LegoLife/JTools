using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JTail;

public partial class FontSettingsWindow : Window
{
    private LogThemeStore? _themeStore;
    private Action<double>? _onFontSizeChanged;

    public FontSettingsWindow()
    {
        InitializeComponent();
    }

    public FontSettingsWindow(LogThemeStore themeStore, Action<double> onFontSizeChanged) : this()
    {
        _themeStore = themeStore;
        _onFontSizeChanged = onFontSizeChanged;

        FontSizeUpDown.Value = (decimal)themeStore.FontSize;

        FontSizeUpDown.ValueChanged += (_, _) => ApplyChange();
    }

    private void ApplyChange()
    {
        var fontSize = (double)(FontSizeUpDown.Value ?? (decimal)LogThemeStore.DefaultFontSize);

        _themeStore!.Update(_themeStore.Background, _themeStore.Text, fontSize);
        _onFontSizeChanged!(fontSize);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _themeStore!.Update(_themeStore.Background, _themeStore.Text, LogThemeStore.DefaultFontSize);
        FontSizeUpDown.Value = (decimal)_themeStore.FontSize;
        _onFontSizeChanged!(_themeStore.FontSize);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
