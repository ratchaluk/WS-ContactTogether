namespace ContactTogetherApi.Reports.Export;

/// <summary>A cell of a (possibly multi-row) table header. Row and column are zero-based.</summary>
internal sealed record HeaderCell(int Row, int Column, int RowSpan, int ColumnSpan, string Text);

internal static class HeaderGrid
{
    /// <summary>
    /// Lays out the header. A column with N group labels gets a group cell on each of the first N rows
    /// (merged with neighbours that share the same labels) and its own header on the remaining rows.
    /// </summary>
    public static (IReadOnlyList<HeaderCell> Cells, int Depth) Build(IReadOnlyList<ExportColumn> columns)
    {
        var depth = columns.Count == 0 ? 1 : columns.Max(c => c.Groups.Count) + 1;
        var cells = new List<HeaderCell>();

        for (var level = 0; level < depth; level++)
        {
            var c = 0;
            while (c < columns.Count)
            {
                var groups = columns[c].Groups;
                if (level < groups.Count)
                {
                    var end = c + 1;
                    while (end < columns.Count && SharesGroup(columns[end].Groups, groups, level))
                    {
                        end++;
                    }

                    cells.Add(new HeaderCell(level, c, 1, end - c, groups[level]));
                    c = end;
                }
                else
                {
                    if (level == groups.Count)
                    {
                        cells.Add(new HeaderCell(level, c, depth - level, 1, columns[c].Header));
                    }

                    c++;
                }
            }
        }

        return (cells, depth);
    }

    private static bool SharesGroup(IReadOnlyList<string> candidate, IReadOnlyList<string> groups, int level)
    {
        if (candidate.Count <= level)
        {
            return false;
        }

        for (var i = 0; i <= level; i++)
        {
            if (candidate[i] != groups[i])
            {
                return false;
            }
        }

        return true;
    }
}
