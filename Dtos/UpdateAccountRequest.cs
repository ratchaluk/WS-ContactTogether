namespace ContactTogetherApi.Dtos;

/// <summary>
/// Payload of <c>PUT /Account/UpdateAccountById</c>: the same fields as <see cref="CreateAccountRequest"/>.
/// It replaces every one of them, so a field left out is stored as null (or its default, for
/// <c>IsScret</c>/<c>IsEnable</c>); send the whole account, e.g. what <c>GetAccountById</c> returned.
/// </summary>
public class UpdateAccountRequest : CreateAccountRequest
{
}
