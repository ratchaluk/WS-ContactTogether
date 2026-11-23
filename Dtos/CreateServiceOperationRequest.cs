using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// งาน (การดำเนินการ) ใหม่ของเรื่อง หนึ่งแถวใน <c>TblActivity</c> ส่วน <c>Line</c>, <c>Code</c>,
/// <c>IsEnable</c> และ created/updated server เป็นคนกำหนด
/// </summary>
public class CreateServiceOperationRequest
{
    /// <summary><c>TblService.Id</c> ของเรื่องที่งานนี้อยู่</summary>
    [Required]
    [MaxLength(50)]
    public string? ServiceId { get; set; }

    /// <summary><c>TblStatus.Id</c> ที่ <c>StatusType = "ACTIVITY"</c> (เปิด / ปิด / ยกเลิก)</summary>
    [Required]
    [MaxLength(50)]
    public string? StatusId { get; set; }

    [Required]
    public string? Summary { get; set; }

    /// <summary>ไม่จำกัดความยาว (<c>nvarchar(max)</c>)</summary>
    public string? Detail { get; set; }

    /// <summary>
    /// <c>TblCategory.Id</c> ชุด <c>ACTIVITY</c> (ประเภทการดำเนินการ) ที่เปิดใช้งาน; ไม่ส่งมา = แถว ACTIVITY ที่
    /// <c>IsDefault = 'T'</c> (ไม่มีแถว default = 400)
    /// </summary>
    [MaxLength(50)]
    public string? CategoryId { get; set; }

    /// <summary><c>TblContact.Id</c> (เบอร์ผู้ติดต่อของงานนี้)</summary>
    [MaxLength(50)]
    public string? ContactId { get; set; }

    /// <summary><c>TblEmployee.Id</c>; ไม่ส่งมา = พนักงานที่เรียก</summary>
    [MaxLength(50)]
    public string? OwnerId { get; set; }

    [MaxLength(50)]
    public string? GroupId { get; set; }

    /// <summary><c>T</c> หรือ <c>F</c>; ไม่ส่งมา = <c>F</c></summary>
    [RegularExpression("^[TF]$", ErrorMessage = "IsReceived must be 'T' or 'F'.")]
    public string? IsReceived { get; set; }

    /// <summary>เวลาไทย; ไม่ส่งมา = เวลาปัจจุบัน</summary>
    public DateTime? DateOpened { get; set; }
}
