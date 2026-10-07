using System.Globalization;
using ContactTogetherApi.Data;
using ContactTogetherApi.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Controllers;

[ApiController]
[Route("[controller]")]
//[Authorize]
public class ReportController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReportController> _logger;

    public ReportController(ApplicationDbContext db, ILogger<ReportController> logger)
    {
        _db = db;
        _logger = logger;
    }


    [HttpPost("GetReport01")]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport01(
        [FromBody] ServiceRequestReportRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        try
        {
            var result = await (
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
                    SrOpened = sr.DateOpened,
                    SrClosed = sr.DateClosed,
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

            var message = result.Count == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {result.Count} รายการ";
            return Ok(_0BaseReturn.Success(result, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport01 failed for {Start} - {Finish}.", start, finish);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    /// <summary>
    /// Parses the report's <c>MM/dd/yyyy</c> dates and optional <c>HH:mm:ss</c> times into an
    /// inclusive <c>DateTime</c> range. <c>TblService.Created</c> and <c>TblContact.ContactStart</c>
    /// are <c>datetime2</c>, so the queries compare against these values directly.
    /// </summary>
    private static bool TryBuildDateRange(
        ServiceRequestReportRequest request,
        out DateTime start,
        out DateTime finish,
        out string error)
    {
        start = finish = default;
        var culture = CultureInfo.InvariantCulture;

        if (string.IsNullOrWhiteSpace(request.P_Start) || string.IsNullOrWhiteSpace(request.P_Finish))
        {
            error = "กรุณาระบุ P_Start และ P_Finish";
            return false;
        }

        if (!DateTime.TryParseExact(request.P_Start, "MM/dd/yyyy", culture, DateTimeStyles.None, out var startDate))
        {
            error = "P_Start ต้องอยู่ในรูปแบบ MM/dd/yyyy";
            return false;
        }

        if (!DateTime.TryParseExact(request.P_Finish, "MM/dd/yyyy", culture, DateTimeStyles.None, out var finishDate))
        {
            error = "P_Finish ต้องอยู่ในรูปแบบ MM/dd/yyyy";
            return false;
        }

        // "08:00" is accepted too, as TimeSpan.Parse did before.
        string[] timeFormats = [@"hh\:mm\:ss", @"hh\:mm"];

        var startTime = TimeSpan.Zero;
        if (!string.IsNullOrWhiteSpace(request.P_Time_Start)
            && !TimeSpan.TryParseExact(request.P_Time_Start, timeFormats, culture, out startTime))
        {
            error = "P_Time_Start ต้องอยู่ในรูปแบบ HH:mm:ss";
            return false;
        }

        var finishTime = new TimeSpan(23, 59, 59);
        if (!string.IsNullOrWhiteSpace(request.P_Time_Finish)
            && !TimeSpan.TryParseExact(request.P_Time_Finish, timeFormats, culture, out finishTime))
        {
            error = "P_Time_Finish ต้องอยู่ในรูปแบบ HH:mm:ss";
            return false;
        }

        var startDateTime = startDate.Date.Add(startTime);
        var finishDateTime = finishDate.Date.Add(finishTime);

        if (startDateTime > finishDateTime)
        {
            error = "P_Start ต้องไม่มากกว่า P_Finish";
            return false;
        }

        start = startDateTime;
        finish = finishDateTime;
        error = string.Empty;
        return true;
    }

    [HttpPost("GetReport02")]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport2(
        [FromBody] ServiceRequestReportRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        _logger.LogInformation("GetReport02 range: {Start} - {Finish}", start, finish);

        // ============================================================
        // Query
        // ============================================================

        try
        {
            var result = await (
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
                    SrOpened = sr.DateOpened,
                    SrClosed = sr.DateClosed,
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

            var message = result.Count == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {result.Count} รายการ";
            return Ok(_0BaseReturn.Success(result, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport02 failed for {Start} - {Finish}.", start, finish);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    [HttpPost("GetReport03")]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport3(
        [FromBody] ServiceRequestReportRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        // ============================================================
        // Query
        // ============================================================

        try
        {
            var result = await (
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
                    SrOpened = sr.DateOpened,
                    SrClosed = sr.DateClosed,
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

            var message = result.Count == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {result.Count} รายการ";
            return Ok(_0BaseReturn.Success(result, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport03 failed for {Start} - {Finish}.", start, finish);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

    [HttpPost("GetReport04")]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport4(
        [FromBody] ServiceRequestReportRequestMainOrganization request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        if (string.IsNullOrWhiteSpace(request.P_MainOrg))
        {
            return BadRequest(_0BaseReturn.Fail("กรุณาระบุ P_MainOrg"));
        }

        // ============================================================
        // Query
        // ============================================================

        try
        {
            var result = await (
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
                    && org.Id == request.P_MainOrg
                    && sr.Created >= start
                    && sr.Created <= finish

                select new SrCallbackResponse
                {
                
                    Code = sr.Code,
                    Summary = sr.Summary,
                    Detail = sr.Detail,
                    SrReferenceLink = sr.ServiceReferenceLink,
                    SrOpened = sr.DateOpened,
                    SrClosed = sr.DateClosed,
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

            var message = result.Count == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {result.Count} รายการ";
            return Ok(_0BaseReturn.Success(result, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex, "GetReport04 failed for {Start} - {Finish}, org {MainOrg}.", start, finish, request.P_MainOrg);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

 [HttpPost("GetReport05")]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport5(
        [FromBody] ServiceRequestReportRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        // This report counts whole days, so P_Time_Start / P_Time_Finish do not apply.
        var startDay = start.Date;
        var finishDay = finish.Date;
        var dayAfterFinish = finishDay.AddDays(1);

         // ============================================================
    // Query
    // ============================================================

    var query =
    from c in _db.TblContacts

    // users_user_group (Left Join)
    join uug in _db.TblEmployeeGroups
        on c.CreatedBy equals uug.EmployeeId
        into uugGroup
    from uug in uugGroup.DefaultIfEmpty()

    // user_group (Left Join)
    join ug in _db.TblOrganizationGroups
        on uug.GroupId equals ug.GroupId
        into ugGroup
    from ug in ugGroup.DefaultIfEmpty()

    where
        c.ContactStart >= startDay
        && c.ContactStart < dayAfterFinish
        // *** เอาเงื่อนไข ug.RefGroupId ออกจากตรงนี้ เพื่อไม่ให้แถวถูกตัดทิ้ง ***

    // รวม c และ ug เข้าไปด้วยกันเพื่อให้ดึง ug มาเช็คใน Count ได้
    group new { c, ug } by c.ContactStart into g

    select new
    {
        Contact_Start = g.Key,

        // ====================================================
        // Kidding call
        // ====================================================

        // จิตไม่ปกติ (นับเฉพาะเมื่อเป็น Group 53 หรือ 77 และ Category ตรงกัน)
        Insane = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200012"),
                                  
        // เด็กโทรเล่น
        Prankcall = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200013"),

        // เสียงเงียบ
        Silence = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200014"),

        // โทรด่าหยาบคาย
        Rude = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200015"),

        // น้ำท่วม
        Flood = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200018"),

        // ====================================================
        // สัญญาณไม่ชัดเจน
        // ====================================================

        // สัญญาณไม่ชัดเจน
        Badline = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200002"),

        // ====================================================
        // สายหลุด
        // ====================================================

        // สายหลุด
        CutOff = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200003"),

        // ====================================================
        // อื่น ๆ
        // ====================================================

        // อื่น ๆ
        Other = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200016"),

        The_Pizza_Company = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200017"),

        // ====================================================
        // Out of Scope
        // ====================================================

        //ขอคำปรึกษาเจ้าหน้าที่
        Request = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200006"),

        //ระบายความเครียดด้านสังคม
        RelievingSocial = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200007"),

        //ระบายความเครียดด้านเศรษฐกิจ 
        RelievingEconomic = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200008"),

        //ระบายความเครียดด้านการเมือง
        RelievingPolitical = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200009"),

        //ระบายความเครียดด้านกฏหมาย 
        RelievingLegal = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200010"),

        //ระบายความเครียดด้านทรัพยากรธรรมชาติ
        RelievingNatural = g.Count(x =>
            (x.ug.RefGroupId == "53" || x.ug.RefGroupId == "77")
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200011")
    };

    // ============================================================
    // Execute
    // ============================================================

        try
        {
            var result = await query
                .OrderBy(x => x.Contact_Start)
                .ToListAsync(cancellationToken);

            var message = result.Count == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {result.Count} รายการ";
            return Ok(_0BaseReturn.Success(result, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport05 failed for {Start} - {Finish}.", startDay, finishDay);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }

[HttpPost("GetReport06")]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport6(
        [FromBody] ServiceRequestReportRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        // This report counts whole days, so P_Time_Start / P_Time_Finish do not apply.
        var startDay = start.Date;
        var finishDay = finish.Date;
        var dayAfterFinish = finishDay.AddDays(1);

         // ============================================================
    // Query
    // ============================================================

    var query =
    from c in _db.TblContacts

    // users_user_group (Left Join)
    join uug in _db.TblEmployeeGroups
        on c.CreatedBy equals uug.EmployeeId
        into uugGroup
    from uug in uugGroup.DefaultIfEmpty()

    // user_group (Left Join)
    join ug in _db.TblOrganizationGroups
        on uug.GroupId equals ug.GroupId
        into ugGroup
    from ug in ugGroup.DefaultIfEmpty()

    where
        c.ContactStart >= startDay
        && c.ContactStart < dayAfterFinish
        // *** เอาเงื่อนไข ug.RefGroupId ออกจากตรงนี้ เพื่อไม่ให้แถวถูกตัดทิ้ง ***

    // รวม c และ ug เข้าไปด้วยกันเพื่อให้ดึง ug มาเช็คใน Count ได้
    group new { c, ug } by c.ContactStart into g

    select new
    {

        Contact_Start = g.Key,
        // ====================================================
        // Kidding call
        // ====================================================

        // จิตไม่ปกติ (นับเฉพาะเมื่อเป็น Group 53 หรือ 77 และ Category ตรงกัน)
        Insane = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200012"),

        // เด็กโทรเล่น
        Prankcall = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200013"),

        // เสียงเงียบ
        Silence = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200014"),

        // โทรด่าหยาบคาย
        Rude = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200015"),

        // น้ำท่วม
        Flood = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200018"),

        // ====================================================
        // สัญญาณไม่ชัดเจน
        // ====================================================

        // สัญญาณไม่ชัดเจน
        Badline = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200002"),

        // ====================================================
        // สายหลุด
        // ====================================================

        // สายหลุด
        CutOff = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200003"),

        // ====================================================
        // อื่น ๆ
        // ====================================================

        // อื่น ๆ
        Other = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200016"),

        The_Pizza_Company = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200017"),

        // ====================================================
        // Out of Scope
        // ====================================================

        //ขอคำปรึกษาเจ้าหน้าที่
        Request = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200006"),

        //ระบายความเครียดด้านสังคม
        RelievingSocial = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200007"),

        //ระบายความเครียดด้านเศรษฐกิจ 
        RelievingEconomic = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200008"),

        //ระบายความเครียดด้านการเมือง
        RelievingPolitical = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200009"),

        //ระบายความเครียดด้านกฏหมาย 
        RelievingLegal = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200010"),

        //ระบายความเครียดด้านทรัพยากรธรรมชาติ
        RelievingNatural = g.Count(x =>
            (x.ug.RefGroupId != "53" && x.ug.RefGroupId != "77" && x.ug.RefGroupId != "79" && x.ug.RefGroupId != "68" )
            && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200011")
    };

    // ============================================================
    // Execute
    // ============================================================

        try
        {
            var result = await query
                .OrderBy(x => x.Contact_Start)
                .ToListAsync(cancellationToken);

            var message = result.Count == 0 ? "ไม่พบข้อมูล" : $"พบข้อมูล {result.Count} รายการ";
            return Ok(_0BaseReturn.Success(result, message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GetReport06 failed for {Start} - {Finish}.", startDay, finishDay);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }


    [HttpPost("GetReport07")]
    public async Task<IActionResult> GetReport07([FromBody] ServiceRequestReportRequestService request)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var startTime = string.IsNullOrWhiteSpace(request.P_Time_Start)
            ? TimeSpan.Zero
            : TimeSpan.Parse(request.P_Time_Start);

        var finishTime = string.IsNullOrWhiteSpace(request.P_Time_Finish)
            ? new TimeSpan(23, 59, 59)
            : TimeSpan.Parse(request.P_Time_Finish);


        var startDate = DateTime.ParseExact(request.P_Start, "MM/dd/yyyy", culture);
        var finishDate = DateTime.ParseExact(request.P_Finish, "MM/dd/yyyy", culture);
        var startDateTime = startDate.Date.Add(startTime);
        var finishDateTime = finishDate.Date.Add(finishTime);

        var result = await (
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
              && sr.CategoryId == request.P_Service
              && sr.Created >= startDateTime
              && sr.Created <= finishDateTime

            orderby sr.Code

            select new ServiceRequestReportDto
            {
                Code = sr.Code,
                Summary = sr.Summary,
                Detail = sr.Detail,
                SrReferenceLink = sr.ServiceReferenceLink,
                SrOpened = sr.DateOpened,
                SrClosed = sr.DateClosed,
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
            .ToListAsync();
            

        return Ok(result);
    }


}