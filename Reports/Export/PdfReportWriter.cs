using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ContactTogetherApi.Reports.Export;

/// <summary>
/// Writes a report as PDF with QuestPDF, laid out like the legacy reports: title, sub-titles and the
/// table header on every page, rows kept whole, and "หน้าที่ X / Y" in the footer.
/// </summary>
/// <remarks>
/// QuestPDF lays out a whole document in memory before writing it (about 60 MB per 1,000 SR rows), so
/// reports larger than <see cref="ChunkRows"/> rows are generated as several documents, merged, and
/// given their page numbers by an overlay. Memory then stays at about one chunk however long the
/// report is.
/// </remarks>
public static class PdfReportWriter
{
    /// <summary>Rows per QuestPDF document.</summary>
    internal const int ChunkRows = 3000;

    private const float BorderWidth = 0.5f;
    private const float CellPadding = 2f;

    public static void Write<T>(
        ReportLayout<T> layout,
        ReportHeader header,
        IReadOnlyList<T> rows,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        var report = new PdfReport<T>(layout, header, rows);

        if (rows.Count <= ChunkRows)
        {
            BuildDocument(report, 0, rows.Count, pageNumbers: true, onTotalPages: null).GeneratePdf(output);
            return;
        }

        var workDir = Directory.CreateTempSubdirectory("ct-report-pdf-");
        try
        {
            var chunks = new List<string>();
            var totalPages = 0;

            for (var first = 0; first < rows.Count; first += ChunkRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var count = Math.Min(ChunkRows, rows.Count - first);
                var chunkPages = 0;
                var path = Path.Combine(workDir.FullName, $"chunk-{chunks.Count:D5}.pdf");
                BuildDocument(report, first, count, pageNumbers: false, onTotalPages: pages => chunkPages = pages)
                    .GeneratePdf(path);

                chunks.Add(path);
                totalPages += chunkPages;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var pageNumbers = Path.Combine(workDir.FullName, "page-numbers.pdf");
            BuildPageNumberLayer(layout, totalPages).GeneratePdf(pageNumbers);

            var merged = Path.Combine(workDir.FullName, "merged.pdf");
            var operation = DocumentOperation.LoadFile(chunks[0]);
            foreach (var chunk in chunks.Skip(1))
            {
                operation = operation.MergeFile(chunk);
            }

            operation
                .OverlayFile(new DocumentOperation.LayerConfiguration { FilePath = pageNumbers })
                .Save(merged);

            using var mergedStream = File.OpenRead(merged);
            mergedStream.CopyTo(output);
        }
        finally
        {
            try
            {
                workDir.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Best effort; the OS temp cleanup removes anything left behind.
            }
        }
    }

    /// <summary>
    /// One document holding rows [first, first + count). With <paramref name="pageNumbers"/> false the
    /// footer leaves the page number out (the overlay adds it) and reports the page count instead.
    /// </summary>
    private static IDocument BuildDocument<T>(
        PdfReport<T> report,
        int first,
        int count,
        bool pageNumbers,
        Action<int>? onTotalPages)
    {
        var layout = report.Layout;
        var columns = report.Columns;
        var offset = layout.RowNumber ? 1 : 0;
        var isLastChunk = first + count >= report.Rows.Count;

        return Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page, layout);

            // Title, sub-titles and the table header sit in the page header, so they repeat on every page.
            page.Header().Column(column =>
            {
                column.Item().AlignCenter().Text(layout.Title).Bold().FontSize(layout.PdfFontSize + 4);
                foreach (var subTitle in report.Header.SubTitles)
                {
                    column.Item().AlignCenter().Text(subTitle).Bold().FontSize(layout.PdfFontSize + 2);
                }

                column.Item().PaddingTop(10).Table(table =>
                {
                    DefineColumns(table, columns);
                    foreach (var cell in report.HeaderCells)
                    {
                        table.Cell()
                            .Row((uint)cell.Row + 1)
                            .Column((uint)cell.Column + 1)
                            .RowSpan((uint)cell.RowSpan)
                            .ColumnSpan((uint)cell.ColumnSpan)
                            .Element(HeaderCellStyle)
                            // A zero-width space after "_" lets "Last_updated_by_Id" wrap between words.
                            .Text(cell.Text.Replace("_", "_​")).Bold().AlignCenter();
                    }
                });
            });

            // Each data row is its own one-row table: the cells of a row share one height, and
            // PreventPageBreak moves the whole row to the next page instead of splitting it. A row taller
            // than a whole page still breaks normally, so it can never fail the layout.
            page.Content().Column(body =>
            {
                for (var r = first; r < first + count; r++)
                {
                    var row = report.Rows[r];
                    var number = r + 1;
                    body.Item().PreventPageBreak().Table(table =>
                    {
                        DefineColumns(table, columns);
                        if (layout.RowNumber)
                        {
                            table.Cell().Element(CenterCellStyle).Text(number.ToString(CultureInfo.InvariantCulture));
                        }

                        foreach (var column in layout.Columns)
                        {
                            var text = table.Cell()
                                .Element(column.Kind == ColumnKind.Text ? TextCellStyle : CenterCellStyle)
                                .Text(ReportText.Format(column.Value(row)));
                            if (column.BreakAnywhere)
                            {
                                text.BreakAnywhere();
                            }
                        }
                    });
                }

                if (report.Rows.Count == 0)
                {
                    body.Item().Element(CenterCellStyle).Text(ExportColumns.NoDataText);
                }
                else if (layout.TotalsRow && isLastChunk)
                {
                    body.Item().PreventPageBreak().Table(table =>
                    {
                        DefineColumns(table, columns);
                        table.Cell().Element(CenterCellStyle).Text(ExportColumns.TotalsLabel).Bold();
                        for (var c = 1; c < columns.Count; c++)
                        {
                            var sum = c >= offset && layout.Columns[c - offset].Sum;
                            table.Cell().Element(CenterCellStyle)
                                .Text(sum ? report.Totals[c].ToString(CultureInfo.InvariantCulture) : string.Empty).Bold();
                        }
                    });
                }
            });

            page.Footer().PaddingTop(5).Row(footer =>
            {
                footer.RelativeItem().Text(ReportText.PrintedLine(report.Header));
                footer.RelativeItem().AlignRight().Text(text =>
                {
                    if (pageNumbers)
                    {
                        WritePageNumber(text);
                    }
                    else
                    {
                        // Prints nothing; QuestPDF passes the real page count on its final pass.
                        text.TotalPages().Format(pages =>
                        {
                            if (pages is { } n)
                            {
                                onTotalPages?.Invoke(n);
                            }

                            return string.Empty;
                        });
                    }
                });
            });
        }));
    }

    /// <summary>Blank pages carrying only "หน้าที่ X / Y" where the report footer puts it.</summary>
    private static IDocument BuildPageNumberLayer<T>(ReportLayout<T> layout, int pages) =>
        Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page, layout);
            page.Content().Column(column =>
            {
                for (var i = 1; i < pages; i++)
                {
                    column.Item().PageBreak();
                }
            });
            page.Footer().PaddingTop(5).Row(footer =>
            {
                footer.RelativeItem().Text(string.Empty);
                footer.RelativeItem().AlignRight().Text(WritePageNumber);
            });
        }));

    private static void ConfigurePage<T>(PageDescriptor page, ReportLayout<T> layout)
    {
        page.Size(layout.Paper == PdfPaper.A4Landscape ? PageSizes.A4.Landscape() : PageSizes.A3.Landscape());
        page.Margin(20);
        page.DefaultTextStyle(style => style.FontFamily(ReportFonts.Family).FontSize(layout.PdfFontSize));
    }

    private static void WritePageNumber(TextDescriptor text)
    {
        text.Span("หน้าที่ ");
        text.CurrentPageNumber().Format(FormatPageNumber);
        text.Span(" / ");
        text.TotalPages().Format(FormatPageNumber);
    }

    private static string FormatPageNumber(int? number) =>
        (number ?? 1).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The header table and every row table use the same relative widths, so the columns line up.</summary>
    private static void DefineColumns(TableDescriptor table, IReadOnlyList<ExportColumn> columns) =>
        table.ColumnsDefinition(definition =>
        {
            foreach (var column in columns)
            {
                definition.RelativeColumn(column.Width);
            }
        });

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Border(BorderWidth).Background(Colors.Grey.Lighten3).Padding(CellPadding).AlignCenter().AlignMiddle();

    private static IContainer TextCellStyle(IContainer container) =>
        container.Border(BorderWidth).Padding(CellPadding);

    private static IContainer CenterCellStyle(IContainer container) =>
        container.Border(BorderWidth).Padding(CellPadding).AlignCenter();

    /// <summary>Per-export data shared by every chunk: columns, header layout and grand totals.</summary>
    private sealed class PdfReport<T>
    {
        public PdfReport(ReportLayout<T> layout, ReportHeader header, IReadOnlyList<T> rows)
        {
            Layout = layout;
            Header = header;
            Rows = rows;
            Columns = ExportColumns.For(layout);
            HeaderCells = HeaderGrid.Build(Columns).Cells;
            Totals = new long[Columns.Count];

            if (layout.TotalsRow)
            {
                var offset = layout.RowNumber ? 1 : 0;
                foreach (var row in rows)
                {
                    for (var i = 0; i < layout.Columns.Count; i++)
                    {
                        if (layout.Columns[i].Sum && layout.Columns[i].Value(row) is int n)
                        {
                            Totals[i + offset] += n;
                        }
                    }
                }
            }
        }

        public ReportLayout<T> Layout { get; }
        public ReportHeader Header { get; }
        public IReadOnlyList<T> Rows { get; }
        public List<ExportColumn> Columns { get; }
        public IReadOnlyList<HeaderCell> HeaderCells { get; }
        public long[] Totals { get; }
    }
}
