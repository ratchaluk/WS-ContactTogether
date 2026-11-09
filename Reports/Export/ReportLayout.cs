namespace ContactTogetherApi.Reports.Export;

public enum PdfPaper
{
    A3Landscape,
    A4Landscape,
}

/// <summary>Everything about a report's file that does not depend on the request.</summary>
public sealed class ReportLayout<T>
{
    /// <summary>Short code used for the file name and the sheet name, e.g. <c>Report01</c>.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<ReportColumn<T>> Columns { get; init; }

    /// <summary>Add a leading "Rec.no" column numbered 1, 2, 3…</summary>
    public bool RowNumber { get; init; }

    /// <summary>Add a final "รวม" row that sums the <see cref="ReportColumn{T}.Sum"/> columns.</summary>
    public bool TotalsRow { get; init; }

    public PdfPaper Paper { get; init; } = PdfPaper.A3Landscape;

    public float PdfFontSize { get; init; } = 7f;
}

/// <summary>The request-specific lines printed above and below the table.</summary>
/// <param name="SubTitles">Lines under the title: date range, organization, service type.</param>
/// <param name="PrintedBy">User name of the person exporting.</param>
/// <param name="PrintedAt">Server local time of the export.</param>
public sealed record ReportHeader(IReadOnlyList<string> SubTitles, string PrintedBy, DateTime PrintedAt);

/// <summary>Header/column info shared by both writers, including the optional row-number column.</summary>
internal sealed record ExportColumn(string Header, IReadOnlyList<string> Groups, float Width, ColumnKind Kind);

internal static class ExportColumns
{
    public const string RowNumberHeader = "Rec.no";
    public const string NoDataText = "ไม่พบข้อมูล";
    public const string TotalsLabel = "รวม";

    public static List<ExportColumn> For<T>(ReportLayout<T> layout)
    {
        var columns = new List<ExportColumn>(layout.Columns.Count + 1);
        if (layout.RowNumber)
        {
            columns.Add(new ExportColumn(RowNumberHeader, [], 0.5f, ColumnKind.Number));
        }

        columns.AddRange(layout.Columns.Select(c => new ExportColumn(c.Header, c.Groups, c.Width, c.Kind)));
        return columns;
    }
}
