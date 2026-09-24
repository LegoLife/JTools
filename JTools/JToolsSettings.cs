using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JTools", "settings.json");

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public static JToolsSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<JToolsSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new JToolsSettings();
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
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
