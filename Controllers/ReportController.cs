using System.Globalization;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Reports.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContactTogetherApi.Controllers;

[ApiController]
[Route("[controller]")]
//[Authorize]
public partial class ReportController : ControllerBase
{
    /// <summary>Upper bound on <c>PageSize</c> so a single page cannot pull the whole table.</summary>
    private const int MaxPageSize = 200;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReportController> _logger;
    private readonly ReportExportOptions _exportOptions;

    public ReportController(
        ApplicationDbContext db,
        ILogger<ReportController> logger,
        IOptions<ReportExportOptions> exportOptions)
    {
        _db = db;
        _logger = logger;
        _exportOptions = exportOptions.Value;
    }


    [HttpPost("GetReport01")]
    [ProducesResponseType(typeof(PagedEnvelope<ServiceRequestReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport01(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await LoadReport01Async(start, finish, cancellationToken);

            var total = result.Count;

            var data = result
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<ServiceRequestReportDto>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport01 failed for {Start} - {Finish}.", start, finish);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    /// <summary>
    /// Parses ISO 8601 local date-times (<c>yyyy-MM-ddTHH:mm:ss</c>, seconds optional, no offset)
    /// into an inclusive <c>DateTime</c> range. Values are Thai local time, like the
    /// <c>datetime2</c> columns they are compared with, so no time-zone conversion happens.
    /// </summary>
    private static bool TryBuildDateTimeRange(
        ServiceRequestReportDateTimeRequest request,
        out DateTime start,
        out DateTime finish,
        out string error)
    {
        start = finish = default;
        var culture = CultureInfo.InvariantCulture;
        // Seconds are optional because <input type="datetime-local"> omits them.
        string[] formats = ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm"];

        if (string.IsNullOrWhiteSpace(request.P_Start) || string.IsNullOrWhiteSpace(request.P_Finish))
        {
            error = "กรุณาระบุ P_Start และ P_Finish";
            return false;
        }

        if (!DateTime.TryParseExact(request.P_Start, formats, culture, DateTimeStyles.None, out start))
        {
            error = "P_Start ต้องอยู่ในรูปแบบ yyyy-MM-ddTHH:mm:ss";
            return false;
        }

        if (!DateTime.TryParseExact(request.P_Finish, formats, culture, DateTimeStyles.None, out finish))
        {
            error = "P_Finish ต้องอยู่ในรูปแบบ yyyy-MM-ddTHH:mm:ss";
            return false;
        }

        if (start > finish)
        {
            error = "P_Start ต้องไม่มากกว่า P_Finish";
            return false;
        }

        error = string.Empty;
        return true;
    }

    [HttpPost("GetReport02")]
    [ProducesResponseType(typeof(PagedEnvelope<SrCallbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport2(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        _logger.LogInformation("GetReport02 range: {Start} - {Finish}", start, finish);

        // ============================================================
        // Query
        // ============================================================

        try
        {
            var result = await LoadReport02Async(start, finish, cancellationToken);

            var total = result.Count;

            var data = result
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<SrCallbackResponse>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport02 failed for {Start} - {Finish}.", start, finish);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    [HttpPost("GetReport03")]
    [ProducesResponseType(typeof(PagedEnvelope<SrCallbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport3(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // ============================================================
        // Query
        // ============================================================

        try
        {
            var result = await LoadReport03Async(start, finish, cancellationToken);

            var total = result.Count;

            var data = result
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<SrCallbackResponse>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport03 failed for {Start} - {Finish}.", start, finish);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    [HttpPost("GetReport04")]
    [ProducesResponseType(typeof(PagedEnvelope<SrCallbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport4(
        [FromBody] ServiceRequestReportRequestMainOrganization request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // A missing organization means "all organizations".
        var mainOrgId = NormalizeId(request.P_MainOrg);
        var subOrgId = NormalizeId(request.P_SubOrg);
        // ============================================================
        // Query
        // ============================================================

        try
        {
            var result = await LoadReport04Async(start, finish, mainOrgId, subOrgId, cancellationToken);

            var total = result.Count;

            var data = result
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<SrCallbackResponse>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex, "GetReport04 failed for {Start} - {Finish}, org {MainOrg}.", start, finish, mainOrgId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    [HttpPost("GetReport05")]
    [ProducesResponseType(typeof(PagedEnvelope<ClaimContactDailyReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport5(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // ============================================================
        // Execute
        // ============================================================
        try
        {
            var allRows = await LoadReport05Async(start, finish, cancellationToken);

            var total = allRows.Count;

            var data = allRows
                .OrderBy(x => x.Contact_Date)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<ClaimContactDailyReportDto>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport05 failed for {Start} - {Finish}.", start.Date, finish.Date);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    [HttpPost("GetReport06")]
    [ProducesResponseType(typeof(PagedEnvelope<NonServiceContactDailyReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport6(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // ============================================================
        // Execute
        // ============================================================
        try
        {
            var allRows = await LoadReport06Async(start, finish, cancellationToken);

            var total = allRows.Count;

            var data = allRows
                .OrderBy(x => x.Contact_Date)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<NonServiceContactDailyReportDto>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport06 failed for {Start} - {Finish}.", start.Date, finish.Date);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }


    [HttpPost("GetReport07")]
    [ProducesResponseType(typeof(PagedEnvelope<ServiceRequestReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport07(
        [FromBody] ServiceRequestReportRequestService request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.P_Service))
        {
            ModelState.AddModelError(nameof(request.P_Service), "กรุณาระบุ P_Service");
            return ValidationProblem(ModelState);
        }

        if (request.PageNumber < 1)
        {
            ModelState.AddModelError(nameof(request.PageNumber), "PageNumber ต้องมากกว่าหรือเท่ากับ 1");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            ModelState.AddModelError(nameof(request.PageSize), $"PageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Ids are upper-case and the collation is case-sensitive.
        var serviceId = request.P_Service.Trim().ToUpperInvariant();

        try
        {
            var allRows = await LoadReport07Async(start, finish, serviceId, cancellationToken);

            var total = allRows.Count;

            var data = allRows
                .OrderBy(x => x.Code)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var message = total == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {total} รายการ";

            return Ok(new PagedEnvelope<ServiceRequestReportDto>
            {
                Status = "success",
                Message = message,
                Data = data,
                Meta = new PagedMeta
                {
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = request.PageNumber,
                        PerPage = request.PageSize,
                        Total = total,
                        TotalPages = (int)Math.Ceiling(total / (double)request.PageSize),
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex, "GetReport07 failed for {Start} - {Finish}, service {Service}.", start, finish, serviceId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
    }



    // ============================================================
    // Queries shared by GetReportXX (paged) and ExportReportXX (whole range)
    // ============================================================

    /// <summary>All Report 01 rows in the range, sorted by SR code.</summary>
    private async Task<List<ServiceRequestReportDto>> LoadReport01Async(
        DateTime start, DateTime finish, CancellationToken cancellationToken)
    {
        var rows = await (
            from sr in _db.TblServices.AsNoTracking()

            join st in _db.TblStatuses.AsNoTracking()
                on sr.StatusId equals st.Id

            where st.RefId != "1B751556C1458196BA0EB37037415A25"

            // Activity
            join ac in _db.TblActivities.AsNoTracking()
                on sr.Id equals ac.ServiceId into acGroup
            from ac in acGroup.DefaultIfEmpty()

            // Contact
            join c1 in _db.TblContacts.AsNoTracking()
                on ac.ContactId equals c1.Id into cGroup
            from c1 in cGroup.DefaultIfEmpty()

            // Channel
            join ch in _db.TblChannels.AsNoTracking()
                on sr.ChannelIncomingId equals ch.Id into chGroup
            from ch in chGroup.DefaultIfEmpty()

            // Category
            join cat in _db.TblCategories.AsNoTracking()
                on sr.CategoryId equals cat.Id into catGroup
            from cat in catGroup.DefaultIfEmpty()

            // Reference
            join rf in _db.TblReferences.AsNoTracking()
                on sr.ServiceReference equals rf.Id into refGroup
            from rf in refGroup.DefaultIfEmpty()

            // Organization
            join org in _db.TblOrganizations.AsNoTracking()
                on sr.OrganizationId equals org.Id into orgGroup
            from org in orgGroup.DefaultIfEmpty()

            join org2 in _db.TblOrganizations.AsNoTracking()
                on org.RefId equals org2.Id into org2Group
            from org2 in org2Group.DefaultIfEmpty()

            // Account
            join acc in _db.TblAccounts.AsNoTracking()
                on sr.AccountId equals acc.Id into accGroup
            from acc in accGroup.DefaultIfEmpty()

            // Gender
            join gen in _db.TblGenders.AsNoTracking()
                on acc.GenderId equals gen.Id into genGroup
            from gen in genGroup.DefaultIfEmpty()

            // Employee (Created)
            join emp1 in _db.TblEmployees.AsNoTracking()
                on sr.CreatedBy equals emp1.Id into emp1Group
            from emp1 in emp1Group.DefaultIfEmpty()

            // Employee (Owner)
            join emp2 in _db.TblEmployees.AsNoTracking()
                on sr.OwnerId equals emp2.Id into emp2Group
            from emp2 in emp2Group.DefaultIfEmpty()

            // Employee (Updated)
            join emp3 in _db.TblEmployees.AsNoTracking()
                on sr.UpdatedBy equals emp3.Id into emp3Group
            from emp3 in emp3Group.DefaultIfEmpty()

           

            where sr.IsEnable == "T"
              && sr.Created >= start
              && sr.Created <= finish
            
            orderby sr.Code

            select new ServiceRequestReportDto
            {
                Code = sr.Code,
                Summary = sr.Summary,
                Detail = sr.Detail,
                SrReferenceLink = sr.ServiceReferenceLink,
               SrOpened = sr.DateOpened.HasValue
                    ? sr.DateOpened.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrClosed = sr.DateClosed.HasValue
                    ? sr.DateClosed.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrRequireCallBack = sr.CallBack,
                Created = sr.Created,

                ANumber = c1.ContactDetail ?? "",

                ChannelName = ch != null ? ch.NameTh : "",
                SrTypeName = cat != null ? cat.NameTh : "",
                SrReference = rf != null ? rf.NameTh : "",
                SrStatusName = st.NameTh,

                CreatedUName = emp1.UserName ?? "",
                CreaterName = emp1.Id == null
                    ? ""
                    : $"{emp1.SalutationTh}{emp1.FirstnameTh} {emp1.LastnameTh}",
                SkillAgentCreated =
                    emp1.Id != null && EF.Functions.Like(emp1.Position ?? "", "%Claim%")
                        ? 1 // ใส่ข้อมูลแสดง Info
                        : 2,// ปล่อยว่าง
                OwnerUName = emp2.UserName ?? "",
                OwnerName = emp2.Id == null
                    ? ""
                    : $"{emp2.SalutationTh}{emp2.FirstnameTh} {emp2.LastnameTh}",
                LastUpdatedUName = emp3.UserName ?? "",
                UpdateName = emp3.Id == null
                    ? ""
                    : $"{emp3.SalutationTh}{emp3.FirstnameTh} {emp3.LastnameTh}",
                SubOrgId = sr.OrganizationId,
                SubOrgName = org != null ? org.NameTh : "",
                MainOrgId = org2 != null ? org2.Id : null,
                MainOrgName = org2 != null ? org2.NameTh : "",
                ContactName = acc.Id == null
                    ? ""
                    : $"{acc.SalutationTh}{acc.FirstnameTh} {acc.LastnameTh}",

                Gender = gen != null ? gen.NameTh : "",

                Remark = sr.Remark
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        // Sort after Distinct() so the order (and therefore paging) is always the same.
        return rows.OrderBy(x => x.Code, StringComparer.Ordinal).ToList();
    }

    /// <summary>All Report 02 rows (CallBack = "D", dropped calls) in the range, sorted by SR code.</summary>
    private async Task<List<SrCallbackResponse>> LoadReport02Async(
        DateTime start, DateTime finish, CancellationToken cancellationToken)
    {
        return await (
            from sr in _db.TblServices

            // Activity
            join ac in _db.TblActivities
                on sr.Id equals ac.ServiceId into acJoin
            from ac in acJoin.DefaultIfEmpty()

            // Contact
            join c1 in _db.TblContacts
                on ac.ContactId equals c1.Id into cJoin
            from c1 in cJoin.DefaultIfEmpty()

            // Creator
            join emp1 in _db.TblEmployees
                on sr.CreatedBy equals emp1.Id into emp1Join
            from emp1 in emp1Join.DefaultIfEmpty()

            // Owner
            join emp2 in _db.TblEmployees
                on sr.OwnerId equals emp2.Id into emp2Join
            from emp2 in emp2Join.DefaultIfEmpty()

            // Last Updated
            join emp3 in _db.TblEmployees
                on sr.UpdatedBy equals emp3.Id into emp3Join
            from emp3 in emp3Join.DefaultIfEmpty()

            // Channel
            join ch in _db.TblChannels
                on sr.ChannelIncomingId equals ch.Id into chJoin
            from ch in chJoin.DefaultIfEmpty()

            // Category
            join cat in _db.TblCategories
                on sr.CategoryId equals cat.Id into catJoin
            from cat in catJoin.DefaultIfEmpty()

            // Reference
            join rf in _db.TblReferences
                on sr.ServiceReference equals rf.Id into refJoin
            from rf in refJoin.DefaultIfEmpty()

            // Status
            join st in _db.TblStatuses
                on sr.StatusId equals st.Id into stJoin
            from st in stJoin.DefaultIfEmpty()

            // Organization
            join org in _db.TblOrganizations
                on sr.OrganizationId equals org.Id into orgJoin
            from org in orgJoin.DefaultIfEmpty()

            // Main Organization
            join org2 in _db.TblOrganizations
                on org.RefId equals org2.Id into org2Join
            from org2 in org2Join.DefaultIfEmpty()

            // Account
            join acc in _db.TblAccounts
                on sr.AccountId equals acc.Id into accJoin
            from acc in accJoin.DefaultIfEmpty()

            where sr.IsEnable == "T"
                && sr.CallBack == "D"
                && sr.Created >= start
                && sr.Created <= finish

            select new SrCallbackResponse
            {
            
                Code = sr.Code,
                Summary = sr.Summary,
                Detail = sr.Detail,
                SrReferenceLink = sr.ServiceReferenceLink,
               SrOpened = sr.DateOpened.HasValue
                    ? sr.DateOpened.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrClosed = sr.DateClosed.HasValue
                    ? sr.DateClosed.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrRequireCallBack = sr.CallBack,
                Created = sr.Created,

                ANumber = c1.ContactDetail ?? "",

                ChannelName = ch != null
                    ? ch.NameTh
                    : null,

                SrTypeName = cat != null
                    ? cat.NameTh
                    : null,

                SrReference = rf != null
                    ? rf.NameTh
                    : null,

                SrStatusName = st != null
                    ? st.NameTh
                    : null,

                CreatedUname = emp1.Id != null
                    ? emp1.UserName
                    : null,
                CreatorName = emp1.Id != null
                    ? (emp1.SalutationTh ?? "") +
                    (emp1.FirstnameTh ?? "") + " " +
                    (emp1.LastnameTh ?? "")
                    : null,
                SkillAgentCreated =
                    emp1.Id != null &&
                    EF.Functions.Like(
                        emp1.Position ?? "",
                        "%Claim%"
                    )
                        ? 1
                        : 2,
                OwnerUname = emp2.Id != null
                    ? emp2.UserName
                    : null,
                OwnerName = emp2.Id != null
                    ? (emp2.SalutationTh ?? "") +
                    (emp2.FirstnameTh ?? "") + " " +
                    (emp2.LastnameTh ?? "")
                    : null,
                LastUpdatedUname = emp3.Id != null
                    ? emp3.UserName
                    : null,
                UpdaterName = emp3.Id != null
                    ? (emp3.SalutationTh ?? "") +
                    (emp3.FirstnameTh ?? "") + " " +
                    (emp3.LastnameTh ?? "")
                    : null,
                SubOrgId = sr.OrganizationId,
                SubOrgName = org != null
                    ? org.NameTh
                    : null,
                MainOrgId = org2 != null
                    ? org2.Id
                    : null,
                MainOrgName = org2 != null
                    ? org2.NameTh
                    : null,
                ContactName = acc.Id != null
                    ? (acc.SalutationTh ?? "") +
                    (acc.FirstnameTh ?? "") + " " +
                    (acc.LastnameTh ?? "")
                    : null
            })
            .Distinct()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    /// <summary>All Report 03 rows (CallBack = "Y") in the range, sorted by SR code.</summary>
    private async Task<List<SrCallbackResponse>> LoadReport03Async(
        DateTime start, DateTime finish, CancellationToken cancellationToken)
    {
        return await (
            from sr in _db.TblServices

            // Activity
            join ac in _db.TblActivities
                on sr.Id equals ac.ServiceId into acJoin
            from ac in acJoin.DefaultIfEmpty()

            // Contact
            join c1 in _db.TblContacts
                on ac.ContactId equals c1.Id into cJoin
            from c1 in cJoin.DefaultIfEmpty()

            // Creator
            join emp1 in _db.TblEmployees
                on sr.CreatedBy equals emp1.Id into emp1Join
            from emp1 in emp1Join.DefaultIfEmpty()

            // Owner
            join emp2 in _db.TblEmployees
                on sr.OwnerId equals emp2.Id into emp2Join
            from emp2 in emp2Join.DefaultIfEmpty()

            // Last Updated
            join emp3 in _db.TblEmployees
                on sr.UpdatedBy equals emp3.Id into emp3Join
            from emp3 in emp3Join.DefaultIfEmpty()

            // Channel
            join ch in _db.TblChannels
                on sr.ChannelIncomingId equals ch.Id into chJoin
            from ch in chJoin.DefaultIfEmpty()

            // Category
            join cat in _db.TblCategories
                on sr.CategoryId equals cat.Id into catJoin
            from cat in catJoin.DefaultIfEmpty()

            // Reference
            join rf in _db.TblReferences
                on sr.ServiceReference equals rf.Id into refJoin
            from rf in refJoin.DefaultIfEmpty()

            // Status
            join st in _db.TblStatuses
                on sr.StatusId equals st.Id into stJoin
            from st in stJoin.DefaultIfEmpty()

            // Organization
            join org in _db.TblOrganizations
                on sr.OrganizationId equals org.Id into orgJoin
            from org in orgJoin.DefaultIfEmpty()

            // Main Organization
            join org2 in _db.TblOrganizations
                on org.RefId equals org2.Id into org2Join
            from org2 in org2Join.DefaultIfEmpty()

            // Account
            join acc in _db.TblAccounts
                on sr.AccountId equals acc.Id into accJoin
            from acc in accJoin.DefaultIfEmpty()

            where sr.IsEnable == "T"
                && sr.CallBack == "Y"

                // SR Code ตามวันที่
                && sr.Created >= start
                && sr.Created <= finish

            select new SrCallbackResponse
            {
            
                Code = sr.Code,
                Summary = sr.Summary,
                Detail = sr.Detail,
                SrReferenceLink = sr.ServiceReferenceLink,
                SrOpened = sr.DateOpened.HasValue
                    ? sr.DateOpened.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrClosed = sr.DateClosed.HasValue
                    ? sr.DateClosed.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrRequireCallBack = sr.CallBack,
                // Contact_No in the legacy report: the caller account's phone, mobile first.
                ContactNo = _db.TblAccountDetails
                    .Where(d => d.AccountId == sr.AccountId
                        && d.IsEnable == "T"
                        && (d.DetailType == "MOBILE" || d.DetailType == "HOME" || d.DetailType == "OFFICE"))
                    .OrderBy(d => d.DetailType == "MOBILE" ? 0 : d.DetailType == "HOME" ? 1 : 2)
                    .Select(d => d.Detail)
                    .FirstOrDefault(),
                Created = sr.Created,

                ANumber = c1.ContactDetail ?? "",

                ChannelName = ch != null
                    ? ch.NameTh
                    : null,

                SrTypeName = cat != null
                    ? cat.NameTh
                    : null,

                SrReference = rf != null
                    ? rf.NameTh
                    : null,

                SrStatusName = st != null
                    ? st.NameTh
                    : null,

                CreatedUname = emp1.Id != null
                    ? emp1.UserName
                    : null,
                CreatorName = emp1.Id != null
                    ? (emp1.SalutationTh ?? "") +
                    (emp1.FirstnameTh ?? "") + " " +
                    (emp1.LastnameTh ?? "")
                    : null,
                SkillAgentCreated =
                    emp1.Id != null &&
                    EF.Functions.Like(
                        emp1.Position ?? "",
                        "%Claim%"
                    )
                        ? 1
                        : 2,
                OwnerUname = emp2.Id != null
                    ? emp2.UserName
                    : null,
                OwnerName = emp2.Id != null
                    ? (emp2.SalutationTh ?? "") +
                    (emp2.FirstnameTh ?? "") + " " +
                    (emp2.LastnameTh ?? "")
                    : null,
                LastUpdatedUname = emp3.Id != null
                    ? emp3.UserName
                    : null,
                UpdaterName = emp3.Id != null
                    ? (emp3.SalutationTh ?? "") +
                    (emp3.FirstnameTh ?? "") + " " +
                    (emp3.LastnameTh ?? "")
                    : null,
                SubOrgId = sr.OrganizationId,
                SubOrgName = org != null
                    ? org.NameTh
                    : null,
                MainOrgId = org2 != null
                    ? org2.Id
                    : null,
                MainOrgName = org2 != null
                    ? org2.NameTh
                    : null,
                ContactName = acc.Id != null
                    ? (acc.SalutationTh ?? "") +
                    (acc.FirstnameTh ?? "") + " " +
                    (acc.LastnameTh ?? "")
                    : null
            })
            .Distinct()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// All Report 04 rows in the range, sorted by SR code. <paramref name="mainOrgId"/> and
    /// <paramref name="subOrgId"/> are optional; null means no filter.
    /// </summary>
    private async Task<List<SrCallbackResponse>> LoadReport04Async(
        DateTime start, DateTime finish, string? mainOrgId, string? subOrgId, CancellationToken cancellationToken)
    {
        return await (
            from sr in _db.TblServices

            // Activity
            join ac in _db.TblActivities
                on sr.Id equals ac.ServiceId into acJoin
            from ac in acJoin.DefaultIfEmpty()

            // Contact
            join c1 in _db.TblContacts
                on ac.ContactId equals c1.Id into cJoin
            from c1 in cJoin.DefaultIfEmpty()

            // Creator
            join emp1 in _db.TblEmployees
                on sr.CreatedBy equals emp1.Id into emp1Join
            from emp1 in emp1Join.DefaultIfEmpty()

            // Owner
            join emp2 in _db.TblEmployees
                on sr.OwnerId equals emp2.Id into emp2Join
            from emp2 in emp2Join.DefaultIfEmpty()

            // Last Updated
            join emp3 in _db.TblEmployees
                on sr.UpdatedBy equals emp3.Id into emp3Join
            from emp3 in emp3Join.DefaultIfEmpty()

            // Channel
            join ch in _db.TblChannels
                on sr.ChannelIncomingId equals ch.Id into chJoin
            from ch in chJoin.DefaultIfEmpty()

            // Category
            join cat in _db.TblCategories
                on sr.CategoryId equals cat.Id into catJoin
            from cat in catJoin.DefaultIfEmpty()

            // Reference
            join rf in _db.TblReferences
                on sr.ServiceReference equals rf.Id into refJoin
            from rf in refJoin.DefaultIfEmpty()

            // Status
            join st in _db.TblStatuses
                on sr.StatusId equals st.Id into stJoin
            from st in stJoin.DefaultIfEmpty()

            // Organization
            join org in _db.TblOrganizations
                on sr.OrganizationId equals org.Id into orgJoin
            from org in orgJoin.DefaultIfEmpty()

            // Main Organization
            join org2 in _db.TblOrganizations
                on org.RefId equals org2.Id into org2Join
            from org2 in org2Join.DefaultIfEmpty()

            // Account
            join acc in _db.TblAccounts
                on sr.AccountId equals acc.Id into accJoin
            from acc in accJoin.DefaultIfEmpty()

            // No organization = every organization. P_MainOrg alone = that organization and its direct
            // sub-organizations. P_SubOrg = only that sub-organization.
            where sr.IsEnable == "T"
                && (mainOrgId == null || org.Id == mainOrgId || org2.Id == mainOrgId)
                && (subOrgId == null || org.Id == subOrgId)
                && sr.Created >= start
                && sr.Created <= finish

            select new SrCallbackResponse
            {
            
                Code = sr.Code,
                Summary = sr.Summary,
                Detail = sr.Detail,
                SrReferenceLink = sr.ServiceReferenceLink,
               SrOpened = sr.DateOpened.HasValue
                    ? sr.DateOpened.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrClosed = sr.DateClosed.HasValue
                    ? sr.DateClosed.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrRequireCallBack = sr.CallBack,
                Created = sr.Created,

                ANumber = c1.ContactDetail ?? "",

                ChannelName = ch != null
                    ? ch.NameTh
                    : null,

                SrTypeName = cat != null
                    ? cat.NameTh
                    : null,

                SrReference = rf != null
                    ? rf.NameTh
                    : null,

                SrStatusName = st != null
                    ? st.NameTh
                    : null,

                CreatedUname = emp1.Id != null
                    ? emp1.UserName
                    : null,
                CreatorName = emp1.Id != null
                    ? (emp1.SalutationTh ?? "") +
                    (emp1.FirstnameTh ?? "") + " " +
                    (emp1.LastnameTh ?? "")
                    : null,
                SkillAgentCreated =
                    emp1.Id != null &&
                    EF.Functions.Like(
                        emp1.Position ?? "",
                        "%Claim%"
                    )
                        ? 1
                        : 2,
                OwnerUname = emp2.Id != null
                    ? emp2.UserName
                    : null,
                OwnerName = emp2.Id != null
                    ? (emp2.SalutationTh ?? "") +
                    (emp2.FirstnameTh ?? "") + " " +
                    (emp2.LastnameTh ?? "")
                    : null,
                LastUpdatedUname = emp3.Id != null
                    ? emp3.UserName
                    : null,
                UpdaterName = emp3.Id != null
                    ? (emp3.SalutationTh ?? "") +
                    (emp3.FirstnameTh ?? "") + " " +
                    (emp3.LastnameTh ?? "")
                    : null,
                SubOrgId = sr.OrganizationId,
                SubOrgName = org != null
                    ? org.NameTh
                    : null,
                MainOrgId = org2 != null
                    ? org2.Id
                    : null,
                MainOrgName = org2 != null
                    ? org2.NameTh
                    : null,
                ContactName = acc.Id != null
                    ? (acc.SalutationTh ?? "") +
                    (acc.FirstnameTh ?? "") + " " +
                    (acc.LastnameTh ?? "")
                    : null
            })
            .Distinct()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Report 05 daily counts for the days of the range, sorted by date.</summary>
    private async Task<List<ClaimContactDailyReportDto>> LoadReport05Async(
        DateTime start, DateTime finish, CancellationToken cancellationToken)
    {
               // The report counts whole days (TblContact.ContactStart holds dates only), so the time part
        // of P_Start / P_Finish is ignored.
        var startDay = start.Date;
        var finishDay = finish.Date;
        var dayAfterFinish = finishDay.AddDays(1);

        // ============================================================
        // Query
        // ============================================================
        // กรองเฉพาะ cat.Ref_ID ทั้ง 4 หมวดหลักตาม SQL
        var targetRefCategories = new[]
        {
            "0168B738100C4CBAB9AA45906A100002",
            "0168B738100C4CBAB9AA45906A200005",
            "0168B738100C4CBAB9AA45906A200001",
            "0168B738100C4CBAB9AA45906A200004"
        };

        // พนักงานที่มี Position LIKE '%Claim%' และอยู่ในกลุ่มที่ไม่ใช่ 39 (group_id <> 39)
        var claimMembers = (
            from emp in _db.TblEmployees
            join uug in _db.TblEmployeeGroups
                on emp.Id equals uug.EmployeeId
            where EF.Functions.Like(emp.Position, "%Claim%")
             //   && uug.GroupId != "39" // *ถ้าใน Entity GroupId เป็น int ให้แก้เป็น != 39
            select emp.Id)
            .Distinct();

        var query =
            from c in _db.TblContacts
            join cat in _db.TblCategories
                on c.CategoryId equals cat.Id
            join m in claimMembers
                on c.CreatedBy equals m
            where c.ContactStart >= startDay
                && c.ContactStart < dayAfterFinish
                && targetRefCategories.Contains(cat.RefId)
            group new { c, cat } by c.ContactStart!.Value.Date into g
            select new
            {
                contact_date = g.Key,
                bad_signal = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200002"),
                kidding_abnormal = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200012"),
                kidding_child = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200013"),
                kidding_rude = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200015"),
                kidding_silent = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200014"),
                flood = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200018"),
                lost_connection = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200003"),
                wrong_number_pizza = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200017"),
                wrong_number_other = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200016"),
                stress_social = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%สังคม%")),
                stress_economic = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%เศรษฐกิจ%")),
                stress_political = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%การเมือง%")),
                stress_legal = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%กฎหมาย%")),
                stress_natural_resources = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%ทรัพยากร%")),
                consult_officer = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200006"),
                total = g.Count()
            };

        var rawResult = await query
            .OrderBy(x => x.contact_date)
            .ToListAsync(cancellationToken);

        var allRows = rawResult.Select(x => new ClaimContactDailyReportDto
        {
            Contact_Date = x.contact_date,
            Bad_Signal = x.bad_signal,
            Kidding_Abnormal = x.kidding_abnormal,
            Kidding_Child = x.kidding_child,
            Kidding_Rude = x.kidding_rude,
            Kidding_Silent = x.kidding_silent,
            Flood = x.flood,
            Lost_Connection = x.lost_connection,
            Wrong_Number_Pizza = x.wrong_number_pizza,
            Wrong_Number_Other = x.wrong_number_other,
            Stress_Social = x.stress_social,
            Stress_Economic = x.stress_economic,
            Stress_Political = x.stress_political,
            Stress_Legal = x.stress_legal,
            Stress_Natural_Resources = x.stress_natural_resources,
            Consult_Officer = x.consult_officer,
            Total = x.total
        }).ToList();

        return allRows;
    }

    /// <summary>Report 06 daily counts for the days of the range, sorted by date.</summary>
    private async Task<List<NonServiceContactDailyReportDto>> LoadReport06Async(
        DateTime start, DateTime finish, CancellationToken cancellationToken)
    {
        // The report counts whole days (TblContact.ContactStart holds dates only), so the time part
        // of P_Start / P_Finish is ignored.
                var startDay = start.Date;
        var finishDay = finish.Date;
        var dayAfterFinish = finishDay.AddDays(1);

        // ============================================================
        // Query
        // ============================================================
        var targetRefCategories = new[]
        {
            "0168B738100C4CBAB9AA45906A100002",
            "0168B738100C4CBAB9AA45906A200005",
            "0168B738100C4CBAB9AA45906A200001",
            "0168B738100C4CBAB9AA45906A200004"
        };

        var query =
            from c in _db.TblContacts
            // JOIN tbl_category cat ON c1.Category_ID = cat.ID
            join cat in _db.TblCategories
                on c.CategoryId equals cat.Id
            // LEFT JOIN tbl_employee emp1 ON c1.Created_By = emp1.ID
            join emp in _db.TblEmployees
                on c.CreatedBy equals emp.Id into empGroup
            from emp in empGroup.DefaultIfEmpty()
            where c.ContactStart >= startDay
                && c.ContactStart < dayAfterFinish
                // IF(emp1.Position LIKE '%Claim%', 1, 2) = 2 (ตัด Claim ออก รวมกรณี NULL)
                && (emp.Position == null || !EF.Functions.Like(emp.Position, "%Claim%"))
                // cat.Ref_ID IN (...)
                && targetRefCategories.Contains(cat.RefId)
                // EXISTS (SELECT 1 FROM users_user_group g WHERE g.user_id = c1.Created_By AND g.group_id <> 39)
                && _db.TblEmployeeGroups.Any(g => g.EmployeeId == c.CreatedBy && g.GroupId != "39")
            // GROUP BY contact_date (ใช้ .Date เพื่อให้ EF Core แปลเป็น SQL ได้)
            group new { c, cat } by c.ContactStart!.Value.Date into g
            select new
            {
                Contact_Date = g.Key,
                // 1. Kidding Call
                Kidding_Abnormal = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200012"),
                Kidding_Child = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200013"),
                Kidding_Rude = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200015"),
                Kidding_Silent = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200014"),
                Flood = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200018"),
                Kidding_Total = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200001"),
                // 2. Bad Signal / 3. Lost Connection
                Bad_Signal = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200002"),
                Lost_Connection = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200003"),
                // 4. Wrong Number
                Wrong_Number_Pizza = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200017"),
                Wrong_Number_Other = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200016"),
                // 5. Out of Scope
                Consult_Officer = g.Count(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200006"),
                Stress_Social = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                            && EF.Functions.Like(x.cat.NameTh, "%สังคม%")),
                Stress_Economic = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                            && EF.Functions.Like(x.cat.NameTh, "%เศรษฐกิจ%")),
                Stress_Political = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                            && EF.Functions.Like(x.cat.NameTh, "%การเมือง%")),
                Stress_Legal = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                            && EF.Functions.Like(x.cat.NameTh, "%กฎหมาย%")),
                Stress_Natural_Resources = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                            && EF.Functions.Like(x.cat.NameTh, "%ทรัพยากร%")),
                Out_Of_Scope_Total = g.Count(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"),
                // รวม Nonservice
                Total = g.Count()
            };

        var rawResult = await query
            .OrderBy(x => x.Contact_Date)
            .ToListAsync(cancellationToken);

        // แปลง Contact_Date เป็น DateOnly? หลังจากดึงข้อมูลมาแล้ว
        var allRows = rawResult.Select(x => new NonServiceContactDailyReportDto
        {
            Contact_Date = DateOnly.FromDateTime(x.Contact_Date),
            Kidding_Abnormal = x.Kidding_Abnormal,
            Kidding_Child = x.Kidding_Child,
            Kidding_Rude = x.Kidding_Rude,
            Kidding_Silent = x.Kidding_Silent,
            Flood = x.Flood,
            Kidding_Total = x.Kidding_Total,
            Bad_Signal = x.Bad_Signal,
            Lost_Connection = x.Lost_Connection,
            Wrong_Number_Pizza = x.Wrong_Number_Pizza,
            Wrong_Number_Other = x.Wrong_Number_Other,
            Consult_Officer = x.Consult_Officer,
            Stress_Social = x.Stress_Social,
            Stress_Economic = x.Stress_Economic,
            Stress_Political = x.Stress_Political,
            Stress_Legal = x.Stress_Legal,
            Stress_Natural_Resources = x.Stress_Natural_Resources,
            Out_Of_Scope_Total = x.Out_Of_Scope_Total,
            Total = x.Total
        }).ToList();

        return allRows;
    }

    /// <summary>All Report 07 rows of one service type in the range, sorted by SR code.</summary>
    private async Task<List<ServiceRequestReportDto>> LoadReport07Async(
        DateTime start, DateTime finish, string serviceId, CancellationToken cancellationToken)
    {
        // EF Core 10 cannot translate Count()/OFFSET over a Distinct() of this projection (it has
        // constant/computed members and throws in the shaper). The proven .Distinct().ToListAsync()
        // shape works, so materialise it and page in memory. DB cost matches the pre-paging
        // behaviour; only the response payload is paged.
        var rows = await (
            from sr in _db.TblServices.AsNoTracking()

            join st in _db.TblStatuses.AsNoTracking()
                on sr.StatusId equals st.Id

            // Activity
            join ac in _db.TblActivities.AsNoTracking()
                on sr.Id equals ac.ServiceId into acGroup
            from ac in acGroup.DefaultIfEmpty()

            // Contact
            join c1 in _db.TblContacts.AsNoTracking()
                on ac.ContactId equals c1.Id into cGroup
            from c1 in cGroup.DefaultIfEmpty()

            // Channel
            join ch in _db.TblChannels.AsNoTracking()
                on sr.ChannelIncomingId equals ch.Id into chGroup
            from ch in chGroup.DefaultIfEmpty()

            // Category
            join cat in _db.TblCategories.AsNoTracking()
                on sr.CategoryId equals cat.Id into catGroup
            from cat in catGroup.DefaultIfEmpty()

            // Reference
            join rf in _db.TblReferences.AsNoTracking()
                on sr.ServiceReference equals rf.Id into refGroup
            from rf in refGroup.DefaultIfEmpty()

            // Organization
            join org in _db.TblOrganizations.AsNoTracking()
                on sr.OrganizationId equals org.Id into orgGroup
            from org in orgGroup.DefaultIfEmpty()

            join org2 in _db.TblOrganizations.AsNoTracking()
                on org.RefId equals org2.Id into org2Group
            from org2 in org2Group.DefaultIfEmpty()

            // Account
            join acc in _db.TblAccounts.AsNoTracking()
                on sr.AccountId equals acc.Id into accGroup
            from acc in accGroup.DefaultIfEmpty()

            // Gender
            join gen in _db.TblGenders.AsNoTracking()
                on acc.GenderId equals gen.Id into genGroup
            from gen in genGroup.DefaultIfEmpty()

            // Employee (Created)
            join emp1 in _db.TblEmployees.AsNoTracking()
                on sr.CreatedBy equals emp1.Id into emp1Group
            from emp1 in emp1Group.DefaultIfEmpty()

            // Employee (Owner)
            join emp2 in _db.TblEmployees.AsNoTracking()
                on sr.OwnerId equals emp2.Id into emp2Group
            from emp2 in emp2Group.DefaultIfEmpty()

            // Employee (Updated)
            join emp3 in _db.TblEmployees.AsNoTracking()
                on sr.UpdatedBy equals emp3.Id into emp3Group
            from emp3 in emp3Group.DefaultIfEmpty()

            where sr.IsEnable == "T"
              && sr.CategoryId == serviceId
              && sr.Created >= start
              && sr.Created <= finish

            select new ServiceRequestReportDto
            {
                Code = sr.Code,
                Summary = sr.Summary,
                Detail = sr.Detail,
                SrReferenceLink = sr.ServiceReferenceLink,
                SrOpened = sr.DateOpened.HasValue
                    ? sr.DateOpened.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrClosed = sr.DateClosed.HasValue
                    ? sr.DateClosed.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    : "",
                SrRequireCallBack = sr.CallBack,
                Created = sr.Created,

                ANumber = c1.ContactDetail ?? "",

                ChannelName = ch != null ? ch.NameTh : "",
                SrTypeName = cat != null ? cat.NameTh : "",
                SrReference = rf != null ? rf.NameTh : "",
                SrStatusName = st.NameTh,

                CreatedUName = emp1.UserName ?? "",
                CreaterName = emp1.Id == null
                    ? ""
                    : $"{emp1.SalutationTh}{emp1.FirstnameTh} {emp1.LastnameTh}",
                SkillAgentCreated =
                    emp1.Id != null && EF.Functions.Like(emp1.Position ?? "", "%Claim%")
                        ? 1 // ใส่ข้อมูลแสดง Info
                        : 2,// ปล่อยว่าง
                OwnerUName = emp2.UserName ?? "",
                OwnerName = emp2.Id == null
                    ? ""
                    : $"{emp2.SalutationTh}{emp2.FirstnameTh} {emp2.LastnameTh}",
                LastUpdatedUName = emp3.UserName ?? "",
                UpdateName = emp3.Id == null
                    ? ""
                    : $"{emp3.SalutationTh}{emp3.FirstnameTh} {emp3.LastnameTh}",
                SubOrgId = sr.OrganizationId,
                SubOrgName = org != null ? org.NameTh : "",
                MainOrgId = org2 != null ? org2.Id : null,
                MainOrgName = org2 != null ? org2.NameTh : "",
                ContactName = acc.Id == null
                    ? ""
                    : $"{acc.SalutationTh}{acc.FirstnameTh} {acc.LastnameTh}",

                Gender = gen != null ? gen.NameTh : "",

                Remark = sr.Remark
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.OrderBy(x => x.Code, StringComparer.Ordinal).ToList();
    }

    /// <summary>Trims and upper-cases an id (ids are upper-case and the collation is case-sensitive); blank = null.</summary>
    private static string? NormalizeId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : id.Trim().ToUpperInvariant();
}