using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblSubDistrict
{
    public Guid Id { get; set; }

    public string DistrictId { get; set; } = null!;

    public string SubDistrictId { get; set; } = null!;

    public string SubDistrictName { get; set; } = null!;

    public string ZipCode { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
