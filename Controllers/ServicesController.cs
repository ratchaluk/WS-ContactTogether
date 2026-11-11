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
public class ServicesController : ControllerBase
{
    /// <summary>
    /// <c>TblAccountDetail.DetailType</c> values that hold a telephone number; <c>MAIL</c> holds
    /// e-mail addresses and is deliberately excluded.
    /// </summary>
    private static readonly string[] PhoneDetailTypes = ["MOBILE", "HOME", "OFFICE", "FAX"];

    /// <summary>Upper bound on <see cref="GetServices"/> results, so a broad search stays cheap.</summary>
    private const int MaxSearchResults = 200;

    /// <summary><c>Code</c> is a six-digit <c>yyMMdd</c> prefix plus a five-digit running number.</summary>
    private const int ServiceCodeLength = 11;

    private const int MaxDailyRunningNumber = 99999;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(ApplicationDbContext db, ILogger<ServicesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns the enabled, not-yet-closed <c>TblService</c> rows owned by the calling employee. The
    /// employee is identified by the token's <c>sub</c> claim.
    /// </summary>
    [HttpGet("GetInProcessServices")]
    [ProducesResponseType(typeof(IEnumerable<ServiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInProcessServices(CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot identify the employee."));
        }

        var services = await ToResponses(_db.TblServices.AsNoTracking()
                .Where(service => service.OwnerId == employeeId
                    && service.DateClosed == null
                    && service.IsEnable == "T"))
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    /// <summary>
    /// Returns the enabled <c>TblService</c> rows that have an open, enabled <c>TblActivity</c>
    /// assigned to the calling employee, whoever owns the service itself. The employee is identified
    /// by the token's <c>sub</c> claim.
    /// </summary>
    [HttpGet("GetPendingServices")]
    [ProducesResponseType(typeof(IEnumerable<ServiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPendingServices(CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot identify the employee."));
        }

        // A service can carry several such activities; filtering with Any() returns it once.
        var services = await ToResponses(_db.TblServices.AsNoTracking()
                .Where(service => service.IsEnable == "T"
                    && _db.TblActivities.Any(activity => activity.ServiceId == service.Id
                        && activity.OwnerId == employeeId
                        && activity.DateClosed == null
                        && activity.IsEnable == "T")))
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    /// <summary>
    /// Searches enabled <c>TblService</c> rows. At least one filter is required, and every one given
    /// must match (see <see cref="ServiceSearchRequest"/>). Newest first, at most
    /// <see cref="MaxSearchResults"/> rows.
    /// </summary>
    [HttpGet("GetServices")]
    [ProducesResponseType(typeof(IEnumerable<ServiceSearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetServices(
        [FromQuery] ServiceSearchRequest request,
        CancellationToken cancellationToken)
    {
        var phoneNo = Normalize(request.AccountPhoneNo);
        var statusId = Normalize(request.ServicesStatus);
        var code = Normalize(request.ServiceCode);
        var summary = Normalize(request.ServiceSummary);
        var categoryId = Normalize(request.ServiceType);
        var ownerId = Normalize(request.ServiceOwner);
        var organizationGroupId = Normalize(request.OrganizationGroup);
        var organizationId = Normalize(request.Organization);
        var createdFrom = request.ServiceCreatedFrom;
        // A bare date means the whole of that day. Created is datetime2(0), so stop at the last whole
        // second: a fractional value would be rounded up to the next midnight.
        var createdTo = request.ServiceCreatedTo is { TimeOfDay.Ticks: 0 } to
            ? to.AddDays(1).AddSeconds(-1)
            : request.ServiceCreatedTo;

        if (new[] { phoneNo, statusId, code, summary, categoryId, ownerId, organizationGroupId, organizationId }
                .All(value => value is null)
            && createdFrom is null
            && createdTo is null)
        {
            ModelState.AddModelError(string.Empty, "At least one search parameter is required.");
            return ValidationProblem(ModelState);
        }

        if (createdFrom > createdTo)
        {
            ModelState.AddModelError(
                nameof(request.ServiceCreatedFrom),
                $"{nameof(request.ServiceCreatedFrom)} must not be after {nameof(request.ServiceCreatedTo)}.");
            return ValidationProblem(ModelState);
        }

        var services = _db.TblServices.AsNoTracking().Where(s => s.IsEnable == "T");

        if (phoneNo is not null)
        {
            services = services.Where(s => _db.TblAccountDetails.Any(d => d.AccountId == s.AccountId
                && d.Detail == phoneNo
                && PhoneDetailTypes.Contains(d.DetailType)));
        }

        if (statusId is not null)
        {
            services = services.Where(s => s.StatusId == statusId);
        }

        // Contains() becomes LIKE '%...%'. The DB collation is Thai_100_CS_AI, so it is case-sensitive.
        if (code is not null)
        {
            services = services.Where(s => s.Code!.Contains(code));
        }

        if (summary is not null)
        {
            services = services.Where(s => s.Summary!.Contains(summary));
        }

        if (categoryId is not null)
        {
            services = services.Where(s => s.CategoryId == categoryId);
        }

        if (ownerId is not null)
        {
            services = services.Where(s => s.OwnerId == ownerId);
        }

        if (organizationGroupId is not null)
        {
            services = services.Where(s => s.OrganizationId == organizationGroupId
                || _db.TblOrganizations.Any(o => o.Id == s.OrganizationId && o.RefId == organizationGroupId));
        }

        if (organizationId is not null)
        {
            services = services.Where(s => s.OrganizationId == organizationId);
        }

        if (createdFrom is not null)
        {
            services = services.Where(s => s.Created >= createdFrom);
        }

        if (createdTo is not null)
        {
            services = services.Where(s => s.Created <= createdTo);
        }

        // None of these columns has an FK constraint, hence the manual joins. TblAccount.Id has legacy
        // duplicates, so the account is picked with a TOP 1 subquery rather than joined, which would
        // repeat the service once per duplicate.
        var rows = await (
            from service in services
            join owner in _db.TblEmployees.AsNoTracking() on service.OwnerId equals owner.Id into owners
            from owner in owners.DefaultIfEmpty()
            join category in _db.TblCategories.AsNoTracking() on service.CategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            join status in _db.TblStatuses.AsNoTracking() on service.StatusId equals status.Id into statuses
            from status in statuses.DefaultIfEmpty()
            let account = _db.TblAccounts
                .Where(a => a.Id == service.AccountId)
                .Select(a => new { a.SalutationTh, a.FirstnameTh, a.LastnameTh })
                .FirstOrDefault()
            orderby service.Created descending, service.Code descending
            select new
            {
                service.Id,
                service.Code,
                OwnerSalutation = owner != null ? owner.SalutationTh : null,
                OwnerFirstname = owner != null ? owner.FirstnameTh : null,
                OwnerLastname = owner != null ? owner.LastnameTh : null,
                AccountSalutation = account != null ? account.SalutationTh : null,
                AccountFirstname = account != null ? account.FirstnameTh : null,
                AccountLastname = account != null ? account.LastnameTh : null,
                Category = category != null ? category.NameTh : null,
                service.Summary,
                Status = status != null ? status.NameTh : null,
                service.Created,
            }).Take(MaxSearchResults).ToListAsync(cancellationToken);

        var results = rows.Select(r => new ServiceSearchResponse
        {
            ServiceId = r.Id,
            ServiceCode = r.Code,
            ServiceOwner = BuildFullName(r.OwnerSalutation, r.OwnerFirstname, r.OwnerLastname),
            AccountFullname = BuildFullName(r.AccountSalutation, r.AccountFirstname, r.AccountLastname),
            ServiceType = r.Category,
            ServiceSummary = r.Summary,
            ServicesStatus = r.Status,
            ServiceCreated = r.Created,
        });

        return Ok(results);
    }

    /// <summary>
    /// Creates a <c>TblService</c> row and returns it. <c>Code</c> is generated as
    /// <c>yyMMdd</c> in the Buddhist year plus a five-digit running number that restarts every day
    /// (e.g. <c>69082500001</c> is the first service of 25 Aug 2569). <c>IsEnable</c> is always
    /// <c>T</c>; <c>CreatedBy</c>/<c>UpdatedBy</c> come from the token.
    /// </summary>
    [HttpPost("AddNewService")]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddNewService(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var actingEmployeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(actingEmployeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot attribute the new service."));
        }

        await ValidateLookupsAsync(request, cancellationToken);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Thai local time, whole seconds (the columns are datetime2(0)).
        var now = DateTime.Now;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

        var service = new TblService
        {
            Id = GuidStringValueGenerator.NewId(),
            CategoryId = request.CategoryId,
            StatusId = request.StatusId,
            OwnerId = request.OwnerId ?? actingEmployeeId,
            ChannelIncomingId = request.ChannelIncomingId,
            CallBack = request.CallBack,
            ChannelOutgoingId = request.ChannelOutgoingId,
            AccountId = request.AccountId,
            Summary = request.Summary,
            Detail = request.Detail,
            ServiceArea = request.ServiceArea,
            OrganizationId = request.OrganizationId,
            ServiceReference = request.ServiceReference,
            ServiceReferenceLink = request.ServiceReferenceLink,
            AreaId = request.AreaId,
            Remark = request.Remark,
            LevelSeverityId = request.LevelSeverityId,
            LevelPriorityId = request.LevelPriorityId,
            LevelSecretId = request.LevelSecretId,
            IsEnable = "T",
            Created = now,
            CreatedBy = actingEmployeeId,
            Updated = now,
            UpdatedBy = actingEmployeeId,
            DateOpened = request.DateOpened ?? now,
            DateClosed = request.DateClosed,
            OnScene = request.OnScene,
        };

        // TblService is keyless in the model, so EF cannot Add() it; insert with SQL instead. The
        // app lock makes concurrent requests take numbers one at a time, so no two get the same code.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC sp_getapplock @Resource = 'TblService.Code', @LockMode = 'Exclusive', @LockOwner = 'Transaction'",
            cancellationToken);

        var prefix = ServiceCodePrefix(now);
        var lastCode = await _db.TblServices.AsNoTracking()
            .Where(s => s.Code!.StartsWith(prefix) && s.Code.Length == ServiceCodeLength)
            .MaxAsync(s => s.Code, cancellationToken);
        var next = lastCode is null ? 1 : int.Parse(lastCode[prefix.Length..], CultureInfo.InvariantCulture) + 1;
        if (next > MaxDailyRunningNumber)
        {
            return Conflict(new MessageResponse(
                StatusCodes.Status409Conflict,
                "The daily limit of service codes has been reached."));
        }

        service.Code = prefix + next.ToString("D5", CultureInfo.InvariantCulture);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TblService (
                Id, Code, Category_ID, Status_ID, Owner_ID, Channel_Incoming_ID, Call_Back,
                Channel_Outgoing_ID, Account_ID, Summary, Detail, Service_Area, Organization_ID,
                Service_Reference, Service_Reference_Link, Area_ID, Remark, Level_Severity_ID,
                Level_Priority_ID, Level_Secret_ID, Is_Enable, Created_By, Updated_By, Created,
                Updated, Date_Opened, Date_Closed, On_Scene)
            VALUES (
                {service.Id}, {service.Code}, {service.CategoryId}, {service.StatusId}, {service.OwnerId},
                {service.ChannelIncomingId}, {service.CallBack}, {service.ChannelOutgoingId},
                {service.AccountId}, {service.Summary}, {service.Detail}, {service.ServiceArea},
                {service.OrganizationId}, {service.ServiceReference}, {service.ServiceReferenceLink},
                {service.AreaId}, {service.Remark}, {service.LevelSeverityId}, {service.LevelPriorityId},
                {service.LevelSecretId}, {service.IsEnable}, {service.CreatedBy}, {service.UpdatedBy},
                {service.Created}, {service.Updated}, {service.DateOpened}, {service.DateClosed},
                {service.OnScene})
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Service {ServiceCode} ({ServiceId}) created by {ActingEmployeeId}.",
            service.Code,
            service.Id,
            actingEmployeeId);

        var created = await ToResponses(_db.TblServices.AsNoTracking().Where(s => s.Id == service.Id))
            .FirstAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// <c>yyMMdd</c> with the last two digits of the Buddhist year, e.g. <c>690825</c> for
    /// 25 Aug 2026 (2569 B.E.).
    /// </summary>
    private static string ServiceCodePrefix(DateTime date) =>
        string.Create(CultureInfo.InvariantCulture, $"{(date.Year + 543) % 100:D2}{date.Month:D2}{date.Day:D2}");

    /// <summary>
    /// The schema has no FK constraints on these columns, so a bad id would otherwise be stored
    /// happily. Check them against the lookup tables and report them as validation errors.
    /// </summary>
    private async Task ValidateLookupsAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        await CheckAsync(nameof(request.CategoryId), request.CategoryId, _db.TblCategories.Select(x => x.Id));
        await CheckAsync(nameof(request.StatusId), request.StatusId, _db.TblStatuses.Select(x => x.Id));
        await CheckAsync(nameof(request.OwnerId), request.OwnerId, _db.TblEmployees.Select(x => x.Id!));
        await CheckAsync(nameof(request.ChannelIncomingId), request.ChannelIncomingId, _db.TblChannels.Select(x => x.Id));
        await CheckAsync(nameof(request.ChannelOutgoingId), request.ChannelOutgoingId, _db.TblChannels.Select(x => x.Id));
        await CheckAsync(nameof(request.AccountId), request.AccountId, _db.TblAccounts.Select(x => x.Id));
        await CheckAsync(nameof(request.OrganizationId), request.OrganizationId, _db.TblOrganizations.Select(x => x.Id));
        await CheckAsync(nameof(request.AreaId), request.AreaId, _db.TblAreas.Select(x => x.Id));
        await CheckAsync(nameof(request.LevelSeverityId), request.LevelSeverityId, _db.TblLevels.Select(x => x.Id));
        await CheckAsync(nameof(request.LevelPriorityId), request.LevelPriorityId, _db.TblLevels.Select(x => x.Id));
        await CheckAsync(nameof(request.LevelSecretId), request.LevelSecretId, _db.TblLevels.Select(x => x.Id));

        async Task CheckAsync(string field, string? id, IQueryable<string> ids)
        {
            if (!string.IsNullOrEmpty(id) && !await ids.AnyAsync(x => x == id, cancellationToken))
            {
                ModelState.AddModelError(field, $"No row with Id '{id}'.");
            }
        }
    }

    /// <summary>Trims a query-string value, treating an empty or blank one as not given.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Joins the Thai name parts, skipping the ones that are missing.</summary>
    private static string BuildFullName(string? salutation, string? firstname, string? lastname) =>
        string.Join(' ', new[] { salutation, firstname, lastname }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>
    /// Adds each service's status name and orders the rows by <c>Code</c>. TblService is keyless, so
    /// callers pass an untracked query. Status_ID has no FK constraint, hence the manual left join.
    /// </summary>
    private IQueryable<ServiceResponse> ToResponses(IQueryable<TblService> services) =>
            from service in services
            join status in _db.TblStatuses.AsNoTracking() on service.StatusId equals status.Id into statuses
            from status in statuses.DefaultIfEmpty()
            orderby service.Code
            select new ServiceResponse
            {
                Id = service.Id,
                Code = service.Code,
                CategoryId = service.CategoryId,
                StatusId = service.StatusId,
                StatusNameTh = status != null ? status.NameTh : null,
                StatusNameEn = status != null ? status.NameEn : null,
                OwnerId = service.OwnerId,
                DateOpened = service.DateOpened,
                DateClosed = service.DateClosed,
                ChannelIncomingId = service.ChannelIncomingId,
                CallBack = service.CallBack,
                ChannelOutgoingId = service.ChannelOutgoingId,
                AccountId = service.AccountId,
                Summary = service.Summary,
                Detail = service.Detail,
                ServiceArea = service.ServiceArea,
                OrganizationId = service.OrganizationId,
                OnScene = service.OnScene,
                ServiceReference = service.ServiceReference,
                ServiceReferenceLink = service.ServiceReferenceLink,
                AreaId = service.AreaId,
                Remark = service.Remark,
                LevelSeverityId = service.LevelSeverityId,
                LevelPriorityId = service.LevelPriorityId,
                LevelSecretId = service.LevelSecretId,
                IsEnable = service.IsEnable,
                Created = service.Created,
                CreatedBy = service.CreatedBy,
                Updated = service.Updated,
                UpdatedBy = service.UpdatedBy,
            };
}
