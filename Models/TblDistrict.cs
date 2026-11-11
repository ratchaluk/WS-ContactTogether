using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblDistrict
{
    public Guid Id { get; set; }

    public string ProvinceId { get; set; } = null!;

    public string DistrictId { get; set; } = null!;

    public string DistrictName { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
