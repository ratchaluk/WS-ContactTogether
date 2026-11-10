using System.Diagnostics;
using System.Globalization;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Reports;
using ContactTogetherApi.Reports.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

/// <summary>
/// ExportReport01–07 return the same rows as GetReportXX, for the whole range (no paging), as a PDF or
/// an .xlsx file chosen with <c>?format=pdf|xlsx</c>. At most <c>ReportExport:MaxConcurrent</c>
/// exports run at once (further requests get 429). [Authorize] is commented out on each action while
/// the exports are being tested; without a token "พิมพ์โดย" shows "-".
/// </summary>
public partial class ReportController
{
    private const string PdfContentType = "application/pdf";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpPost("ExportReport01")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport01(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (!TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat))
        {
            return ValidationProblem(ModelState);
        }

        return await RunExportAsync(
            ReportLayouts.Report01, fileFormat, start, finish,
            () => Task.FromResult(SubTitles(SrDateLine(start, finish))),
            () => LoadReport01Async(start, finish, cancellationToken),
            cancellationToken);
    }

    [HttpPost("ExportReport02")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport02(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (!TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat))
        {
            return ValidationProblem(ModelState);
        }

        return await RunExportAsync(
            ReportLayouts.Report02, fileFormat, start, finish,
            () => Task.FromResult(SubTitles(SrDateLine(start, finish))),
            () => LoadReport02Async(start, finish, cancellationToken),
            cancellationToken);
    }

    [HttpPost("ExportReport03")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport03(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (!TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat))
        {
            return ValidationProblem(ModelState);
        }

        return await RunExportAsync(
            ReportLayouts.Report03, fileFormat, start, finish,
            () => Task.FromResult(SubTitles(SrDateLine(start, finish))),
            () => LoadReport03Async(start, finish, cancellationToken),
            cancellationToken);
    }

    /// <remarks>P_MainOrg and P_SubOrg are optional: none = every organization.</remarks>
    [HttpPost("ExportReport04")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport04(
        [FromBody] ServiceRequestReportRequestMainOrganization request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (!TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat))
        {
            return ValidationProblem(ModelState);
        }

        var mainOrgId = NormalizeId(request.P_MainOrg);
        var subOrgId = NormalizeId(request.P_SubOrg);

        return await RunExportAsync(
            ReportLayouts.Report04, fileFormat, start, finish,
            async () => SubTitles(
                SrDateLine(start, finish),
                await OrganizationLineAsync(mainOrgId, subOrgId, cancellationToken)),
            () => LoadReport04Async(start, finish, mainOrgId, subOrgId, cancellationToken),
            cancellationToken);
    }

    /// <remarks>Counts whole days: only the date part of P_Start / P_Finish is used.</remarks>
    [HttpPost("ExportReport05")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport05(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (!TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat))
        {
            return ValidationProblem(ModelState);
        }

        return await RunExportAsync(
            ReportLayouts.Report05, fileFormat, start.Date, finish.Date,
            () => Task.FromResult(SubTitles(DayRangeLine(start, finish))),
            () => LoadReport05Async(start, finish, cancellationToken),
            cancellationToken);
    }

    /// <remarks>Counts whole days: only the date part of P_Start / P_Finish is used.</remarks>
    [HttpPost("ExportReport06")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport06(
        [FromBody] ServiceRequestReportDateTimePagedRequest request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (!TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat))
        {
            return ValidationProblem(ModelState);
        }

        return await RunExportAsync(
            ReportLayouts.Report06, fileFormat, start.Date, finish.Date,
            () => Task.FromResult(SubTitles(DayRangeLine(start, finish))),
            () => LoadReport06Async(start, finish, cancellationToken),
            cancellationToken);
    }

    [HttpPost("ExportReport07")]
    //[Authorize] // ปิดไว้ชั่วคราวระหว่างทดสอบ
    [EnableRateLimiting(ReportExportOptions.RateLimitPolicy)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType, XlsxContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportReport07(
        [FromBody] ServiceRequestReportRequestService request,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var valid = TryReadExportRequest(request, format, out var start, out var finish, out var fileFormat);
        var serviceId = NormalizeId(request.P_Service);
        if (serviceId is null)
        {
            ModelState.AddModelError(nameof(request.P_Service), "กรุณาระบุ P_Service");
            valid = false;
        }

        if (!valid)
        {
            return ValidationProblem(ModelState);
        }

        return await RunExportAsync(
            ReportLayouts.Report07, fileFormat, start, finish,
            async () => SubTitles(
                SrDateLine(start, finish),
                await ServiceLineAsync(serviceId!, cancellationToken)),
            () => LoadReport07Async(start, finish, serviceId!, cancellationToken),
            cancellationToken);
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>Validates the date range and <c>format</c>, adding every problem to ModelState.</summary>
    private bool TryReadExportRequest(
        ServiceRequestReportDateTimeRequest request,
        string? format,
        out DateTime start,
        out DateTime finish,
        out ReportFormat fileFormat)
    {
        if (!ReportFormats.TryParse(format, out fileFormat))
        {
            ModelState.AddModelError("format", "format ต้องเป็น xlsx หรือ pdf");
        }

        if (!TryBuildDateTimeRange(request, out start, out finish, out var error))
        {
            ModelState.AddModelError(nameof(request.P_Start), error);
        }

        return ModelState.IsValid;
    }

    /// <summary>
    /// Loads the rows, writes the file to a temporary file (deleted once the response has been sent),
    /// and returns it. Failures are logged and answered with the usual 500 MessageResponse.
    /// </summary>
    private async Task<IActionResult> RunExportAsync<T>(
        ReportLayout<T> layout,
        ReportFormat format,
        DateTime start,
        DateTime finish,
        Func<Task<IReadOnlyList<string>>> subTitles,
        Func<Task<List<T>>> load,
        CancellationToken cancellationToken)
    {
        // Wide ranges take longer than EF Core's default 30-second command timeout.
        _db.Database.SetCommandTimeout(TimeSpan.FromSeconds(_exportOptions.CommandTimeoutSeconds));

        var printedBy = User.Identity?.Name ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "-";
        var range = string.Create(CultureInfo.InvariantCulture, $"{start:yyyyMMdd}-{finish:yyyyMMdd}");
        var stopwatch = Stopwatch.StartNew();
        FileStream? file = null;

        try
        {
            var rows = await load();
            var header = new ReportHeader(await subTitles(), printedBy, DateTime.Now);

            file = new FileStream(
                Path.Combine(Path.GetTempPath(), $"ct-report-{Guid.NewGuid():N}.tmp"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            if (format == ReportFormat.Xlsx)
            {
                await ExcelReportWriter.WriteAsync(layout, header, rows, file, cancellationToken);
            }
            else
            {
                PdfReportWriter.Write(layout, header, rows, file, cancellationToken);
            }

            file.Position = 0;

            _logger.LogInformation(
                "Export {Report} ({Format}) by {User}: range {Range}, {Rows} rows, {Bytes} bytes in {ElapsedMs} ms.",
                layout.Code, format, printedBy, range, rows.Count, file.Length, stopwatch.ElapsedMilliseconds);

            var result = File(
                file,
                ReportFormats.ContentType(format),
                $"{layout.Code}_{range}.{ReportFormats.Extension(format)}");
            file = null; // the result now owns it and disposes (deletes) it after sending
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Export {Report} ({Format}) failed for range {Range}.", layout.Code, format, range);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse(StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ"));
        }
        finally
        {
            file?.Dispose();
        }
    }

    private static IReadOnlyList<string> SubTitles(params string[] lines) => lines;

    /// <summary>Reports 01–04 and 07, e.g. "ประจำวันที่ 2026-07-01 ถึง 2026-07-31 เวลา 00:00 ถึง 23:59".</summary>
    private static string SrDateLine(DateTime start, DateTime finish) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"ประจำวันที่ {start:yyyy-MM-dd} ถึง {finish:yyyy-MM-dd} เวลา {start:HH:mm} ถึง {finish:HH:mm}");

    /// <summary>Reports 05 and 06, which count whole days.</summary>
    private static string DayRangeLine(DateTime start, DateTime finish) =>
        string.Create(CultureInfo.InvariantCulture, $"ระหว่างวันที่ {start:yyyy-MM-dd} ถึง {finish:yyyy-MM-dd}");

    private async Task<string> OrganizationLineAsync(string? mainOrgId, string? subOrgId, CancellationToken cancellationToken)
    {
        if (mainOrgId is null && subOrgId is null)
        {
            return "หน่วยงาน: ทุกหน่วยงาน";
        }

        var names = await _db.TblOrganizations.AsNoTracking()
            .Where(o => o.Id == mainOrgId || o.Id == subOrgId)
            .ToDictionaryAsync(o => o.Id, o => o.NameTh, cancellationToken);

        string Name(string id) => names.TryGetValue(id, out var name) ? name : id;

        return (mainOrgId, subOrgId) switch
        {
            ({ } main, { } sub) => $"หน่วยงาน: {Name(main)} / {Name(sub)}",
            ({ } main, null) => $"หน่วยงาน: {Name(main)} และหน่วยงานรอง",
            (null, { } sub) => $"หน่วยงาน: {Name(sub)}",
            _ => "หน่วยงาน: ทุกหน่วยงาน",
        };
    }

    private async Task<string> ServiceLineAsync(string serviceId, CancellationToken cancellationToken)
    {
        var name = await _db.TblCategories.AsNoTracking()
            .Where(c => c.Id == serviceId)
            .Select(c => c.NameTh)
            .FirstOrDefaultAsync(cancellationToken);

        return $"ประเภทบริการ: {name ?? serviceId}";
    }
}
