namespace ContactTogetherApi.Dtos;

/// <summary>
/// An employee row as returned to the caller. Never carries <c>UserPassword</c>. Dates and
/// <see cref="DefaultRowPerPage"/> are strings because the columns are <c>nvarchar</c>.
/// </summary>
public class EmployeeResponse
{
    public int StatusCode { get; set; }

    public string? Id { get; set; }

    public string? UserName { get; set; }

    public string? GenderId { get; set; }

    public string? SalutationTh { get; set; }

    public string? FirstnameTh { get; set; }

    public string? LastnameTh { get; set; }

    public string? SalutationEn { get; set; }

    public string? FirstnameEn { get; set; }

    public string? LastnameEn { get; set; }

    public string? Birthdate { get; set; }

    public string? ContactDetail { get; set; }

    public string? Position { get; set; }

    public string? DateHire { get; set; }

    public string? DateExpire { get; set; }

    public string? RoleId { get; set; }

    public string? OrganizationId { get; set; }

    public string? DefaultLanguage { get; set; }

    public string? DefaultRowPerPage { get; set; }

    public string? IsEnable { get; set; }

    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }
}
