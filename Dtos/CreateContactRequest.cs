using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Payload of the call-receiving form (<c>POST /Contact/AddNewContact</c>), saved as one
/// <c>TblContact</c> row. <c>Id</c>, <c>CreatedBy</c> and <c>ContactEnd</c> are set by the server.
/// </summary>
public class CreateContactRequest
{
    /// <summary>
    /// The caller's number from "เบอร์โทรเข้า", or the contact detail of the chosen channel.
    /// <c>TblContact.ContactDetail</c> is <c>varchar(50)</c>. Leading and trailing spaces are trimmed.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(50)]
    public string? ContactDetail { get; set; }

    /// <summary>An enabled <c>TblChannel.Id</c>, from <c>GET /ListOfValues/GetChannel</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(50)]
    public string? ChannelId { get; set; }

    /// <summary>
    /// The call type: the id of the leaf picked in <c>GET /ListOfValues/GetContactType</c> (the
    /// drop-down item, or the radio button itself when it has no children).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(50)]
    public string? CategoryId { get; set; }

    /// <summary>
    /// When the call started, Thai local time without an offset or <c>Z</c> (e.g.
    /// <c>2026-11-12T10:15:00</c>). Defaults to the time of the request.
    /// </summary>
    public DateTime? ContactStart { get; set; }
}
