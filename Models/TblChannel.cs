using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblChannel
{
    public string Id { get; set; } = null!;

    public string NameTh { get; set; } = null!;

    public string? NameEn { get; set; }

    public string IsDefault { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public string? Remark { get; set; }

    public string? Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
