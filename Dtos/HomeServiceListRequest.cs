using System.ComponentModel;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Query string ของ <c>GET /Home/GetInboxServices</c> และ <c>GET /Home/GetProcessingServices</c>
/// (หน้าแรก: รายการรับ / งานดำเนินการ) ไม่มีตัวกรอง พนักงานมาจาก token
/// </summary>
public class HomeServiceListRequest
{
    /// <summary>Page to return, starting at 1.</summary>
    [DefaultValue(1)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Rows per page, 1 to 100.</summary>
    [DefaultValue(10)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// ภาษาของชื่อประเภทและสถานะ: <c>th</c> (ค่าเริ่มต้น) หรือ <c>en</c> ไม่สนตัวพิมพ์เล็กและใหญ่
    /// <c>en</c> ที่ไม่มีชื่ออังกฤษจะได้ชื่อไทยแทน
    /// </summary>
    [DefaultValue("th")]
    public string? Lang { get; set; }
}
