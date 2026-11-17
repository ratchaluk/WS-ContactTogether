namespace ContactTogetherApi.Dtos;

/// <summary>An enabled row of <c>TblOrganization</c>, for the organization drop-downs.</summary>
public class OrganizationResponse
{
    /// <summary><c>TblOrganization.Id</c>, the value to send back as <c>OrganizationId</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The parent organization (กระทรวง). Equal to <see cref="Id"/> for a top-level organization; a
    /// department (กรม) points at its ministry. The tree has exactly these two levels.
    /// </summary>
    public string RefId { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }
}
