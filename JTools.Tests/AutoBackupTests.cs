namespace JTools.Tests;

public sealed class AutoBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "JToolsTests", Guid.NewGuid().ToString("N"));

    private string AppData => Path.Combine(_root, "AppData");

    private string Folder => Path.Combine(_root, "Backups");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteSnippets(string json)
    {
        var path = Path.Combine(AppData, "TextExpander", "snippets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    [Fact]
    public void Writes_nothing_when_nothing_changed()
    {
        WriteSnippets("""[{ "Abbreviation": "brb", "Expansion": "be right back" }]""");
        var data = new DataBundle(AppData);
        var start = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

        var first = AutoBackup.BackUpIfChanged(data, Folder, start);
        var second = AutoBackup.BackUpIfChanged(data, Folder, start.AddMinutes(15));

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(Folder));
    }

    [Fact]
    public void Writes_a_new_backup_once_something_changed()
    {
        WriteSnippets("""[{ "Abbreviation": "brb", "Expansion": "be right back" }]""");
        var data = new DataBundle(AppData);
        var start = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        AutoBackup.BackUpIfChanged(data, Folder, start);

        WriteSnippets("""[{ "Abbreviation": "ty", "Expansion": "thank you" }]""");
        var newest = AutoBackup.BackUpIfChanged(data, Folder, start.AddMinutes(15));

        Assert.Equal(2, Directory.GetFiles(Folder).Length);
        Assert.Contains("thank you", File.ReadAllText(newest));
    }

    [Fact]
    public void Writes_a_backup_the_importer_accepts()
    {
        WriteSnippets("""[{ "Abbreviation": "brb", "Expansion": "be right back" }]""");
        var data = new DataBundle(AppData);

        var path = AutoBackup.BackUpIfChanged(data, Folder, DateTimeOffset.Now);

        Assert.Contains("expandSnippets", data.Parse(File.ReadAllText(path)).Keys);
    }

    [Fact]
    public void Never_exports_where_this_PC_backs_up_to()
    {
        var settings = Path.Combine(AppData, "JTools", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, """{ "Theme": "Light", "AutoBackup": true, "BackupFolder": "D:\\Backups" }""");

        var exported = new DataBundle(AppData).Export(DateTimeOffset.Now);

        Assert.Contains("Light", exported);
        Assert.DoesNotContain("Backups", exported);
        Assert.DoesNotContain("AutoBackup", exported);
    }

    [Fact]
    public void Importing_keeps_where_this_PC_backs_up_to()
    {
        var settings = Path.Combine(AppData, "JTools", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, """{ "Theme": "Dark", "AutoBackup": true, "BackupFolder": "D:\\Backups" }""");
        var data = new DataBundle(AppData);
        var json = """{ "app": "JTools", "version": 1, "sections": { "settings": { "Theme": "Light", "AutoBackup": false, "BackupFolder": "E:\\Other" } } }""";

        data.Apply(data.Parse(json));

        var written = File.ReadAllText(settings);
        Assert.Contains("Light", written);
        Assert.Contains("""D:\\Backups""", written);
        Assert.Contains("true", written);
        Assert.DoesNotContain("Other", written);
    }
}
