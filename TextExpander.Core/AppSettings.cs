using System.Text.Json;
using System.Text.Json.Serialization;

namespace TextExpander;

public enum ExpandMode
{
    /// <summary>Expand as soon as the last character of the abbreviation is typed.</summary>
    Immediate,

    /// <summary>Expand when a trigger key (Space/Tab/Enter) is pressed after the abbreviation.</summary>
    TriggerKey,
}

/// <summary>User preferences, saved to %APPDATA%\TextExpander\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextExpander", "settings.json");

    public ExpandMode Mode { get; set; } = ExpandMode.TriggerKey;
    public bool TriggerOnSpace { get; set; } = true;
    public bool TriggerOnTab { get; set; } = true;
    public bool TriggerOnEnter { get; set; } = true;

    /// <summary>Whether the trigger key is typed after the expansion (e.g. keep the space).</summary>
    public bool KeepTriggerKey { get; set; } = true;

    /// <summary>Raised after the settings are saved.</summary>
    public event Action? Changed;

    public static AppSettings Load()
    {
        if (!File.Exists(FilePath))
            return new AppSettings();

        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        Changed?.Invoke();
    }

    public bool IsTriggerKey(int vk) => vk switch
    {
        NativeMethods.VK_SPACE => TriggerOnSpace,
        NativeMethods.VK_TAB => TriggerOnTab,
        NativeMethods.VK_RETURN => TriggerOnEnter,
        _ => false,
    };

    /// <summary>Human-readable description of how expansion is triggered, e.g. "Space or Tab".</summary>
    public string DescribeTriggerKeys()
    {
        var keys = new List<string>();
        if (TriggerOnSpace) keys.Add("Space");
        if (TriggerOnTab) keys.Add("Tab");
        if (TriggerOnEnter) keys.Add("Enter");
        return keys.Count switch
        {
            0 => "(no key selected)",
            1 => keys[0],
            _ => string.Join(", ", keys[..^1]) + " or " + keys[^1],
        };
    }
}
