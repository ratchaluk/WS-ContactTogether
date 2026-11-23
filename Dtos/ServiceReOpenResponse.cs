namespace ContactTogetherApi.Dtos;

/// <summary>หนึ่งครั้งที่เรื่องถูกเปิดซ้ำ (แถวใน <c>TblReOpenService</c>) เวลาเป็นเวลาไทยแบบไม่มี offset</summary>
public class ServiceReOpenResponse
{
    /// <summary>ครั้งที่เปิดซ้ำของเรื่องนี้ เริ่มที่ 1</summary>
    public int Line { get; set; }

    public string ReOpenComment { get; set; } = string.Empty;

    /// <summary>วันที่ปิดเรื่อง ก่อนถูกเปิดซ้ำครั้งนี้</summary>
    public DateTime Closed { get; set; }

    /// <summary><c>TblEmployee.Id</c> ของผู้ปิดเรื่อง</summary>
    public string? ClosedBy { get; set; }

    public string? ClosedByName { get; set; }

    public DateTime ReOpened { get; set; }

    /// <summary><c>TblEmployee.Id</c> ของผู้เปิดเรื่องซ้ำ</summary>
    public string? ReOpenedBy { get; set; }

    public string? ReOpenedByName { get; set; }
}
