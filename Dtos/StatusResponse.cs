namespace ContactTogetherApi.Dtos;

/// <summary>One <c>TblStatus</c> row of one <c>StatusType</c>, for a status drop-down.</summary>
public class StatusResponse
{
    /// <summary><c>TblStatus.Id</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent status; equal to <see cref="Id"/> for a main status (เปิดเรื่อง / ปิดเรื่อง / ยกเลิก).</summary>
    public string RefId { get; set; } = string.Empty;

    public string NameTh { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    /// <summary>The kind of state: <c>OPEN</c>, <c>PENDING</c>, <c>CLOSE</c> or <c>CANCEL</c>.</summary>
    public string IsType { get; set; } = string.Empty;

    /// <summary>"T" or "F" (a string flag, as in the table).</summary>
    public string IsDefault { get; set; } = string.Empty;
}
