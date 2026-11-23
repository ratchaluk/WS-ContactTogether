namespace ContactTogetherApi.Dtos;

/// <summary>
/// หนึ่งงานใน <c>GET /Services/GetServiceOperations</c>: ทุกช่องของ <see cref="ServiceOperationResponse"/>
/// พร้อมชื่อที่แปลงจาก id แล้ว ชื่อเป็น <c>null</c> ถ้า id ว่างหรือหาแถวไม่เจอ
/// </summary>
public class ServiceOperationListItemResponse : ServiceOperationResponse
{
    public string? StatusNameTh { get; set; }

    /// <summary><c>OPEN</c> / <c>CLOSE</c> / <c>CANCEL</c> ใช้ตัดสินว่างานเสร็จหรือยัง</summary>
    public string? StatusIsType { get; set; }

    public string? CategoryNameTh { get; set; }

    public string? OwnerName { get; set; }

    /// <summary><c>TblContact.ContactDetail</c> (เบอร์ที่รับสาย) ของ <c>ContactId</c></summary>
    public string? ContactDetail { get; set; }

    public string? CreatedByName { get; set; }

    public string? UpdatedByName { get; set; }
}
