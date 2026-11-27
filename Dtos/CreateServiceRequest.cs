using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Payload for creating a <c>TblService</c> row. <c>Id</c>, <c>Code</c>, <c>IsEnable</c> and the
/// created/updated columns are set by the server, so they are not here.
/// </summary>
public class CreateServiceRequest : ServiceFieldsRequest
{
    /// <summary>
    /// <c>TblServiceTitles.Id</c> (หัวข้อเรื่อง จาก <c>GET /ListOfValues/GetServiceTitle</c>) ต้องระบุ
    /// เพราะคอลัมน์เป็น NOT NULL รับได้ทั้งตัวพิมพ์เล็กและใหญ่
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string? ServiceTitleId { get; set; }

    /// <summary>
    /// <c>TblContact.Id</c> ของการรับสายที่เปิดเรื่องนี้ (<c>id</c> จาก <c>POST /Contact/AddNewContact</c>) บังคับ ต้องมีอยู่จริง
    /// ไม่ใช่คอลัมน์ของ <c>TblService</c>: ใส่ให้งานแรก (<c>TblActivity</c> Line 1) ที่สร้างพร้อมเรื่อง
    /// ซึ่งระบบเดิมและรายงานใช้หาเบอร์ผู้ติดต่อ
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string? ContactId { get; set; }
}
