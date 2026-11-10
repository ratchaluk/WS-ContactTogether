using ContactTogetherApi.Models;

namespace ContactTogetherApi.Auth;

public interface IPasswordVerifier
{
    /// <summary>Checks a submitted password against <see cref="TblEmployee.UserPassword"/>.</summary>
    bool Verify(TblEmployee employee, string password);

    /// <summary>Produces a value suitable for storing in <see cref="TblEmployee.UserPassword"/>.</summary>
    string Hash(string password);

    /// <summary>Whether a stored value is a hash made by <see cref="Hash"/> rather than plain text.</summary>
    bool IsHash(string stored);
}
