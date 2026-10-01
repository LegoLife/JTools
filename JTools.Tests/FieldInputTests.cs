using TextExpander;

namespace JTools.Tests;

public class FieldInputTests
{
    [Fact]
    public void Starts_as_the_label_selected()
    {
        var field = new FieldInput("name");

        Assert.Equal("name", field.Value);
        Assert.True(field.IsSelected);
        Assert.Equal(1, field.StepsToEnd); // Right collapses the selection to its end
    }

    [Fact]
    public void Typing_replaces_the_label()
    {
        var field = new FieldInput("name");

        field.Type("J");
        field.Type("ake");

        Assert.Equal("Jake", field.Value);
        Assert.False(field.IsSelected);
        Assert.Equal(0, field.StepsToEnd);
    }

    [Fact]
    public void Backspace_first_deletes_the_label_then_characters()
    {
        var field = new FieldInput("name");

        field.Backspace();
        Assert.Equal("", field.Value);
        field.Backspace(); // deletes text before the field; the value stays empty
        field.Type("ab");
        field.Backspace();

        Assert.Equal("a", field.Value);
    }

    [Fact]
    public void Left_and_Right_collapse_the_selection_and_keep_the_label()
    {
        var left = new FieldInput("name");
        left.Left();
        Assert.Equal("name", left.Value);
        Assert.Equal(4, left.StepsToEnd);

        var right = new FieldInput("name");
        right.Right();
        Assert.Equal(0, right.StepsToEnd);
    }

    [Fact]
    public void Typing_inserts_at_the_caret()
    {
        var field = new FieldInput("x");
        field.Type("Jke");
        field.Left();
        field.Left();
        field.Type("a");

        Assert.Equal("Jake", field.Value);
        Assert.Equal(2, field.StepsToEnd);
    }

    [Fact]
    public void Delete_removes_the_character_after_the_caret()
    {
        var field = new FieldInput("x");
        field.Type("Jaake");
        field.Home();
        field.Right();
        field.Delete();
        field.End();

        Assert.Equal("Jake", field.Value);
        Assert.Equal(0, field.StepsToEnd);
    }

    [Fact]
    public void Caret_stays_within_the_value()
    {
        var field = new FieldInput("x");
        field.Type("ab");
        field.Right();
        field.Home();
        field.Left();
        field.Delete();
        field.Delete();
        field.Delete();

        Assert.Equal("", field.Value);
    }

    [Fact]
    public void Moves_over_an_emoji_as_one_character()
    {
        var field = new FieldInput("x");
        field.Type("a😀b");
        field.Left();
        field.Left();
        Assert.Equal(2, field.StepsToEnd);

        field.Backspace();
        Assert.Equal("😀b", field.Value);
    }

    [Fact]
    public void An_empty_label_starts_unselected()
    {
        var field = new FieldInput("");

        Assert.False(field.IsSelected);
        Assert.Equal(0, field.StepsToEnd);
    }
}
