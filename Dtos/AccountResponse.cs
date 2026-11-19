namespace ContactTogetherApi.Dtos;

/// <summary>A <c>TblAccount</c> row as returned to the caller.</summary>
public class AccountResponse
{
    public int StatusCode { get; set; }

    public string Id { get; set; } = string.Empty;

    public string? SalutationTh { get; set; }

    public string? FirstnameTh { get; set; }

    public string? LastnameTh { get; set; }

    public string? SalutationEn { get; set; }

    public string? FirstnameEn { get; set; }

    public string? LastnameEn { get; set; }

    public string? GenderId { get; set; }

    public string? Birthdate { get; set; }

    public string? Address { get; set; }

    public string? AreaId { get; set; }

    public string? Zipcode { get; set; }

    public string? Remark { get; set; }

    public string IsScret { get; set; } = string.Empty;

    public string? IsEnable { get; set; }

    /// <summary>Stored as text in the legacy format <c>M/d/yyyy H:mm</c>.</summary>
    public string? Created { get; set; }

    public string? CreatedBy { get; set; }

    public string? Updated { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>แถว <c>TblAccountDetail</c> ที่บันทึกพร้อมบัญชีนี้ (ว่างถ้าไม่ได้ส่ง <c>Contacts</c>)</summary>
    public List<AccountContactResponse> Contacts { get; set; } = [];
}
