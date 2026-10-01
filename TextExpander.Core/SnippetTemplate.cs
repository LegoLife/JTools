using System.Globalization;
using System.Text;

namespace TextExpander;

/// <summary>
/// Fills in the placeholders in an expansion: <c>{date}</c>, <c>{time}</c> (both optionally with a .NET format,
/// e.g. <c>{date:yyyy-MM-dd}</c>), <c>{clipboard}</c>, <c>{cursor}</c> and <c>{input}</c> / <c>{input:name}</c>.
/// <c>{{</c> is a literal brace, and anything else in braces is kept as typed.
/// </summary>
public static class SnippetTemplate
{
    public const string Help = "Placeholders: {date}, {time}, {date:yyyy-MM-dd}, {clipboard}, {cursor}, " +
                               "{input:name} (type a value, then press Tab; later {input:name}s repeat it). " +
                               "Type {{ for a literal {.";

    /// <summary>
    /// Fills in every placeholder except <c>{cursor}</c> and <c>{input}</c>, which become places in an
    /// <see cref="Expansion"/> that's output a piece at a time.
    /// </summary>
    public static Expansion Render(string template, DateTime now, string? clipboardText, CultureInfo culture)
    {
        var parts = new List<object>();
        var text = new StringBuilder(template.Length);

        void Add(object part)
        {
            if (text.Length > 0)
            {
                parts.Add(text.ToString());
                text.Clear();
            }
            parts.Add(part);
        }

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
                Add(Expansion.Cursor);
                i = close + 1;
            }
            else if (close >= 0 && TryParseField(token) is { } field)
            {
                Add(field);
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

        if (text.Length > 0)
        {
            parts.Add(text.ToString());
        }
        return new Expansion(parts);
    }

    /// <returns>The field <paramref name="token"/> asks for, or null if it isn't <c>input</c> or <c>input:name</c>.</returns>
    private static Field? TryParseField(string token)
    {
        int colon = token.IndexOf(':');
        var name = colon < 0 ? token : token[..colon];
        if (!name.Equals("input", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = colon < 0 ? "" : token[(colon + 1)..].Trim();
        return new Field(id.Length > 0 ? id : null);
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
