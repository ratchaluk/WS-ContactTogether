namespace ContactTogetherApi.Reports.Export;

public enum ColumnKind
{
    Text,
    Number,
    DateTime,
}

/// <summary>
/// One column of an exported report. The same definition drives both the PDF and the Excel file.
/// </summary>
public sealed class ReportColumn<T>
{
    public ReportColumn(string header, Func<T, object?> value)
    {
        Header = header;
        Value = value;
    }

    /// <summary>Column header. A <c>\n</c> starts a new line in the header cell.</summary>
    public string Header { get; }

    /// <summary>Reads the cell value: a <see cref="string"/>, an <see cref="int"/> or a <see cref="System.DateTime"/>.</summary>
    public Func<T, object?> Value { get; }

    public ColumnKind Kind { get; init; } = ColumnKind.Text;

    /// <summary>Relative width. PDF columns share the page width in this ratio; Excel uses about 10 characters per unit.</summary>
    public float Width { get; init; } = 1f;

    /// <summary>Header group labels above this column, outermost first. Adjacent columns with the same labels share one merged cell.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Add this column to the totals row (number columns only).</summary>
    public bool Sum { get; init; }

    /// <summary>Allow the PDF to break the text anywhere, for long URLs without spaces.</summary>
    public bool BreakAnywhere { get; init; }
}

/// <summary>Fluent builder so report layouts read as one column per line.</summary>
public sealed class ColumnSet<T> : List<ReportColumn<T>>
{
    public ColumnSet<T> Text(
        string header,
        Func<T, string?> value,
        float width = 1f,
        bool breakAnywhere = false,
        IReadOnlyList<string>? groups = null)
    {
        Add(new ReportColumn<T>(header, row => value(row))
        {
            Width = width,
            BreakAnywhere = breakAnywhere,
            Groups = groups ?? [],
        });
        return this;
    }

    public ColumnSet<T> DateTime(string header, Func<T, DateTime?> value, float width = 1f)
    {
        Add(new ReportColumn<T>(header, row => value(row)) { Kind = ColumnKind.DateTime, Width = width });
        return this;
    }

    /// <summary>A count column; it is summed in the totals row.</summary>
    public ColumnSet<T> Number(string header, Func<T, int> value, float width = 1f, IReadOnlyList<string>? groups = null)
    {
        Add(new ReportColumn<T>(header, row => value(row))
        {
            Kind = ColumnKind.Number,
            Width = width,
            Sum = true,
            Groups = groups ?? [],
        });
        return this;
    }
}
