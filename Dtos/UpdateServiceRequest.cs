using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Payload ของ <c>PUT /Services/UpdateService?id=...</c> แก้เฉพาะช่องที่ส่งมา
/// <list type="bullet">
/// <item>ช่องที่ไม่ส่ง (<c>null</c>) คงค่าเดิม</item>
/// <item>ช่องข้อความที่ส่งเป็น <c>""</c> ล้างค่าเป็น NULL (ยกเว้น <see cref="ServiceTitleId"/> ซึ่งล้างไม่ได้)</item>
/// <item>วันที่ล้างไม่ได้ ยกเว้น <c>DateClosed</c> ที่ระบบล้างให้เองเมื่อเปิดเรื่องซ้ำ (ดู <see cref="ReOpenComment"/>)</item>
/// </list>
/// </summary>
public class UpdateServiceRequest : ServiceFieldsRequest
{
    /// <summary><c>TblServiceTitles.Id</c> (หัวข้อเรื่อง) ไม่ส่งหรือส่งค่าว่าง = คงค่าเดิม</summary>
    [MaxLength(50)]
    public string? ServiceTitleId { get; set; }

    /// <summary>
    /// เหตุผลที่เปิดเรื่องซ้ำ ใช้เมื่อเปลี่ยน <c>StatusId</c> เป็น Pending (อยู่ระหว่างดำเนินการ) ให้เรื่องที่ปิดไปแล้ว
    /// (<c>DateClosed</c> มีค่า) ซึ่งระบบจะบันทึกลง <c>TblReOpenService</c> และล้าง <c>DateClosed</c>
    /// ไม่ส่งก็ได้ (บันทึกเป็นค่าว่าง)
    /// </summary>
    [MaxLength(200)]
    public string? ReOpenComment { get; set; }
}
