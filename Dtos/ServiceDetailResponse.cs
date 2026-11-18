namespace ContactTogetherApi.Dtos;

/// <summary>
/// One <c>TblService</c> row for the service detail page: every id of <see cref="ServiceResponse"/>
/// (to pre-fill an edit form) plus the names they point at (to display). A name is <c>null</c> when
/// its id is empty or matches no row. <see cref="ServiceResponse.CallBack"/> stays the raw code:
/// <c>Y</c> = wants a call back, <c>N</c> = does not, <c>D</c> = dropped call, <c>E</c> = แก้ไข.
/// </summary>
public class ServiceDetailResponse : ServiceResponse
{
    /// <summary>
    /// <c>TblStatus.IsType</c> of the status, e.g. <c>OPEN</c>, <c>PENDING</c>, <c>CLOSE</c> or <c>CANCEL</c>.
    /// </summary>
    public string? StatusIsType { get; set; }

    public string? ServiceTitleText { get; set; }

    /// <summary>Thai salutation, first name and last name of the owner (<c>TblEmployee</c>).</summary>
    public string? OwnerName { get; set; }

    public string? CreatedByName { get; set; }

    public string? UpdatedByName { get; set; }

    public string? ChannelIncomingName { get; set; }

    public string? ChannelOutgoingName { get; set; }

    /// <summary>Thai name of <see cref="ServiceResponse.ServiceReference"/> (a <c>TblReference</c> id).</summary>
    public string? ServiceReferenceName { get; set; }

    public string? LevelSeverityName { get; set; }

    public string? LevelPriorityName { get; set; }

    public string? LevelSecretName { get; set; }

    /// <summary>Thai salutation, first name and last name of the account (<c>TblAccount</c>).</summary>
    public string? AccountFullName { get; set; }

    /// <summary>
    /// The whole account (names, gender, address, phone numbers and e-mail in <c>Contacts</c>) in the
    /// shape <c>GET /Account/GetAccounts</c> returns; <c>null</c> when the service has no matching account.
    /// </summary>
    public AccountSearchResponse? Account { get; set; }

    /// <summary>The category and its parents, top level first (e.g. Digital Wallet, การร้องเรียน, …).</summary>
    public List<LookupItemResponse> CategoryPath { get; set; } = [];

    /// <summary>The organization and its parents, top level first (กระทรวง, then กรม).</summary>
    public List<LookupItemResponse> OrganizationPath { get; set; } = [];
}
