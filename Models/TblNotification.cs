using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblNotification
{
    public Guid Id { get; set; }

    public string EmployeeId { get; set; } = null!;

    public string TransferId { get; set; } = null!;

    public string IsRead { get; set; } = null!;

    public DateTime? ReadAt { get; set; }

    public DateTime Created { get; set; }
}
