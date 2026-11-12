namespace ContactTogetherApi.Dtos;

/// <summary>A saved <c>TblContact</c> row, with the Thai names of its channel and call type.</summary>
public class ContactResponse
{
    /// <summary><c>TblContact.ID</c>, 32 upper-case hex characters.</summary>
    public string Id { get; set; } = string.Empty;

    public string ContactDetail { get; set; } = string.Empty;

    /// <summary><c>TblChannel.Id</c>.</summary>
    public string ChannelId { get; set; } = string.Empty;

    public string? ChannelNameTh { get; set; }

    /// <summary><c>TblCategory.Id</c> (a call type, <c>CategoryType = "CONTACT"</c>).</summary>
    public string CategoryId { get; set; } = string.Empty;

    public string? CategoryNameTh { get; set; }

    /// <summary>Thai local time.</summary>
    public DateTime? ContactStart { get; set; }

    /// <summary>Thai local time: when the form was saved.</summary>
    public DateTime? ContactEnd { get; set; }

    /// <summary><c>TblEmployee.Id</c> of the employee who took the call (the token's <c>sub</c>).</summary>
    public string CreatedBy { get; set; } = string.Empty;
}
