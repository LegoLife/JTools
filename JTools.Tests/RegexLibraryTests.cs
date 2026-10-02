namespace JTools.Tests;

public class RegexLibraryTests
{
    private static RegexTesterState StateWith(string pattern, string sampleText) =>
        new() { Pattern = pattern, SampleText = sampleText, IgnoreCase = true };

    [Fact]
    public void Saves_the_pattern_test_text_and_options_together()
    {
        var state = StateWith(@"\d+", "a1b22");

        state.SaveToLibrary("Numbers");

        var saved = Assert.Single(state.Library);
        Assert.Equal(("Numbers", @"\d+", "a1b22", true), (saved.Name, saved.Pattern, saved.SampleText, saved.IgnoreCase));
        Assert.Equal("Numbers", state.LoadedName);
        Assert.False(state.IsModified);
    }

    [Fact]
    public void Is_modified_once_the_working_copy_changes()
    {
        var state = StateWith("a", "abc");
        state.SaveToLibrary("A");

        state.SampleText = "xyz";

        Assert.True(state.IsModified);
    }

    [Fact]
    public void Loading_replaces_the_working_copy()
    {
        var state = StateWith("a", "abc");
        state.SaveToLibrary("A");
        state.Pattern = "b";
        state.SampleText = "bcd";
        state.IgnoreCase = false;
        state.SaveToLibrary("B");

        state.LoadFromLibrary(state.Find("A")!);

        Assert.Equal(("a", "abc", true, "A"), (state.Pattern, state.SampleText, state.IgnoreCase, state.LoadedName));
        Assert.False(state.IsModified);
    }

    [Fact]
    public void Saving_under_an_existing_name_replaces_that_entry()
    {
        var state = StateWith("a", "abc");
        state.SaveToLibrary("Mine");
        state.Pattern = "b";

        state.SaveToLibrary("MINE");

        var saved = Assert.Single(state.Library);
        Assert.Equal(("MINE", "b"), (saved.Name, saved.Pattern));
    }

    [Fact]
    public void Keeps_the_library_sorted_by_name()
    {
        var state = StateWith("a", "");

        state.SaveToLibrary("zip");
        state.SaveToLibrary("Date");
        state.SaveToLibrary("email");

        Assert.Equal(["Date", "email", "zip"], state.Library.Select(r => r.Name));
    }

    [Fact]
    public void Deleting_the_loaded_entry_keeps_the_working_copy()
    {
        var state = StateWith("a", "abc");
        state.SaveToLibrary("A");

        state.DeleteLoaded();

        Assert.Empty(state.Library);
        Assert.Null(state.LoadedName);
        Assert.Equal("a", state.Pattern);
        Assert.False(state.IsModified);
    }
}
