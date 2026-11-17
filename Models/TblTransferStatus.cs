using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblTransferStatus
{
    public Guid Id { get; set; }

    public string TransferStatusText { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public DateTime Created { get; set; }

    public string? CreatedBy { get; set; }
}
