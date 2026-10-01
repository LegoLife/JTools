using System.Globalization;
using System.Text;

namespace TextExpander;

/// <summary>
/// Follows what the user types into a field while an expansion waits at it, so the value can be repeated
/// later. The field starts as its label, typed and selected, so the first key typed replaces it, as in a text
/// box. It knows nothing about Windows: <see cref="KeyboardHook"/> turns keystrokes into calls here.
/// </summary>
/// <remarks>
/// Only keys are seen, so the value is wrong if the user selects with Shift or the mouse, or clicks elsewhere.
/// </remarks>
public sealed class FieldInput(string label)
{
    private readonly StringBuilder _value = new(label);
    private int _caret = label.Length;

    public string Value => _value.ToString();

    /// <summary>True while the whole value is selected, i.e. until the first key is typed.</summary>
    public bool IsSelected { get; private set; } = label.Length > 0;

    /// <summary>How many times to press Right to put the caret at the end of the value (collapsing the selection).</summary>
    public int StepsToEnd => IsSelected ? 1 : new StringInfo(_value.ToString(_caret, _value.Length - _caret)).LengthInTextElements;

    /// <summary>A key typed <paramref name="text"/>, or text was pasted.</summary>
    public void Type(string text)
    {
        DeleteSelection();
        _value.Insert(_caret, text);
        _caret += text.Length;
    }

    public void Backspace()
    {
        if (DeleteSelection() || _caret == 0)
        {
            return;
        }
        int length = PreviousLength();
        _caret -= length;
        _value.Remove(_caret, length);
    }

    public void Delete()
    {
        if (DeleteSelection() || _caret == _value.Length)
        {
            return;
        }
        _value.Remove(_caret, NextLength());
    }

    public void Left()
    {
        if (Deselect(toEnd: false) || _caret == 0)
        {
            return;
        }
        _caret -= PreviousLength();
    }

    public void Right()
    {
        if (Deselect(toEnd: true) || _caret == _value.Length)
        {
            return;
        }
        _caret += NextLength();
    }

    public void Home()
    {
        IsSelected = false;
        _caret = 0;
    }

    public void End()
    {
        IsSelected = false;
        _caret = _value.Length;
    }

    /// <returns>true if there was a selection.</returns>
    private bool DeleteSelection()
    {
        if (!IsSelected)
        {
            return false;
        }
        IsSelected = false;
        _value.Clear();
        _caret = 0;
        return true;
    }

    /// <summary>Left and Right with a selection only collapse it, to its start or end.</summary>
    /// <returns>true if there was a selection.</returns>
    private bool Deselect(bool toEnd)
    {
        if (!IsSelected)
        {
            return false;
        }
        IsSelected = false;
        _caret = toEnd ? _value.Length : 0;
        return true;
    }

    /// <summary>Length of the character after the caret, which is two chars for a surrogate pair.</summary>
    private int NextLength() =>
        _caret + 1 < _value.Length && char.IsSurrogatePair(_value[_caret], _value[_caret + 1]) ? 2 : 1;

    private int PreviousLength() =>
        _caret >= 2 && char.IsSurrogatePair(_value[_caret - 2], _value[_caret - 1]) ? 2 : 1;
}
