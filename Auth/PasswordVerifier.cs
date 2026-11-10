using System.Security.Cryptography;
using System.Text;
using ContactTogetherApi.Models;
using Microsoft.AspNetCore.Identity;

namespace ContactTogetherApi.Auth;

/// <summary>
/// Verifies and produces <see cref="TblEmployee.UserPassword"/>, which is a plain <c>nvarchar(255)</c>
/// column with no format enforced by the schema.
/// </summary>
/// <remarks>
/// <para>
/// New passwords are stored in the legacy GCC web application's format (see <see cref="LegacyHash"/>),
/// because that application still logs users in against the same column and almost every existing
/// row uses it.
/// </para>
/// <para>
/// <see cref="Verify"/> also accepts the formats other code has written: an ASP.NET Core PBKDF2 hash
/// (stored by earlier versions of this API), a bare MD5 hex digest, and, unless
/// <c>Auth:AllowLegacyPlaintextPasswords</c> is <c>false</c>, plain text.
/// </para>
/// </remarks>
public class PasswordVerifier : IPasswordVerifier
{
    public const string AllowLegacyPlaintextKey = "Auth:AllowLegacyPlaintextPasswords";

    // The legacy application's fixed RC2 key and IV (GeneralClass.encryptString in 2022GCC).
    private static readonly byte[] LegacyKey = Encoding.ASCII.GetBytes("0123456789");
    private static readonly byte[] LegacyIv = [15, 0, 53, 255, 78, 25, 72, 84];

    /// <summary>
    /// The legacy application ran on .NET Framework and read the password with <c>Encoding.Default</c>,
    /// the server's ANSI code page. Windows-874 is the Thai one. Every code page gives the same bytes
    /// for ASCII, so this only matters for passwords containing Thai characters.
    /// </summary>
    private static readonly Encoding LegacyPasswordEncoding = GetLegacyPasswordEncoding();

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

        if (IsMd5Hex(stored))
        {
            return FixedTimeEquals(stored, LegacyHash(password))
                || FixedTimeEquals(stored, Md5Hex(Encoding.UTF8.GetBytes(password)));
        }

        if (LooksLikePbkdf2Hash(stored))
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

        return FixedTimeEquals(stored, password);
    }

    public string Hash(string password) => LegacyHash(password);

    public bool IsHash(string stored) => IsMd5Hex(stored) || LooksLikePbkdf2Hash(stored);

    /// <summary>
    /// The legacy GCC format, <c>MD5String(encryptString(password))</c>: RC2-CBC encrypt the password
    /// with a fixed key and IV, Base64 the ciphertext, then MD5 that text as 32 lower-case hex digits.
    /// There is no salt, so equal passwords give equal values.
    /// </summary>
    private static string LegacyHash(string password)
    {
        // RC2 and MD5 are weak, but this has to match what the legacy application writes and checks.
#pragma warning disable CA5351
        using var rc2 = RC2.Create();
        rc2.Key = LegacyKey;
        rc2.IV = LegacyIv;

        var plain = LegacyPasswordEncoding.GetBytes(password);
        using var encryptor = rc2.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
#pragma warning restore CA5351

        return Md5Hex(Encoding.ASCII.GetBytes(Convert.ToBase64String(cipher)));
    }

    private static string Md5Hex(byte[] data)
    {
#pragma warning disable CA5351
        return Convert.ToHexStringLower(MD5.HashData(data));
#pragma warning restore CA5351
    }

    private static bool IsMd5Hex(string stored) =>
        stored.Length == 32 && stored.All(char.IsAsciiHexDigit);

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    /// <summary>
    /// An ASP.NET Core password hash is Base64 of a payload whose first byte is the format marker:
    /// 0x00 for the 49-byte v2 layout, 0x01 for the 61-byte v3 layout.
    /// </summary>
    private static bool LooksLikePbkdf2Hash(string stored)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (!Convert.TryFromBase64String(stored, buffer, out var written))
        {
            return false;
        }

        return (written == 49 && buffer[0] == 0x00)
            || (written == 61 && buffer[0] == 0x01);
    }

    private static Encoding GetLegacyPasswordEncoding()
    {
        // .NET (Core) ships only the Unicode encodings unless the code-page provider is registered.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(874);
    }
}
