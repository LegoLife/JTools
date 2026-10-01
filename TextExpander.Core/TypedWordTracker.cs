using System.Text;

namespace TextExpander;

/// <summary>
/// Swaps a typed abbreviation for its expansion: erase <see cref="Backspaces"/> characters, paste
/// <see cref="Text"/> (which may contain placeholders), then press <see cref="TrailingKey"/> if there is one.
/// </summary>
public sealed record Replacement(int Backspaces, string Text, ushort? TrailingKey);

/// <summary>
/// Follows the word being typed and decides when it's an abbreviation to expand. It knows nothing about
/// Windows: <see cref="KeyboardHook"/> turns keystrokes into calls here.
/// </summary>
public sealed class TypedWordTracker(SnippetStore store, AppSettings settings)
{
    public const int MaxWordLength = 64;

    private readonly StringBuilder _word = new();

    /// <summary>What's been typed since the word started, up to the last <see cref="MaxWordLength"/> characters.</summary>
    public string Word => _word.ToString();

    /// <summary>Forgets the word, e.g. because the caret moved or another window got focus.</summary>
    public void Reset() => _word.Clear();

    public void Backspace()
    {
        if (_word.Length > 0)
        {
            _word.Length--;
        }
    }

    /// <summary>A key typed <paramref name="text"/>, usually one character.</summary>
    /// <returns>The replacement to make, in which case the key must be swallowed; otherwise null.</returns>
    public Replacement? Typed(string text)
    {
        _word.Append(text);
        if (_word.Length > MaxWordLength)
        {
            _word.Remove(0, _word.Length - MaxWordLength);
        }

        if (settings.Mode != ExpandMode.Immediate || FindMatch() is not { } snippet)
        {
            return null;
        }

        Reset();
        // The keystroke that completed the abbreviation is swallowed, so it never reaches the screen.
        return new Replacement(Math.Max(0, snippet.Abbreviation.Length - text.Length), snippet.Expansion, null);
    }

    /// <summary>Space, Tab or Enter (<paramref name="key"/> is its virtual-key code) ended the word.</summary>
    /// <returns>The replacement to make, in which case the key must be swallowed; otherwise null.</returns>
    public Replacement? WordEnded(ushort key)
    {
        var snippet = settings.Mode == ExpandMode.TriggerKey && settings.IsTriggerKey(key) ? FindMatch() : null;
        Reset();
        if (snippet is null)
        {
            return null;
        }

        // The trigger key is swallowed, so everything typed so far is on screen.
        return new Replacement(snippet.Abbreviation.Length, snippet.Expansion, settings.KeepTriggerKey ? key : null);
    }

    private Snippet? FindMatch() => _word.Length == 0 ? null : store.FindMatch(_word.ToString());
}
