using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace JTail;

/// <summary>
/// Colors the background of any line matching a highlight rule (case-insensitive substring match).
/// When a line matches multiple rules, the first matching rule (in list order) wins.
/// </summary>
public sealed class LineHighlightColorizer(HighlightRulesStore rulesStore) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);

        foreach (var rule in rulesStore.Rules)
        {
            if (string.IsNullOrEmpty(rule.Word))
            {
                continue;
            }

            if (text.Contains(rule.Word, StringComparison.OrdinalIgnoreCase))
            {
                var brush = new SolidColorBrush(rule.Color);
                ChangeLinePart(line.Offset, line.EndOffset, element => element.BackgroundBrush = brush);
                return;
            }
        }
    }
}
