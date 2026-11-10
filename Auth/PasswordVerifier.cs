using System.Security.Cryptography;
using System.Text;
using ContactTogetherApi.Models;
using Microsoft.AspNetCore.Identity;

namespace ContactTogetherApi.Auth;

/// <summary>
/// Verifies <see cref="TblEmployee.UserPassword"/>, which is a plain <c>nvarchar(255)</c> column with
/// no format enforced by the schema.
/// </summary>
/// <remarks>
/// New passwords should be stored as an ASP.NET Core PBKDF2 hash (see <see cref="Hash"/>). Rows whose
/// value is not such a hash fall back to a plain-text comparison so hand-seeded accounts keep working;
/// set <c>Auth:AllowLegacyPlaintextPasswords</c> to <c>false</c> to turn that fallback off in production.
/// </remarks>
public class PasswordVerifier : IPasswordVerifier
{
    public const string AllowLegacyPlaintextKey = "Auth:AllowLegacyPlaintextPasswords";

    private readonly PasswordHasher<TblEmployee> _hasher = new();
    private readonly bool _allowLegacyPlaintext;
    private readonly ILogger<PasswordVerifier> _logger;

    public PasswordVerifier(IConfiguration configuration, ILogger<PasswordVerifier> logger)
    {
        _allowLegacyPlaintext = configuration.GetValue(AllowLegacyPlaintextKey, true);
        _logger = logger;
    }

    public bool Verify(TblEmployee employee, string password)
    {
        var stored = employee.UserPassword;
        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (LooksLikeHash(stored))
        {
            return _hasher.VerifyHashedPassword(employee, stored, password) != PasswordVerificationResult.Failed;
        }

        if (!_allowLegacyPlaintext)
        {
            _logger.LogWarning(
                "Employee {EmployeeId} has a non-hashed password and the plain-text fallback is disabled.",
                employee.Id);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(password));
    }

    public string Hash(string password) => _hasher.HashPassword(new TblEmployee(), password);

    public bool IsHash(string stored) => LooksLikeHash(stored);

    /// <summary>
    /// An ASP.NET Core password hash is Base64 of a payload whose first byte is the format marker:
    /// 0x00 for the 49-byte v2 layout, 0x01 for the 61-byte v3 layout.
    /// </summary>
    private static bool LooksLikeHash(string stored)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (!Convert.TryFromBase64String(stored, buffer, out var written))
        {
            return false;
        }

        return (written == 49 && buffer[0] == 0x00)
            || (written == 61 && buffer[0] == 0x01);
    }
}
