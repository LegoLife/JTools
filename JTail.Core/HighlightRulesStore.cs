using System.Text.Json;
using Avalonia.Media;

namespace JTail;

/// <summary>
/// Persists the user's line-highlight rules (word + color) as JSON in %AppData%\JTail\highlights.json.
/// </summary>
public sealed class HighlightRulesStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JTail",
        "highlights.json");

    private readonly List<HighlightRule> _rules;

    private HighlightRulesStore(List<HighlightRule> rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<HighlightRule> Rules => _rules;

    public static HighlightRulesStore Load()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            var rules = JsonSerializer.Deserialize<List<HighlightRule>>(json);
            return new HighlightRulesStore(rules ?? []);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new HighlightRulesStore([]);
        }
    }

    public void Add(string word, Color color)
    {
        _rules.Add(new HighlightRule(word, color.ToString()));
        Save();
    }

    public void RemoveAt(int index)
    {
        _rules.RemoveAt(index);
        Save();
    }

    public void SetColorAt(int index, Color color)
    {
        _rules[index] = _rules[index] with { ColorHex = color.ToString() };
        Save();
    }

    public void SetWordAt(int index, string word)
    {
        _rules[index] = _rules[index] with { Word = word };
        Save();
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_rules));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting highlight rules is a convenience; a failed write just means they reset next launch.
        }
    }
}
