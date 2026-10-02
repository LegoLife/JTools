namespace JTools.Tests;

public sealed class BackupsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "JToolsTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Keeps_only_the_newest_three()
    {
        var start = new DateTime(2026, 10, 2, 9, 0, 0);
        for (int i = 0; i < 5; i++)
        {
            Backups.Write(_folder, "jtools-backup", $"{i}", start.AddMinutes(15 * i));
        }

        var kept = Backups.List(_folder, "jtools-backup").Select(Path.GetFileName);

        Assert.Equal(
            ["jtools-backup-2026-10-02_100000.json", "jtools-backup-2026-10-02_094500.json", "jtools-backup-2026-10-02_093000.json"],
            kept);
    }

    [Fact]
    public void Only_counts_backups_with_the_same_prefix()
    {
        var now = new DateTime(2026, 10, 2, 9, 0, 0);
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(_folder).FullName, "notes.json"), "{}");
        Backups.Write(_folder, "before-import", "{}", now);
        for (int i = 0; i < 4; i++)
        {
            Backups.Write(_folder, "jtools-backup", "{}", now.AddMinutes(i));
        }

        Assert.Single(Backups.List(_folder, "before-import"));
        Assert.True(File.Exists(Path.Combine(_folder, "notes.json")));
    }
}
