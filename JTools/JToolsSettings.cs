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

/// <summary>A tool in the side bar.</summary>
public enum Tool
{
    Tail,
    Expander,
    Regex,
}

/// <summary>App-wide preferences, saved to %APPDATA%\JTools\settings.json.</summary>
public sealed class JToolsSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JTools", "settings.json");

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Tools turned off in the side bar's Tools menu: hidden, and not running.</summary>
    public List<Tool> DisabledTools { get; set; } = [];

    /// <summary>The tool used last, shown when JTools starts.</summary>
    public Tool LastTool { get; set; } = Tool.Tail;

    /// <summary>Whether to back up to <see cref="BackupFolder"/> every 15 minutes and on exit.</summary>
    public bool AutoBackup { get; set; }

    /// <summary>Where automatic backups go; null until the user picks a folder.</summary>
    public string? BackupFolder { get; set; }

    public bool IsEnabled(Tool tool) => !DisabledTools.Contains(tool);

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
