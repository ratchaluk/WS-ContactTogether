using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ContactTogetherApi.Auth;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

/// <summary>
/// Diagnostic helpers for setup and monitoring. Deliberately anonymous so connectivity can be
/// checked before any token is available.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HelperController : ControllerBase
{
    /// <summary>The server date-time format the reports use for Thai local date-times.</summary>
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HelperController> _logger;

    public HelperController(
        ApplicationDbContext db,
        IWebHostEnvironment env,
        ILogger<HelperController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    /// <summary>Confirms the service is running; does not touch the database.</summary>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public IActionResult Ping() =>
        Ok(new MessageResponse(StatusCodes.Status200OK, "บริการพร้อมใช้งาน"));

    /// <summary>Tests that the application can reach the SQL Server database.</summary>
    [HttpGet("db")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestDbConnection(CancellationToken cancellationToken)
    {
        try
        {
            if (await _db.Database.CanConnectAsync(cancellationToken))
            {
                return Ok(new MessageResponse(StatusCodes.Status200OK, "เชื่อมต่อฐานข้อมูลสำเร็จ"));
            }

            _logger.LogWarning("Database connectivity check returned false.");
        }
        catch (Exception ex)
        {
            // Keep the details out of the response; surface only the Thai message to the client.
            _logger.LogError(ex, "Database connectivity check threw.");
        }

        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new MessageResponse(StatusCodes.Status503ServiceUnavailable, "ไม่สามารถเชื่อมต่อฐานข้อมูลได้"));
    }

    /// <summary>
    /// Reports the server's clock and time zone, plus the database clock. The reports treat the
    /// server's local time as Thai local time without any conversion, so a wrong time zone or a
    /// server/database clock skew shows up directly in the results.
    /// </summary>
    [HttpGet("time")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Time(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        string? databaseTime = null;
        try
        {
            // SYSDATETIME() is the database server's local clock; compare it with serverLocal to spot skew.
            var dbNow = await _db.Database
                .SqlQueryRaw<DateTime>("SELECT SYSDATETIME() AS Value")
                .FirstOrDefaultAsync(cancellationToken);
            databaseTime = dbNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            // Keep reporting the server clock even if the database is unreachable.
            _logger.LogWarning(ex, "Could not read the database clock.");
        }

        return Ok(new
        {
            serverLocal = now.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
            serverUtc = DateTime.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
            timeZone = TimeZoneInfo.Local.Id,
            utcOffset = TimeZoneInfo.Local.GetUtcOffset(now).ToString(),
            databaseTime,
        });
    }

    /// <summary>Reports the deployed build and hosting environment.</summary>
    [HttpGet("version")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Version()
    {
        var assembly = Assembly.GetExecutingAssembly();

        return Ok(new
        {
            version = assembly.GetName().Version?.ToString(),
            informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            environment = _env.EnvironmentName,
            startedAtUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime()
                .ToString(DateTimeFormat, CultureInfo.InvariantCulture),
        });
    }

    /// <summary>
    /// Returns the claims decoded from the caller's token. Useful for checking the custom claim
    /// mapping (<c>sub</c>/<c>name</c>/<c>role</c>/<c>org</c>/<c>lang</c>). Requires a valid token.
    /// </summary>
    [HttpGet("whoami")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult WhoAmI() =>
        Ok(new
        {
            sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            name = User.FindFirst(JwtRegisteredClaimNames.Name)?.Value,
            role = User.FindFirst(AuthClaimTypes.Role)?.Value,
            org = User.FindFirst(AuthClaimTypes.OrganizationId)?.Value,
            lang = User.FindFirst(AuthClaimTypes.Language)?.Value,
            jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value,
            exp = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value,
        });

    /// <summary>Aggregate health check for load balancers / monitoring: service plus database.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var databaseHealthy = false;
        try
        {
            databaseHealthy = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check: database connectivity threw.");
        }

        var body = new
        {
            status = databaseHealthy ? "Healthy" : "Unhealthy",
            service = "Healthy",
            database = databaseHealthy ? "Healthy" : "Unhealthy",
        };

        return databaseHealthy
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
