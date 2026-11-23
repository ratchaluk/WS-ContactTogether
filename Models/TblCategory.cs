using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblCategory
{
    public string Id { get; set; } = null!;

    public string RefId { get; set; } = null!;

    public string NameTh { get; set; } = null!;

    public string? NameEn { get; set; }

    public string CategoryType { get; set; } = null!;

    public string? Remark { get; set; }

    public string IsDefault { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public DateTime Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime Updated { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public virtual ICollection<TblCategory> InverseRef { get; set; } = new List<TblCategory>();

    public virtual TblCategory Ref { get; set; } = null!;
}
