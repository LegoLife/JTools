using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;

namespace JTools;

/// <summary>
/// While turned on, backs up the data bundle to the user's backup folder every 15 minutes (and on exit),
/// whenever something changed since the newest backup there. Only the newest <see cref="Backups.Keep"/> are kept.
/// </summary>
public sealed class AutoBackup
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private const string Prefix = "jtools-backup";

    private readonly DataBundle _data;
    private readonly JToolsSettings _settings;
    private readonly DispatcherTimer _timer;

    public AutoBackup(DataBundle data, JToolsSettings settings)
    {
        _data = data;
        _settings = settings;
        _timer = new DispatcherTimer { Interval = Interval };
        _timer.Tick += (_, _) => Run();
        if (IsOn)
        {
            _timer.Start();
        }
    }

    public bool IsOn => _settings.AutoBackup && !string.IsNullOrEmpty(_settings.BackupFolder);

    /// <summary>How the last backup went, for the Data menu; null if there's nothing to say yet.</summary>
    public string? Status { get; private set; }

    /// <summary>Starts backing up to <paramref name="folder"/>, beginning now; false if that first backup failed.</summary>
    public bool TurnOn(string folder)
    {
        _settings.BackupFolder = folder;
        _settings.AutoBackup = true;
        _settings.Save();
        _timer.Start();
        return Run();
    }

    /// <summary>Stops backing up. The folder is remembered for turning it back on.</summary>
    public void TurnOff()
    {
        _settings.AutoBackup = false;
        _settings.Save();
        _timer.Stop();
        Status = null;
    }

    /// <summary>Backs up now if it's on and something changed; false if the backup failed.</summary>
    public bool Run()
    {
        if (!IsOn)
        {
            return true;
        }

        try
        {
            var newest = BackUpIfChanged(_data, _settings.BackupFolder!, DateTimeOffset.Now);
            Status = $"Last backup: {File.GetLastWriteTime(newest):g}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Backup failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Writes a backup to <paramref name="folder"/> unless the newest one there already holds the same data.
    /// Returns the path of the newest backup: the new one, or the one that was already up to date.
    /// </summary>
    public static string BackUpIfChanged(DataBundle data, string folder, DateTimeOffset now)
    {
        var sections = data.ReadSections();
        if (Backups.List(folder, Prefix).FirstOrDefault() is { } newest && HasSections(newest, sections))
        {
            return newest;
        }
        return Backups.Write(folder, Prefix, DataBundle.ToJson(sections, now), now.LocalDateTime);
    }

    private static bool HasSections(string backupPath, JsonObject sections)
    {
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(backupPath))?["sections"], sections);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }
}
