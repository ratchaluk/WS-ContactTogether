using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblOrganizationGroup
{
    public string? GroupId { get; set; }

    public string? RefGroupId { get; set; }

    public string? GroupName { get; set; }

    public string? IsDefault { get; set; }

    public string? IsEnable { get; set; }

    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
