using System.Text;
using System.Text.RegularExpressions;

namespace JTail;

/// <summary>A stretch of the log text, by document offset.</summary>
public readonly record struct TextSpan(int Offset, int Length)
{
    public int End => Offset + Length;
}

/// <summary>What a find or filter box asks for: its text and options. Each tab keeps its own.</summary>
public sealed record SearchQuery(string Text = "", bool MatchCase = false, bool IsRegex = false);

/// <summary>A find or filter: plain text or a .NET regex, matched against one line at a time.</summary>
public sealed class LineSearch
{
    // A pathological regex gives up instead of freezing the window; a line that times out doesn't match.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

    private readonly Regex _regex;

    private LineSearch(Regex regex) => _regex = regex;

    /// <returns>The search, or null if the text is empty or (with <paramref name="error"/> set) an invalid regex.</returns>
    public static LineSearch? Create(SearchQuery query, out string? error) =>
        Create(query.Text, query.MatchCase, query.IsRegex, out error);

    /// <returns>The search, or null if the text is empty or (with <paramref name="error"/> set) an invalid regex.</returns>
    public static LineSearch? Create(string text, bool matchCase, bool isRegex, out string? error)
    {
        error = null;
        if (text.Length == 0)
        {
            return null;
        }

        var options = RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new LineSearch(new Regex(isRegex ? text : Regex.Escape(text), options, MatchTimeout));
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <param name="line">One line, without its line break.</param>
    public bool IsMatch(ReadOnlySpan<char> line)
    {
        try
        {
            return _regex.IsMatch(line);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Every non-empty match in <paramref name="text"/>, which starts at a line start.</summary>
    /// <param name="baseOffset">Where <paramref name="text"/> starts in the document.</param>
    public List<TextSpan> FindAll(string text, int baseOffset)
    {
        var hits = new List<TextSpan>();
        int start = 0;
        while (start <= text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            try
            {
                foreach (var match in _regex.EnumerateMatches(LineContent(text, start, end)))
                {
                    if (match.Length > 0)
                    {
                        hits.Add(new TextSpan(baseOffset + start + match.Index, match.Length));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip the line, as IsMatch does.
            }

            start = end + 1;
        }
        return hits;
    }

    /// <summary>The line from <paramref name="start"/> to the '\n' at <paramref name="end"/>, without a trailing '\r'.</summary>
    internal static ReadOnlySpan<char> LineContent(string text, int start, int end)
    {
        var line = text.AsSpan(start, end - start);
        return line.EndsWith("\r") ? line[..^1] : line;
    }
}

/// <summary>
/// Keeps the lines of a log that match a search, as text arrives in chunks that can end mid-line.
/// A line is judged once its line break arrives, so an unfinished last line stays out until then.
/// </summary>
public sealed class LineFilter(LineSearch search)
{
    private string _partialLine = "";

    public int ShownLines { get; private set; }

    public int HiddenLines { get; private set; }

    /// <summary>Takes the next chunk of the log and returns the matching lines in it, line breaks included.</summary>
    public string Append(string chunk)
    {
        var text = _partialLine + chunk;
        int lastBreak = text.LastIndexOf('\n');
        _partialLine = text[(lastBreak + 1)..];

        var shown = new StringBuilder();
        int start = 0;
        while (start <= lastBreak)
        {
            int end = text.IndexOf('\n', start);
            if (search.IsMatch(LineSearch.LineContent(text, start, end)))
            {
                shown.Append(text, start, end + 1 - start);
                ShownLines++;
            }
            else
            {
                HiddenLines++;
            }
            start = end + 1;
        }
        return shown.ToString();
    }
}
