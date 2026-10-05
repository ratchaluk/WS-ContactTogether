using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblStatus
{
    public string Id { get; set; } = null!;

    public string RefId { get; set; } = null!;

    public string NameTh { get; set; } = null!;

    public string? NameEn { get; set; }

    public string StatusType { get; set; } = null!;

    public string IsDefault { get; set; } = null!;

    public string IsType { get; set; } = null!;

    public string IsEnable { get; set; } = null!;

    public DateTime Created { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime Updated { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public virtual ICollection<TblStatus> InverseRef { get; set; } = new List<TblStatus>();

    public virtual TblStatus Ref { get; set; } = null!;
}
