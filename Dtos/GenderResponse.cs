namespace ContactTogetherApi.Dtos;

/// <summary>An enabled row of <c>TblGender</c>, for the gender drop-down.</summary>
public class GenderResponse
{
    /// <summary><c>TblGender.Id</c>, the value to send back as <c>GenderId</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>"T" for the entry the drop-down selects by default, otherwise "F" (a string flag, as in the table).</summary>
    public string IsDefault { get; set; } = string.Empty;
}
