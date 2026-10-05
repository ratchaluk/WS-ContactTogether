using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblAccountDetail
{
    public string Id { get; set; } = null!;

    public string AccountId { get; set; } = null!;

    public string Detail { get; set; } = null!;

    public string DetailType { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public string? Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string? Updated { get; set; }

    public string UpdatedBy { get; set; } = null!;
}
