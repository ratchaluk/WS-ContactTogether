using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Helper;
using ContactTogetherApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

/// <summary>
/// รายการในหน้าแรกหลังล็อกอิน (กล่อง "รายการให้บริการ" ของระบบเดิม): แท็บรายการรับ และแท็บงานดำเนินการ
/// ทั้งสองแท็บใช้ตารางเดียวกัน ต่างกันแค่เงื่อนไข พนักงานมาจาก claim <c>sub</c> เสมอ ไม่รับจาก request
/// การรับงาน (ทำให้หายจากรายการรับ) ใช้ <c>POST /Services/ReceiveService</c> ที่มีอยู่แล้ว
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class HomeController : ControllerBase
{
    /// <summary>ค่าสูงสุดของ <see cref="HomeServiceListRequest.PageSize"/></summary>
    private const int MaxPageSize = 100;

    private const string LangTh = "th";
    private const string LangEn = "en";

    /// <summary>เรื่องที่เปิดมาแล้วตั้งแต่ 7 วันขึ้นไปถือว่าเกินกำหนด (ไอคอน <c>*_over_due</c> ของระบบเดิม)</summary>
    private const int OverDueMinutes = 7 * 24 * 60;

    /// <summary>ค่า <c>TblService.Call_Back</c> ที่หมายถึงต้องโทรกลับ <c>D</c> (สายหลุด) ไม่นับ เหมือนรายงาน 03</summary>
    private const string CallBackRequired = "Y";

    /// <summary>
    /// <c>TblStatus.IsType</c> ของเรื่องที่ยังไม่ปิด ระบบเดิมดูแค่ <c>PENDING</c> แต่ระบบนี้เปิดเรื่องซ้ำด้วยสถานะ
    /// <c>OPEN</c> (เช่น "เปิดเรื่อง") จึงนับทั้งสองค่า
    /// </summary>
    private static readonly string[] OpenStatusIsTypes = [StatusIsTypes.Open, StatusIsTypes.Pending];

    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// รายการรับ: เรื่องที่ยังไม่ปิด ซึ่งคนอื่นโอนงานมาให้พนักงานที่เรียก และยังไม่ได้รับงาน คือมีงาน
    /// (<c>TblActivity</c>) ที่เปิดใช้งาน มอบให้พนักงานคนนี้ <c>IsReceived = F</c> และสถานะของงานเป็นประเภท
    /// <c>CLOSE</c> (ขั้นตอนโอนทำเสร็จแล้ว) ตามระบบเดิม เรียงตาม <c>Code</c> มากไปน้อย แบ่งหน้าที่ DB
    /// </summary>
    [HttpGet("GetInboxServices")]
    [ProducesResponseType(typeof(PagedEnvelope<HomeServiceListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInboxServices(
        [FromQuery] HomeServiceListRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeId))
        {
            return NoEmployee();
        }

        // Any() returns a service once, however many such operations it has.
        var services = OpenServices().Where(service => _db.TblActivities.Any(activity =>
            activity.ServiceId == service.Id
            && activity.IsEnable == "T"
            && activity.OwnerId == employeeId
            && activity.IsReceived == "F"
            && _db.TblStatuses.Any(status => status.Id == activity.StatusId
                && status.IsType == StatusIsTypes.Close)));

        return await ListAsync(services, request, cancellationToken);
    }

    /// <summary>
    /// งานดำเนินการ: เรื่องที่ยังไม่ปิดซึ่งพนักงานที่เรียกเป็นเจ้าของ (<c>TblService.Owner_ID</c>)
    /// เรียงตาม <c>Code</c> มากไปน้อย แบ่งหน้าที่ DB เรื่องที่โอนมาแต่ยังไม่รับอยู่ทั้งแท็บนี้และรายการรับ (เหมือนระบบเดิม)
    /// </summary>
    [HttpGet("GetProcessingServices")]
    [ProducesResponseType(typeof(PagedEnvelope<HomeServiceListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProcessingServices(
        [FromQuery] HomeServiceListRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeId))
        {
            return NoEmployee();
        }

        var services = OpenServices().Where(service => service.OwnerId == employeeId);

        return await ListAsync(services, request, cancellationToken);
    }

    /// <summary>เรื่องที่เปิดใช้งาน และสถานะเป็นประเภทที่ยังไม่ปิด (<see cref="OpenStatusIsTypes"/>)</summary>
    private IQueryable<TblService> OpenServices() =>
        _db.TblServices.AsNoTracking().Where(service => service.IsEnable == "T"
            && _db.TblStatuses.Any(status => status.Id == service.StatusId
                && OpenStatusIsTypes.Contains(status.IsType)));

    /// <summary>
    /// ตรวจ query แล้วคืนหน้าที่ขอของ <paramref name="services"/> พร้อมชื่อประเภทและสถานะ
    /// ห่อด้วย <see cref="PagedEnvelope{T}"/> แบบเดียวกับ <c>SearchService</c>
    /// </summary>
    private async Task<IActionResult> ListAsync(
        IQueryable<TblService> services,
        HomeServiceListRequest request,
        CancellationToken cancellationToken)
    {
        var lang = string.IsNullOrWhiteSpace(request.Lang) ? LangTh : request.Lang.Trim().ToLowerInvariant();
        if (lang is not (LangTh or LangEn))
        {
            ModelState.AddModelError(nameof(request.Lang), $"{nameof(request.Lang)} must be '{LangTh}' or '{LangEn}'.");
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

        // Counted before the joins below: each adds at most one row per service (both ids are keys).
        var total = await services.CountAsync(cancellationToken);

        // long, because a huge PageNumber would overflow int into a negative OFFSET (a SQL error).
        var skip = (long)(request.PageNumber - 1) * request.PageSize;
        if (skip >= total)
        {
            return Ok(Paged([], total, request));
        }

        // No FK constraints, hence the manual joins. Every service here has a status (OpenServices
        // checks it); a missing category leaves the name null rather than hiding the service.
        var rows = await (
            from service in services
            join status in _db.TblStatuses.AsNoTracking() on service.StatusId equals status.Id
            join category in _db.TblCategories.AsNoTracking() on service.CategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            // Id after Code makes the order total, so a row cannot move between pages.
            orderby service.Code descending, service.Id
            select new
            {
                service.Id,
                service.Code,
                service.DateOpened,
                CategoryNameTh = category != null ? category.NameTh : null,
                CategoryNameEn = category != null ? category.NameEn : null,
                StatusNameTh = status.NameTh,
                StatusNameEn = status.NameEn,
                service.Summary,
                service.CallBack,
            })
            .Skip((int)skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Thai local time, like DateOpened.
        var now = DateTime.Now;
        var english = lang == LangEn;
        var data = rows.Select(row =>
        {
            var elapsed = now - row.DateOpened;
            return new HomeServiceListItemResponse
            {
                Id = row.Id,
                Code = row.Code,
                DateOpened = row.DateOpened,
                CategoryName = PickName(row.CategoryNameTh, row.CategoryNameEn, english),
                StatusName = PickName(row.StatusNameTh, row.StatusNameEn, english),
                Summary = row.Summary,
                CallBack = row.CallBack,
                RequireCallBack = row.CallBack == CallBackRequired,
                ElapsedHours = elapsed is { } e ? (int)Math.Round(e.TotalHours) : null,
                IsOverDue = elapsed?.TotalMinutes >= OverDueMinutes,
            };
        }).ToList();

        return Ok(Paged(data, total, request));
    }

    /// <summary>ชื่ออังกฤษเมื่อขอ <c>en</c> และมีค่า ไม่งั้นชื่อไทย</summary>
    private static string? PickName(string? nameTh, string? nameEn, bool english) =>
        english && !string.IsNullOrWhiteSpace(nameEn) ? nameEn : nameTh;

    private static PagedEnvelope<HomeServiceListItemResponse> Paged(
        IEnumerable<HomeServiceListItemResponse> data,
        int total,
        HomeServiceListRequest request) => new()
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

    private UnauthorizedObjectResult NoEmployee() => Unauthorized(new MessageResponse(
        StatusCodes.Status401Unauthorized,
        "Token has no sub claim; cannot identify the employee."));
}
