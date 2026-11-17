using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblServiceTitle
{
    public Guid Id { get; set; }

    public string ServiceTitleText { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public DateTime Created { get; set; }

    public string? CreatedBy { get; set; }
}
