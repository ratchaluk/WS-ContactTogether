using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

/// <summary>การรับสาย (<c>TblContact</c>): หนึ่งแถวต่อการกดยืนยันฟอร์มรับสายหนึ่งครั้ง</summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class ContactController : ControllerBase
{
    /// <summary>ค่า <c>TblCategory.CategoryType</c> ของประเภทการรับสาย</summary>
    private const string ContactCategoryType = "CONTACT";

    /// <summary>ระยะที่ <c>ContactStart</c> ล้ำหน้านาฬิกาของ server ได้ เผื่อนาฬิกาเครื่อง client เดินไม่ตรง</summary>
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(1);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ContactController> _logger;

    public ContactController(ApplicationDbContext db, ILogger<ContactController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// บันทึกฟอร์มรับสายเป็นแถวใหม่ใน <c>TblContact</c> แล้วคืนแถวนั้นกลับไป
    /// <c>ID</c> สร้างให้อัตโนมัติ <c>CreatedBy</c> มาจาก token และ <c>ContactEnd</c> คือเวลาที่บันทึก
    /// <c>CategoryId</c> ต้องเป็นรายการสุดท้าย (leaf) ของ <c>GetContactType</c>
    /// </summary>
    [HttpPost("AddNewContact")]
    [ProducesResponseType(typeof(ContactResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddNewContact(
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken)
    {
        // CreatedBy คือพนักงานที่ทำรายการ เอามาจาก token ไม่ใช่จาก body
        var actingEmployeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(actingEmployeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot attribute the new contact."));
        }

        // เวลาไทย ตัดเศษให้เหลือหน่วยวินาที (คอลัมน์เป็น datetime2(0))
        // ตั้ง Kind เป็น Unspecified ให้เหมือนค่าที่อ่านจาก DB: JSON จะไม่มี "+07:00" ต่อท้าย
        // และรูปแบบตรงกับ contactStart ที่ client ส่งมา
        var now = DateTime.Now;
        now = DateTime.SpecifyKind(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)), DateTimeKind.Unspecified);

        // ถ้าขาด field ที่บังคับ [ApiController] ตอบ 400 ไปก่อนแล้ว มาถึงตรงนี้จึงมีค่าแน่นอน
        var contactDetail = request.ContactDetail!.Trim();
        var channelId = request.ChannelId!;
        var categoryId = request.CategoryId!;

        ValidateContactStart(request.ContactStart, now);
        var channelNameTh = await FindEnabledChannelNameAsync(channelId, cancellationToken);
        var categoryNameTh = await FindSelectableCallTypeNameAsync(categoryId, cancellationToken);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var contactStart = request.ContactStart is { } start
            ? start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond))
            : now;
        if (contactStart > now)
        {
            // ล้ำหน้าไม่เกินระยะที่ยอมให้ แต่ห้ามบันทึกเวลาเริ่มให้อยู่หลังเวลาจบ
            contactStart = now;
        }

        var contact = new TblContact
        {
            // ไม่กำหนด Id: Add() สร้าง UUID ให้เอง (ดู ApplicationDbContext.Keys.cs)
            CategoryId = categoryId,
            ContactDetail = contactDetail,
            ChannelId = channelId,
            CreatedBy = actingEmployeeId,
            ContactStart = contactStart,
            ContactEnd = now,
        };

        _db.TblContacts.Add(contact);
        await _db.SaveChangesAsync(cancellationToken);

        // ตั้งใจไม่ log contactDetail เพราะเป็นเบอร์โทรของผู้ติดต่อ (ข้อมูลส่วนบุคคล)
        _logger.LogInformation(
            "Contact {ContactId} created by {ActingEmployeeId}.",
            contact.Id,
            actingEmployeeId);

        return StatusCode(StatusCodes.Status201Created, new ContactResponse
        {
            Id = contact.Id!,
            ContactDetail = contactDetail,
            ChannelId = channelId,
            ChannelNameTh = channelNameTh,
            CategoryId = categoryId,
            CategoryNameTh = categoryNameTh,
            ContactStart = contact.ContactStart,
            ContactEnd = contact.ContactEnd,
            CreatedBy = actingEmployeeId,
        });
    }

    /// <summary>
    /// <c>ContactStart</c> ไม่บังคับ ถ้าส่งมาต้องเป็นเวลาไทยแบบไม่มี offset (กติกาเดียวกับรายงาน)
    /// และต้องไม่เกินเวลาปัจจุบัน
    /// </summary>
    private void ValidateContactStart(DateTime? contactStart, DateTime now)
    {
        if (contactStart is not { } start)
        {
            return;
        }

        if (start.Kind != DateTimeKind.Unspecified)
        {
            ModelState.AddModelError(
                nameof(CreateContactRequest.ContactStart),
                "Use Thai local time without an offset or Z, e.g. 2026-11-12T10:15:00.");
        }
        else if (start > now + AllowedClockSkew)
        {
            ModelState.AddModelError(
                nameof(CreateContactRequest.ContactStart),
                "ContactStart cannot be in the future.");
        }
    }

    /// <summary>
    /// คืนชื่อภาษาไทยของช่องทางที่เปิดใช้งาน ถ้าไม่พบจะเพิ่ม validation error
    /// schema ไม่มี FK บน <c>TblContact.ChannelId</c> ถ้าไม่ตรวจเอง id ที่ผิดก็จะถูกบันทึกลงไป
    /// </summary>
    private async Task<string?> FindEnabledChannelNameAsync(string channelId, CancellationToken cancellationToken)
    {
        var nameTh = await _db.TblChannels.AsNoTracking()
            .Where(c => c.Id == channelId && c.IsEnable == "T")
            .Select(c => c.NameTh)
            .FirstOrDefaultAsync(cancellationToken);

        if (nameTh is null)
        {
            ModelState.AddModelError(
                nameof(CreateContactRequest.ChannelId),
                $"No enabled TblChannel row with Id '{channelId}'.");
        }

        return nameTh;
    }

    /// <summary>
    /// คืนชื่อภาษาไทยของประเภทการรับสาย ถ้าเลือกไม่ได้จะเพิ่ม validation error
    /// ต้องเป็นรายการที่ <c>GetContactType</c> แสดงเป็น leaf คือเป็นหมวด <c>CONTACT</c> ที่เปิดใช้งาน
    /// ไม่ใช่หัวกลุ่ม ระดับแม่ทุกระดับเปิดใช้งาน และไม่มีลูกที่เปิดใช้งาน
    /// <c>TblCategory</c> รวมต้นไม้ CONTACT, SERVICE และ ACTIVITY ไว้ในตารางเดียว
    /// ถ้าตรวจแค่ว่ามี id อยู่ จะรับประเภทบริการเข้ามาได้ด้วย
    /// </summary>
    private async Task<string?> FindSelectableCallTypeNameAsync(string categoryId, CancellationToken cancellationToken)
    {
        // มีราว 20 แถว: โหลดประเภทการรับสายที่เปิดใช้งานทั้งหมดมาครั้งเดียว แล้วไล่ต้นไม้ในหน่วยความจำ
        var callTypes = await _db.TblCategories.AsNoTracking()
            .Where(c => c.CategoryType == ContactCategoryType && c.IsEnable == "T")
            .Select(c => new CallTypeRow(c.Id, c.RefId, c.NameTh))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        if (!IsSelectableLeaf(categoryId, callTypes))
        {
            ModelState.AddModelError(
                nameof(CreateContactRequest.CategoryId),
                $"'{categoryId}' is not a selectable call type. Use the id of a leaf from GET /ListOfValues/GetContactType.");
            return null;
        }

        return callTypes[categoryId].NameTh;
    }

    /// <summary>ตรวจว่า id เป็นประเภทการรับสายที่เลือกบันทึกได้ (leaf ที่ระดับแม่ทุกระดับเปิดใช้งาน)</summary>
    private static bool IsSelectableLeaf(string id, IReadOnlyDictionary<string, CallTypeRow> callTypes)
    {
        // root (RefId = Id) คือหัวกลุ่ม Service / Non Service ไม่ใช่ตัวเลือก
        if (!callTypes.TryGetValue(id, out var node) || node.RefId == node.Id)
        {
            return false;
        }

        // radio ที่มี drop-down ต้องบันทึกเป็นรายการใน drop-down แทน
        if (callTypes.Values.Any(c => c.RefId == id && c.Id != id))
        {
            return false;
        }

        // ระดับแม่ทุกระดับจนถึง root ต้องเปิดใช้งาน จำกัดจำนวนรอบไว้กันกรณี RefId วนลูป
        var current = node;
        for (var steps = 0; steps < callTypes.Count; steps++)
        {
            if (!callTypes.TryGetValue(current.RefId, out var parent))
            {
                return false;
            }

            if (parent.RefId == parent.Id)
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    /// <summary>แถวของประเภทการรับสายที่ใช้ตรวจต้นไม้ในหน่วยความจำ</summary>
    private sealed record CallTypeRow(string Id, string RefId, string NameTh);
}
