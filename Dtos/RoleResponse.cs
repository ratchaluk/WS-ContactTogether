namespace ContactTogetherApi.Dtos;

/// <summary>An enabled row of <c>TblRole</c>, for the role drop-down.</summary>
public class RoleResponse
{
    /// <summary><c>TblRole.Id</c>, the value to send back as <c>RoleId</c> (and the token's <c>role</c> claim).</summary>
    public string Id { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>The highest secrecy level this role may see, a <c>TblLevel.Id</c>.</summary>
    public string LevelSecretId { get; set; } = string.Empty;

    /// <summary>"T" for the entry the drop-down selects by default, otherwise "F" (a string flag, as in the table).</summary>
    public string IsDefault { get; set; } = string.Empty;

    /// <summary>"T" for an administrator role, otherwise "F".</summary>
    public string IsAdmin { get; set; } = string.Empty;
}
