namespace ContactTogetherApi.Helper;

/// <summary>
/// Values of <c>TblLevel.LevelType</c>. One table holds three kinds of level, told apart by this column.
/// The DB collation is case-sensitive, so these must be compared in upper case.
/// </summary>
public static class LevelTypes
{
    /// <summary>ความเร่งด่วน, for <c>TblService.Level_Severity_ID</c>.</summary>
    public const string Severity = "SEVERITY";

    /// <summary>ความสำคัญ, for <c>TblService.Level_Priority_ID</c>.</summary>
    public const string Priority = "PRIORITY";

    /// <summary>ชั้นความลับ, for <c>TblService.Level_Secret_ID</c> and <c>TblRole.LevelSecretId</c>.</summary>
    public const string Secret = "SECRET";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Severity, Priority, Secret };
}
