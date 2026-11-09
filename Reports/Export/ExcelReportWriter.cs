using System.Drawing;
using SpreadCheetah;
using SpreadCheetah.Styling;
using SpreadCheetah.Worksheets;

namespace ContactTogetherApi.Reports.Export;

/// <summary>
/// Writes a report as .xlsx with SpreadCheetah, one row at a time straight to the output stream, so
/// memory does not grow with the file. Layout: title, sub-titles, "printed by", a blank row, the
/// header (one or more rows), the data, then an optional totals row.
/// </summary>
public static class ExcelReportWriter
{
    /// <summary>Excel's row limit per sheet. Larger reports continue on "Report01 (2)" and so on.</summary>
    private const int MaxRowsPerSheet = 1_048_576;

    private const int MaxCellTextLength = 32_767;

    public static async Task WriteAsync<T>(
        ReportLayout<T> layout,
        ReportHeader header,
        IReadOnlyList<T> rows,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using var spreadsheet = await Spreadsheet.CreateNewAsync(output, cancellationToken: cancellationToken);
        var writer = new SheetWriter<T>(spreadsheet, layout, header, cancellationToken);

        await writer.StartSheetAsync();

        var columnCount = writer.Columns.Count;
        var offset = layout.RowNumber ? 1 : 0;
        var totals = new long[columnCount];
        var cells = new Cell[columnCount];
        var index = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (writer.RowsInSheet >= MaxRowsPerSheet)
            {
                await writer.StartSheetAsync();
            }

            index++;
            if (layout.RowNumber)
            {
                cells[0] = new Cell(index, writer.Styles.Number);
            }

            for (var i = 0; i < layout.Columns.Count; i++)
            {
                var column = layout.Columns[i];
                var value = column.Value(row);
                if (column.Sum && value is int n)
                {
                    totals[i + offset] += n;
                }

                cells[i + offset] = ToCell(value, writer.Styles);
            }

            await writer.AddRowAsync(cells);
        }

        if (index == 0)
        {
            await writer.AddMergedRowAsync(ExportColumns.NoDataText, writer.Styles.Number);
        }
        else if (layout.TotalsRow)
        {
            await writer.AddTotalsRowAsync(layout, totals, index);
        }

