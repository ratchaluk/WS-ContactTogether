namespace ContactTogetherApi.Dtos;

/// <summary>
/// A <c>TblService</c> row as returned to the caller, plus the name of its status. The date columns
/// are <c>datetime2</c> holding Thai local time, serialized without an offset
/// (<c>2026-08-25T14:51:00</c>).
/// </summary>
public class ServiceResponse
{
    public string? Id { get; set; }

    public string? Code { get; set; }

    public string? CategoryId { get; set; }

    public string? StatusId { get; set; }

    public string? StatusNameTh { get; set; }

    public string? StatusNameEn { get; set; }

    public string? OwnerId { get; set; }

    public DateTime? DateOpened { get; set; }

    public DateTime? DateClosed { get; set; }

    public string? ChannelIncomingId { get; set; }

    public string? CallBack { get; set; }

    public string? ChannelOutgoingId { get; set; }

    public string? AccountId { get; set; }

    public string? Summary { get; set; }

    public string? Detail { get; set; }

    public string? ServiceArea { get; set; }

    public string? OrganizationId { get; set; }

    public DateTime? OnScene { get; set; }

    public string? ServiceReference { get; set; }

    public string? ServiceReferenceLink { get; set; }

    public string? AreaId { get; set; }

    public string? Remark { get; set; }

    public string? LevelSeverityId { get; set; }

    public string? LevelPriorityId { get; set; }

    public string? LevelSecretId { get; set; }

    public string? IsEnable { get; set; }

    public DateTime? Created { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
