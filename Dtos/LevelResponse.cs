namespace ContactTogetherApi.Dtos;

/// <summary>An enabled row of <c>TblLevel</c> of one <c>LevelType</c>, for the severity, priority or secrecy drop-down.</summary>
public class LevelResponse
{
    /// <summary><c>TblLevel.Id</c>, the value to send back as <c>LevelSeverityId</c>, <c>LevelPriorityId</c> or <c>LevelSecretId</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>
    /// The level's rank within its type (1–4); the list is sorted by it. The direction differs by type:
    /// for SEVERITY and SECRET 1 is the lowest ("ปกติ"), for PRIORITY 1 is the most urgent ("1-ASAP").
    /// </summary>
    public float? Multiplier { get; set; }

    /// <summary>"T" for the entry the drop-down selects by default, otherwise "F" (a string flag, as in the table).</summary>
    public string IsDefault { get; set; } = string.Empty;
}
