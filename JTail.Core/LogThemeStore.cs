using System.Text.Json;
using Avalonia.Media;

namespace JTail;

/// <summary>
/// Persists the user's chosen log pane background/text colors as JSON in %AppData%\JTail\theme.json.
/// A null color means "not customized": the log pane uses the app theme's colors (JLogBackgroundBrush /
/// JLogTextBrush in Themes/Palette.axaml) and follows dark/light switches.
/// </summary>
public sealed class LogThemeStore
{
    public const double DefaultFontSize = 13;
    public const double MinFontSize = 8;
    public const double MaxFontSize = 32;

    // Before the app had light/dark themes, these defaults were saved explicitly. Treat them as
    // "not customized" so those users' log panes follow the theme too. Only files from before
    // CurrentVersion get this treatment, since the same colors are now the "Matrix" preset.
    private static readonly Color LegacyDefaultBackground = Color.Parse("#000000");
    private static readonly Color LegacyDefaultText = Color.Parse("#33FF66");
    private const int CurrentVersion = 2;

    /// <summary>The original green-on-black look, offered as a preset.</summary>
    public static readonly Color MatrixBackground = LegacyDefaultBackground;
    public static readonly Color MatrixText = LegacyDefaultText;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JTail",
        "theme.json");

    public Color? Background { get; private set; }

    public Color? Text { get; private set; }

    public double FontSize { get; private set; }

    private LogThemeStore(Color? background, Color? text, double fontSize)
    {
        Background = background;
        Text = text;
        FontSize = fontSize;
    }

    public static LogThemeStore Load()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<ThemeData>(json);
            if (data is null)
            {
                return new LogThemeStore(null, null, DefaultFontSize);
            }

            var fontSize = data.FontSize is >= MinFontSize and <= MaxFontSize ? data.FontSize : DefaultFontSize;
            var isLegacy = data.Version < CurrentVersion;
            var background = ParseCustom(data.Background, isLegacy ? LegacyDefaultBackground : null);
            var text = ParseCustom(data.Text, isLegacy ? LegacyDefaultText : null);
            return new LogThemeStore(background, text, fontSize);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or FormatException)
        {
            return new LogThemeStore(null, null, DefaultFontSize);
        }
    }

    private static Color? ParseCustom(string? value, Color? legacyDefault)
    {
        if (value is null)
        {
            return null;
        }

        var color = Color.Parse(value);
        return color == legacyDefault ? null : color;
    }

    public void Update(Color? background, Color? text, double fontSize)
    {
        Background = background;
        Text = text;
        FontSize = fontSize;
        Save();
    }

    /// <summary>Clears custom colors (back to following the app theme) and resets the font size.</summary>
    public void ResetToDefault()
    {
        Update(null, null, DefaultFontSize);
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            var data = new ThemeData(Background?.ToString(), Text?.ToString(), FontSize, CurrentVersion);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the theme is a convenience; a failed write just means it resets next launch.
        }
    }

    private sealed record ThemeData(string? Background, string? Text, double FontSize, int Version = 0);
}
