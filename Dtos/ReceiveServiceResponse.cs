namespace ContactTogetherApi.Dtos;

/// <summary>ผลของ <c>POST /Services/ReceiveService</c></summary>
public class ReceiveServiceResponse
{
    /// <summary><c>TblService.Id</c> ของเรื่อง</summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>จำนวนงานที่เปลี่ยนจากรอการตอบรับเป็นรับงานแล้วในครั้งนี้ (0 = ไม่มีงานที่รอเรารับ)</summary>
    public int ReceivedCount { get; set; }
}
