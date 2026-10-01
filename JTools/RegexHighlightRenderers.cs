using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace JTools;

/// <summary>
/// Paints regex matches behind the test text in two alternating shades, so neighboring matches stay distinct,
/// and outlines the range picked in the results table. Only matches on visible lines are drawn.
/// </summary>
public sealed class MatchHighlightRenderer(Control themeSource) : IBackgroundRenderer
{
    private const double EmptyMatchWidth = 2;

    public MatchSpan[] Spans { get; set; } = [];

    /// <summary>The match or group selected in the table, if any.</summary>
    public MatchSpan? Outline { get; set; }

    /// <summary>Draws faded, while the pattern is invalid and these are the last good results.</summary>
    public bool IsDimmed { get; set; }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0 || textView.Document is not { } document)
        {
            return;
        }

        var visibleStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var visibleEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        var brush = Resource("JMatchBrush");
        var altBrush = Resource("JMatchAltBrush");

        using (drawingContext.PushOpacity(IsDimmed ? 0.4 : 1))
        {
            var builders = new[] { NewBuilder(), NewBuilder() };
            for (var i = FirstSpanEndingAtOrAfter(visibleStart); i < Spans.Length && Spans[i].Index <= visibleEnd; i++)
            {
                var span = Spans[i];
                if (span.End > document.TextLength)
                {
                    break; // The text changed since this run; the next run will catch up.
                }

                if (span.Length == 0)
                {
                    drawingContext.FillRectangle(i % 2 == 0 ? brush : altBrush, EmptySpanRect(textView, document, span.Index));
                }
                else
                {
                    builders[i % 2].AddSegment(textView, new TextSegment { StartOffset = span.Index, Length = span.Length });
                }
            }

            DrawGeometry(drawingContext, builders[0], brush, null);
            DrawGeometry(drawingContext, builders[1], altBrush, null);

            if (Outline is { } outline && outline.End <= document.TextLength)
            {
                var pen = new Pen(Resource("JMatchOutlineBrush"), 1.5);
                if (outline.Length == 0)
                {
                    drawingContext.DrawRectangle(pen.Brush, null, EmptySpanRect(textView, document, outline.Index));
                }
                else
                {
                    var builder = NewBuilder();
                    builder.AddSegment(textView, new TextSegment { StartOffset = outline.Index, Length = outline.Length });
                    DrawGeometry(drawingContext, builder, null, pen);
                }
            }
        }
    }

    /// <summary>Index of the first match that ends at or after <paramref name="offset"/> (binary search; spans are in order).</summary>
    private int FirstSpanEndingAtOrAfter(int offset)
    {
        int low = 0, high = Spans.Length;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (Spans[mid].End < offset)
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

    /// <summary>A thin bar marking a zero-length match, which has no text to paint behind.</summary>
    private static Rect EmptySpanRect(TextView textView, TextDocument document, int offset)
    {
        var position = new TextViewPosition(document.GetLocation(offset));
        var top = textView.GetVisualPosition(position, VisualYPosition.LineTop) - textView.ScrollOffset;
        var bottom = textView.GetVisualPosition(position, VisualYPosition.LineBottom) - textView.ScrollOffset;
        return new Rect(top.X - EmptyMatchWidth / 2, top.Y, EmptyMatchWidth, bottom.Y - top.Y);
    }

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

/// <summary>Underlines the character where the pattern stopped parsing.</summary>
public sealed class PatternErrorRenderer(Control themeSource) : IBackgroundRenderer
{
    /// <summary>Index of the bad character in the pattern, or null when the pattern is fine.</summary>
    public int? ErrorIndex { get; set; }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (ErrorIndex is not { } index || textView.Document is not { } document || index >= document.TextLength)
        {
            return;
        }

        var brush = themeSource.TryFindResource("JErrorTextBrush", themeSource.ActualThemeVariant, out var value)
            ? value as IBrush ?? Brushes.Red
            : Brushes.Red;
        var pen = new Pen(brush, 2);
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = index, Length = 1 }))
        {
            drawingContext.DrawLine(pen, rect.BottomLeft - new Vector(0, 1), rect.BottomRight - new Vector(0, 1));
        }
    }
}
