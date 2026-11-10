namespace ContactTogetherApi.Dtos;

/// <summary>Result of the Development-only <c>POST /Auth/hash-password</c>.</summary>
public class PasswordCheckResponse
{
    /// <summary>
    /// A new PBKDF2 hash of the given password. The salt is random, so it differs on every call: it
    /// can be stored in <c>TblEmployee.UserPassword</c>, but not compared with the stored value as text.
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
