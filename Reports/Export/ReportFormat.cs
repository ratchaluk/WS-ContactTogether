namespace ContactTogetherApi.Reports.Export;

public enum ReportFormat
{
    Xlsx,
    Pdf,
}

public static class ReportFormats
{
    /// <summary>Accepts <c>xlsx</c>/<c>excel</c> and <c>pdf</c>, case-insensitive.</summary>
    public static bool TryParse(string? value, out ReportFormat format)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "xlsx":
            case "excel":
                format = ReportFormat.Xlsx;
                return true;
            case "pdf":
                format = ReportFormat.Pdf;
                return true;
            default:
                format = default;
                return false;
        }
    }

    public static string ContentType(ReportFormat format) => format switch
    {
        ReportFormat.Pdf => "application/pdf",
        _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    public static string Extension(ReportFormat format) => format == ReportFormat.Pdf ? "pdf" : "xlsx";
}
