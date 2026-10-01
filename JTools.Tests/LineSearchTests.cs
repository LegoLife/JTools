using JTail;

namespace JTools.Tests;

public class LineSearchTests
{
    private static LineSearch Search(string text, bool matchCase = false, bool isRegex = false)
    {
        var search = LineSearch.Create(text, matchCase, isRegex, out var error);
        Assert.Null(error);
        return Assert.IsType<LineSearch>(search);
    }

    [Fact]
    public void Empty_text_searches_for_nothing()
    {
        Assert.Null(LineSearch.Create("", false, true, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Ignores_case_unless_told_to_match_it()
    {
        Assert.True(Search("error").IsMatch("ERROR here"));
        Assert.False(Search("error", matchCase: true).IsMatch("ERROR here"));
    }

    [Fact]
    public void Plain_text_takes_regex_characters_literally()
    {
        Assert.False(Search("a.b").IsMatch("axb"));
        Assert.True(Search("a.b").IsMatch("a.b"));
    }

    [Fact]
    public void Regex_anchors_apply_to_each_line()
    {
        Assert.True(Search(@"^\d+", isRegex: true).IsMatch("12 x"));
        Assert.False(Search(@"^\d+", isRegex: true).IsMatch("x 12"));
    }

    [Fact]
    public void Reports_an_invalid_regex()
    {
        Assert.Null(LineSearch.Create("(", false, true, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Finds_matches_at_document_offsets()
    {
        var hits = Search("foo").FindAll("foo bar\r\nbar foo\n", baseOffset: 100);

        Assert.Equal([new TextSpan(100, 3), new TextSpan(113, 3)], hits);
    }

    [Fact]
    public void End_of_line_anchor_matches_before_a_carriage_return()
    {
        Assert.Equal([new TextSpan(2, 3)], Search("foo$", isRegex: true).FindAll("a foo\r\nb", 0));
    }

    [Fact]
    public void Skips_empty_matches()
    {
        Assert.Empty(Search("x*", isRegex: true).FindAll("ab\ncd", 0));
    }
}

public class LineFilterTests
{
    private static LineFilter Filter(string text) => new(LineSearch.Create(text, false, false, out _)!);

    [Fact]
    public void Keeps_matching_lines_with_their_line_breaks()
    {
        var filter = Filter("error");

        Assert.Equal("ERROR b\r\n", filter.Append("INFO a\r\nERROR b\r\nINFO c\r\n"));
        Assert.Equal(1, filter.ShownLines);
        Assert.Equal(2, filter.HiddenLines);
    }

    [Fact]
    public void Judges_a_line_only_once_it_is_complete()
    {
        var filter = Filter("error");

        Assert.Equal("", filter.Append("ERR"));
        Assert.Equal("ERROR here\n", filter.Append("OR here\nINFO"));
        Assert.Equal(0, filter.HiddenLines); // "INFO" is still incomplete
        Assert.Equal("", filter.Append(" done\n"));
        Assert.Equal(1, filter.HiddenLines);
    }

    [Fact]
    public void Counts_empty_lines_as_hidden()
    {
        var filter = Filter("error");

        filter.Append("\n\n");

        Assert.Equal(2, filter.HiddenLines);
    }
}
