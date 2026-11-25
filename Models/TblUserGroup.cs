using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblUserGroup
{
    public int GroupId { get; set; }

    public int? RefGroupId { get; set; }

    public string? GroupName { get; set; }

    public string? IsDefault { get; set; }

    public string? Enable { get; set; }

    public DateTime? Created { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? LastUpdated { get; set; }

    public string? LastUpdatedBy { get; set; }
}
