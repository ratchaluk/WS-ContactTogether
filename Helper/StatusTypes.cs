namespace ContactTogetherApi.Helper;

/// <summary>
/// Values of <c>TblStatus.StatusType</c>. Each type is its own two-level tree (main status -> sub-status).
/// The DB collation is case-sensitive, so these must be compared in upper case.
/// </summary>
public static class StatusTypes
{
    /// <summary>Statuses of a service request, for <c>TblService.Status_ID</c>.</summary>
    public const string Service = "SERVICE";

    /// <summary>Statuses of a work-log entry, for <c>TblActivity.Status_ID</c>.</summary>
    public const string Activity = "ACTIVITY";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Service, Activity };
}

/// <summary>
/// Values of <c>TblStatus.IsType</c>: what a status means for the request, whatever its name. Upper
/// case, as stored (the collation is case-sensitive).
/// </summary>
public static class StatusIsTypes
{
    public const string Open = "OPEN";

    public const string Pending = "PENDING";

    public const string Close = "CLOSE";

    public const string Cancel = "CANCEL";

    /// <summary>A service with one of these statuses is finished, so it carries a <c>DateClosed</c>.</summary>
    public static bool IsFinished(string? isType) => isType is Close or Cancel;
}
