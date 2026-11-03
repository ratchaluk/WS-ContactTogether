namespace ContactTogetherApi.Dtos;

/// <summary>One <c>TblAccountDetail</c> row: a phone number, e-mail address, etc. of an account.</summary>
public class AccountContactResponse
{
    /// <summary>For example <c>MOBILE</c>, <c>HOME</c>, <c>OFFICE</c>, <c>FAX</c> or <c>MAIL</c>.</summary>
    public string DetailType { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
}
