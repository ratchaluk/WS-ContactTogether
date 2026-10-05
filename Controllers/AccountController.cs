using System.Globalization;
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
public class AccountController : ControllerBase
{
    /// <summary>
    /// <c>TblAccount.Created</c>/<c>Updated</c> are <c>nvarchar(50)</c>, and every existing row uses
    /// this format, so new rows have to match it or the legacy application cannot parse them.
    /// </summary>
    private const string LegacyDateFormat = "M/d/yyyy H:mm";

    /// <summary>
    /// <c>TblAccountDetail.DetailType</c> values that hold a telephone number; <c>MAIL</c> holds
    /// e-mail addresses and is deliberately excluded.
    /// </summary>
    private static readonly string[] PhoneDetailTypes = ["MOBILE", "HOME", "OFFICE", "FAX"];

    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApplicationDbContext db, ILogger<AccountController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Creates a <c>TblAccount</c> row and returns it.</summary>
    [HttpPost("accounts")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddNewAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        // CreatedBy/UpdatedBy are the acting employee, taken from the token rather than the body.
        var actingEmployeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(actingEmployeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot attribute the new account."));
        }

        string? zipcode = null;
        if (!string.IsNullOrWhiteSpace(request.Zipcode))
        {
            // The model maps this column as nvarchar(100) in the database, so anything that is not a
            // plain number cannot round-trip; normalize it to the canonical string form.
            if (!decimal.TryParse(request.Zipcode, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                ModelState.AddModelError(
                    nameof(request.Zipcode),
                    "Zipcode must be digits only; the database column stores it as text.");
            }
            else
            {
                zipcode = parsed.ToString(CultureInfo.InvariantCulture);
            }
        }

        await ValidateLookupsAsync(request, cancellationToken);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTime.Now.ToString(LegacyDateFormat, CultureInfo.InvariantCulture);
        var account = new TblAccount
        {
            // Keys elsewhere in this schema are 32-char upper-case GUIDs without dashes.
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            SalutationTh = request.SalutationTh,
            FirstnameTh = request.FirstnameTh,
            LastnameTh = request.LastnameTh,
            SalutationEn = request.SalutationEn,
            FirstnameEn = request.FirstnameEn,
            LastnameEn = request.LastnameEn,
            GenderId = request.GenderId,
            Birthdate = request.Birthdate,
            Address = request.Address,
            AreaId = request.AreaId,
            Zipcode = zipcode,
            Remark = request.Remark,
            IsScret = request.IsScret,
            IsEnable = request.IsEnable,
            Created = now,
            CreatedBy = actingEmployeeId,
            Updated = now,
            UpdatedBy = actingEmployeeId,
        };

        _db.TblAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account {AccountId} created by {ActingEmployeeId}.",
            account.Id,
            actingEmployeeId);

        return StatusCode(StatusCodes.Status201Created, ToResponse(account, request.Zipcode));
    }

    /// <summary>
    /// Looks up accounts by one of their telephone numbers in <c>TblAccountDetail</c>. A number can
    /// belong to several accounts, so this returns a list.
    /// </summary>
    [HttpGet("by-phone")]
    [ProducesResponseType(typeof(IEnumerable<AccountByPhoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccountByPhoneNo(
        [FromQuery] string phoneNo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNo))
        {
            ModelState.AddModelError(nameof(phoneNo), "A phone number is required.");
            return ValidationProblem(ModelState);
        }

        // Only the listed columns are read: the scaffolded model types TblAccount.Zipcode as decimal
        // while the column is nvarchar, so materialising whole entities from this table throws.
        var matches = await (
            from detail in _db.TblAccountDetails.AsNoTracking()
            join account in _db.TblAccounts.AsNoTracking() on detail.AccountId equals account.Id
            join gender in _db.TblGenders.AsNoTracking() on account.GenderId equals gender.Id into genders
            from gender in genders.DefaultIfEmpty()
            where detail.Detail == phoneNo && PhoneDetailTypes.Contains(detail.DetailType) && account.IsEnable == "T"
            orderby account.FirstnameTh, account.LastnameTh
            select new
            {
                PhoneNo = detail.Detail,
                account.SalutationTh,
                account.FirstnameTh,
                account.LastnameTh,
                Gender = gender != null ? gender.NameTh : null,
                account.Address,
                account.Created,
            }).ToListAsync(cancellationToken);

        var results = matches.Select(m => new AccountByPhoneResponse
        {
            PhoneNo = m.PhoneNo,
            FullName = BuildFullName(m.SalutationTh, m.FirstnameTh, m.LastnameTh),
            Gender = m.Gender,
            Address = m.Address,
            Created = m.Created,
        });

        return Ok(results);
    }

    /// <summary>
    /// The schema has no FK constraints on these columns, so a bad id would otherwise be stored
    /// happily. Check them against the lookup tables and report them as validation errors.
    /// </summary>
    private async Task ValidateLookupsAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.GenderId)
            && !await _db.TblGenders.AsNoTracking().AnyAsync(g => g.Id == request.GenderId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.GenderId), $"No TblGender row with Id '{request.GenderId}'.");
        }

        if (!string.IsNullOrEmpty(request.AreaId)
            && !await _db.TblAreas.AsNoTracking().AnyAsync(a => a.Id == request.AreaId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.AreaId), $"No TblArea row with Id '{request.AreaId}'.");
        }
    }

    /// <summary>Joins the Thai name parts, skipping the ones that are missing.</summary>
    private static string BuildFullName(string? salutation, string? firstname, string? lastname) =>
        string.Join(' ', new[] { salutation, firstname, lastname }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static AccountResponse ToResponse(TblAccount account, string? zipcode) => new()
    {
        StatusCode = StatusCodes.Status201Created,
        Id = account.Id,
        SalutationTh = account.SalutationTh,
        FirstnameTh = account.FirstnameTh,
        LastnameTh = account.LastnameTh,
        SalutationEn = account.SalutationEn,
        FirstnameEn = account.FirstnameEn,
        LastnameEn = account.LastnameEn,
        GenderId = account.GenderId,
        Birthdate = account.Birthdate,
        Address = account.Address,
        AreaId = account.AreaId,
        Zipcode = zipcode,
        Remark = account.Remark,
        IsScret = account.IsScret,
        IsEnable = account.IsEnable,
        Created = account.Created,
        CreatedBy = account.CreatedBy,
        Updated = account.Updated,
        UpdatedBy = account.UpdatedBy,
    };
}
