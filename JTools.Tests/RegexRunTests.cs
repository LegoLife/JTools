using System.Text.RegularExpressions;

namespace JTools.Tests;

public class RegexRunTests
{
    private static RegexRunResult.Success Run(string pattern, string input, RegexOptions options = RegexOptions.None) =>
        Assert.IsType<RegexRunResult.Success>(RegexRun.Execute(pattern, options, input, () => false));

    [Fact]
    public void Finds_every_match_in_order()
    {
        var result = Run(@"\d+", "a1b22c");

        Assert.Equal([new MatchSpan(1, 1), new MatchSpan(3, 2)], result.Spans);
        Assert.Equal(["1", "22"], result.Rows.Select(r => r.Value));
    }

    [Fact]
    public void Heads_group_columns_the_way_a_replacement_refers_to_them()
    {
        var result = Run(@"(?<year>\d{4})-(\d{2})", "2026-10");

        // .NET numbers unnamed groups before named ones.
        Assert.Equal(["$1", "${year}"], result.GroupHeaders);
        Assert.Equal(["10", "2026"], result.Rows[0].Groups.Select(g => g.Value));
    }

    [Fact]
    public void Marks_groups_that_did_not_take_part_in_a_match()
    {
        var result = Run("(a)|(b)", "b");

        Assert.False(result.Rows[0].Groups[0].Participated);
        Assert.True(result.Rows[0].Groups[1].Participated);
    }

    [Fact]
    public void Reports_an_invalid_pattern_without_repeating_it()
    {
        var result = Assert.IsType<RegexRunResult.Invalid>(RegexRun.Execute("ab(", RegexOptions.None, "", () => false));

        Assert.Equal(3, result.Offset);
        Assert.StartsWith("Offset 3: ", result.Message);
        Assert.DoesNotContain("Invalid pattern", result.Message);
    }

    [Fact]
    public void Returns_null_when_cancelled()
    {
        Assert.Null(RegexRun.Execute("a", RegexOptions.None, "aaa", () => true));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(5, 0)] // the end of a match counts
    [InlineData(6, -1)]
    [InlineData(10, 1)]
    public void Finds_the_match_at_a_text_offset(int offset, int expected)
    {
        MatchSpan[] spans = [new(2, 3), new(10, 1)];

        Assert.Equal(expected, RegexRun.FindMatchAt(spans, offset));
    }

    [Fact]
    public void Writes_the_pattern_as_a_verbatim_CSharp_string()
    {
        Assert.Equal(@"new Regex(@""\d"")", RegexRun.ToCSharp(@"\d", RegexOptions.None));
        Assert.Equal(@"new Regex(@""a""""b"", RegexOptions.IgnoreCase | RegexOptions.Multiline)",
            RegexRun.ToCSharp(@"a""b", RegexOptions.IgnoreCase | RegexOptions.Multiline));
    }

    [Fact]
    public void Escapes_tabs_and_line_breaks_in_TSV_rows()
    {
        var result = Run(@"a\tb\r\n", "a\tb\r\n");

        Assert.Equal(@"0	0	a\tb\r\n", result.Rows[0].ToTsv());
    }
}
