using Avalonia.Controls;
using Avalonia.Interactivity;
using TextExpander;

namespace JTools;

/// <summary>Choose whether abbreviations expand immediately or on a trigger key.</summary>
public partial class ExpanderSettingsWindow : Window
{
    private readonly AppSettings? _settings;

    public ExpanderSettingsWindow()
    {
        InitializeComponent();
        TriggerRadio.IsCheckedChanged += (_, _) => TriggerOptions.IsEnabled = TriggerRadio.IsChecked == true;
    }

    public ExpanderSettingsWindow(AppSettings settings) : this()
    {
        _settings = settings;

        ImmediateRadio.IsChecked = settings.Mode == ExpandMode.Immediate;
        TriggerRadio.IsChecked = settings.Mode == ExpandMode.TriggerKey;
        SpaceCheckBox.IsChecked = settings.TriggerOnSpace;
        TabCheckBox.IsChecked = settings.TriggerOnTab;
        EnterCheckBox.IsChecked = settings.TriggerOnEnter;
        KeepTriggerCheckBox.IsChecked = settings.KeepTriggerKey;
        TriggerOptions.IsEnabled = TriggerRadio.IsChecked == true;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        bool triggerMode = TriggerRadio.IsChecked == true;
        bool space = SpaceCheckBox.IsChecked == true;
        bool tab = TabCheckBox.IsChecked == true;
        bool enter = EnterCheckBox.IsChecked == true;

        if (triggerMode && !space && !tab && !enter)
        {
            ErrorText.Text = "Choose at least one trigger key.";
            ErrorBanner.IsVisible = true;
            return;
        }

        _settings.Mode = triggerMode ? ExpandMode.TriggerKey : ExpandMode.Immediate;
        _settings.TriggerOnSpace = space;
        _settings.TriggerOnTab = tab;
        _settings.TriggerOnEnter = enter;
        _settings.KeepTriggerKey = KeepTriggerCheckBox.IsChecked == true;
        _settings.Save();

        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
