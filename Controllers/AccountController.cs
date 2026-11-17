using System.Globalization;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Helper;
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

    /// <summary>Upper bound on <see cref="GetAccounts"/> results, so a one-letter search stays cheap.</summary>
    private const int MaxSearchResults = 100;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApplicationDbContext db, ILogger<AccountController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Creates a <c>TblAccount</c> row and returns it.</summary>
    [HttpPost("AddNewAccount")]
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

        await ValidateLookupsAsync(request, cancellationToken);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTime.Now.ToString(LegacyDateFormat, CultureInfo.InvariantCulture);
        var account = new TblAccount
        {
            // Id is left null: Add() assigns a UUID (see ApplicationDbContext.Keys.cs).
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
            Zipcode = request.Zipcode,
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

        return StatusCode(StatusCodes.Status201Created, ToResponse(account));
    }

    /// <summary>
    /// Looks up accounts by one of their telephone numbers in <c>TblAccountDetail</c>. A number can
    /// belong to several accounts, so this returns a list.
    /// </summary>
    [HttpGet("GetAccountByPhoneNo")]
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

        // Projected and untracked: TblAccount.Id has legacy duplicates, see ApplicationDbContext.Keys.cs.
        var matches = await (
            from detail in _db.TblAccountDetails.AsNoTracking()
            join account in _db.TblAccounts.AsNoTracking() on detail.AccountId equals account.Id
            join gender in _db.TblGenders.AsNoTracking() on account.GenderId equals gender.Id into genders
            from gender in genders.DefaultIfEmpty()
            where detail.Detail == phoneNo && PhoneDetailTypes.Contains(detail.DetailType) && account.IsEnable == "T"
            orderby account.FirstnameTh, account.LastnameTh
            select new
            {
                AccountId = account.Id,
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
    /// Searches enabled accounts by partial first name, last name and/or contact info; at least one
    /// is required, and every one given must match. Names match Thai or English. Contact info matches
    /// the address or any <c>TblAccountDetail</c> row (phone, e-mail, ...). Returns at most
    /// <see cref="MaxSearchResults"/> accounts.
    /// </summary>
    [HttpGet("GetAccounts")]
    [ProducesResponseType(typeof(IEnumerable<AccountSearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? firstName,
        [FromQuery] string? lastName,
        [FromQuery] string? contactInfo,
        CancellationToken cancellationToken)
    {
        firstName = firstName?.Trim();
        lastName = lastName?.Trim();
        contactInfo = contactInfo?.Trim();

        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName) && string.IsNullOrEmpty(contactInfo))
        {
            ModelState.AddModelError(
                string.Empty,
                $"At least one of {nameof(firstName)}, {nameof(lastName)} or {nameof(contactInfo)} is required.");
            return ValidationProblem(ModelState);
        }

        // Contains() becomes LIKE '%...%'. The DB collation is Thai_100_CS_AI, so it is case-sensitive.
        var accounts = _db.TblAccounts.AsNoTracking().Where(a => a.IsEnable == "T");

        if (!string.IsNullOrEmpty(firstName))
        {
            accounts = accounts.Where(a => a.FirstnameTh!.Contains(firstName) || a.FirstnameEn!.Contains(firstName));
        }

        if (!string.IsNullOrEmpty(lastName))
        {
            accounts = accounts.Where(a => a.LastnameTh!.Contains(lastName) || a.LastnameEn!.Contains(lastName));
        }

        if (!string.IsNullOrEmpty(contactInfo))
        {
            // Collect the matching ids with a UNION, then filter with IN. Neither table has an index on
            // these columns (TblAccount is a heap, TblAccountDetail has none on AccountId), and the
            // obvious "Address LIKE ... OR EXISTS (detail ...)" makes SQL Server re-scan
            // TblAccountDetail once per account: over 30 s, so the request timed out. The UNION scans
            // each table once and finishes in well under 100 ms.
            var matchingIds = _db.TblAccounts
                .Where(a => a.Address!.Contains(contactInfo))
                .Select(a => a.Id)
                .Union(_db.TblAccountDetails
                    .Where(d => d.IsEnable == "T" && d.Detail.Contains(contactInfo))
                    .Select(d => d.AccountId));

            accounts = accounts.Where(a => matchingIds.Contains(a.Id));
        }

        // Projected and untracked: TblAccount.Id has legacy duplicates, see ApplicationDbContext.Keys.cs.
        var results = await (
            from account in accounts
            join gender in _db.TblGenders.AsNoTracking() on account.GenderId equals gender.Id into genders
            from gender in genders.DefaultIfEmpty()
            orderby account.FirstnameTh, account.LastnameTh
            select new AccountSearchResponse
            {
                Id = account.Id,
                SalutationTh = account.SalutationTh,
                FirstnameTh = account.FirstnameTh,
                LastnameTh = account.LastnameTh,
                SalutationEn = account.SalutationEn,
                FirstnameEn = account.FirstnameEn,
                LastnameEn = account.LastnameEn,
                GenderId = account.GenderId,
                Gender = gender != null ? gender.NameTh : null,
                Birthdate = account.Birthdate,
                Address = account.Address,
                AreaId = account.AreaId,
                Zipcode = account.Zipcode,
                Remark = account.Remark,
                IsScret = account.IsScret,
                IsEnable = account.IsEnable,
                Created = account.Created,
            }).Take(MaxSearchResults).ToListAsync(cancellationToken);

        // Second query for the contacts of the accounts found, rather than one per account.
        var accountIds = results.Select(r => r.Id).Distinct().ToList();
        var contacts = (await _db.TblAccountDetails
                .AsNoTracking()
                .Where(d => accountIds.Contains(d.AccountId) && d.IsEnable == "T")
                .OrderBy(d => d.DetailType)
                .ThenBy(d => d.Detail)
                .Select(d => new { d.AccountId, d.DetailType, d.Detail })
                .ToListAsync(cancellationToken))
            .ToLookup(d => d.AccountId);

        foreach (var result in results)
        {
            result.FullName = BuildFullName(result.SalutationTh, result.FirstnameTh, result.LastnameTh);
            result.Contacts = contacts[result.Id]
                .Select(d => new AccountContactResponse { DetailType = d.DetailType, Detail = d.Detail })
                .ToList();
        }

        return Ok(results);
    }

    /// <summary>
    /// Returns one account by <c>TblAccount.Id</c>, with its gender name and enabled contact details, in
    /// the shape of a <see cref="GetAccounts"/> row. Disabled accounts are returned too, so the account
    /// of an old service can still be shown; <c>IsEnable</c> tells them apart.
    /// </summary>
    [HttpGet("GetAccountById")]
    [ProducesResponseType(typeof(AccountSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccountById(
        [FromQuery] string? id,
        CancellationToken cancellationToken)
    {
        // Ids are stored in upper case and the DB collation is case-sensitive.
        var accountId = id?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(accountId))
        {
            ModelState.AddModelError(nameof(id), "An id is required.");
            return ValidationProblem(ModelState);
        }

        var account = await _db.LoadAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return NotFound(new MessageResponse(StatusCodes.Status404NotFound, "ไม่พบข้อมูลผู้ติดต่อ"));
        }

        return Ok(account);
    }

    /// <summary>
    /// Replaces the editable columns of one account (names, gender, birth date, address, zip code,
    /// remark, flags) and returns it as <see cref="GetAccountById"/> does. Phone numbers and e-mail
    /// addresses (<c>TblAccountDetail</c>) are not touched. <c>Updated</c>/<c>UpdatedBy</c> come from the
    /// clock and the token; <c>Created</c>/<c>CreatedBy</c> are kept.
    /// </summary>
    [HttpPut("UpdateAccountById")]
    [ProducesResponseType(typeof(AccountSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAccountById(
        [FromQuery] string? id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var actingEmployeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(actingEmployeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot attribute the change."));
        }

        // Ids are stored in upper case and the DB collation is case-sensitive.
        var accountId = id?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(accountId))
        {
            ModelState.AddModelError(nameof(id), "An id is required.");
            return ValidationProblem(ModelState);
        }

        // Projected and untracked: TblAccount.Id has legacy duplicates, see ApplicationDbContext.Keys.cs.
        var current = await _db.TblAccounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new { a.AreaId })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return NotFound(new MessageResponse(StatusCodes.Status404NotFound, "ไม่พบข้อมูลผู้ติดต่อ"));
        }

        // TblArea is empty, so the legacy AreaId an account already has cannot pass the lookup check.
        // Keeping that value is allowed, or saving an unchanged form would fail.
        await ValidateLookupsAsync(request, cancellationToken, keepAreaId: current.AreaId);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTime.Now.ToString(LegacyDateFormat, CultureInfo.InvariantCulture);

        // A set-based UPDATE, so every duplicate row of this id gets the same values and none is lost
        // the way a tracked entity would collapse them.
        await _db.TblAccounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.SalutationTh, request.SalutationTh)
                    .SetProperty(a => a.FirstnameTh, request.FirstnameTh)
                    .SetProperty(a => a.LastnameTh, request.LastnameTh)
                    .SetProperty(a => a.SalutationEn, request.SalutationEn)
                    .SetProperty(a => a.FirstnameEn, request.FirstnameEn)
                    .SetProperty(a => a.LastnameEn, request.LastnameEn)
                    .SetProperty(a => a.GenderId, request.GenderId)
                    .SetProperty(a => a.Birthdate, request.Birthdate)
                    .SetProperty(a => a.Address, request.Address)
                    .SetProperty(a => a.AreaId, request.AreaId)
                    .SetProperty(a => a.Zipcode, request.Zipcode)
                    .SetProperty(a => a.Remark, request.Remark)
                    .SetProperty(a => a.IsScret, request.IsScret)
                    .SetProperty(a => a.IsEnable, request.IsEnable)
                    .SetProperty(a => a.Updated, now)
                    .SetProperty(a => a.UpdatedBy, actingEmployeeId),
                cancellationToken);

        _logger.LogInformation(
            "Account {AccountId} updated by {ActingEmployeeId}.",
            accountId,
            actingEmployeeId);

        return Ok(await _db.LoadAccountAsync(accountId, cancellationToken));
    }

    /// <summary>
    /// The schema has no FK constraints on these columns, so a bad id would otherwise be stored
    /// happily. Check them against the lookup tables and report them as validation errors.
    /// </summary>
    /// <param name="keepAreaId">An update passes the account's current <c>AreaId</c>, which is accepted unchecked.</param>
    private async Task ValidateLookupsAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken,
        string? keepAreaId = null)
    {
        if (!string.IsNullOrEmpty(request.GenderId)
            && !await _db.TblGenders.AsNoTracking().AnyAsync(g => g.Id == request.GenderId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.GenderId), $"No TblGender row with Id '{request.GenderId}'.");
        }

        if (!string.IsNullOrEmpty(request.AreaId)
            && request.AreaId != keepAreaId
            && !await _db.TblAreas.AsNoTracking().AnyAsync(a => a.Id == request.AreaId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.AreaId), $"No TblArea row with Id '{request.AreaId}'.");
        }
    }

    /// <summary>Joins the Thai name parts, skipping the ones that are missing.</summary>
    private static string BuildFullName(string? salutation, string? firstname, string? lastname) =>
        string.Join(' ', new[] { salutation, firstname, lastname }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static AccountResponse ToResponse(TblAccount account) => new()
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
        Zipcode = account.Zipcode,
        Remark = account.Remark,
        IsScret = account.IsScret,
        IsEnable = account.IsEnable,
        Created = account.Created,
        CreatedBy = account.CreatedBy,
        Updated = account.Updated,
        UpdatedBy = account.UpdatedBy,
    };
}
