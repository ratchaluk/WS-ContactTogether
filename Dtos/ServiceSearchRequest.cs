namespace ContactTogetherApi.Dtos;

/// <summary>
/// Query-string filters for <c>GET /Services/GetServices</c>. At least one is required; every one
/// given must match. Ids are matched exactly, <see cref="ServiceCode"/> and
/// <see cref="ServiceSummary"/> partially.
/// </summary>
public class ServiceSearchRequest
{
    /// <summary>A telephone number in the account's <c>TblAccountDetail</c> rows.</summary>
    public string? AccountPhoneNo { get; set; }

    /// <summary><c>TblStatus.Id</c>.</summary>
    public string? ServicesStatus { get; set; }

    public string? ServiceCode { get; set; }

    public string? ServiceSummary { get; set; }

    /// <summary><c>TblCategory.Id</c>.</summary>
    public string? ServiceType { get; set; }

    /// <summary><c>TblEmployee.Id</c> of the service owner.</summary>
    public string? ServiceOwner { get; set; }

    /// <summary>
    /// A parent <c>TblOrganization.Id</c>: matches services in that organization or in any
    /// organization whose <c>RefId</c> points to it.
    /// </summary>
    public string? OrganizationGroup { get; set; }

    /// <summary><c>TblOrganization.Id</c>.</summary>
    public string? Organization { get; set; }

    /// <summary>Inclusive lower bound on <c>TblService.Created</c>.</summary>
    public DateTime? ServiceCreatedFrom { get; set; }

    /// <summary>Inclusive upper bound on <c>TblService.Created</c>; a date without a time covers the whole day.</summary>
    public DateTime? ServiceCreatedTo { get; set; }
}
