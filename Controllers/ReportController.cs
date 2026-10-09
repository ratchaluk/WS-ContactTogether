using System.Globalization;
using ContactTogetherApi.Data;
using ContactTogetherApi.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Controllers;

[ApiController]
[Route("[controller]")]
[BaseReturnModelStateFilter]
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
        [FromBody] ServiceRequestReportDateTimeRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
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
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport2(
        [FromBody] ServiceRequestReportDateTimeRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
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
        [FromBody] ServiceRequestReportDateTimeRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
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
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        if (string.IsNullOrWhiteSpace(request.P_MainOrg))
        {
            return BadRequest(_0BaseReturn.Fail("กรุณาระบุ P_MainOrg"));
        }

        // Ids are upper-case and the collation is case-sensitive.
        var mainOrgId = request.P_MainOrg.Trim().ToUpperInvariant();

        var subOrgId =  request.P_SubOrg.Trim().ToUpperInvariant();
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

                // The SR's organization is the main organization itself or one of its sub-organizations.
                where sr.IsEnable == "T"
                    
                    && (org.Id == subOrgId && org2.Id == mainOrgId)
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
            _logger.LogError(
                ex, "GetReport04 failed for {Start} - {Finish}, org {MainOrg}.", start, finish, mainOrgId);
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
        [FromBody] ServiceRequestReportDateTimeRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        // The report counts whole days (TblContact.ContactStart holds dates only), so the time part
        // of P_Start / P_Finish is ignored.
        var startDay = start.Date;
        var finishDay = finish.Date;
        var dayAfterFinish = finishDay.AddDays(1);

        // ============================================================
        // Query
        // ============================================================
         var targetCategories = new[]
        {
            "0168B738100C4CBAB9AA45906A200012", // จิตไม่ปกติ
            "0168B738100C4CBAB9AA45906A200013", // เด็กโทรเล่น
            "0168B738100C4CBAB9AA45906A200014", // เสียงเงียบ
            "0168B738100C4CBAB9AA45906A200015", // โทรด่าหยาบคาย
            "0168B738100C4CBAB9AA45906A200018", // น้ำท่วม
            "0168B738100C4CBAB9AA45906A200002", // สัญญาณไม่ชัดเจน
            "0168B738100C4CBAB9AA45906A200003", // สายหลุด
            "0168B738100C4CBAB9AA45906A200016", // อื่น ๆ
            "0168B738100C4CBAB9AA45906A200017", // The Pizza Company
            "0168B738100C4CBAB9AA45906A200006", // ขอคำปรึกษาเจ้าหน้าที่
            "0168B738100C4CBAB9AA45906A200007", // ระบายความเครียดด้านสังคม
            "0168B738100C4CBAB9AA45906A200008", // ระบายความเครียดด้านเศรษฐกิจ
            "0168B738100C4CBAB9AA45906A200009", // ระบายความเครียดด้านการเมือง
            "0168B738100C4CBAB9AA45906A200010", // ระบายความเครียดด้านกฏหมาย
            "0168B738100C4CBAB9AA45906A200011"  // ระบายความเครียดด้านทรัพยากรธรรมชาติ
        };



        // Employees in the claim teams (TblOrganizationGroup.RefGroupId 53 or 77). Distinct, because an
        // employee can belong to more than one of these groups and must not count a contact twice.
        var claimMembers = (
            from uug in _db.TblEmployeeGroups
            join ug in _db.TblOrganizationGroups
                on uug.GroupId equals ug.GroupId
            where ug.RefGroupId == "53" || ug.RefGroupId == "77"
            select uug.EmployeeId)
            .Distinct();

        var query =
            from c in _db.TblContacts

            // Left join keeps days that have contacts but none by the claim teams.
            //join m in claimMembers
            //    on c.CreatedBy equals m into mGroup
            //from m in mGroup.DefaultIfEmpty()

            join m in claimMembers
                on c.CreatedBy equals m

            where c.ContactStart >= startDay
                && c.ContactStart < dayAfterFinish
                //กรองเฉพาะสายที่อยู่ใน 15 หมวดหมู่นี้เท่านั้น
                && targetCategories.Contains(c.CategoryId)

            //group new { c, m } by c.ContactStart!.Value.Date into g
            group new { c, m } by DateOnly.FromDateTime(c.ContactStart!.Value) into g
            select new
            {
                Contact_Start = g.Key,

                // ====================================================
                // Kidding call
                // ====================================================

                // จิตไม่ปกติ
                Insane =g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200012")
                        .Select(x => x.c.Id)
                        .Distinct()
                        .Count(),

                // เด็กโทรเล่น
                Prankcall = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200013")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // เสียงเงียบ
                Silence = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200014")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),
                // โทรด่าหยาบคาย
                Rude = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200015")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // น้ำท่วม
                Flood = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200018")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // ====================================================
                // สัญญาณไม่ชัดเจน
                // ====================================================

                Badline = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200002")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // ====================================================
                // สายหลุด
                // ====================================================

                CutOff = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200003")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // ====================================================
                // โทรผิด
                // ====================================================

                // อื่น ๆ
                Other = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200016")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // The Pizza Company
                The_Pizza_Company = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200017")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // ====================================================
                // Out of Scope
                // ====================================================

                // ขอคำปรึกษาเจ้าหน้าที่
                Request = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200006")
                            .Select(x => x.c.Id)
                            .Distinct()
                            .Count(),

                // ระบายความเครียดด้านสังคม
                RelievingSocial = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200007")
                                    .Select(x => x.c.Id)
                                    .Distinct()
                                    .Count(),

                // ระบายความเครียดด้านเศรษฐกิจ
                RelievingEconomic = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200008")
                                       .Select(x => x.c.Id)
                                       .Distinct()
                                       .Count(),

                // ระบายความเครียดด้านการเมือง
                RelievingPolitical = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200009")
                                        .Select(x => x.c.Id)
                                        .Distinct()
                                        .Count(),

                // ระบายความเครียดด้านกฏหมาย
                RelievingLegal = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200010")
                                    .Select(x => x.c.Id)
                                    .Distinct()
                                    .Count(),

                // ระบายความเครียดด้านทรัพยากรธรรมชาติ
                RelievingNatural = g.Where(x => x.m != null && x.c.CategoryId == "0168B738100C4CBAB9AA45906A200011")
                                      .Select(x => x.c.Id)
                                      .Distinct()
                                      .Count(),
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
        [FromBody] ServiceRequestReportDateTimeRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        // The report counts whole days (TblContact.ContactStart holds dates only), so the time part
        // of P_Start / P_Finish is ignored.
        var startDay = start.Date;
        var finishDay = finish.Date;
        var dayAfterFinish = finishDay.AddDays(1);

        // ============================================================
        // Query
        // ============================================================
        
         var targetCategoryIds = new[]
        {
            "0168B738100C4CBAB9AA45906A200012", // จิตไม่ปกติ
            "0168B738100C4CBAB9AA45906A200013", // เด็กโทรเล่น
            "0168B738100C4CBAB9AA45906A200014", // เสียงเงียบ
            "0168B738100C4CBAB9AA45906A200015", // โทรด่าหยาบคาย
            "0168B738100C4CBAB9AA45906A200018", // น้ำท่วม
            "0168B738100C4CBAB9AA45906A200002", // สัญญาณไม่ชัดเจน
            "0168B738100C4CBAB9AA45906A200003", // สายหลุด
            "0168B738100C4CBAB9AA45906A200016", // อื่น ๆ
            "0168B738100C4CBAB9AA45906A200017", // The Pizza Company
            "0168B738100C4CBAB9AA45906A200006", // ขอคำปรึกษาเจ้าหน้าที่
            "0168B738100C4CBAB9AA45906A200007", // ระบายความเครียดด้านสังคม
            "0168B738100C4CBAB9AA45906A200008", // ระบายความเครียดด้านเศรษฐกิจ
            "0168B738100C4CBAB9AA45906A200009", // ระบายความเครียดด้านการเมือง
            "0168B738100C4CBAB9AA45906A200010", // ระบายความเครียดด้านกฏหมาย
            "0168B738100C4CBAB9AA45906A200011"  // ระบายความเครียดด้านทรัพยากรธรรมชาติ
        };


        // Employees in the teams this report leaves out: the claim teams counted by GetReport05
        // (RefGroupId 53, 77), 1212 (79) and training (68). A contact counts here only when its creator
        // is in none of them, so each contact is counted once and never in both 05 and 06. Creators
        // with no group, or whose group id is missing from TblOrganizationGroup, are counted here.
        var excludedMembers = (
            from uug in _db.TblEmployeeGroups
            join ug in _db.TblOrganizationGroups
                on uug.GroupId equals ug.GroupId
            where ug.RefGroupId == "53" || ug.RefGroupId == "77"
                || ug.RefGroupId == "79" || ug.RefGroupId == "68"
            select uug.EmployeeId)
            .Distinct();

        var query =
            from c in _db.TblContacts

            //join m in excludedMembers
            //    on c.CreatedBy equals m into mGroup
            //from m in mGroup.DefaultIfEmpty()

            where c.ContactStart >= startDay
                && c.ContactStart < dayAfterFinish
                && !excludedMembers.Contains(c.CreatedBy)
                && targetCategoryIds.Contains(c.CategoryId)


            group c by c.ContactStart!.Value.Date into g
            orderby g.Key

            select new
            {
                 Contact_Start = DateOnly.FromDateTime(g.Key),

                // ====================================================
                // Kidding call
                // ====================================================

                // จิตไม่ปกติ
                Insane = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200012")
                          .Select(x => x.Id)
                          .Distinct()
                          .Count(),

                // เด็กโทรเล่น
                Prankcall = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200013")
                             .Select(x => x.Id)
                             .Distinct()
                             .Count(),

                // เสียงเงียบ
                Silence = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200014")
                           .Select(x => x.Id)
                           .Distinct()
                           .Count(),

                // โทรด่าหยาบคาย
                Rude = g.Where(x =>x.CategoryId == "0168B738100C4CBAB9AA45906A200015")
                        .Select(x => x.Id)
                        .Distinct()
                        .Count(),   

                // น้ำท่วม
                Flood = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200018")
                         .Select(x => x.Id)
                         .Distinct()
                         .Count(),

                // ====================================================
                // สัญญาณไม่ชัดเจน
                // ====================================================

                Badline = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200002")
                           .Select(x => x.Id)
                           .Distinct()
                           .Count(),

                // ====================================================
                // สายหลุด
                // ====================================================

                CutOff = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200003")
                          .Select(x => x.Id)
                          .Distinct()
                          .Count(),

                // ====================================================
                // โทรผิด
                // ====================================================

                // อื่น ๆ
                Other = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200016")
                         .Select(x => x.Id)
                         .Distinct()
                         .Count(),

                // The Pizza Company
                The_Pizza_Company = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200017")
                                      .Select(x => x.Id)
                                      .Distinct()
                                      .Count(),

                // ====================================================
                // Out of Scope
                // ====================================================

                // ขอคำปรึกษาเจ้าหน้าที่
                Request = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200006")
                           .Select(x => x.Id)
                           .Distinct()
                           .Count(),

                // ระบายความเครียดด้านสังคม
                RelievingSocial = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200007")
                                    .Select(x => x.Id)
                                    .Distinct()
                                    .Count(),

                // ระบายความเครียดด้านเศรษฐกิจ
                RelievingEconomic = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200008")
                                       .Select(x => x.Id)
                                       .Distinct()
                                       .Count(),

                // ระบายความเครียดด้านการเมือง
                RelievingPolitical = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200009")
                                        .Select(x => x.Id)
                                        .Distinct()
                                        .Count(),

                // ระบายความเครียดด้านกฏหมาย
                RelievingLegal = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200010")
                                   .Select(x => x.Id)
                                   .Distinct()
                                   .Count(),

                // ระบายความเครียดด้านทรัพยากรธรรมชาติ
                RelievingNatural = g.Where(x => x.CategoryId == "0168B738100C4CBAB9AA45906A200011")
                                     .Select(x => x.Id)
                                     .Distinct()
                                     .Count(),
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
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(_0BaseReturn), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReport07(
        [FromBody] ServiceRequestReportRequestService request,
        CancellationToken cancellationToken)
    {
        // -------------------------
        // วันที่ + เวลา
        // -------------------------
        if (!TryBuildDateTimeRange(request, out var start, out var finish, out var error))
        {
            return BadRequest(_0BaseReturn.Fail(error));
        }

        if (string.IsNullOrWhiteSpace(request.P_Service))
        {
            return BadRequest(_0BaseReturn.Fail("กรุณาระบุ P_Service"));
        }

        // Ids are upper-case and the collation is case-sensitive.
        var serviceId = request.P_Service.Trim().ToUpperInvariant();

        try
        {
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
                  && sr.CategoryId == serviceId
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
            _logger.LogError(
                ex, "GetReport07 failed for {Start} - {Finish}, service {Service}.", start, finish, serviceId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                _0BaseReturn.Fail("เกิดข้อผิดพลาดภายในระบบ"));
        }
    }


}