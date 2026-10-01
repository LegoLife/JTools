using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace JTail;

/// <summary>
/// Paints every find match on the visible lines, and outlines the current one. Draws in the selection layer,
/// which sits above the line colors of highlight rules and below the text.
/// </summary>
public sealed class FindHighlightRenderer(Control themeSource) : IBackgroundRenderer
{
    /// <summary>Matches in document order.</summary>
    public List<TextSpan> Hits { get; set; } = [];

    /// <summary>Index into <see cref="Hits"/> of the match last navigated to, or -1.</summary>
    public int Current { get; set; } = -1;

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Hits.Count == 0 || !textView.VisualLinesValid || textView.VisualLines.Count == 0
            || textView.Document is not { } document)
        {
            return;
        }

        var visibleStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var visibleEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;

        var builder = NewBuilder();
        for (var i = FirstHitEndingAtOrAfter(visibleStart); i < Hits.Count && Hits[i].Offset <= visibleEnd; i++)
        {
            if (Hits[i].End > document.TextLength)
            {
                break; // The text changed since the search; the next one will catch up.
            }
            builder.AddSegment(textView, Segment(Hits[i]));
        }

        DrawGeometry(drawingContext, builder, Resource("JMatchBrush"), null);

        if (Current >= 0 && Current < Hits.Count && Hits[Current].End <= document.TextLength)
        {
            var outline = NewBuilder();
            outline.AddSegment(textView, Segment(Hits[Current]));
            DrawGeometry(drawingContext, outline, null, new Pen(Resource("JMatchOutlineBrush"), 1.5));
        }
    }

    /// <summary>Index of the first hit that ends at or after <paramref name="offset"/> (binary search; hits are in order).</summary>
    public int FirstHitEndingAtOrAfter(int offset)
    {
        int low = 0, high = Hits.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (Hits[mid].End < offset)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }

    private static TextSegment Segment(TextSpan span) => new() { StartOffset = span.Offset, Length = span.Length };

    private static BackgroundGeometryBuilder NewBuilder() => new() { AlignToWholePixels = true, CornerRadius = 2 };

    private static void DrawGeometry(DrawingContext drawingContext, BackgroundGeometryBuilder builder, IBrush? brush, IPen? pen)
    {
        if (builder.CreateGeometry() is { } geometry)
        {
            drawingContext.DrawGeometry(brush, pen, geometry);
        }
    }

    private IBrush Resource(string key) =>
        themeSource.TryFindResource(key, themeSource.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Transparent;
}
