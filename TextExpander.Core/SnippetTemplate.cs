using System.Globalization;
using System.Text;

namespace TextExpander;

/// <summary>An expansion with its placeholders filled in.</summary>
/// <param name="CaretStepsBack">
/// How many times to press Left after pasting to put the caret where <c>{cursor}</c> was, or null if there was none.
/// </param>
public readonly record struct RenderedSnippet(string Text, int? CaretStepsBack);

/// <summary>
/// Fills in the placeholders in an expansion: <c>{date}</c>, <c>{time}</c> (both optionally with a .NET format,
/// e.g. <c>{date:yyyy-MM-dd}</c>), <c>{clipboard}</c> and <c>{cursor}</c>. <c>{{</c> is a literal brace, and
/// anything else in braces is kept as typed.
/// </summary>
public static class SnippetTemplate
{
    public const string Help = "Placeholders: {date}, {time}, {date:yyyy-MM-dd}, {clipboard}, {cursor}. Type {{ for a literal {.";

    public static RenderedSnippet Render(string template, DateTime now, string? clipboardText, CultureInfo culture)
    {
        var text = new StringBuilder(template.Length);
        int? cursorAt = null;

        int i = 0;
        while (i < template.Length)
        {
            if (template[i] != '{')
            {
                text.Append(template[i++]);
                continue;
            }

            if (i + 1 < template.Length && template[i + 1] == '{')
            {
                text.Append('{');
                i += 2;
                continue;
            }

            int close = template.IndexOf('}', i + 1);
            var token = close < 0 ? "" : template[(i + 1)..close];
            if (token.Equals("cursor", StringComparison.OrdinalIgnoreCase))
            {
                // Only the first one can be where the caret goes; later ones just disappear.
                cursorAt ??= text.Length;
                i = close + 1;
            }
            else if (close >= 0 && TryResolve(token, now, clipboardText, culture) is { } value)
            {
                text.Append(value);
                i = close + 1;
            }
            else
            {
                text.Append('{');
                i++;
            }
        }

        // Left moves over a whole character as the user sees it, so "\r\n" or an emoji is one step.
        int? steps = cursorAt is { } at ? new StringInfo(text.ToString(at, text.Length - at)).LengthInTextElements : null;
        return new RenderedSnippet(text.ToString(), steps);
    }

    /// <returns>The placeholder's value, or null if <paramref name="token"/> isn't a placeholder.</returns>
    private static string? TryResolve(string token, DateTime now, string? clipboardText, CultureInfo culture)
    {
        int colon = token.IndexOf(':');
        var name = colon < 0 ? token : token[..colon];
        var format = colon < 0 ? "" : token[(colon + 1)..];

        if (name.Equals("date", StringComparison.OrdinalIgnoreCase))
            return FormatOrNull(now, format.Length > 0 ? format : "d", culture);
        if (name.Equals("time", StringComparison.OrdinalIgnoreCase))
            return FormatOrNull(now, format.Length > 0 ? format : "t", culture);
        if (name.Equals("clipboard", StringComparison.OrdinalIgnoreCase) && colon < 0)
            return clipboardText ?? "";
        return null;
    }

    private static string? FormatOrNull(DateTime now, string format, CultureInfo culture)
    {
        try
        {
            return now.ToString(format, culture);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
