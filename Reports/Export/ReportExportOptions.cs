namespace ContactTogetherApi.Reports.Export;

/// <summary>Settings for the ExportReportXX endpoints (section <c>ReportExport</c>).</summary>
public sealed class ReportExportOptions
{
    public const string SectionName = "ReportExport";

    /// <summary>Name of the concurrency limiter policy applied to the export endpoints.</summary>
    public const string RateLimitPolicy = "report-export";

    /// <summary>SQL command timeout while exporting. EF Core's default (30 s) is too short for wide ranges.</summary>
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>How many exports may run at once; further requests get 429.</summary>
    public int MaxConcurrent { get; set; } = 2;
}
