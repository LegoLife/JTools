using TextExpander;

namespace JTools.Tests;

public class TypedWordTrackerTests
{
    private const ushort Space = 0x20;
    private const ushort Tab = 0x09;

    private static TypedWordTracker Tracker(AppSettings? settings = null) =>
        new(SnippetStoreTests.StoreWith(("brb", "be right back"), (";sig", "Best regards")), settings ?? new AppSettings());

    private static Replacement? Type(TypedWordTracker tracker, string text)
    {
        Replacement? last = null;
        foreach (var c in text)
        {
            last = tracker.Typed(c.ToString());
        }
        return last;
    }

    [Fact]
    public void Expands_when_a_trigger_key_follows_an_abbreviation()
    {
        var tracker = Tracker();

        Assert.Null(Type(tracker, "brb"));
        Assert.Equal(new Replacement(3, "be right back", Space), tracker.WordEnded(Space));
    }

    [Fact]
    public void Drops_the_trigger_key_when_told_not_to_keep_it()
    {
        var tracker = Tracker(new AppSettings { KeepTriggerKey = false });

        Type(tracker, "brb");

        Assert.Equal(new Replacement(3, "be right back", null), tracker.WordEnded(Space));
    }

    [Fact]
    public void A_key_that_is_not_a_trigger_ends_the_word_without_expanding()
    {
        var tracker = Tracker(new AppSettings { TriggerOnTab = false });

        Type(tracker, "brb");

        Assert.Null(tracker.WordEnded(Tab));
        Assert.Null(tracker.WordEnded(Space)); // the word was forgotten
    }

    [Fact]
    public void Starts_a_new_word_after_a_trigger_key_that_did_not_expand()
    {
        var tracker = Tracker();

        Type(tracker, "nope");
        Assert.Null(tracker.WordEnded(Space));
        Type(tracker, "brb");

        Assert.NotNull(tracker.WordEnded(Space));
    }

    [Fact]
    public void Ignores_an_abbreviation_inside_a_longer_word()
    {
        var tracker = Tracker();

        Type(tracker, "abrb");

        Assert.Null(tracker.WordEnded(Space));
    }

    [Fact]
    public void Backspace_removes_the_last_character_typed()
    {
        var tracker = Tracker();

        Type(tracker, "brx");
        tracker.Backspace();
        Type(tracker, "b");

        Assert.Equal("brb", tracker.Word);
        Assert.NotNull(tracker.WordEnded(Space));
    }

    [Fact]
    public void Backspace_on_an_empty_word_does_nothing()
    {
        var tracker = Tracker();

        tracker.Backspace();

        Assert.Equal("", tracker.Word);
    }

    [Fact]
    public void Reset_forgets_the_word()
    {
        var tracker = Tracker();

        Type(tracker, "brb");
        tracker.Reset();

        Assert.Null(tracker.WordEnded(Space));
    }

    [Fact]
    public void Remembers_only_the_last_64_characters()
    {
        var tracker = Tracker();

        Type(tracker, new string('x', 100) + " ;sig");

        Assert.Equal(TypedWordTracker.MaxWordLength, tracker.Word.Length);
        Assert.NotNull(tracker.WordEnded(Space));
    }

    [Fact]
    public void Immediate_mode_expands_on_the_last_character_and_erases_only_what_is_on_screen()
    {
        var tracker = Tracker(new AppSettings { Mode = ExpandMode.Immediate });

        Assert.Null(Type(tracker, "br"));

        // The "b" that completes the abbreviation is swallowed, so only "br" needs erasing.
        Assert.Equal(new Replacement(2, "be right back", null), tracker.Typed("b"));
        Assert.Equal("", tracker.Word);
    }

    [Fact]
    public void Immediate_mode_never_expands_on_a_trigger_key()
    {
        var tracker = Tracker(new AppSettings { Mode = ExpandMode.Immediate });

        Type(tracker, "br");

        Assert.Null(tracker.WordEnded(Space));
        Assert.Null(tracker.Typed("b")); // the space started a new word: "b"
    }

    [Fact]
    public void Immediate_mode_never_erases_a_negative_number_of_characters()
    {
        var tracker = new TypedWordTracker(SnippetStoreTests.StoreWith(("b", "bee")), new AppSettings { Mode = ExpandMode.Immediate });

        // A dead key can deliver two characters in one keystroke.
        Assert.Equal(0, tracker.Typed(";b")?.Backspaces);
    }
}
