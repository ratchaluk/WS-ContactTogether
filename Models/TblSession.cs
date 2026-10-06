using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblSession
{
    public string Id { get; set; } = null!;

    public string EmployeeId { get; set; } = null!;

    public string? ServerName { get; set; }

    public string? SessionTime { get; set; }

    public string? TokenHash { get; set; }

    public string? LastActiveAt { get; set; }

    public string? ExpiresAt { get; set; }

    public string? IpAddress { get; set; }
}
