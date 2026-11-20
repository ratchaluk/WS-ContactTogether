using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblArea
{
    public string Id { get; set; } = null!;

    public string RefId { get; set; } = null!;

    public string NameTh { get; set; } = null!;

    public string? NameEn { get; set; }

    public string AreaType { get; set; } = null!;

    public string? Zipcode { get; set; }

    public string? Remark { get; set; }

    public string IsDefault { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public DateTime Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime Updated { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public virtual ICollection<TblArea> InverseRef { get; set; } = new List<TblArea>();

    public virtual TblArea Ref { get; set; } = null!;
}
