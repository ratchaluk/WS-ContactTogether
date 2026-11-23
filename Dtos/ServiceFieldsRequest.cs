using System.ComponentModel.DataAnnotations;

namespace ContactTogetherApi.Dtos;

/// <summary>
/// คอลัมน์ของ <c>TblService</c> ที่ผู้เรียกกำหนดได้ ใช้ร่วมกันระหว่าง <see cref="CreateServiceRequest"/> และ
/// <see cref="UpdateServiceRequest"/> ส่วน <c>Id</c>, <c>Code</c>, <c>IsEnable</c> และ created/updated
/// server เป็นคนกำหนด
/// </summary>
public abstract class ServiceFieldsRequest
{
    /// <summary><c>TblCategory.Id</c> (the service type).</summary>
    [MaxLength(200)]
    public string? CategoryId { get; set; }

    /// <summary><c>TblStatus.Id</c>.</summary>
    [MaxLength(200)]
    public string? StatusId { get; set; }

    /// <summary><c>TblEmployee.Id</c>. AddNewService: defaults to the calling employee.</summary>
    [MaxLength(200)]
    public string? OwnerId { get; set; }

    /// <summary><c>TblChannel.Id</c>.</summary>
    [MaxLength(200)]
    public string? ChannelIncomingId { get; set; }

    /// <summary><c>Y</c> = callback wanted, <c>N</c> = no, <c>D</c> = dropped call.</summary>
    [MaxLength(200)]
    public string? CallBack { get; set; }

    /// <summary><c>TblChannel.Id</c>.</summary>
    [MaxLength(200)]
    public string? ChannelOutgoingId { get; set; }

    /// <summary><c>TblAccount.Id</c>.</summary>
    [MaxLength(200)]
    public string? AccountId { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }

    /// <summary>
    /// รายละเอียด ไม่จำกัดความยาว (<c>TblService.Detail</c> เป็น <c>nvarchar(max)</c>) เก็บ emoji และทุกภาษาได้
    /// </summary>
    public string? Detail { get; set; }

    [MaxLength(2000)]
    public string? ServiceArea { get; set; }

    /// <summary><c>TblOrganization.Id</c>.</summary>
    [MaxLength(200)]
    public string? OrganizationId { get; set; }

    /// <summary><c>TblReference.Id</c>: where the request came from ("ที่มาข้อมูล" in the reports).</summary>
    [MaxLength(500)]
    public string? ServiceReference { get; set; }

    /// <summary>Free-text link to the source (a post, a news page, …): "Link ที่มาข้อมูล" in the reports.</summary>
    [MaxLength(500)]
    public string? ServiceReferenceLink { get; set; }

    /// <summary><c>TblArea.Id</c>.</summary>
    [MaxLength(200)]
    public string? AreaId { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary><c>TblLevel.Id</c> with <c>LevelType = "SEVERITY"</c> (ความเร่งด่วน).</summary>
    [MaxLength(500)]
    public string? LevelSeverityId { get; set; }

    /// <summary><c>TblLevel.Id</c> with <c>LevelType = "PRIORITY"</c> (ความสำคัญ).</summary>
    [MaxLength(500)]
    public string? LevelPriorityId { get; set; }

    /// <summary><c>TblLevel.Id</c> with <c>LevelType = "SECRET"</c> (ชั้นความลับ).</summary>
    [MaxLength(500)]
    public string? LevelSecretId { get; set; }

    /// <summary>Thai local time. AddNewService: defaults to now.</summary>
    public DateTime? DateOpened { get; set; }

    /// <summary>Thai local time.</summary>
    public DateTime? OnScene { get; set; }
}
