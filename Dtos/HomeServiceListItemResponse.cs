namespace ContactTogetherApi.Dtos;

/// <summary>
/// หนึ่งแถวของตารางในหน้าแรก (รายการรับ / งานดำเนินการ) วันที่เป็นเวลาไทยแบบไม่มี offset
/// (<c>2026-11-25T09:51:08</c>) ให้ frontend แปลงเป็นปี พ.ศ. เอง
/// </summary>
public class HomeServiceListItemResponse
{
    /// <summary><c>TblService.Id</c> ใช้เปิดหน้ารายละเอียดและเรียก <c>POST /Services/ReceiveService?id=</c></summary>
    public string? Id { get; set; }

    public string? Code { get; set; }

    public DateTime? DateOpened { get; set; }

    /// <summary>ชื่อประเภทบริการ (<c>TblCategory</c>) ตาม <c>Lang</c></summary>
    public string? CategoryName { get; set; }

    /// <summary>ชื่อสถานะของเรื่อง (<c>TblStatus</c>) ตาม <c>Lang</c></summary>
    public string? StatusName { get; set; }

    /// <summary>ข้อความเต็ม frontend ตัดคำเอง</summary>
    public string? Summary { get; set; }

    /// <summary>ค่าเดิมของ <c>TblService.Call_Back</c>: <c>Y</c> / <c>N</c> / <c>D</c> (สายหลุด) หรืออื่น ๆ</summary>
    public string? CallBack { get; set; }

    /// <summary><c>true</c> เมื่อ <see cref="CallBack"/> เป็น <c>Y</c> เท่านั้น (เหมือนรายงาน 03)</summary>
    public bool RequireCallBack { get; set; }

    /// <summary>ชั่วโมงที่ผ่านไปแล้วนับจาก <see cref="DateOpened"/> (ปัดเศษ) <c>null</c> เมื่อไม่มีวันเปิด</summary>
    public int? ElapsedHours { get; set; }

    /// <summary>เปิดมาแล้วตั้งแต่ 7 วัน (10,080 นาที) ขึ้นไป</summary>
    public bool IsOverDue { get; set; }
}
