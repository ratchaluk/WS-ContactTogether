using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using ContactTogetherApi.Auth;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
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

    /// <summary>
    /// camelCase options for the hand-serialized SSE payload. The relaxed encoder keeps the ISO
    /// offset's '+' readable (not escaped to +) in the raw stream; still valid JSON.
    /// </summary>
    private static readonly JsonSerializerOptions StreamJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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

    /// <summary>
    /// Streams the server clock as Server-Sent Events (text/event-stream), one "time" frame every
    /// <paramref name="intervalMs"/> ms (default 1000, clamped 250..60000). Anonymous and DB-free.
    /// The client (EventSource) keeps the connection open; the loop ends when the client disconnects
    /// (RequestAborted). Note: Swagger "Try it out" will appear to hang because the stream is endless.
    /// </summary>
    [HttpGet("time/stream")]
    [Produces("text/event-stream")]
    public async Task TimeStream([FromQuery] int? intervalMs, CancellationToken cancellationToken)
    {
        var interval = Math.Clamp(intervalMs ?? 1000, 250, 60000);

        Response.Headers.ContentType = "text/event-stream";
        // no-transform stops proxies (e.g. Next.js rewrites) from gzipping the stream, which buffers it.
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no"; // stop nginx/proxy buffering
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        // Tell EventSource how long to wait before reconnecting.
        await Response.WriteAsync($"retry: {interval}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
        try
        {
            do
            {
                var now = DateTimeOffset.Now;
                var payload = JsonSerializer.Serialize(new
                {
                    iso = now.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
                    serverLocal = now.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                    serverUtc = now.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                    unixMs = now.ToUnixTimeMilliseconds(),
                    timeZone = TimeZoneInfo.Local.Id,
                    utcOffset = now.Offset.ToString(),
                }, StreamJson);

                await Response.WriteAsync($"event: time\ndata: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal end of an SSE stream, nothing to log.
        }
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
