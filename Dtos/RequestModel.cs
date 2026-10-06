public class ServiceRequestReportRequest
{
    public string? P_Start { get; set; }
    public string? P_Finish { get; set; }

    // optional เช่น "08:00:00"
    public string? P_Time_Start { get; set; }
    public string? P_Time_Finish { get; set; }
}

public class ServiceRequestReportRequestMainOrganization : ServiceRequestReportRequest
{
    public string? P_MainOrg { get; set; }
}