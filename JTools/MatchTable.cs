using Avalonia.Controls;

namespace JTools;

/// <summary>
/// The Regex tester's results grid: Index, Position and Matched String, then one column per capture group.
/// The group columns depend on the pattern, so the header and rows are built in code rather than XAML.
/// </summary>
public static class MatchTable
{
    /// <summary>Columns before the first capture group: Index, Position, Matched String.</summary>
    public const int FixedColumns = 3;

    private const double IndexWidth = 70;
    private const double PositionWidth = 90;
    private const double MatchMinWidth = 200;
    private const double GroupMinWidth = 140;

    /// <summary>Narrowest the table can get before it scrolls sideways.</summary>
    public static double MinWidth(int groupCount) => IndexWidth + PositionWidth + MatchMinWidth + groupCount * GroupMinWidth;

    /// <summary>The header row: bold column names over the same columns as <see cref="MatchRowView"/>.</summary>
    public static Control CreateHeader(string[] groupHeaders)
    {
        string[] names = ["Index", "Position", "Matched String", .. groupHeaders];
        var grid = CreateGrid(groupHeaders.Length, out _);
        for (var i = 0; i < names.Length; i++)
        {
            ((TextBlock)((Border)grid.Children[i]).Child!).Text = names[i];
        }
        return new Border { Classes = { "headerRow" }, Child = grid };
    }

    /// <summary>A grid of bordered cells, one per column, each holding an empty TextBlock.</summary>
    internal static Grid CreateGrid(int groupCount, out TextBlock[] texts)
    {
        var columns = new ColumnDefinitions
        {
            new(IndexWidth, GridUnitType.Pixel),
            new(PositionWidth, GridUnitType.Pixel),
            new(2, GridUnitType.Star) { MinWidth = MatchMinWidth },
        };
        for (var g = 0; g < groupCount; g++)
        {
            columns.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = GroupMinWidth });
        }

        var grid = new Grid { ColumnDefinitions = columns };
        texts = new TextBlock[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            texts[i] = new TextBlock();
            var cell = new Border { Classes = { "cell" }, Tag = i, Child = texts[i] };
            cell.Classes.Set("last", i == columns.Count - 1);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        return grid;
    }
}

/// <summary>
/// One match in the results grid. Fills its cells from the <see cref="MatchRow"/> in its DataContext directly
/// (no reflection bindings, so it survives trimming), and is reused as the list scrolls.
/// </summary>
public sealed class MatchRowView : Border
{
    private readonly TextBlock[] _texts;

    public MatchRowView(int groupCount)
    {
        Classes.Add("row");
        Child = MatchTable.CreateGrid(groupCount, out _texts);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MatchRow row)
        {
            return;
        }

        var matchTip = $"Position {row.Index}, length {row.Length}";
        Set(0, row.Number.ToString(), true, matchTip);
        Set(1, row.Index.ToString(), true, matchTip);
        Set(2, MatchRow.Display(row.Value), true, matchTip);
        for (var g = 0; g < row.Groups.Length && MatchTable.FixedColumns + g < _texts.Length; g++)
        {
            var group = row.Groups[g];
            Set(MatchTable.FixedColumns + g,
                group.Participated ? MatchRow.Display(group.Value) : "—",
                group.Participated,
                group.Participated ? $"Position {group.Index}, length {group.Length}" : "This group didn't take part in this match");
        }
    }

    private void Set(int column, string text, bool participated, string tip)
    {
        var textBlock = _texts[column];
        textBlock.Text = text;
        textBlock.Classes.Set("dim", !participated);
        ToolTip.SetTip((Control)textBlock.Parent!, tip); // on the cell, so it shows anywhere in it
    }
}