        await spreadsheet.FinishAsync(cancellationToken);
    }

    private static Cell ToCell(object? value, ExcelStyles styles) => value switch
    {
        null => new Cell(string.Empty, styles.Text),
        int n => new Cell(n, styles.Number),
        DateTime d => new Cell(d, styles.DateTime),
        string s => new Cell(Truncate(ReportText.Clean(s)), styles.Text),
        _ => new Cell(Truncate(ReportText.Format(value)), styles.Text),
    };

    private static string Truncate(string text) =>
        text.Length <= MaxCellTextLength ? text : string.Concat(text.AsSpan(0, MaxCellTextLength - 1), "…");

    /// <summary>1 → A, 26 → Z, 27 → AA.</summary>
    internal static string ColumnName(int number)
    {
        var name = string.Empty;
        while (number > 0)
        {
            var rem = (number - 1) % 26;
            name = (char)('A' + rem) + name;
            number = (number - 1) / 26;
        }

        return name;
    }

    /// <summary>Per-file state: styles, the current sheet and its row count.</summary>
    private sealed class SheetWriter<T>
    {
        private readonly Spreadsheet _spreadsheet;
        private readonly ReportLayout<T> _layout;
        private readonly ReportHeader _header;
        private readonly CancellationToken _ct;
        private readonly IReadOnlyList<HeaderCell> _headerCells;
        private readonly int _headerDepth;
        private readonly string _lastColumn;
        private int _sheetNumber;

        public SheetWriter(Spreadsheet spreadsheet, ReportLayout<T> layout, ReportHeader header, CancellationToken ct)
        {
            _spreadsheet = spreadsheet;
            _layout = layout;
            _header = header;
            _ct = ct;
            Columns = ExportColumns.For(layout);
            (_headerCells, _headerDepth) = HeaderGrid.Build(Columns);
            _lastColumn = ColumnName(Columns.Count);
            Styles = new ExcelStyles(spreadsheet);
        }

        public List<ExportColumn> Columns { get; }

        public ExcelStyles Styles { get; }

        /// <summary>Rows written to the current sheet (1-based number of the last row).</summary>
        public int RowsInSheet { get; private set; }

        /// <summary>Rows above the header: title, sub-titles, printed-by line, blank row.</summary>
        private int TopRows => 1 + _header.SubTitles.Count + 2;

        private int FirstDataRow => TopRows + _headerDepth + 1;

        public async Task StartSheetAsync()
        {
            _sheetNumber++;
            RowsInSheet = 0;

            var headerBottom = TopRows + _headerDepth;
            var options = new WorksheetOptions
            {
                FrozenRows = headerBottom,
                AutoFilter = new AutoFilterOptions($"A{headerBottom}:{_lastColumn}{headerBottom}"),
            };

            for (var i = 0; i < Columns.Count; i++)
            {
                options.Column(i + 1).Width = Math.Clamp(Columns[i].Width * 10, 6, 60);
            }

            var name = _sheetNumber == 1 ? _layout.Code : $"{_layout.Code} ({_sheetNumber})";
            await _spreadsheet.StartWorksheetAsync(name, options, _ct);

            await AddMergedRowAsync(_layout.Title, Styles.Title);
            foreach (var subTitle in _header.SubTitles)
            {
                await AddMergedRowAsync(subTitle, Styles.SubTitle);
            }

            await AddMergedRowAsync(ReportText.PrintedLine(_header), Styles.Note);
            await AddRowAsync([]);

            for (var level = 0; level < _headerDepth; level++)
            {
                var cells = new Cell[Columns.Count];
                for (var c = 0; c < cells.Length; c++)
                {
                    cells[c] = new Cell(string.Empty, Styles.Header);
                }

                foreach (var cell in _headerCells.Where(h => h.Row == level))
                {
                    cells[cell.Column] = new Cell(cell.Text, Styles.Header);
                }

                await AddRowAsync(cells);
            }

            foreach (var cell in _headerCells.Where(h => h.RowSpan > 1 || h.ColumnSpan > 1))
            {
                var top = TopRows + cell.Row + 1;
                _spreadsheet.MergeCells(
                    $"{ColumnName(cell.Column + 1)}{top}:{ColumnName(cell.Column + cell.ColumnSpan)}{top + cell.RowSpan - 1}");
            }
        }

        public async Task AddRowAsync(Cell[] cells)
        {
            await _spreadsheet.AddRowAsync(cells, _ct);
            RowsInSheet++;
        }

        public async Task AddMergedRowAsync(string text, StyleId style)
        {
            await AddRowAsync([new Cell(text, style)]);
            _spreadsheet.MergeCells($"A{RowsInSheet}:{_lastColumn}{RowsInSheet}");
        }

        /// <summary>
        /// "รวม" row. On a single sheet each total is a SUM formula (with the value cached, so viewers that do
        /// not recalculate still show it); if the data spilled onto more sheets it is the plain number.
        /// </summary>
        public async Task AddTotalsRowAsync(ReportLayout<T> layout, long[] totals, int dataRows)
        {
            var offset = layout.RowNumber ? 1 : 0;
            var cells = new Cell[Columns.Count];
            var singleSheet = _sheetNumber == 1;
            var first = FirstDataRow;
            var last = FirstDataRow + dataRows - 1;

            for (var c = 0; c < cells.Length; c++)
            {
                cells[c] = new Cell(string.Empty, Styles.TotalNumber);
            }

            cells[0] = new Cell(ExportColumns.TotalsLabel, Styles.TotalNumber);

            for (var i = 0; i < layout.Columns.Count; i++)
            {
                if (!layout.Columns[i].Sum)
                {
                    continue;
                }

                var c = i + offset;
                var letter = ColumnName(c + 1);
                cells[c] = singleSheet
                    ? new Cell(new Formula($"SUM({letter}{first}:{letter}{last})"), totals[c], Styles.TotalNumber)
                    : new Cell(totals[c], Styles.TotalNumber);
            }

            await AddRowAsync(cells);
        }
    }

    private sealed class ExcelStyles
    {
        private const string FontName = "Tahoma";

        public ExcelStyles(Spreadsheet spreadsheet)
        {
            Title = spreadsheet.AddStyle(Make(bold: true, size: 14, horizontal: HorizontalAlignment.Center));
            SubTitle = spreadsheet.AddStyle(Make(bold: true, size: 11, horizontal: HorizontalAlignment.Center));
            Note = spreadsheet.AddStyle(Make(size: 9, horizontal: HorizontalAlignment.Right));

            var header = Make(bold: true, horizontal: HorizontalAlignment.Center, wrap: true, border: true);
            header.Alignment.Vertical = VerticalAlignment.Center;
            header.Fill.Color = Color.FromArgb(230, 230, 230);
            Header = spreadsheet.AddStyle(header);

            Text = spreadsheet.AddStyle(Make(wrap: true, border: true));
            Number = spreadsheet.AddStyle(Make(horizontal: HorizontalAlignment.Center, border: true));

            var dateTime = Make(border: true, horizontal: HorizontalAlignment.Center);
            dateTime.Format = NumberFormat.Custom(ReportText.ExcelDateTimeFormat);
            DateTime = spreadsheet.AddStyle(dateTime);

            TotalNumber = spreadsheet.AddStyle(Make(bold: true, horizontal: HorizontalAlignment.Center, border: true));
        }

        public StyleId Title { get; }
        public StyleId SubTitle { get; }
        public StyleId Note { get; }
        public StyleId Header { get; }
        public StyleId Text { get; }
        public StyleId Number { get; }
        public StyleId DateTime { get; }
        public StyleId TotalNumber { get; }

        private static Style Make(
            bool bold = false,
            double size = 10,
            HorizontalAlignment horizontal = HorizontalAlignment.None,
            bool wrap = false,
            bool border = false)
        {
            var style = new Style();
            style.Font.Name = FontName;
            style.Font.Size = size;
            style.Font.Bold = bold;
            style.Alignment.Horizontal = horizontal;
            style.Alignment.Vertical = VerticalAlignment.Top;
            style.Alignment.WrapText = wrap;
            if (border)
            {
                style.Border.Left.BorderStyle = BorderStyle.Thin;
                style.Border.Right.BorderStyle = BorderStyle.Thin;
                style.Border.Top.BorderStyle = BorderStyle.Thin;
                style.Border.Bottom.BorderStyle = BorderStyle.Thin;
            }

            return style;
        }
    }
}
