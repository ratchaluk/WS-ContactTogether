using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblReOpenService
{
    public Guid Id { get; set; }

    public string ServiceId { get; set; } = null!;

    public int Line { get; set; }

    public string ReOpenComment { get; set; } = null!;

    public DateTime Closed { get; set; }

    public string? ClosedBy { get; set; }

    public DateTime ReOpened { get; set; }

    public string? ReOpenedBy { get; set; }
}
