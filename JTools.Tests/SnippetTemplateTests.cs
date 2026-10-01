using System.Globalization;
using TextExpander;

namespace JTools.Tests;

public class SnippetTemplateTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 14, 5, 9);

    /// <summary>The whole text of an expansion that has no fields, and where its caret goes.</summary>
    private readonly record struct RenderedSnippet(string Text, int? CaretStepsBack);

    private static RenderedSnippet Render(string template, string? clipboard = null, CultureInfo? culture = null)
    {
        var expansion = SnippetTemplate.Render(template, Now, clipboard, culture ?? CultureInfo.InvariantCulture);
        var text = expansion.Continue();
        Assert.True(expansion.IsComplete);
        return new RenderedSnippet(text, expansion.CaretStepsBack);
    }

    [Fact]
    public void Leaves_text_without_placeholders_alone()
    {
        Assert.Equal(new RenderedSnippet("Best regards,\r\nJake", null), Render("Best regards,\r\nJake"));
    }

    [Fact]
    public void Date_and_time_use_the_short_formats_of_the_culture()
    {
        Assert.Equal("10/01/2026 14:05", Render("{date} {time}").Text);
        Assert.Equal("01.10.2026", Render("{date}", culture: new CultureInfo("de-DE")).Text);
    }

    [Theory]
    [InlineData("{date:yyyy-MM-dd}", "2026-10-01")]
    [InlineData("{time:HH:mm:ss}", "14:05:09")]
    [InlineData("{date:dddd}", "Thursday")]
    [InlineData("{date:}", "10/01/2026")]
    public void Date_and_time_take_an_optional_format(string template, string expected)
    {
        Assert.Equal(expected, Render(template).Text);
    }

    [Fact]
    public void Placeholder_names_ignore_case()
    {
        Assert.Equal("2026", Render("{DATE:yyyy}").Text);
    }

    [Fact]
    public void Clipboard_inserts_the_clipboard_text_as_is()
    {
        Assert.Equal("Re: {date} ticket", Render("Re: {clipboard} ticket", clipboard: "{date}").Text);
    }

    [Fact]
    public void Clipboard_without_text_inserts_nothing()
    {
        Assert.Equal("Re: ", Render("Re: {clipboard}", clipboard: null).Text);
    }

    [Fact]
    public void Cursor_is_removed_and_counted_back_from_the_end()
    {
        Assert.Equal(new RenderedSnippet("Hi , thanks", 8), Render("Hi {cursor}, thanks"));
    }

    [Fact]
    public void Cursor_counts_a_line_break_as_one_step()
    {
        Assert.Equal(3, Render("a{cursor}b\r\nc").CaretStepsBack);
        Assert.Equal(3, Render("a{cursor}b\nc").CaretStepsBack);
    }

    [Fact]
    public void Cursor_counts_steps_after_the_other_placeholders_are_filled_in()
    {
        Assert.Equal(new RenderedSnippet("Hi  2026", 5), Render("Hi {cursor} {date:yyyy}"));
    }

    [Fact]
    public void Cursor_at_the_end_needs_no_steps()
    {
        Assert.Equal(0, Render("Dear {cursor}").CaretStepsBack);
    }

    [Fact]
    public void Only_the_first_cursor_counts()
    {
        Assert.Equal(new RenderedSnippet("abc", 2), Render("a{cursor}b{cursor}c"));
    }

    [Theory]
    [InlineData("{{date}", "{date}")]
    [InlineData("{{{date:yyyy}", "{2026")]
    [InlineData("a {{ b", "a { b")]
    public void A_double_brace_is_a_literal_brace(string template, string expected)
    {
        Assert.Equal(expected, Render(template).Text);
    }

    [Theory]
    [InlineData("{foo} and {date:yyyy}", "{foo} and 2026")]
    [InlineData("{date", "{date")]
    [InlineData("a } b", "a } b")]
    [InlineData("{ {date:yyyy}", "{ 2026")]
    [InlineData("{date:%}", "{date:%}")] // not a valid format
    [InlineData("{clipboard:x}", "{clipboard:x}")]
    public void Anything_that_is_not_a_placeholder_is_kept_as_typed(string template, string expected)
    {
        Assert.Equal(expected, Render(template).Text);
    }
}
