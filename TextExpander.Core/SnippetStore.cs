using System.Text.Json;

namespace TextExpander;

public sealed class Snippet
{
    public string Abbreviation { get; set; } = "";
    public string Expansion { get; set; } = "";
}

/// <summary>Loads and saves abbreviations to %APPDATA%\TextExpander\snippets.json.</summary>
public sealed class SnippetStore
{
    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextExpander", "snippets.json");

    public List<Snippet> Snippets { get; private set; } = [];

    /// <returns>true if this is the first run (no file existed yet).</returns>
    public bool Load()
    {
        if (!File.Exists(FilePath))
        {
            Snippets =
            [
                new() { Abbreviation = "brb", Expansion = "be right back" },
                new() { Abbreviation = ";sig", Expansion = "Best regards,\r\nYour Name" },
                new() { Abbreviation = ";addr", Expansion = "123 Main Street\r\nAnytown, USA 12345" },
            ];
            Save();
            return true;
        }

        var json = File.ReadAllText(FilePath);
        Snippets = JsonSerializer.Deserialize(json, JsonContext.Default.ListSnippet) ?? [];
        return false;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(Snippets, JsonContext.Default.ListSnippet));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    /// <summary>
    /// Finds the snippet whose abbreviation ends the typed word. The abbreviation must start at a
    /// word boundary, so "brb" matches "brb" and "(brb" but not "abrb". Longest match wins.
    /// </summary>
    public Snippet? FindMatch(string typedWord)
    {
        Snippet? best = null;
        foreach (var snippet in Snippets)
        {
            var abbreviation = snippet.Abbreviation;
            if (abbreviation.Length == 0 || !typedWord.EndsWith(abbreviation, StringComparison.Ordinal))
                continue;

            int start = typedWord.Length - abbreviation.Length;
            if (start > 0 && char.IsLetterOrDigit(typedWord[start - 1]))
                continue;

            if (best is null || abbreviation.Length > best.Abbreviation.Length)
                best = snippet;
        }
        return best;
    }
}
