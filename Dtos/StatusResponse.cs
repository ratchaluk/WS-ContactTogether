namespace ContactTogetherApi.Dtos;

/// <summary>
/// One node of the status tree of one <c>StatusType</c> (<c>TblStatus</c>). The top level is the main
/// status (เปิดเรื่อง / ปิดเรื่อง / ยกเลิก), the second level its sub-statuses. A service stores the
/// sub-status as <c>StatusId</c>; a main status is sent only when it has no children.
/// </summary>
public class StatusResponse
{
    /// <summary><c>TblStatus.Id</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>The kind of state: <c>OPEN</c>, <c>PENDING</c>, <c>CLOSE</c> or <c>CANCEL</c>.</summary>
    public string IsType { get; set; } = string.Empty;

    /// <summary>"T" for the entry selected by default at its level, otherwise "F" (a string flag, as in the table).</summary>
    public string IsDefault { get; set; } = string.Empty;

    /// <summary>The enabled sub-statuses, sorted by <c>NameTh</c>. Empty for a leaf.</summary>
    public List<StatusResponse> Children { get; set; } = [];
}
