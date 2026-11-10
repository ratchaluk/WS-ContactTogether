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

    private readonly ApplicationDbContext _db;

    public ServicesController(ApplicationDbContext db)
    {
        _db = db;
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
