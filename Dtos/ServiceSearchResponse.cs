namespace ContactTogetherApi.Dtos;

/// <summary>One row of a <c>GET /Services/GetServices</c> result. Names are the Thai ones.</summary>
public class ServiceSearchResponse
{
    public string? ServiceId { get; set; }

    public string? ServiceCode { get; set; }

    /// <summary>The owning employee's full name.</summary>
    public string? ServiceOwner { get; set; }

    public string? AccountFullname { get; set; }

    /// <summary>The <c>TblCategory</c> name.</summary>
    public string? ServiceType { get; set; }

    public string? ServiceSummary { get; set; }

    /// <summary>The <c>TblStatus</c> name.</summary>
    public string? ServicesStatus { get; set; }

    /// <summary><c>TblService.Created</c>, Thai local time without an offset.</summary>
    public DateTime? ServiceCreated { get; set; }
}
