using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// ช่องของงาน (<c>TblActivity</c>) ที่แก้ไขได้ ทุกช่องไม่บังคับ: ไม่ส่งมา (<c>null</c>) = คงค่าเดิม
/// ค่าว่าง = ล้างเป็น NULL สำหรับช่องที่เป็น NULL ได้ ส่วนช่องที่ห้าม NULL (<c>StatusId</c>, <c>Summary</c>,
/// <c>CategoryId</c>, <c>IsReceived</c>) ค่าว่างถือว่าคงค่าเดิม <c>ServiceId</c>, <c>Line</c>, <c>Code</c>
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

    /// <summary><c>TblCategory.Id</c></summary>
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

    /// <summary><c>T</c> หรือ <c>F</c></summary>
    [RegularExpression("^[TF]$", ErrorMessage = "IsReceived must be 'T' or 'F'.")]
    public string? IsReceived { get; set; }

    /// <summary>เวลาไทย</summary>
    public DateTime? DateOpened { get; set; }

    /// <summary>เวลาไทย</summary>
    public DateTime? DateClosed { get; set; }
}
