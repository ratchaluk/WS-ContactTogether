namespace ContactTogetherApi.Dtos;

/// <summary>An enabled row of <c>TblArea</c> (จังหวัด / อำเภอ / ตำบล) for the area drop-downs.</summary>
public class AreaResponse
{
    /// <summary><c>TblArea.Id</c>, the value to send back as <c>areaId</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent area's <c>Id</c>. A province points at itself.</summary>
    public string RefId { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary><c>PROVINCE</c>, <c>DISTRICT</c> or <c>SUBDISTRICT</c>.</summary>
    public string AreaType { get; set; } = string.Empty;

    /// <summary>Postal code. Filled on districts (and most provinces), empty on sub-districts.</summary>
    public string? Zipcode { get; set; }

    /// <summary>"T" for the entry the drop-down selects by default, otherwise "F".</summary>
    public string IsDefault { get; set; } = string.Empty;
}
