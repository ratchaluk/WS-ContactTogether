namespace ContactTogetherApi.Dtos;

/// <summary>
/// A group of agents (<c>TblUserGroup</c>), for the กลุ่ม drop-down of the operation form.
/// Ids are strings because <c>TblActivity.GroupId</c> and <c>TblEmployeeGroup.GroupId</c> store them as text.
/// </summary>
public class EmployeeGroupResponse
{
    /// <summary><c>TblUserGroup.GroupId</c>; send it back as <c>groupId</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The main group's id. A main group points at itself (<c>refId == id</c>).</summary>
    public string RefId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
