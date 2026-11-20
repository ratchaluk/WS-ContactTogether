using ContactTogetherApi.Dtos;

namespace ContactTogetherApi.Helper;

/// <summary>
/// ค่า <c>TblAccountDetail.DetailType</c> ทั้งหมดที่ใช้ในระบบ ไม่มีตารางเก็บ จึงกำหนดไว้ที่นี่ที่เดียว
/// ทั้งตัวตรวจค่าตอนบันทึกและรายการ drop-down ใช้ชุดนี้ร่วมกัน
/// </summary>
public static class AccountDetailTypes
{
    /// <summary>เรียงตามที่ควรแสดงใน drop-down (<c>Id</c> คือค่าที่ต้องส่งกลับไปเป็น <c>detailType</c>)</summary>
    public static readonly IReadOnlyList<LookupItemResponse> Items =
    [
        new() { Id = "MOBILE", NameTh = "มือถือ" },
        new() { Id = "HOME", NameTh = "โทรศัพท์บ้าน" },
        new() { Id = "OFFICE", NameTh = "ที่ทำงาน" },
        new() { Id = "FAX", NameTh = "โทรสาร" },
        new() { Id = "MAIL", NameTh = "อีเมล" },
    ];

    public static readonly string[] Codes = [.. Items.Select(i => i.Id)];
}
