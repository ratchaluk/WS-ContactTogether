using ContactTogetherApi.Auth;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        ApplicationDbContext db,
        IPasswordVerifier passwordVerifier,
        ILogger<UserManagementController> logger)
    {
        _db = db;
        _passwordVerifier = passwordVerifier;
        _logger = logger;
    }

    /// <summary>Creates a <c>TblEmployee</c> row and returns it without the password.</summary>
    [HttpPost("employees")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        // CreatedBy/UpdatedBy are the acting employee, taken from the token rather than the body.
        var actingEmployeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(actingEmployeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot attribute the new employee."));
        }

        // The DB collation is Thai_100_CS_AI, so this comparison is case-sensitive in SQL. There is
        // no unique index on UserName, so this is an application-level check only.
        var userNameTaken = await _db.TblEmployees
            .AsNoTracking()
            .AnyAsync(e => e.UserName == request.UserName, cancellationToken);

        if (userNameTaken)
        {
            return Conflict(new MessageResponse(
                StatusCodes.Status409Conflict,
                $"User name '{request.UserName}' is already in use."));
        }

        await ValidateLookupsAsync(request, cancellationToken);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTime.Now;
        var employee = new TblEmployee
        {
            // Keys elsewhere in this schema are 32-char upper-case GUIDs without dashes.
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            UserName = request.UserName,
            UserPassword = _passwordVerifier.Hash(request.Password),
            GenderId = request.GenderId,
            SalutationTh = request.SalutationTh,
            FirstnameTh = request.FirstnameTh,
            LastnameTh = request.LastnameTh,
            SalutationEn = request.SalutationEn,
            FirstnameEn = request.FirstnameEn ?? string.Empty,
            LastnameEn = request.LastnameEn,
            Birthdate = request.Birthdate?.ToString("yyyy-MM-dd"),
            ContactDetail = request.ContactDetail,
            Position = request.Position,
            DateHire = request.DateHire?.ToString("yyyy-MM-dd") ?? now.ToString("yyyy-MM-dd"),
            DateExpire = request.DateExpire?.ToString("yyyy-MM-dd"),
            RoleId = request.RoleId,
            OrganizationId = request.OrganizationId,
            DefaultLanguage = request.DefaultLanguage,
            DefaultRowPerPage = request.DefaultRowPerPage.ToString(),
            IsEnable = request.IsEnable,
            Created = now.ToString("yyyy-MM-dd HH:mm:ss"),
            CreatedBy = actingEmployeeId,
            Updated = now.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedBy = actingEmployeeId,
        };

        _db.TblEmployees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee {EmployeeId} ({UserName}) created by {ActingEmployeeId}.",
            employee.Id,
            employee.UserName,
            actingEmployeeId);

        return StatusCode(StatusCodes.Status201Created, ToResponse(employee));
    }

    /// <summary>
    /// The schema has no FK constraints on these columns, so a bad id would otherwise be stored
    /// happily. Check them against the lookup tables and report them as validation errors.
    /// </summary>
    private async Task ValidateLookupsAsync(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.TblGenders.AsNoTracking().AnyAsync(g => g.Id == request.GenderId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.GenderId), $"No TblGender row with Id '{request.GenderId}'.");
        }

        if (!await _db.TblRoles.AsNoTracking().AnyAsync(r => r.Id == request.RoleId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.RoleId), $"No TblRole row with Id '{request.RoleId}'.");
        }

        if (!string.IsNullOrEmpty(request.OrganizationId)
            && !await _db.TblOrganizations.AsNoTracking()
                .AnyAsync(o => o.Id == request.OrganizationId, cancellationToken))
        {
            ModelState.AddModelError(
                nameof(request.OrganizationId),
                $"No TblOrganization row with Id '{request.OrganizationId}'.");
        }
    }

    private static EmployeeResponse ToResponse(TblEmployee employee) => new()
    {
        StatusCode = StatusCodes.Status201Created,
        Id = employee.Id,
        UserName = employee.UserName,
        GenderId = employee.GenderId,
        SalutationTh = employee.SalutationTh,
        FirstnameTh = employee.FirstnameTh,
        LastnameTh = employee.LastnameTh,
        SalutationEn = employee.SalutationEn,
        FirstnameEn = employee.FirstnameEn,
        LastnameEn = employee.LastnameEn,
        Birthdate = DateTime.TryParse(employee.Birthdate, out var birthdate) ? birthdate : null,
        ContactDetail = employee.ContactDetail,
        Position = employee.Position,
        DateHire = DateTime.TryParse(employee.DateHire, out var dateHire) ? dateHire : null,
        DateExpire = DateTime.TryParse(employee.DateExpire, out var dateExpire) ? dateExpire : null,
        RoleId = employee.RoleId,
        OrganizationId = employee.OrganizationId,
        DefaultLanguage = employee.DefaultLanguage,
        DefaultRowPerPage = int.TryParse(employee.DefaultRowPerPage, out var defaultRowPerPage)
            ? defaultRowPerPage
            : 0,
        IsEnable = employee.IsEnable,
        Created = DateTime.TryParse(employee.Created, out var created) ? created : default,
        CreatedBy = employee.CreatedBy,
        Updated = DateTime.TryParse(employee.Updated, out var updated) ? updated : default,
        UpdatedBy = employee.UpdatedBy,
    };
}