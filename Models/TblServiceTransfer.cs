using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblServiceTransfer
{
    public Guid Id { get; set; }

    public string ServiceId { get; set; } = null!;

    public string FromEmployeeId { get; set; } = null!;

    public string ToEmployeeId { get; set; } = null!;

    public string? Reason { get; set; }

    public string TransferStatusId { get; set; } = null!;

    public DateTime Created { get; set; }

    public DateTime? Responded { get; set; }
}
