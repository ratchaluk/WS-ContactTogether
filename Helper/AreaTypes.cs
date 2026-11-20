namespace ContactTogetherApi.Helper;

/// <summary>
/// Values of <c>TblArea.AreaType</c>: three levels in one table, linked by <c>RefId</c>
/// (จังหวัด → อำเภอ → ตำบล). A province's <c>RefId</c> is its own <c>Id</c>.
/// The DB collation is case-sensitive, so these must be compared in upper case.
/// </summary>
public static class AreaTypes
{
    /// <summary>จังหวัด</summary>
    public const string Province = "PROVINCE";

    /// <summary>อำเภอ / เขต; its <c>RefId</c> is a province.</summary>
    public const string District = "DISTRICT";

    /// <summary>ตำบล / แขวง; its <c>RefId</c> is a district.</summary>
    public const string Subdistrict = "SUBDISTRICT";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Province, District, Subdistrict };
}
