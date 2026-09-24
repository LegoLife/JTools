using Avalonia.Media;

namespace JTail;

/// <summary>
/// A single "lines containing this word get this background color" rule.
/// </summary>
public sealed record HighlightRule(string Word, string ColorHex)
{
    public Color Color => Color.Parse(ColorHex);
}
