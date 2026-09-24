using System.Text.Json;

namespace JTail;

/// <summary>
/// Persists the most-recently-opened file paths as JSON in %AppData%\JTail\recent.json.
/// </summary>
public sealed class RecentFilesStore
{
    private const int MaxEntries = 10;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JTail",
        "recent.json");

    private readonly List<string> _paths;

    private RecentFilesStore(List<string> paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<string> Paths => _paths;

    public static RecentFilesStore Load()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            var paths = JsonSerializer.Deserialize<List<string>>(json);
            return new RecentFilesStore(paths ?? []);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new RecentFilesStore([]);
        }
    }

    /// <summary>
    /// Moves the path to the front of the list, adding it if new, and persists the result.
    /// </summary>
    public void Add(string path)
    {
        _paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _paths.Insert(0, path);

        if (_paths.Count > MaxEntries)
        {
            _paths.RemoveRange(MaxEntries, _paths.Count - MaxEntries);
        }

        Save();
    }

    /// <summary>
    /// Removes a path that turned out not to exist on disk, and persists the result.
    /// </summary>
    public void Remove(string path)
    {
        _paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_paths));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting recent files is a convenience; a failed write just means the list resets next launch.
        }
    }
}
