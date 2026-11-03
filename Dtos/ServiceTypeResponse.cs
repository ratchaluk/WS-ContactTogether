namespace ContactTogetherApi.Dtos;

/// <summary>
/// A <c>TblCategory</c> row offered as a service type. Categories form a hierarchy, so
/// <see cref="RefId"/> is the parent category's id.
/// </summary>
public class ServiceTypeResponse
{
    public string Id { get; set; } = string.Empty;

    public string RefId { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    public string CategoryType { get; set; } = string.Empty;
}
