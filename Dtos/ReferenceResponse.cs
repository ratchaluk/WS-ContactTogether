namespace ContactTogetherApi.Dtos;

/// <summary>An enabled row of <c>TblReference</c> (the source of a service request), for the "ที่มาข้อมูล" drop-down.</summary>
public class ReferenceResponse
{
    /// <summary><c>TblReference.Id</c>, the value to send back as <c>ServiceReference</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>"T" for the entry the drop-down selects by default, otherwise "F" (a string flag, as in the table).</summary>
    public string IsDefault { get; set; } = string.Empty;
}
