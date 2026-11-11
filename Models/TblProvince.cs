using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblProvince
{
    public Guid Id { get; set; }

    public string ProvinceId { get; set; } = null!;

    public string ProvinceName { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
