using System.ComponentModel;

public class ServiceRequestReportDateTimeRequest
{
    // ISO 8601 เวลาไทย ไม่มี offset เช่น "2026-07-01T08:00:00" (วินาทีไม่บังคับ)
    [DefaultValue("2026-07-01T00:00:00")]
    public string? P_Start { get; set; }

    [DefaultValue("2026-07-31T23:59:59")]
    public string? P_Finish { get; set; }
}

public class ServiceRequestReportRequestMainOrganization : ServiceRequestReportDateTimeRequest
{
    // TblOrganization.Id ของหน่วยงานหลัก ได้ SR ของหน่วยงานหลักและทุกหน่วยงานย่อยใต้มัน
    [DefaultValue("00E071487E1343B987C548DEDAE71113")]
    public string? P_MainOrg { get; set; }
    [DefaultValue("00E071487E1343B987C548DEDAE71267")]
    public string? P_SubOrg { get; set; }
}

public class ServiceRequestReportRequestService : ServiceRequestReportDateTimeRequest
{
    // TblCategory.Id ของประเภทบริการระดับบนสุด (Q&A, Claim, Literature, Contact Info, …) เทียบกับ TblService.CategoryId
    [DefaultValue("0168B738100C4CBAB9AA45906AD4D8E5")]
    public string? P_Service { get; set; }
}