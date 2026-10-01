using System.Globalization;
using System.Text;

namespace TextExpander;

/// <summary>A place in an expansion where the user types a value: <c>{input}</c>, or <c>{input:name}</c> when <see cref="Name"/> is set.</summary>
public sealed record Field(string? Name)
{
    /// <summary>What's typed and selected at the field until the user types over it.</summary>
    public string Label => Name ?? "input";
}

/// <summary>
/// An expansion with its placeholders filled in, output a piece at a time: everything up to the first field
/// that needs a value, then, once the user has typed that value, everything up to the next one, and so on.
/// A field with the same name as an earlier one repeats its value instead of stopping again.
/// </summary>
public sealed class Expansion
{
    /// <summary>The part that marks where <c>{cursor}</c> was.</summary>
    internal static readonly object Cursor = new();

    private readonly IReadOnlyList<object> _parts; // text, Cursor or a Field
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _text = new();
    private int _nextPart;
    private int? _cursorAt;

    internal Expansion(IReadOnlyList<object> parts)
    {
        _parts = parts;
    }

    /// <summary>Everything output so far, including the values typed into fields.</summary>
    public string Text => _text.ToString();

    /// <summary>The field the expansion has stopped at, waiting for <see cref="Fill"/>; null when it hasn't stopped.</summary>
    public Field? WaitingAt { get; private set; }

    /// <summary>True once everything has been output.</summary>
    public bool IsComplete => WaitingAt is null && _nextPart == _parts.Count;

    /// <summary>
    /// How many times to press Left after the whole expansion is output to put the caret where <c>{cursor}</c>
    /// was, or null if there was none.
    /// </summary>
    public int? CaretStepsBack =>
        // Left moves over a whole character as the user sees it, so "\r\n" or an emoji is one step.
        _cursorAt is { } at ? new StringInfo(_text.ToString(at, _text.Length - at)).LengthInTextElements : null;

    /// <summary>Outputs the text up to the next field that needs a value, or to the end.</summary>
    /// <returns>The text to insert.</returns>
    public string Continue()
    {
        if (WaitingAt is not null)
        {
            throw new InvalidOperationException("The expansion is waiting for a field's value.");
        }

        int start = _text.Length;
        while (_nextPart < _parts.Count)
        {
            var part = _parts[_nextPart++];
            if (part is string text)
            {
                _text.Append(text);
            }
            else if (part == Cursor)
            {
                // Only the first one can be where the caret goes; later ones just disappear.
                _cursorAt ??= _text.Length;
            }
            else if (part is Field { Name: { } name } && _values.TryGetValue(name, out var value))
            {
                _text.Append(value);
            }
            else
            {
                WaitingAt = (Field)part;
                break;
            }
        }
        return _text.ToString(start, _text.Length - start);
    }

    /// <summary>The user typed <paramref name="value"/> into the field the expansion is waiting at.</summary>
    public void Fill(string value)
    {
        var field = WaitingAt ?? throw new InvalidOperationException("The expansion isn't waiting for a field.");
        if (field.Name is { } name)
        {
            _values[name] = value;
        }
        _text.Append(value);
        WaitingAt = null;
    }
}
