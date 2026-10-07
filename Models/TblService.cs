using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblService
{
    public string? Id { get; set; }

    public string? Code { get; set; }

    public string? CategoryId { get; set; }

    public string? StatusId { get; set; }

    public string? OwnerId { get; set; }

    public string? DateOpened { get; set; }

    public string? DateClosed { get; set; }

    public string? ChannelIncomingId { get; set; }

    public string? CallBack { get; set; }

    public string? ChannelOutgoingId { get; set; }

    public string? AccountId { get; set; }

    public string? Summary { get; set; }

    public string? Detail { get; set; }

    public string? ServiceArea { get; set; }

    public string? OrganizationId { get; set; }

    public string? OnScene { get; set; }

    public string? ServiceReference { get; set; }

    public string? ServiceReferenceLink { get; set; }

    public string? AreaId { get; set; }

    public string? Remark { get; set; }

    public string? LevelSeverityId { get; set; }

    public string? LevelPriorityId { get; set; }

    public string? LevelSecretId { get; set; }

    public string? IsEnable { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTime? Created { get; set; }

    public DateTime? Updated { get; set; }
}
