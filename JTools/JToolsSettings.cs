using System.Text.Json;
using Avalonia;
using Avalonia.Styling;

namespace JTools;

public enum AppTheme
{
    Dark,
    Light,

    /// <summary>Follow the Windows light/dark app mode.</summary>
    System,
}

/// <summary>App-wide preferences, saved to %APPDATA%\JTools\settings.json.</summary>
public sealed class JToolsSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JTools", "settings.json");

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public static JToolsSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), JsonContext.Default.JToolsSettings) ?? new JToolsSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new JToolsSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonContext.Default.JToolsSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; a failed write just means they reset next launch.
        }
    }

    /// <summary>Switches every window to <see cref="Theme"/>.</summary>
    public void ApplyTheme()
    {
        Application.Current!.RequestedThemeVariant = Theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark,
        };
    }
}
