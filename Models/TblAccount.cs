using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblAccount
{
    public string Id { get; set; } = null!;

    public string? SalutationTh { get; set; }

    public string? FirstnameTh { get; set; }

    public string? LastnameTh { get; set; }

    public string? SalutationEn { get; set; }

    public string? FirstnameEn { get; set; }

    public string? LastnameEn { get; set; }

    public string? GenderId { get; set; }

    public string? Birthdate { get; set; }

    public string? Address { get; set; }

    public string? AreaId { get; set; }

    public string? Zipcode { get; set; }

    public string? Remark { get; set; }

    public string IsScret { get; set; } = null!;

    public string? IsEnable { get; set; }

    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
