using System.Text.Json;

namespace JTools;

/// <summary>The Regex tester's last pattern, test text and options, saved to %APPDATA%\JTools\regex.json.</summary>
public sealed class RegexTesterState
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JTools", "regex.json");

    // A small example for the first launch, so the tool shows what it does straight away.
    public string Pattern { get; set; } = @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}) (\w+)";

    public string SampleText { get; set; } =
        "2026-09-28 INFO  Service started\n2026-09-29 WARN  Disk at 91%\n2026-09-30 ERROR Connection refused\n";

    public bool IgnoreCase { get; set; }

    public bool Multiline { get; set; }

    public bool Singleline { get; set; }

    public bool IgnorePatternWhitespace { get; set; }

    public bool ExplicitCapture { get; set; }

    public static RegexTesterState Load()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), JsonContext.Default.RegexTesterState) ?? new RegexTesterState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new RegexTesterState();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonContext.Default.RegexTesterState));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Like settings, this is a convenience; a failed write just means the tester starts fresh next launch.
        }
    }
}
