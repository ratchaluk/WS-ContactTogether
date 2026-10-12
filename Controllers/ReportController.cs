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
                && uug.GroupId != "39" // *หมายเหตุ: ถ้า GroupId เป็น int ให้แก้เป็น != 39
            select emp.Id)
            .Distinct();

        var query =
            from c in _db.TblContacts

            // JOIN tbl_category cat ON c1.Category_ID = cat.ID
            join cat in _db.TblCategories
                on c.CategoryId equals cat.Id

            // Left join keeps days that have contacts but none by the claim teams.
            //join m in claimMembers
            //    on c.CreatedBy equals m into mGroup
            //from m in mGroup.DefaultIfEmpty()

            join m in claimMembers
                on c.CreatedBy equals m

            where c.ContactStart >= startDay
                && c.ContactStart < dayAfterFinish
                // cat.Ref_ID IN (...)
                && targetRefCategories.Contains(cat.RefId)

            //group new { c, cat, m } by c.ContactStart!.Value.Date into g
            group new { c, cat, m } by DateOnly.FromDateTime(c.ContactStart!.Value) into g
            select new
            {
                contact_date = g.Key,
                bad_signal = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200002").Select(x => x.c.Id).Distinct().Count(),
                kidding_abnormal = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200012").Select(x => x.c.Id).Distinct().Count(),
                kidding_child = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200013").Select(x => x.c.Id).Distinct().Count(),
                kidding_rude = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200015").Select(x => x.c.Id).Distinct().Count(),
                kidding_silent = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200014").Select(x => x.c.Id).Distinct().Count(),
                flood = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200018").Select(x => x.c.Id).Distinct().Count(),
                lost_connection = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200003").Select(x => x.c.Id).Distinct().Count(),
                wrong_number_pizza = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200017").Select(x => x.c.Id).Distinct().Count(),
                wrong_number_other = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200016").Select(x => x.c.Id).Distinct().Count(),
                stress_social = g.Where(x => x.m != null && x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%สังคม%")).Select(x => x.c.Id).Distinct().Count(),
                stress_economic = g.Where(x => x.m != null && x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%เศรษฐกิจ%")).Select(x => x.c.Id).Distinct().Count(),
                stress_political = g.Where(x => x.m != null && x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%การเมือง%")).Select(x => x.c.Id).Distinct().Count(),
                stress_legal = g.Where(x => x.m != null && x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%กฎหมาย%")).Select(x => x.c.Id).Distinct().Count(),
                stress_natural_resources = g.Where(x => x.m != null && x.cat.RefId == "0168B738100C4CBAB9AA45906A200005" && EF.Functions.Like(x.cat.NameTh, "%ทรัพยากร%")).Select(x => x.c.Id).Distinct().Count(),
                consult_officer = g.Where(x => x.m != null && x.cat.Id == "0168B738100C4CBAB9AA45906A200006").Select(x => x.c.Id).Distinct().Count(),
                total = g.Where(x => x.m != null).Select(x => x.c.Id).Distinct().Count()
            };
        // ============================================================
        // Execute
        // ============================================================

        try
        {
            var result = await query
                .OrderBy(x => x.contact_date)
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
            // JOIN tbl_employee emp1 ON c1.Created_By = emp1.ID
            join emp in _db.TblEmployees
                on c.CreatedBy equals emp.Id
            where c.ContactStart >= startDay
                && c.ContactStart < dayAfterFinish
                // emp1.Position LIKE '%Claim%'
                && EF.Functions.Like(emp.Position, "%Claim%")
                // cat.Ref_ID IN (...)
                && targetRefCategories.Contains(cat.RefId)
                // EXISTS (SELECT 1 FROM users_user_group g WHERE g.user_id = c1.Created_By AND g.group_id <> 39)
                && _db.TblEmployeeGroups.Any(g => g.EmployeeId == c.CreatedBy && g.GroupId != "39")
            // GROUP BY contact_date
            group new { c, cat } by DateOnly.FromDateTime(c.ContactStart!.Value) into g
        select new{
            Contact_Date = g.Key,
            // bad_signal (สัญญาณไม่ชัดเจน)
            Bad_Signal = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200002")
                        .Select(x => x.c.Id).Distinct().Count(),
            // kidding_abnormal (จิตไม่ปกติ)
            Kidding_Abnormal = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200012")
                                .Select(x => x.c.Id).Distinct().Count(),
            // kidding_child (เด็กโทรเล่น)
            Kidding_Child = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200013")
                            .Select(x => x.c.Id).Distinct().Count(),
            // kidding_rude (โทรด่าหยาบคาย)
            Kidding_Rude = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200015")
                            .Select(x => x.c.Id).Distinct().Count(),
            // kidding_silent (เสียงเงียบ)
            Kidding_Silent = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200014")
                            .Select(x => x.c.Id).Distinct().Count(),
            // flood (น้ำท่วม)
            Flood = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200018")
                    .Select(x => x.c.Id).Distinct().Count(),
            // lost_connection (สายหลุด)
            Lost_Connection = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200003")
                            .Select(x => x.c.Id).Distinct().Count(),
            // wrong_number_pizza (The Pizza Company)
            Wrong_Number_Pizza = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200017")
                                .Select(x => x.c.Id).Distinct().Count(),
            // wrong_number_other (อื่น ๆ)
            Wrong_Number_Other = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200016")
                                .Select(x => x.c.Id).Distinct().Count(),
            // stress_social (ระบายความเครียดด้านสังคม)
            Stress_Social = g.Where(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                        && EF.Functions.Like(x.cat.NameTh, "%สังคม%"))
                            .Select(x => x.c.Id).Distinct().Count(),
            // stress_economic (ระบายความเครียดด้านเศรษฐกิจ)
            Stress_Economic = g.Where(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                        && EF.Functions.Like(x.cat.NameTh, "%เศรษฐกิจ%"))
                            .Select(x => x.c.Id).Distinct().Count(),
            // stress_political (ระบายความเครียดด้านการเมือง)
            Stress_Political = g.Where(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                            && EF.Functions.Like(x.cat.NameTh, "%การเมือง%"))
                                .Select(x => x.c.Id).Distinct().Count(),
            // stress_legal (ระบายความเครียดด้านกฎหมาย)
            Stress_Legal = g.Where(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                        && EF.Functions.Like(x.cat.NameTh, "%กฎหมาย%"))
                            .Select(x => x.c.Id).Distinct().Count(),
            // stress_natural_resources (ระบายความเครียดด้านทรัพยากรธรรมชาติ)
            Stress_Natural_Resources = g.Where(x => x.cat.RefId == "0168B738100C4CBAB9AA45906A200005"
                                                && EF.Functions.Like(x.cat.NameTh, "%ทรัพยากร%"))
                                        .Select(x => x.c.Id).Distinct().Count(),
            // consult_officer (ขอคำปรึกษาเจ้าหน้าที่)
            Consult_Officer = g.Where(x => x.cat.Id == "0168B738100C4CBAB9AA45906A200006")
                            .Select(x => x.c.Id).Distinct().Count(),
            // total
            Total = g.Select(x => x.c.Id).Distinct().Count()
        };
        // ============================================================
        // Execute
        // ============================================================

        try
        {
           var result = await query
            .OrderBy(x => x.Contact_Date)
            .ToListAsync(cancellationToken);

           var grandTotal = new
            {
                Contact_Date = (DateOnly?)null,
                Bad_Signal = result.Sum(x => x.Bad_Signal),
                Kidding_Abnormal = result.Sum(x => x.Kidding_Abnormal),
                Kidding_Child = result.Sum(x => x.Kidding_Child),
                Kidding_Rude = result.Sum(x => x.Kidding_Rude),
                Kidding_Silent = result.Sum(x => x.Kidding_Silent),
                Flood = result.Sum(x => x.Flood),
                Lost_Connection = result.Sum(x => x.Lost_Connection),
                Wrong_Number_Pizza = result.Sum(x => x.Wrong_Number_Pizza),
                Wrong_Number_Other = result.Sum(x => x.Wrong_Number_Other),
                Stress_Social = result.Sum(x => x.Stress_Social),
                Stress_Economic = result.Sum(x => x.Stress_Economic),
                Stress_Political = result.Sum(x => x.Stress_Political),
                Stress_Legal = result.Sum(x => x.Stress_Legal),
                Stress_Natural_Resources = result.Sum(x => x.Stress_Natural_Resources),
                Consult_Officer = result.Sum(x => x.Consult_Officer),
                Total = result.Sum(x => x.Total)
            };



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