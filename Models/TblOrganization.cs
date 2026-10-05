using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblOrganization
{
    public string Id { get; set; } = null!;

    public string RefId { get; set; } = null!;

    public string NameTh { get; set; } = null!;

    public string? NameEn { get; set; }

    public string? Remark { get; set; }

    public string IsDefault { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public string? Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string? Updated { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public virtual ICollection<TblOrganization> InverseRef { get; set; } = new List<TblOrganization>();

    public virtual TblOrganization Ref { get; set; } = null!;
}
