using System.Text.Json.Nodes;

namespace JTools.Tests;

public sealed class DataBundleTests : IDisposable
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "JToolsTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_appData))
        {
            Directory.Delete(_appData, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string json)
    {
        var path = Path.Combine(_appData, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private string ReadFile(string relativePath) => File.ReadAllText(Path.Combine(_appData, relativePath));

    private void WriteEveryFile()
    {
        WriteFile(@"JTools\settings.json", """{ "Theme": "Light", "DisabledTools": ["Regex"], "LastTool": "Expander" }""");
        WriteFile(@"JTools\regex.json", """{ "Pattern": "\\d+", "SampleText": "a1", "IgnoreCase": true }""");
        WriteFile(@"TextExpander\settings.json", """{ "Mode": "Immediate", "TriggerOnSpace": false }""");
        WriteFile(@"TextExpander\snippets.json", """[{ "Abbreviation": "brb", "Expansion": "be right back" }]""");
        WriteFile(@"JTail\highlights.json", """[{ "Word": "ERROR", "ColorHex": "#ff800000" }]""");
        WriteFile(@"JTail\theme.json", """{ "Background": null, "Text": null, "FontSize": 15, "Version": 2 }""");
        WriteFile(@"JTail\recent.json", """["C:\\logs\\app.log"]""");
    }

    [Fact]
    public void Importing_an_export_restores_every_data_file()
    {
        WriteEveryFile();
        var bundle = new DataBundle(_appData);
        var exported = bundle.Export(DateTimeOffset.Now);
        var original = bundle.ReadSections();

        WriteFile(@"TextExpander\snippets.json", "[]");
        WriteFile(@"JTail\highlights.json", "[]");
        WriteFile(@"JTools\regex.json", "{}");
        bundle.Apply(bundle.Parse(exported));

        Assert.True(JsonNode.DeepEquals(original, bundle.ReadSections()));
        Assert.Equal(6, original.Count);
    }

    [Fact]
    public void Leaves_out_recent_files()
    {
        WriteEveryFile();

        var exported = JsonNode.Parse(new DataBundle(_appData).Export(DateTimeOffset.Now))!;

        Assert.DoesNotContain("app.log", exported.ToJsonString());
    }

    [Fact]
    public void Leaves_alone_data_files_the_bundle_has_no_section_for()
    {
        WriteFile(@"TextExpander\snippets.json", """[{ "Abbreviation": "brb", "Expansion": "be right back" }]""");
        var exported = new DataBundle(_appData).Export(DateTimeOffset.Now);
        WriteFile(@"JTail\highlights.json", """[{ "Word": "WARN", "ColorHex": "#ff808000" }]""");

        var bundle = new DataBundle(_appData);
        bundle.Apply(bundle.Parse(exported));

        Assert.Contains("WARN", ReadFile(@"JTail\highlights.json"));
    }

    [Fact]
    public void Rejects_a_file_from_a_newer_version()
    {
        var json = $$"""{ "app": "JTools", "version": {{DataBundle.CurrentVersion + 1}}, "sections": { "expandSnippets": [] } }""";

        var error = Assert.Throws<InvalidDataException>(() => new DataBundle(_appData).Parse(json));
        Assert.Contains("newer version", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "version": 1, "sections": {} }""")]
    [InlineData("""{ "app": "Other", "version": 1, "sections": {} }""")]
    [InlineData("""{ "app": "JTools", "version": "1", "sections": {} }""")]
    [InlineData("""{ "app": "JTools", "version": 1, "sections": {} }""")]
    public void Rejects_a_file_that_is_not_a_JTools_bundle(string json)
    {
        Assert.Throws<InvalidDataException>(() => new DataBundle(_appData).Parse(json));
    }

    [Theory]
    [InlineData("expandSnippets", "{}")]
    [InlineData("expandSnippets", "[null]")]
    [InlineData("tailHighlights", """[{ "Word": "ERROR", "ColorHex": "not a color" }]""")]
    [InlineData("settings", """{ "Theme": "Purple" }""")]
    public void Rejects_the_whole_file_when_a_section_is_damaged(string key, string damaged)
    {
        // A good section alongside the damaged one, which mustn't be imported on its own.
        var json = $$"""{ "app": "JTools", "version": 1, "sections": { "regex": { "Pattern": "x" }, "{{key}}": {{damaged}} } }""";

        var error = Assert.Throws<InvalidDataException>(() => new DataBundle(_appData).Parse(json));
        Assert.Contains("nothing was imported", error.Message);
    }
}
