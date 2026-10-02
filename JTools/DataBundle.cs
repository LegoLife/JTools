using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media;

namespace JTools;

/// <summary>
/// Everything the tools keep (settings, abbreviations, regexes, highlights) as one versioned JSON file, for
/// export, import and backups. Each section is one data file's JSON, exactly as its tool saved it.
/// </summary>
public sealed class DataBundle
{
    public const int CurrentVersion = 1;

    private const string AppName = "JTools";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly Section[] _sections;

    public DataBundle(string appDataFolder)
    {
        string jtools = Path.Combine(appDataFolder, "JTools");
        string expander = Path.Combine(appDataFolder, "TextExpander");
        string tail = Path.Combine(appDataFolder, "JTail");

        // Tail's recent files are left out: they're paths on this machine.
        _sections =
        [
            new("settings", "app settings", Path.Combine(jtools, "settings.json"),
                node => JsonSerializer.Deserialize(node, JsonContext.Default.JToolsSettings) is not null),
            new("regex", "regexes",Path.Combine(jtools, "regex.json"),
                node => JsonSerializer.Deserialize(node, JsonContext.Default.RegexTesterState) is not null),
            new("expandSettings", "Expand settings", Path.Combine(expander, "settings.json"),
                node => JsonSerializer.Deserialize(node, JsonContext.Default.AppSettings) is not null),
            new("expandSnippets", "abbreviations", Path.Combine(expander, "snippets.json"),
                node => JsonSerializer.Deserialize(node, JsonContext.Default.ListSnippet) is { } snippets
                    && snippets.All(s => s is { Abbreviation: not null, Expansion: not null })),
            new("tailHighlights", "Tail highlights", Path.Combine(tail, "highlights.json"),
                node => JsonSerializer.Deserialize(node, JsonContext.Default.ListHighlightRule) is { } rules
                    && rules.All(r => r is { Word: not null } && Color.TryParse(r.ColorHex, out _))),
            new("tailAppearance", "Tail log appearance", Path.Combine(tail, "theme.json"),
                node => node is JsonObject),
        ];
    }

    public static DataBundle ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    /// <summary>The bundle's JSON, holding every data file that exists.</summary>
    public string Export(DateTimeOffset now)
    {
        var bundle = new JsonObject
        {
            ["app"] = AppName,
            ["version"] = CurrentVersion,
            ["exported"] = now.ToString("O"),
            ["sections"] = ReadSections(),
        };
        return bundle.ToJsonString(Indented);
    }

    /// <summary>Each data file's JSON, by section key. Missing or unreadable files are left out.</summary>
    public JsonObject ReadSections()
    {
        var sections = new JsonObject();
        foreach (var section in _sections)
        {
            try
            {
                sections[section.Key] = JsonNode.Parse(File.ReadAllText(section.FilePath));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Nothing saved yet, or a damaged file the tool itself would ignore.
            }
        }
        return sections;
    }

    /// <summary>
    /// Reads and checks a bundle, returning its sections by key. Throws <see cref="InvalidDataException"/>,
    /// with a message for the user, if any of it can't be imported, so an import is all or nothing.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> Parse(string json)
    {
        JsonObject? bundle;
        try
        {
            bundle = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            bundle = null;
        }

        if (bundle is null
            || !TryGetValue(bundle["app"], out string? app) || app != AppName
            || !TryGetValue(bundle["version"], out int version) || version < 1
            || bundle["sections"] is not JsonObject sections)
        {
            throw new InvalidDataException("This file isn't a JTools export or backup.");
        }

        if (version > CurrentVersion)
        {
            throw new InvalidDataException("This file comes from a newer version of JTools. Update JTools to import it.");
        }

        var result = new Dictionary<string, JsonNode>();
        foreach (var section in _sections)
        {
            if (sections[section.Key] is not { } node)
            {
                continue;
            }

            if (!IsValid(section, node))
            {
                throw new InvalidDataException($"Part of this file ({section.Name}) is damaged, so nothing was imported.");
            }
            result[section.Key] = node.DeepClone();
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("This file has nothing JTools can import.");
        }
        return result;
    }

    /// <summary>The user-facing names of these sections, e.g. "abbreviations, Tail highlights".</summary>
    public string Describe(IEnumerable<string> keys) =>
        string.Join(", ", _sections.Where(s => keys.Contains(s.Key)).Select(s => s.Name));

    /// <summary>Overwrites each data file that has a section in <paramref name="sections"/>, leaving the rest alone.</summary>
    public void Apply(IReadOnlyDictionary<string, JsonNode> sections)
    {
        foreach (var section in _sections)
        {
            if (!sections.TryGetValue(section.Key, out var node))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(section.FilePath)!);
            var tempPath = section.FilePath + ".tmp";
            File.WriteAllText(tempPath, node.ToJsonString(Indented));
            File.Move(tempPath, section.FilePath, overwrite: true);
        }
    }

    private static bool IsValid(Section section, JsonNode node)
    {
        try
        {
            return section.IsValid(node);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static bool TryGetValue<T>(JsonNode? node, out T? value)
    {
        value = default;
        try
        {
            return node is JsonValue jsonValue && jsonValue.TryGetValue(out value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <param name="Key">The section's name in the bundle.</param>
    /// <param name="Name">What the user calls it, in messages.</param>
    private sealed record Section(string Key, string Name, string FilePath, Func<JsonNode, bool> IsValid);
}
