using System.Globalization;
using System.Security.Claims;
using ContactTogetherApi.Auth;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly ITokenService _tokenService;
    private readonly ITokenRevocationStore _revocationStore;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext db,
        IPasswordVerifier passwordVerifier,
        ITokenService tokenService,
        ITokenRevocationStore revocationStore,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordVerifier = passwordVerifier;
        _tokenService = tokenService;
        _revocationStore = revocationStore;
        _logger = logger;
    }

    /// <summary>Authenticates an employee by user name and password and issues a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        // The DB collation is Thai_100_CS_AI, so this comparison is case-sensitive in SQL.
        var employee = await _db.TblEmployees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserName == request.UserName, cancellationToken);

        if (employee is null || !_passwordVerifier.Verify(employee, request.Password))
        {
            _logger.LogInformation("Failed login attempt for user name {UserName}.", request.UserName);
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Invalid user name or password."));
        }

        if (!IsFlagTrue(employee.IsEnable))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "This account is disabled."));
        }

        if (DateTime.TryParse(employee.DateExpire, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var expiry)
            && expiry < DateTime.Now)
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "This account has expired."));
        }

        var token = _tokenService.CreateToken(employee);
        _logger.LogInformation("Employee {EmployeeId} logged in.", employee.Id);

        return Ok(new LoginResponse
        {
            StatusCode = StatusCodes.Status200OK,
            Token = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            EmployeeId = employee.Id,
            UserName = employee.UserName,
            RoleId = employee.RoleId,
        });
    }

    /// <summary>Revokes the JWT presented in the Authorization header for the rest of its lifetime.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        var tokenId = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrEmpty(tokenId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no jti claim and cannot be revoked."));
        }

        _revocationStore.Revoke(tokenId, GetExpiry());
        _logger.LogInformation(
            "Employee {EmployeeId} logged out, token {TokenId} revoked.",
            User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            tokenId);

        return Ok(new MessageResponse(StatusCodes.Status200OK, "Logged out. This token is no longer valid."));
    }

    /// <summary>Expiry of the current token, from its <c>exp</c> claim (Unix seconds).</summary>
    private DateTime GetExpiry()
    {
        var exp = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (long.TryParse(exp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        // Should not happen - every issued token carries exp - but never revoke for less than the
        // longest lifetime the API hands out.
        return DateTime.UtcNow.AddDays(1);
    }

    /// <summary>
    /// The schema stores flags as <c>nvarchar(10)</c>, so accept the usual truthy spellings.
    /// </summary>
    private static bool IsFlagTrue(string? flag) =>
        flag is not null
        && (flag.Equals("1", StringComparison.OrdinalIgnoreCase)
            || flag.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || flag.Equals("T", StringComparison.OrdinalIgnoreCase)
            || flag.Equals("true", StringComparison.OrdinalIgnoreCase)
            || flag.Equals("yes", StringComparison.OrdinalIgnoreCase));
}
