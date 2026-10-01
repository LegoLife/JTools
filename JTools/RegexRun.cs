using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace JTools;

/// <summary>Where one match sits in the test text.</summary>
public readonly record struct MatchSpan(int Index, int Length)
{
    public int End => Index + Length;
}

/// <summary>One capture group's value within a match.</summary>
/// <param name="Participated">False when the group didn't take part in this match, e.g. the unused side of <c>(a)|(b)</c>.</param>
public readonly record struct GroupValue(bool Participated, int Index, int Length, string Value);

/// <summary>One row in the results table: a match, with a cell per capture group.</summary>
public sealed class MatchRow
{
    private const int MaxDisplayLength = 300;

    /// <summary>0-based position of this match among all matches.</summary>
    public required int Number { get; init; }

    public required int Index { get; init; }

    public required int Length { get; init; }

    public required string Value { get; init; }

    /// <summary>Capture groups 1..n, in the same order as <see cref="RegexRunResult.Success.GroupHeaders"/>.</summary>
    public required GroupValue[] Groups { get; init; }

    /// <summary>Tab-separated line for "Copy all as TSV", with tabs and newlines escaped so rows stay intact.</summary>
    public string ToTsv() =>
        string.Join('\t', [Number.ToString(), Index.ToString(), Escape(Value), .. Groups.Select(g => g.Participated ? Escape(g.Value) : "")]);

    public static string TsvHeader(IEnumerable<string> groupHeaders) =>
        string.Join('\t', ["Index", "Position", "Matched String", .. groupHeaders]);

    /// <summary>A value as shown in a table cell: line breaks and tabs made visible, very long values cut short.</summary>
    public static string Display(string value)
    {
        var escaped = Escape(value.Length > MaxDisplayLength ? value[..MaxDisplayLength] : value);
        return value.Length > MaxDisplayLength ? escaped + "…" : escaped;
    }

    private static string Escape(string text)
    {
        if (text.AsSpan().IndexOfAny('\r', '\n', '\t') < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                '\r' => @"\r",
                '\n' => @"\n",
                '\t' => @"\t",
                _ => c.ToString(),
            });
        }
        return sb.ToString();
    }
}

public abstract record RegexRunResult
{
    private RegexRunResult()
    {
    }

    /// <summary>The pattern didn't parse. <see cref="Offset"/> is the pattern position .NET reports (past the bad character).</summary>
    public sealed record Invalid(string Message, int Offset) : RegexRunResult;

    public sealed record TimedOut : RegexRunResult;

    /// <param name="Spans">Every match, in order, for highlighting.</param>
    /// <param name="Rows">Table rows, one per match, capped at <see cref="RegexRun.MaxRows"/>.</param>
    /// <param name="GroupHeaders">Column header per capture group, written as a .NET substitution: <c>$1</c>, <c>${year}</c>.</param>
    public sealed record Success(
        MatchSpan[] Spans,
        List<MatchRow> Rows,
        string[] GroupHeaders,
        TimeSpan Elapsed) : RegexRunResult
    {
        public bool IsTruncated => Rows.Count < Spans.Length;
    }
}

/// <summary>Runs a pattern against test text. Safe to call off the UI thread.</summary>
public static partial class RegexRun
{
    [GeneratedRegex(@"^Invalid pattern '.*' at offset \d+\.\s*", RegexOptions.Singleline)]
    private static partial Regex ParseMessagePrefix();

    /// <summary>Index into <paramref name="spans"/> of the match containing <paramref name="offset"/> (either end counts), or -1.</summary>
    public static int FindMatchAt(MatchSpan[] spans, int offset)
    {
        int low = 0, high = spans.Length - 1, found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (spans[mid].Index <= offset)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return found >= 0 && offset <= spans[found].End ? found : -1;
    }

    public const int MaxRows = 10_000;

    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(3);

    /// <returns>The result, or null if <paramref name="isCancelled"/> said a newer run made this one pointless.</returns>
    public static RegexRunResult? Execute(string pattern, RegexOptions options, string input, Func<bool> isCancelled)
    {
        Regex regex;
        try
        {
            regex = new Regex(pattern, options, MatchTimeout);
        }
        catch (RegexParseException ex)
        {
            // .NET's message repeats the whole pattern ("Invalid pattern '...' at offset 5. Not enough )'s."); keep the useful part.
            var message = ParseMessagePrefix().Replace(ex.Message, "");
            return new RegexRunResult.Invalid($"Offset {ex.Offset}: {message}", ex.Offset);
        }
        catch (ArgumentException ex)
        {
            return new RegexRunResult.Invalid(ex.Message, -1);
        }

        // Group 0 is the whole match, which has its own columns.
        var groupNumbers = regex.GetGroupNumbers().Where(n => n != 0).ToArray();
        var groupHeaders = groupNumbers.Select(n => regex.GroupNameFromNumber(n)).Select(SubstitutionSyntax).ToArray();

        var stopwatch = Stopwatch.StartNew();
        var spans = new List<MatchSpan>();
        var rows = new List<MatchRow>();
        try
        {
            for (var match = regex.Match(input); match.Success; match = match.NextMatch())
            {
                if (spans.Count % 1000 == 0 && isCancelled())
                {
                    return null;
                }

                if (rows.Count < MaxRows)
                {
                    rows.Add(new MatchRow
                    {
                        Number = spans.Count,
                        Index = match.Index,
                        Length = match.Length,
                        Value = match.Value,
                        Groups = groupNumbers
                            .Select(n => match.Groups[n])
                            .Select(g => new GroupValue(g.Success, g.Index, g.Length, g.Value))
                            .ToArray(),
                    });
                }
                spans.Add(new MatchSpan(match.Index, match.Length));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return new RegexRunResult.TimedOut();
        }

        return new RegexRunResult.Success([.. spans], rows, groupHeaders, stopwatch.Elapsed);
    }

    /// <summary>How a replacement pattern refers to a group: <c>$1</c> for a numbered group, <c>${year}</c> for a named one.</summary>
    private static string SubstitutionSyntax(string groupName) =>
        groupName.All(char.IsAsciiDigit) ? "$" + groupName : "${" + groupName + "}";

    /// <summary>The pattern as C# code, e.g. <c>new Regex(@"\d+", RegexOptions.IgnoreCase)</c>.</summary>
    public static string ToCSharp(string pattern, RegexOptions options)
    {
        var literal = "@\"" + pattern.Replace("\"", "\"\"") + "\"";
        var flags = new (RegexOptions Flag, string Name)[]
            {
                (RegexOptions.IgnoreCase, "RegexOptions.IgnoreCase"),
                (RegexOptions.Multiline, "RegexOptions.Multiline"),
                (RegexOptions.Singleline, "RegexOptions.Singleline"),
                (RegexOptions.IgnorePatternWhitespace, "RegexOptions.IgnorePatternWhitespace"),
                (RegexOptions.ExplicitCapture, "RegexOptions.ExplicitCapture"),
            }
            .Where(f => options.HasFlag(f.Flag))
            .Select(f => f.Name)
            .ToList();

        return flags.Count == 0
            ? $"new Regex({literal})"
            : $"new Regex({literal}, {string.Join(" | ", flags)})";
    }
}
