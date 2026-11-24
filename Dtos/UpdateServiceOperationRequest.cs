using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// ช่องของงาน (<c>TblActivity</c>) ที่แก้ไขได้ ทุกช่องไม่บังคับ: ไม่ส่งมา (<c>null</c>) = คงค่าเดิม
/// ค่าว่าง = ล้างเป็น NULL สำหรับช่องที่เป็น NULL ได้ ส่วนช่องที่ห้าม NULL (<c>StatusId</c>, <c>Summary</c>,
/// <c>CategoryId</c>) ค่าว่างถือว่าคงค่าเดิม <c>IsReceived</c> และ <c>DateClosed</c> server ตั้งเอง <c>ServiceId</c>, <c>Line</c>, <c>Code</c>
/// แก้ไม่ได้ และ <c>Updated</c>/<c>UpdatedBy</c> server เป็นคนกำหนด
/// </summary>
public class UpdateServiceOperationRequest
{
    /// <summary><c>TblStatus.Id</c> ที่ <c>StatusType = "ACTIVITY"</c> (เปิด / ปิด / ยกเลิก)</summary>
    [MaxLength(50)]
    public string? StatusId { get; set; }

    public string? Summary { get; set; }

    /// <summary>ไม่จำกัดความยาว (<c>nvarchar(max)</c>)</summary>
    public string? Detail { get; set; }

    /// <summary><c>TblCategory.Id</c> ชุด <c>ACTIVITY</c> ที่เปิดใช้งาน</summary>
    [MaxLength(50)]
    public string? CategoryId { get; set; }

    /// <summary><c>TblContact.Id</c></summary>
    [MaxLength(50)]
    public string? ContactId { get; set; }

    /// <summary><c>TblEmployee.Id</c></summary>
    [MaxLength(50)]
    public string? OwnerId { get; set; }

    [MaxLength(50)]
    public string? GroupId { get; set; }

    /// <summary>เวลาไทย</summary>
    public DateTime? DateOpened { get; set; }
}
