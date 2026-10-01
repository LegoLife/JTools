using System.Globalization;
using TextExpander;

namespace JTools.Tests;

public class ExpansionTests
{
    private static Expansion Render(string template) =>
        SnippetTemplate.Render(template, new DateTime(2026, 10, 1), "clip", CultureInfo.InvariantCulture);

    [Fact]
    public void Stops_at_each_input_until_its_value_is_filled_in()
    {
        var expansion = Render("Hello {input:name}, from {input:country}!");

        Assert.Equal("Hello ", expansion.Continue());
        Assert.Equal(new Field("name"), expansion.WaitingAt);
        Assert.False(expansion.IsComplete);

        expansion.Fill("Jake");
        Assert.Equal(", from ", expansion.Continue());
        Assert.Equal(new Field("country"), expansion.WaitingAt);

        expansion.Fill("Canada");
        Assert.Equal("!", expansion.Continue());
        Assert.Null(expansion.WaitingAt);
        Assert.True(expansion.IsComplete);
        Assert.Equal("Hello Jake, from Canada!", expansion.Text);
    }

    [Fact]
    public void An_input_with_the_same_name_repeats_the_first_value()
    {
        var expansion = Render("My name is {input:name}. I repeat, {INPUT:Name}.");

        expansion.Continue();
        expansion.Fill("Jake");

        Assert.Equal(". I repeat, Jake.", expansion.Continue());
        Assert.True(expansion.IsComplete);
    }

    [Fact]
    public void An_input_without_a_name_always_stops()
    {
        var expansion = Render("{input} and {input: }");

        Assert.Equal("", expansion.Continue());
        Assert.Equal(new Field(null), expansion.WaitingAt);
        expansion.Fill("a");
        Assert.Equal(" and ", expansion.Continue());
        Assert.Equal(new Field(null), expansion.WaitingAt);
    }

    [Fact]
    public void A_field_label_is_its_name_or_input()
    {
        Assert.Equal("first name", new Field("first name").Label);
        Assert.Equal("input", new Field(null).Label);
    }

    [Fact]
    public void Other_placeholders_are_filled_in_around_inputs()
    {
        var expansion = Render("{date:yyyy} {input:x} {clipboard}");

        Assert.Equal("2026 ", expansion.Continue());
        expansion.Fill("y");
        Assert.Equal(" clip", expansion.Continue());
    }

    [Fact]
    public void Cursor_counts_steps_back_over_the_values_typed_after_it()
    {
        var expansion = Render("To: {cursor}\nRe: {input:subject}");

        expansion.Continue();
        expansion.Fill("Hi there");
        expansion.Continue();

        Assert.Equal("\nRe: Hi there".Length, expansion.CaretStepsBack);
    }

    [Fact]
    public void Input_with_a_double_brace_is_literal()
    {
        var expansion = Render("{{input}");

        Assert.Equal("{input}", expansion.Continue());
        Assert.True(expansion.IsComplete);
    }

    [Fact]
    public void Continue_while_waiting_for_a_value_throws()
    {
        var expansion = Render("{input}");
        expansion.Continue();

        Assert.Throws<InvalidOperationException>(() => expansion.Continue());
    }
}
