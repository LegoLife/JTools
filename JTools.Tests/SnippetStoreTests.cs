using TextExpander;

namespace JTools.Tests;

public class SnippetStoreTests
{
    internal static SnippetStore StoreWith(params (string Abbreviation, string Expansion)[] snippets)
    {
        var store = new SnippetStore();
        store.Snippets.AddRange(snippets.Select(s => new Snippet { Abbreviation = s.Abbreviation, Expansion = s.Expansion }));
        return store;
    }

    [Theory]
    [InlineData("brb")]
    [InlineData("(brb")]
    [InlineData("see you, brb")]
    public void Matches_an_abbreviation_that_starts_at_a_word_boundary(string typed)
    {
        var store = StoreWith(("brb", "be right back"));

        Assert.Equal("brb", store.FindMatch(typed)?.Abbreviation);
    }

    [Theory]
    [InlineData("abrb")]
    [InlineData("2brb")]
    [InlineData("br")]
    [InlineData("brbx")]
    public void Ignores_an_abbreviation_inside_a_longer_word(string typed)
    {
        var store = StoreWith(("brb", "be right back"));

        Assert.Null(store.FindMatch(typed));
    }

    [Fact]
    public void Is_case_sensitive()
    {
        var store = StoreWith(("brb", "be right back"));

        Assert.Null(store.FindMatch("BRB"));
    }

    [Fact]
    public void Prefers_the_longest_matching_abbreviation()
    {
        var store = StoreWith(("b", "short"), (";b", "long"));

        Assert.Equal("long", store.FindMatch(";b")?.Expansion);
    }

    [Fact]
    public void Never_matches_an_empty_abbreviation()
    {
        var store = StoreWith(("", "nothing"));

        Assert.Null(store.FindMatch("anything"));
    }
}
