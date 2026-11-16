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

    /// <summary><c>Code</c> คือ <c>yyMMdd</c> 6 หลัก ตามด้วยเลขรัน 5 หลัก</summary>
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
    /// (ดู <see cref="ServiceSearchRequest"/>) เรียงจากใหม่ไปเก่า คืนครั้งละหนึ่งหน้า ห่อด้วย
    /// <see cref="PagedEnvelope{T}"/> พร้อมจำนวนที่ค้นเจอทั้งหมด
    /// </summary>
    [HttpGet("GetServices")]
    [ProducesResponseType(typeof(PagedEnvelope<ServiceSearchResponse>), StatusCodes.Status200OK)]
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
            // Id last makes the order total, so a row cannot move between pages.
            orderby service.Created descending, service.Code descending, service.Id
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
            })
            .Skip((int)skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

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
        }).ToList();

        return Ok(Paged(results, total, request));
    }

    /// <summary>ห่อผลลัพธ์หนึ่งหน้าของ <see cref="GetServices"/> แบบเดียวกับ <c>GetReportXX</c></summary>
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
        await CheckStatusAsync();
        await CheckAsync(nameof(request.OwnerId), request.OwnerId, _db.TblEmployees.Select(x => x.Id!));
        await CheckAsync(nameof(request.ChannelIncomingId), request.ChannelIncomingId, _db.TblChannels.Select(x => x.Id));
        await CheckAsync(nameof(request.ChannelOutgoingId), request.ChannelOutgoingId, _db.TblChannels.Select(x => x.Id));
        await CheckAsync(nameof(request.AccountId), request.AccountId, _db.TblAccounts.Select(x => x.Id));
        await CheckAsync(nameof(request.OrganizationId), request.OrganizationId, _db.TblOrganizations.Select(x => x.Id));
        await CheckAsync(nameof(request.ServiceReference), request.ServiceReference, _db.TblReferences.Select(x => x.Id));
        await CheckAsync(nameof(request.AreaId), request.AreaId, _db.TblAreas.Select(x => x.Id));
        // One table holds all three kinds of level, so the id must also be of the right LevelType.
        await CheckAsync(nameof(request.LevelSeverityId), request.LevelSeverityId, LevelIds(LevelTypes.Severity));
        await CheckAsync(nameof(request.LevelPriorityId), request.LevelPriorityId, LevelIds(LevelTypes.Priority));
        await CheckAsync(nameof(request.LevelSecretId), request.LevelSecretId, LevelIds(LevelTypes.Secret));

        IQueryable<string> LevelIds(string levelType) =>
            _db.TblLevels.Where(x => x.LevelType == levelType).Select(x => x.Id);

        // TblStatus is a two-level tree per StatusType (main status -> sub-status), and services store
        // the sub-status, so a SERVICE main status that has enabled sub-statuses is rejected.
        async Task CheckStatusAsync()
        {
            if (string.IsNullOrEmpty(request.StatusId))
            {
                return;
            }

            var status = await _db.TblStatuses.AsNoTracking()
                .Where(s => s.Id == request.StatusId && s.StatusType == StatusTypes.Service)
                .Select(s => new
                {
                    HasChildren = _db.TblStatuses.Any(c => c.RefId == s.Id && c.Id != s.Id && c.IsEnable == "T"),
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (status is null)
            {
                ModelState.AddModelError(nameof(request.StatusId), $"No SERVICE status with Id '{request.StatusId}'.");
            }
            else if (status.HasChildren)
            {
                ModelState.AddModelError(nameof(request.StatusId),
                    $"'{request.StatusId}' is a main status; send the Id of one of its sub-statuses.");
            }
        }

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
