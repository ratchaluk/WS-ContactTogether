using System.Globalization;
using System.Text;

namespace ContactTogetherApi.Reports.Export;

internal static class ReportText
{
    /// <summary>Legacy report date-time format: day first, hour without a leading zero (e.g. 6/2/2014 13:09:46).</summary>
    public const string PdfDateTimeFormat = "d/M/yyyy H:mm:ss";

    /// <summary>The same format as an Excel number format.</summary>
    public const string ExcelDateTimeFormat = "d/m/yyyy h:mm:ss";

    /// <summary>Formats a cell value for the PDF.</summary>
    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        string s => Clean(s),
        DateTime d => d.ToString(PdfDateTimeFormat, CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => Clean(value.ToString()),
    };

    /// <summary>
    /// Normalises line breaks to <c>\n</c>, turns tabs into spaces and drops other control characters
    /// and unpaired surrogates. Legacy rows sometimes contain them, and they make an .xlsx unreadable.
    /// </summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (!NeedsCleaning(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                sb.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (ch == '\t')
            {
                sb.Append(' ');
            }
            else if (ch == '\n')
            {
                sb.Append(ch);
            }
            else if (ch < 0x20 || ch == '￾' || ch == '￿')
            {
                // drop
            }
            else if (char.IsHighSurrogate(ch))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    sb.Append(ch).Append(text[i + 1]);
                    i++;
                }
            }
            else if (!char.IsLowSurrogate(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static bool NeedsCleaning(string text)
    {
        foreach (var ch in text)
        {
            if (ch < 0x20 || ch == '￾' || ch == '￿' || char.IsSurrogate(ch))
            {
                return true;
            }
        }

        return false;
    }

    public static string PrintedLine(ReportHeader header) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"พิมพ์โดย {header.PrintedBy} เมื่อ {header.PrintedAt:d/M/yyyy H:mm}");
}
