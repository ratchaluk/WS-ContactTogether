using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Payload for creating a <c>TblService</c> row. <c>Id</c>, <c>Code</c>, <c>IsEnable</c> and the
/// created/updated columns are set by the server, so they are not here.
/// </summary>
public class CreateServiceRequest
{
    /// <summary><c>TblCategory.Id</c> (the service type).</summary>
    [MaxLength(200)]
    public string? CategoryId { get; set; }

    /// <summary><c>TblStatus.Id</c>.</summary>
    [MaxLength(200)]
    public string? StatusId { get; set; }

    /// <summary><c>TblEmployee.Id</c>. Defaults to the calling employee.</summary>
    [MaxLength(200)]
    public string? OwnerId { get; set; }

    /// <summary><c>TblChannel.Id</c>.</summary>
    [MaxLength(200)]
    public string? ChannelIncomingId { get; set; }

    /// <summary><c>Y</c> = callback wanted, <c>N</c> = no, <c>D</c> = dropped call.</summary>
    [MaxLength(200)]
    public string? CallBack { get; set; }

    /// <summary><c>TblChannel.Id</c>.</summary>
    [MaxLength(200)]
    public string? ChannelOutgoingId { get; set; }

    /// <summary><c>TblAccount.Id</c>.</summary>
    [MaxLength(200)]
    public string? AccountId { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(1000)]
    public string? Detail { get; set; }

    [MaxLength(2000)]
    public string? ServiceArea { get; set; }

    /// <summary><c>TblOrganization.Id</c>.</summary>
    [MaxLength(200)]
    public string? OrganizationId { get; set; }

    [MaxLength(500)]
    public string? ServiceReference { get; set; }

    [MaxLength(500)]
    public string? ServiceReferenceLink { get; set; }

    /// <summary><c>TblArea.Id</c>.</summary>
    [MaxLength(200)]
    public string? AreaId { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary><c>TblLevel.Id</c>.</summary>
    [MaxLength(500)]
    public string? LevelSeverityId { get; set; }

    /// <summary><c>TblLevel.Id</c>.</summary>
    [MaxLength(500)]
    public string? LevelPriorityId { get; set; }

    /// <summary><c>TblLevel.Id</c>.</summary>
    [MaxLength(500)]
    public string? LevelSecretId { get; set; }

    /// <summary>Thai local time. Defaults to now.</summary>
    public DateTime? DateOpened { get; set; }

    /// <summary>Thai local time.</summary>
    public DateTime? DateClosed { get; set; }

    /// <summary>Thai local time.</summary>
    public DateTime? OnScene { get; set; }
}
