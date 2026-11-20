using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>เบอร์โทรหรืออีเมล 1 รายการของผู้ติดต่อ บันทึกเป็น 1 แถวใน <c>TblAccountDetail</c></summary>
public class AccountContactRequest
{
    /// <summary>
    /// <c>MOBILE</c> (ค่าเริ่มต้นเมื่อว่าง), <c>HOME</c>, <c>OFFICE</c>, <c>FAX</c> หรือ <c>MAIL</c>
    /// ดูรายการจาก <c>GET /ListOfValues/GetAccountDetailType</c>
    /// </summary>
    [MaxLength(50)]
    public string? DetailType { get; set; }

    /// <summary>เบอร์โทร หรืออีเมลเมื่อ <see cref="DetailType"/> เป็น <c>MAIL</c></summary>
    [Required]
    [MaxLength(100)]
    public string Detail { get; set; } = string.Empty;
}
