namespace ContactTogetherApi.Dtos;

/// <summary>
/// One node of the call-type tree (<c>TblCategory</c> rows with <c>CategoryType = "CONTACT"</c>).
/// The top level is the group (Service / Non Service), the second level is a radio button, and the
/// third level is the drop-down next to that radio button. The value saved as
/// <c>TblContact.CategoryId</c> is the id of the leaf the user picked: the drop-down item, or the
/// radio button itself when it has no children.
/// </summary>
public class ContactTypeResponse
{
    /// <summary><c>TblCategory.Id</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>The enabled children, sorted by <c>NameTh</c>. Empty for a leaf.</summary>
    public List<ContactTypeResponse> Children { get; set; } = [];
}
