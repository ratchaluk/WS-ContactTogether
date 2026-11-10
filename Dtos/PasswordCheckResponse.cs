namespace ContactTogetherApi.Dtos;

/// <summary>Result of the Development-only <c>POST /Auth/hash-password</c>.</summary>
public class PasswordCheckResponse
{
    /// <summary>
    /// The legacy GCC MD5 hash of the given password, as <c>TblEmployee.UserPassword</c> stores it. It
    /// is unsalted, so it equals the stored value whenever the password is right.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    public bool UserFound { get; set; }

    /// <summary>
    /// How <c>UserPassword</c> is stored: <c>hash</c>, <c>plaintext</c> or <c>empty</c>. Null when the
    /// user was not found.
    /// </summary>
    public string? StoredFormat { get; set; }

    /// <summary>Whether the password passes the same check as <c>POST /Auth/login</c>.</summary>
    public bool Matches { get; set; }
}
