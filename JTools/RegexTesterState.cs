using System.Text.Json;

namespace JTools;

/// <summary>A pattern, its test text and options, kept by name in the Regex tester's library.</summary>
public sealed class SavedRegex
{
    public string Name { get; set; } = "";

    public string Pattern { get; set; } = "";

    public string SampleText { get; set; } = "";

    public bool IgnoreCase { get; set; }

    public bool Multiline { get; set; }

    public bool Singleline { get; set; }

    public bool IgnorePatternWhitespace { get; set; }

    public bool ExplicitCapture { get; set; }

    /// <summary>Whether the two hold the same pattern, test text and options, whatever their names.</summary>
    public bool HasSameContentAs(SavedRegex other) =>
        Pattern == other.Pattern && SampleText == other.SampleText &&
        IgnoreCase == other.IgnoreCase && Multiline == other.Multiline && Singleline == other.Singleline &&
        IgnorePatternWhitespace == other.IgnorePatternWhitespace && ExplicitCapture == other.ExplicitCapture;
}

/// <summary>
/// The Regex tester's working copy (its last pattern, test text and options) and its library of saved
/// regexes, saved to %APPDATA%\JTools\regex.json.
/// </summary>
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

    /// <summary>Saved regexes, sorted by name.</summary>
    public List<SavedRegex> Library { get; set; } = [];

    /// <summary>The library entry last loaded or saved, which Save overwrites; null if there's none.</summary>
    public string? LoadedName { get; set; }

    /// <summary>The loaded library entry, if it still exists.</summary>
    public SavedRegex? Loaded => LoadedName is null ? null : Find(LoadedName);

    /// <summary>Whether the working copy differs from the loaded library entry.</summary>
    public bool IsModified => Loaded is { } loaded && !loaded.HasSameContentAs(WorkingCopy(loaded.Name));

    /// <summary>The library entry with this name; names are compared ignoring case.</summary>
    public SavedRegex? Find(string name) =>
        Library.Find(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Saves the working copy to the library under this name, replacing any entry with that name.</summary>
    public void SaveToLibrary(string name)
    {
        Library.RemoveAll(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        Library.Add(WorkingCopy(name));
        Library.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        LoadedName = name;
    }

    /// <summary>Replaces the working copy with a library entry.</summary>
    public void LoadFromLibrary(SavedRegex entry)
    {
        Pattern = entry.Pattern;
        SampleText = entry.SampleText;
        IgnoreCase = entry.IgnoreCase;
        Multiline = entry.Multiline;
        Singleline = entry.Singleline;
        IgnorePatternWhitespace = entry.IgnorePatternWhitespace;
        ExplicitCapture = entry.ExplicitCapture;
        LoadedName = entry.Name;
    }

    /// <summary>Removes the loaded entry from the library. The working copy stays as it is.</summary>
    public void DeleteLoaded()
    {
        if (Loaded is { } loaded)
        {
            Library.Remove(loaded);
        }
        LoadedName = null;
    }

    private SavedRegex WorkingCopy(string name) => new()
    {
        Name = name,
        Pattern = Pattern,
        SampleText = SampleText,
        IgnoreCase = IgnoreCase,
        Multiline = Multiline,
        Singleline = Singleline,
        IgnorePatternWhitespace = IgnorePatternWhitespace,
        ExplicitCapture = ExplicitCapture,
    };

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
