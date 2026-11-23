using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblActivity
{
    public Guid Id { get; set; }

    public string ServiceId { get; set; } = null!;

    public int Line { get; set; }

    public string? ContactId { get; set; }

    public string Code { get; set; } = null!;

    public string CategoryId { get; set; } = null!;

    public string StatusId { get; set; } = null!;

    public DateTime DateOpened { get; set; }

    public DateTime? DateClosed { get; set; }

    public string Summary { get; set; } = null!;

    public string? Detail { get; set; }

    public string? FileName { get; set; }

    public byte[]? Attachment { get; set; }

    public string? OwnerId { get; set; }

    public string? GroupId { get; set; }

    public string IsReceived { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public DateTime Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime Updated { get; set; }

    public string UpdatedBy { get; set; } = null!;
}
