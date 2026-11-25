namespace ContactTogetherApi.Dtos;

/// <summary>
/// สัญญาณใน <c>GET /Home/InboxStream</c> (event <c>inbox</c>) บอกแค่ว่ารายการรับของพนักงานอาจเปลี่ยน
/// ไม่ได้ส่งรายการมาด้วย ให้ frontend โหลด <c>GET /Home/GetInboxServices</c> ใหม่เอง
/// </summary>
/// <param name="Reason">สาเหตุ ค่าหนึ่งใน <see cref="InboxSignalReasons"/></param>
/// <param name="ServiceId"><c>TblService.Id</c> ของเรื่องที่เกี่ยวข้อง</param>
public record InboxSignal(string Reason, string? ServiceId);

/// <summary>ค่าของ <see cref="InboxSignal.Reason"/></summary>
public static class InboxSignalReasons
{
    /// <summary>มีงานใหม่ส่งมาให้ (<c>AddNewServiceOperations</c>)</summary>
    public const string Assigned = "assigned";

    /// <summary>งานหรือเรื่องเปลี่ยน (ผู้รับ สถานะงาน ปิดหรือเปิดเรื่องซ้ำ) อาจเข้าหรือออกจากรายการรับ</summary>
    public const string Updated = "updated";

    /// <summary>งานที่รอรับถูกลบ (<c>DeleteServiceOperations</c>)</summary>
    public const string Removed = "removed";

    /// <summary>พนักงานคนนี้รับงานแล้ว (<c>ReceiveService</c>) แท็บอื่นของคนเดียวกันควรโหลดใหม่</summary>
    public const string Received = "received";
}
