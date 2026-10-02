namespace JTools;

/// <summary>Timestamped copies of the data bundle in a folder, of which only the newest few are kept.</summary>
public static class Backups
{
    public const int Keep = 3;

    /// <summary>
    /// Writes <paramref name="json"/> as <c>{prefix}-yyyy-MM-dd_HHmmss.json</c> in <paramref name="folder"/>, then
    /// deletes all but the newest <see cref="Keep"/> files with that prefix. Returns the new file's path.
    /// </summary>
    public static string Write(string folder, string prefix, string json, DateTime now)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{prefix}-{now:yyyy-MM-dd_HHmmss}.json");
        File.WriteAllText(path, json);

        foreach (var old in List(folder, prefix).Skip(Keep))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An old backup that can't be deleted (e.g. open elsewhere) is tried again next time.
            }
        }
        return path;
    }

    /// <summary>The backups with this prefix, newest first. The timestamped names sort by date.</summary>
    public static IEnumerable<string> List(string folder, string prefix)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, $"{prefix}-*.json")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal);
    }
}
