using ContactTogetherApi.Models;

namespace ContactTogetherApi.Auth;

public interface IPasswordVerifier
{
    /// <summary>Checks a submitted password against <see cref="TblEmployee.UserPassword"/>.</summary>
    bool Verify(TblEmployee employee, string password);

    /// <summary>
    /// Produces a value suitable for storing in <see cref="TblEmployee.UserPassword"/>, in the legacy
    /// GCC application's unsalted MD5 format, so the same password always gives the same value.
    /// </summary>
    string Hash(string password);

    /// <summary>Whether a stored value is a hash (legacy MD5 or PBKDF2) rather than plain text.</summary>
    bool IsHash(string stored);
}
