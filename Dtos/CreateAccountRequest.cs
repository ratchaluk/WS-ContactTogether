using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// Payload for creating a <c>TblAccount</c> row (the caller/contact side of the system).
/// Almost every column is nullable in the schema, so almost everything here is optional.
/// </summary>
public class CreateAccountRequest
{
    [MaxLength(100)]
    public string? SalutationTh { get; set; }

    /// <summary>
    /// The column is nullable, but an account with no name is not usable from the UI, so this
    /// endpoint requires it.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string FirstnameTh { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LastnameTh { get; set; }

    [MaxLength(100)]
    public string? SalutationEn { get; set; }

    [MaxLength(200)]
    public string? FirstnameEn { get; set; }

    [MaxLength(200)]
    public string? LastnameEn { get; set; }

    /// <summary><c>TblGender.Id</c>.</summary>
    [MaxLength(50)]
    public string? GenderId { get; set; }

    /// <summary>Stored as free text - the column is <c>nvarchar(50)</c>, not a date type.</summary>
    [MaxLength(50)]
    public string? Birthdate { get; set; }

    public string? Address { get; set; }

    /// <summary><c>TblArea.Id</c>.</summary>
    [MaxLength(50)]
    public string? AreaId { get; set; }

    /// <summary>
    /// Digits only. The scaffolded model types this column as <c>decimal</c> even though the
    /// database column is <c>nvarchar(100)</c>, so a non-numeric value cannot be stored today.
    /// </summary>
    [MaxLength(100)]
    public string? Zipcode { get; set; }

    public string? Remark { get; set; }

    /// <summary>"Is secret" - note the column name is misspelled <c>IsScret</c> in the schema.</summary>
    [MaxLength(10)]
    public string IsScret { get; set; } = "F";

    [MaxLength(10)]
    public string IsEnable { get; set; } = "T";

    /// <summary>
    /// เบอร์โทร (หรืออีเมลเมื่อ <see cref="DetailType"/> เป็น <c>MAIL</c>) บันทึกลง
    /// <c>TblAccountDetail.Detail</c> โดยผูก <c>AccountId</c> กับบัญชีที่สร้าง ไม่ส่งมาก็ได้
    /// ถ้าไม่ส่งจะไม่เพิ่มแถวใน <c>TblAccountDetail</c>
    /// </summary>
    [MaxLength(100)]
    public string? PhoneNo { get; set; }

    /// <summary>
    /// <c>TblAccountDetail.DetailType</c>: <c>MOBILE</c> (ค่าเริ่มต้น), <c>HOME</c>, <c>OFFICE</c>,
    /// <c>FAX</c> หรือ <c>MAIL</c> ใช้เมื่อส่ง <see cref="PhoneNo"/> มาเท่านั้น
    /// </summary>
    [MaxLength(50)]
    public string? DetailType { get; set; }
}
