using System.ComponentModel;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Query-string filters for <c>GET /Services/SearchService</c>. At least one is required; every one
/// given must match. Ids are matched exactly, <see cref="ServiceCode"/> and
/// <see cref="ServiceSummary"/> partially. <see cref="SortOrder"/>, <see cref="PageNumber"/> and
/// <see cref="PageSize"/> choose the order and the page and are not filters.
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

    /// <summary><c>TblServiceTitles.Id</c> (หัวข้อเรื่อง) รับได้ทั้งตัวพิมพ์เล็กและใหญ่</summary>
    public string? ServiceTitleId { get; set; }

    /// <summary><c>TblReference.Id</c> (ที่มาข้อมูล) จาก <c>GET /ListOfValues/GetReference</c> รับได้ทั้งตัวพิมพ์เล็กและใหญ่</summary>
    public string? ServiceReference { get; set; }

    /// <summary>
    /// <c>TblLevel.Id</c> ประเภท <c>PRIORITY</c> (ความสำคัญ) จาก <c>GET /ListOfValues/GetLevel?levelType=PRIORITY</c>
    /// รับได้ทั้งตัวพิมพ์เล็กและใหญ่
    /// </summary>
    public string? LevelPriorityId { get; set; }

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

    /// <summary>
    /// ลำดับตาม <c>TblService.Created</c>: <c>desc</c> = ล่าสุดก่อน (ค่าเริ่มต้น), <c>asc</c> = เก่าสุดก่อน
    /// ไม่สนตัวพิมพ์เล็กและใหญ่
    /// </summary>
    [DefaultValue("desc")]
    public string? SortOrder { get; set; }

    /// <summary>Page to return, starting at 1.</summary>
    [DefaultValue(1)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Rows per page, 1 to 200.</summary>
    [DefaultValue(20)]
    public int PageSize { get; set; } = 20;
}
