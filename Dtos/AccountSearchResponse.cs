namespace ContactTogetherApi.Dtos;

/// <summary>A <c>TblAccount</c> row found by a search, with its gender name and contact details.</summary>
public class AccountSearchResponse
{
    public string Id { get; set; } = string.Empty;

    public string? SalutationTh { get; set; }

    public string? FirstnameTh { get; set; }

    public string? LastnameTh { get; set; }

    public string? SalutationEn { get; set; }

    public string? FirstnameEn { get; set; }

    public string? LastnameEn { get; set; }

    /// <summary>Thai salutation, first name and last name joined.</summary>
    public string FullName { get; set; } = string.Empty;

    public string? GenderId { get; set; }

    public string? Gender { get; set; }

    public string? Birthdate { get; set; }

    public string? Address { get; set; }

    public string? AreaId { get; set; }

    /// <summary>
    /// <see cref="AreaId"/> resolved up its parents, top level first (จังหวัด, อำเภอ, ตำบล); empty when the id is
    /// not in <c>TblArea</c>. Filled by <c>GetAccountById</c> and everything built on it, <c>null</c> in
    /// <c>GetAccounts</c> rows.
    /// </summary>
    public List<AreaResponse>? AreaPath { get; set; }

    public string? Zipcode { get; set; }

    public string? Remark { get; set; }

    public string IsScret { get; set; } = string.Empty;

    /// <summary><c>T</c> or <c>F</c>. Always <c>T</c> from <c>GetAccounts</c>, which finds enabled accounts only.</summary>
    public string? IsEnable { get; set; }

    /// <summary>Stored as text in the legacy format <c>M/d/yyyy H:mm</c>.</summary>
    public string? Created { get; set; }

    public List<AccountContactResponse> Contacts { get; set; } = [];
}
