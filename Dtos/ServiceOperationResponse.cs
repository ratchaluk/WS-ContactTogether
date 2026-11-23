namespace ContactTogetherApi.Dtos;

/// <summary>หนึ่งแถวของ <c>TblActivity</c> (ไม่รวมไฟล์แนบ) เวลาเป็นเวลาไทยแบบไม่มี offset</summary>
public class ServiceOperationResponse
{
    /// <summary><c>TblActivity.Id</c> (uniqueidentifier)</summary>
    public Guid Id { get; set; }

    public string ServiceId { get; set; } = string.Empty;

    /// <summary>ลำดับงานภายในเรื่อง เริ่มที่ 1</summary>
    public int Line { get; set; }

    /// <summary><c>yyMMdd</c> ปี พ.ศ. ตามด้วยเลขรัน 5 หลักของวัน เช่น <c>69112300001</c></summary>
    public string Code { get; set; } = string.Empty;

    public string? ContactId { get; set; }

    public string CategoryId { get; set; } = string.Empty;

    public string StatusId { get; set; } = string.Empty;

    public DateTime DateOpened { get; set; }

    public DateTime? DateClosed { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string? OwnerId { get; set; }

    public string? GroupId { get; set; }

    public string IsReceived { get; set; } = string.Empty;

    public string IsEnable { get; set; } = string.Empty;

    public DateTime Created { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime Updated { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
