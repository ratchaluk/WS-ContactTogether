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
