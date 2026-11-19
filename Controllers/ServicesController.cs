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
public class ServicesController : ControllerBase
{
    /// <summary>
    /// ค่า <c>TblAccountDetail.DetailType</c> ที่เป็นเบอร์โทร ไม่รวม <c>MAIL</c> โดยตั้งใจ
    /// เพราะเก็บอีเมล
    /// </summary>
    private static readonly string[] PhoneDetailTypes = ["MOBILE", "HOME", "OFFICE", "FAX"];

    /// <summary>ค่าสูงสุดของ <see cref="ServiceSearchRequest.PageSize"/> ให้แต่ละหน้าไม่หนักเกินไป</summary>
    private const int MaxPageSize = 200;

    /// <summary>ค่าของ <see cref="ServiceSearchRequest.SortOrder"/>: ล่าสุดก่อน (ค่าเริ่มต้น) และเก่าสุดก่อน</summary>
    private const string SortDescending = "desc";
    private const string SortAscending = "asc";

    /// <summary><c>Code</c> คือ <c>yyMMdd</c> 6 หลัก ตามด้วยเลขรัน 5 หลัก</summary>
    private const int ServiceCodeLength = 11;

    private const int MaxDailyRunningNumber = 99999;

    /// <summary>
    /// เพดานจำนวนชั้นของ path ประเภทบริการ/หน่วยงาน ข้อมูลจริงลึกไม่เกิน 4 ชั้น ที่ตั้งเพดานไว้ก็เพื่อกันข้อมูลที่ชี้วนกันเอง
    /// </summary>
    private const int MaxPathDepth = 10;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(ApplicationDbContext db, ILogger<ServicesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// คืนเรื่อง (<c>TblService</c>) ที่เปิดใช้งานและยังไม่ปิด ซึ่งพนักงานที่เรียกเป็นเจ้าของ
    /// ระบุตัวพนักงานจาก claim <c>sub</c> ใน token
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
    /// คืนเรื่อง (<c>TblService</c>) ที่เปิดใช้งาน และมีงาน (<c>TblActivity</c>) ที่ยังไม่ปิดและเปิดใช้งาน
    /// มอบหมายให้พนักงานที่เรียก ไม่ว่าใครจะเป็นเจ้าของเรื่อง ระบุตัวพนักงานจาก claim <c>sub</c> ใน token
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
    /// ค้นหาเรื่อง (<c>TblService</c>) ที่เปิดใช้งาน ต้องมีเงื่อนไขอย่างน้อย 1 ตัว และต้องตรงทุกเงื่อนไขที่ส่งมา
    /// (ดู <see cref="ServiceSearchRequest"/>) เรียงตามวันที่สร้าง ล่าสุดก่อนเป็นค่าเริ่มต้น หรือเก่าสุดก่อนเมื่อส่ง
    /// <c>SortOrder=asc</c> คืนครั้งละหนึ่งหน้า ห่อด้วย <see cref="PagedEnvelope{T}"/> พร้อมจำนวนที่ค้นเจอทั้งหมด
    /// </summary>
    [HttpGet("SearchService")]
    [ProducesResponseType(typeof(PagedEnvelope<ServiceSearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchService(
        [FromQuery] ServiceSearchRequest request,
        CancellationToken cancellationToken)
    {
        var phoneNo = Normalize(request.AccountPhoneNo);
        // TblService stores account ids in upper case and the DB collation is case-sensitive.
        var accountId = Normalize(request.AccountId)?.ToUpperInvariant();
        var statusId = Normalize(request.ServicesStatus);
        var code = Normalize(request.ServiceCode);
        var summary = Normalize(request.ServiceSummary);
        var categoryId = Normalize(request.ServiceType);
        // TblService stores it in upper case and the DB collation is case-sensitive.
        var serviceTitleId = Normalize(request.ServiceTitleId)?.ToUpperInvariant();
        var referenceId = Normalize(request.ServiceReference)?.ToUpperInvariant();
        var priorityId = Normalize(request.LevelPriorityId)?.ToUpperInvariant();
        var ownerId = Normalize(request.ServiceOwner);
        var organizationGroupId = Normalize(request.OrganizationGroup);
        var organizationId = Normalize(request.Organization);
        var createdFrom = request.ServiceCreatedFrom;
        // A bare date means the whole of that day. Created is datetime2(0), so stop at the last whole
        // second: a fractional value would be rounded up to the next midnight.
        var createdTo = request.ServiceCreatedTo is { TimeOfDay.Ticks: 0 } to
            ? to.AddDays(1).AddSeconds(-1)
            : request.ServiceCreatedTo;

        if (new[] { phoneNo, accountId, statusId, code, summary, categoryId, serviceTitleId, referenceId, priorityId, ownerId, organizationGroupId, organizationId }
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

        var sortOrder = Normalize(request.SortOrder)?.ToLowerInvariant() ?? SortDescending;
        if (sortOrder is not (SortDescending or SortAscending))
        {
            ModelState.AddModelError(
                nameof(request.SortOrder),
                $"{nameof(request.SortOrder)} must be '{SortDescending}' or '{SortAscending}'.");
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber must be 1 or greater.");
        }

        if (request.PageSize is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize must be between 1 and {MaxPageSize}.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var services = _db.TblServices.AsNoTracking().Where(s => s.IsEnable == "T");

        if (phoneNo is not null)
        {
            services = services.Where(s => _db.TblAccountDetails.Any(d => d.AccountId == s.AccountId
                && d.Detail == phoneNo
                && PhoneDetailTypes.Contains(d.DetailType)));
        }

        if (accountId is not null)
        {
            services = services.Where(s => s.AccountId == accountId);
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

        if (serviceTitleId is not null)
        {
            services = services.Where(s => s.ServiceTitleId == serviceTitleId);
        }

        if (referenceId is not null)
        {
            services = services.Where(s => s.ServiceReference == referenceId);
        }

        if (priorityId is not null)
        {
            services = services.Where(s => s.LevelPriorityId == priorityId);
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

        // Counted before the joins below: they add one row per service at most, so the total matches.
        var total = await services.CountAsync(cancellationToken);

        // long, because a huge PageNumber would overflow int into a negative OFFSET (a SQL error).
        var skip = (long)(request.PageNumber - 1) * request.PageSize;
        if (skip >= total)
        {
            return Ok(Paged([], total, request));
        }

        // None of these columns has an FK constraint, hence the manual joins. TblAccount.Id has legacy
        // duplicates, so the account is picked with a TOP 1 subquery rather than joined, which would
        // repeat the service once per duplicate.
        var projected =
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
            select new
            {
                service.Id,
                service.Code,
                service.AccountId,
                OwnerSalutation = owner != null ? owner.SalutationTh : null,
                OwnerFirstname = owner != null ? owner.FirstnameTh : null,
                OwnerLastname = owner != null ? owner.LastnameTh : null,
                AccountSalutation = account != null ? account.SalutationTh : null,
                AccountFirstname = account != null ? account.FirstnameTh : null,
                AccountLastname = account != null ? account.LastnameTh : null,
                Category = category != null ? category.NameTh : null,
                service.ServiceTitleId,
                service.Summary,
                Status = status != null ? status.NameTh : null,
                service.Created,
            };

        // Created, then Code, then Id makes the order total, so a row cannot move between pages.
        var ordered = sortOrder == SortAscending
            ? projected.OrderBy(r => r.Created).ThenBy(r => r.Code).ThenBy(r => r.Id)
            : projected.OrderByDescending(r => r.Created).ThenByDescending(r => r.Code).ThenBy(r => r.Id);

        var rows = await ordered
            .Skip((int)skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var results = rows.Select(r => new ServiceSearchResponse
        {
            ServiceId = r.Id,
            ServiceCode = r.Code,
            ServiceOwner = BuildFullName(r.OwnerSalutation, r.OwnerFirstname, r.OwnerLastname),
            AccountId = r.AccountId,
            AccountFullname = BuildFullName(r.AccountSalutation, r.AccountFirstname, r.AccountLastname),
            ServiceType = r.Category,
            ServiceTitleId = r.ServiceTitleId,
            ServiceSummary = r.Summary,
            ServicesStatus = r.Status,
            ServiceCreated = r.Created,
        }).ToList();

        return Ok(Paged(results, total, request));
    }

    /// <summary>
    /// คืนเรื่อง (<c>TblService</c>) ที่เปิดใช้งานหนึ่งเรื่องตาม <c>Id</c> สำหรับหน้ารายละเอียดเรื่อง พร้อมชื่อของ
    /// lookup ทุกตัว และ path ของประเภทบริการและหน่วยงาน (ดู <see cref="ServiceDetailResponse"/>)
    /// </summary>
    [HttpGet("GetServiceById")]
    [ProducesResponseType(typeof(ServiceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceById(
        [FromQuery] string? id,
        CancellationToken cancellationToken)
    {
        // Ids are stored in upper case and the DB collation is case-sensitive.
        var serviceId = Normalize(id)?.ToUpperInvariant();
        if (serviceId is null)
        {
            ModelState.AddModelError(nameof(id), "An id is required.");
            return ValidationProblem(ModelState);
        }

        // None of these columns has an FK constraint, hence the subqueries. The lookup ids are unique,
        // except TblAccount.Id, which has legacy duplicates: FirstOrDefault() picks one of them.
        var row = await (
            from service in _db.TblServices.AsNoTracking()
            where service.Id == serviceId && service.IsEnable == "T"
            let status = _db.TblStatuses
                .Where(s => s.Id == service.StatusId)
                .Select(s => new { s.NameTh, s.NameEn, s.IsType })
                .FirstOrDefault()
            let owner = _db.TblEmployees
                .Where(e => e.Id == service.OwnerId)
                .Select(e => new { e.SalutationTh, e.FirstnameTh, e.LastnameTh })
                .FirstOrDefault()
            let createdBy = _db.TblEmployees
                .Where(e => e.Id == service.CreatedBy)
                .Select(e => new { e.SalutationTh, e.FirstnameTh, e.LastnameTh })
                .FirstOrDefault()
            let updatedBy = _db.TblEmployees
                .Where(e => e.Id == service.UpdatedBy)
                .Select(e => new { e.SalutationTh, e.FirstnameTh, e.LastnameTh })
                .FirstOrDefault()
            select new
            {
                Service = service,
                StatusNameTh = status != null ? status.NameTh : null,
                StatusNameEn = status != null ? status.NameEn : null,
                StatusIsType = status != null ? status.IsType : null,
                Owner = owner,
                CreatedBy = createdBy,
                UpdatedBy = updatedBy,
                ChannelIncomingName = _db.TblChannels
                    .Where(c => c.Id == service.ChannelIncomingId).Select(c => c.NameTh).FirstOrDefault(),
                ChannelOutgoingName = _db.TblChannels
                    .Where(c => c.Id == service.ChannelOutgoingId).Select(c => c.NameTh).FirstOrDefault(),
                ServiceReferenceName = _db.TblReferences
                    .Where(r => r.Id == service.ServiceReference).Select(r => r.NameTh).FirstOrDefault(),
                LevelSeverityName = _db.TblLevels
                    .Where(l => l.Id == service.LevelSeverityId).Select(l => l.NameTh).FirstOrDefault(),
                LevelPriorityName = _db.TblLevels
                    .Where(l => l.Id == service.LevelPriorityId).Select(l => l.NameTh).FirstOrDefault(),
                LevelSecretName = _db.TblLevels
                    .Where(l => l.Id == service.LevelSecretId).Select(l => l.NameTh).FirstOrDefault(),
            }).FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return NotFound(new MessageResponse(StatusCodes.Status404NotFound, "ไม่พบข้อมูลเรื่อง"));
        }

        var found = row.Service;

        // TblServiceTitles.Id is a uniqueidentifier, so parse it rather than compare text.
        string? serviceTitleText = null;
        if (Guid.TryParse(found.ServiceTitleId, out var titleId))
        {
            serviceTitleText = await _db.TblServiceTitles.AsNoTracking()
                .Where(t => t.Id == titleId)
                .Select(t => t.ServiceTitleText)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var categoryPath = await BuildPathAsync(
            _db.TblCategories.Select(c => new PathNode(c.Id, c.RefId, c.NameTh)),
            found.CategoryId,
            cancellationToken);
        var organizationPath = await BuildPathAsync(
            _db.TblOrganizations.Select(o => new PathNode(o.Id, o.RefId, o.NameTh)),
            found.OrganizationId,
            cancellationToken);

        var areaPath = await _db.LoadAreaPathAsync(found.AreaId, cancellationToken);

        var account = await _db.LoadAccountAsync(found.AccountId, cancellationToken);

        return Ok(new ServiceDetailResponse
        {
            Id = found.Id,
            Code = found.Code,
            CategoryId = found.CategoryId,
            StatusId = found.StatusId,
            StatusNameTh = row.StatusNameTh,
            StatusNameEn = row.StatusNameEn,
            ServiceTitleId = found.ServiceTitleId,
            OwnerId = found.OwnerId,
            DateOpened = found.DateOpened,
            DateClosed = found.DateClosed,
            ChannelIncomingId = found.ChannelIncomingId,
            CallBack = found.CallBack,
            ChannelOutgoingId = found.ChannelOutgoingId,
            AccountId = found.AccountId,
            Summary = found.Summary,
            Detail = found.Detail,
            ServiceArea = found.ServiceArea,
            OrganizationId = found.OrganizationId,
            OnScene = found.OnScene,
            ServiceReference = found.ServiceReference,
            ServiceReferenceLink = found.ServiceReferenceLink,
            AreaId = found.AreaId,
            Remark = found.Remark,
            LevelSeverityId = found.LevelSeverityId,
            LevelPriorityId = found.LevelPriorityId,
            LevelSecretId = found.LevelSecretId,
            IsEnable = found.IsEnable,
            Created = found.Created,
            CreatedBy = found.CreatedBy,
            Updated = found.Updated,
            UpdatedBy = found.UpdatedBy,
            StatusIsType = row.StatusIsType,
            ServiceTitleText = serviceTitleText,
            OwnerName = row.Owner is { } o ? BuildFullName(o.SalutationTh, o.FirstnameTh, o.LastnameTh) : null,
            CreatedByName = row.CreatedBy is { } c ? BuildFullName(c.SalutationTh, c.FirstnameTh, c.LastnameTh) : null,
            UpdatedByName = row.UpdatedBy is { } u ? BuildFullName(u.SalutationTh, u.FirstnameTh, u.LastnameTh) : null,
            AccountFullName = account?.FullName,
            Account = account,
            ChannelIncomingName = row.ChannelIncomingName,
            ChannelOutgoingName = row.ChannelOutgoingName,
            ServiceReferenceName = row.ServiceReferenceName,
            LevelSeverityName = row.LevelSeverityName,
            LevelPriorityName = row.LevelPriorityName,
            LevelSecretName = row.LevelSecretName,
            CategoryPath = categoryPath,
            OrganizationPath = organizationPath,
            AreaPath = areaPath,
        });
    }

    /// <summary>One row of a self-referencing lookup table (<c>TblCategory</c>, <c>TblOrganization</c>).</summary>
    private sealed record PathNode(string Id, string RefId, string NameTh);

    /// <summary>
    /// Walks <c>RefId</c> up from <paramref name="id"/> and returns the chain top level first. A top-level
    /// row points <c>RefId</c> at itself. The tables are small (under a hundred rows), so they are loaded
    /// whole rather than queried once per level. The walk also stops at a missing id or after
    /// <see cref="MaxPathDepth"/> levels, in case bad data forms a cycle.
    /// </summary>
    private static async Task<List<LookupItemResponse>> BuildPathAsync(
        IQueryable<PathNode> nodes,
        string? id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return [];
        }

        var byId = (await nodes.AsNoTracking().ToListAsync(cancellationToken))
            .GroupBy(n => n.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var path = new List<LookupItemResponse>();
        var currentId = id;
        while (path.Count < MaxPathDepth && byId.TryGetValue(currentId, out var node))
        {
            path.Add(new LookupItemResponse { Id = node.Id, NameTh = node.NameTh });
            if (node.RefId == node.Id)
            {
                break;
            }

            currentId = node.RefId;
        }

        path.Reverse();
        return path;
    }

    /// <summary>ห่อผลลัพธ์หนึ่งหน้าของ <see cref="SearchService"/> แบบเดียวกับ <c>GetReportXX</c></summary>
    private static PagedEnvelope<ServiceSearchResponse> Paged(
        IEnumerable<ServiceSearchResponse> data,
        int total,
        ServiceSearchRequest request) => new()
    {
        Status = "success",
        Message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ",
        Data = data,
        Meta = new PagedMeta
        {
            Pagination = new PaginationMeta
            {
                CurrentPage = request.PageNumber,
                PerPage = request.PageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
            },
        },
    };

    /// <summary>
    /// เปิดเรื่องใหม่ (เพิ่มแถวใน <c>TblService</c>) แล้วคืนแถวนั้นกลับไป <c>Code</c> สร้างให้อัตโนมัติเป็น
    /// <c>yyMMdd</c> แบบปี พ.ศ. ตามด้วยเลขรัน 5 หลักที่เริ่มนับใหม่ทุกวัน
    /// (เช่น <c>69082500001</c> คือเรื่องแรกของวันที่ 25 ส.ค. 2569) <c>IsEnable</c> เป็น <c>T</c> เสมอ
    /// ส่วน <c>CreatedBy</c>/<c>UpdatedBy</c> มาจาก token
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
            // Upper case, as the existing rows and GetServiceTitle have it (the collation is case-sensitive).
            ServiceTitleId = request.ServiceTitleId!.Trim().ToUpperInvariant(),
            // A blank owner (e.g. an unselected drop-down sends "") means the calling employee too.
            OwnerId = string.IsNullOrWhiteSpace(request.OwnerId) ? actingEmployeeId : request.OwnerId.Trim(),
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
                Updated, Date_Opened, Date_Closed, On_Scene, ServiceTitleId)
            VALUES (
                {service.Id}, {service.Code}, {service.CategoryId}, {service.StatusId}, {service.OwnerId},
                {service.ChannelIncomingId}, {service.CallBack}, {service.ChannelOutgoingId},
                {service.AccountId}, {service.Summary}, {service.Detail}, {service.ServiceArea},
                {service.OrganizationId}, {service.ServiceReference}, {service.ServiceReferenceLink},
                {service.AreaId}, {service.Remark}, {service.LevelSeverityId}, {service.LevelPriorityId},
                {service.LevelSecretId}, {service.IsEnable}, {service.CreatedBy}, {service.UpdatedBy},
                {service.Created}, {service.Updated}, {service.DateOpened}, {service.DateClosed},
                {service.OnScene}, {service.ServiceTitleId})
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
    /// <c>yyMMdd</c> โดยใช้เลข 2 หลักท้ายของปี พ.ศ. เช่น <c>690825</c> คือวันที่
    /// 25 ส.ค. 2026 (พ.ศ. 2569)
    /// </summary>
    private static string ServiceCodePrefix(DateTime date) =>
        string.Create(CultureInfo.InvariantCulture, $"{(date.Year + 543) % 100:D2}{date.Month:D2}{date.Day:D2}");

    /// <summary>
    /// คอลัมน์เหล่านี้ไม่มี FK constraint ใน DB ถ้าไม่ตรวจ id ที่ผิดก็จะถูกบันทึกลงไปเฉย ๆ
    /// จึงตรวจกับตาราง lookup แต่ละตัว แล้วรายงานเป็น validation error
    /// </summary>
    private async Task ValidateLookupsAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        await CheckAsync(nameof(request.CategoryId), request.CategoryId, _db.TblCategories.Select(x => x.Id));
        await CheckAsync(nameof(request.StatusId), request.StatusId,
            _db.TblStatuses.Where(x => x.StatusType == StatusTypes.Service).Select(x => x.Id));
        await CheckAsync(nameof(request.OwnerId), request.OwnerId, _db.TblEmployees.Select(x => x.Id!));
        await CheckAsync(nameof(request.ChannelIncomingId), request.ChannelIncomingId, _db.TblChannels.Select(x => x.Id));
        await CheckAsync(nameof(request.ChannelOutgoingId), request.ChannelOutgoingId, _db.TblChannels.Select(x => x.Id));
        await CheckAsync(nameof(request.AccountId), request.AccountId, _db.TblAccounts.Select(x => x.Id));
        await CheckAsync(nameof(request.OrganizationId), request.OrganizationId, _db.TblOrganizations.Select(x => x.Id));
        await CheckAsync(nameof(request.ServiceReference), request.ServiceReference, _db.TblReferences.Select(x => x.Id));
        await CheckAsync(nameof(request.AreaId), request.AreaId, _db.TblAreas.Select(x => x.Id));

        // TblServiceTitles.Id is a uniqueidentifier, so parse it rather than compare text. [Required]
        // has already reported a missing value.
        if (!string.IsNullOrWhiteSpace(request.ServiceTitleId))
        {
            if (!Guid.TryParse(request.ServiceTitleId.Trim(), out var titleId)
                || !await _db.TblServiceTitles.AnyAsync(t => t.Id == titleId && t.IsEnable == "T", cancellationToken))
            {
                ModelState.AddModelError(
                    nameof(request.ServiceTitleId),
                    $"No enabled TblServiceTitles row with Id '{request.ServiceTitleId}'.");
            }
        }
        // One table holds all three kinds of level, so the id must also be of the right LevelType.
        await CheckAsync(nameof(request.LevelSeverityId), request.LevelSeverityId, LevelIds(LevelTypes.Severity));
        await CheckAsync(nameof(request.LevelPriorityId), request.LevelPriorityId, LevelIds(LevelTypes.Priority));
        await CheckAsync(nameof(request.LevelSecretId), request.LevelSecretId, LevelIds(LevelTypes.Secret));

        IQueryable<string> LevelIds(string levelType) =>
            _db.TblLevels.Where(x => x.LevelType == levelType).Select(x => x.Id);

        async Task CheckAsync(string field, string? id, IQueryable<string> ids)
        {
            if (!string.IsNullOrEmpty(id) && !await ids.AnyAsync(x => x == id, cancellationToken))
            {
                ModelState.AddModelError(field, $"No row with Id '{id}'.");
            }
        }
    }

    /// <summary>ตัดช่องว่างหน้า/หลังของค่าจาก query string ค่าว่างหรือมีแต่ช่องว่างถือว่าไม่ได้ส่งมา</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>ต่อคำนำหน้า ชื่อ และนามสกุลภาษาไทย ข้ามส่วนที่ไม่มีค่า</summary>
    private static string BuildFullName(string? salutation, string? firstname, string? lastname) =>
        string.Join(' ', new[] { salutation, firstname, lastname }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>
    /// เติมชื่อสถานะของแต่ละเรื่อง และเรียงตาม <c>Code</c> TblService ไม่มี key ผู้เรียกจึงต้องส่ง query
    /// แบบ untracked มา Status_ID ไม่มี FK constraint จึงต้อง left join เอง
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
                ServiceTitleId = service.ServiceTitleId,
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
