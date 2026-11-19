namespace ContactTogetherApi.Dtos;

/// <summary>One phone number of an account, with the account details shown next to it.</summary>
public class AccountByPhoneResponse
{
    /// <summary>
    /// <c>TblAccount.Id</c> (upper case). Send it as <c>AccountId</c> to <c>GET /Services/SearchService</c> or as
    /// <c>accountId</c> to <c>POST /Services/AddNewService</c>. Each account appears once.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    public string PhoneNo { get; set; } = string.Empty;

    /// <summary>Thai salutation + first name + last name, empty parts skipped.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary><c>TblGender.NameTh</c>, null when the account has no gender or an unknown id.</summary>
    public string? Gender { get; set; }

    public string? Address { get; set; }

    /// <summary>As stored - legacy text in the format <c>M/d/yyyy H:mm</c>.</summary>
    public string? Created { get; set; }
}
