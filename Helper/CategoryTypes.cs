namespace ContactTogetherApi.Helper;

/// <summary>
/// Values of <c>TblCategory.CategoryType</c>: one table holds three separate trees. Upper case, as stored
/// (the collation is case-sensitive).
/// </summary>
public static class CategoryTypes
{
    /// <summary>ประเภทการรับสาย, for <c>TblContact.CategoryId</c>.</summary>
    public const string Contact = "CONTACT";

    /// <summary>ประเภทบริการ, for <c>TblService.CategoryId</c>.</summary>
    public const string Service = "SERVICE";

    /// <summary>ประเภทการดำเนินการ (งาน), for <c>TblActivity.CategoryId</c>.</summary>
    public const string Activity = "ACTIVITY";
}
