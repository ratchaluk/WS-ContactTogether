public class ServiceRequestReportDto
{
    public string? Code { get; set; }
    public string? Summary { get; set; }
    public string? Detail { get; set; }
    public string SrReferenceLink { get; set; }

    public  string? SrOpened { get; set; }
    public  string? SrClosed { get; set; }

    public string? SrRequireCallBack { get; set; }
    public  string? Created { get; set; }

    public string? ANumber { get; set; }

    public string? ChannelName { get; set; }
    public string? SrTypeName { get; set; }
    public string? SrReference { get; set; }
    public string? SrStatusName { get; set; }

    public string? CreatedUName { get; set; }
    public string? CreaterName { get; set; }
    public int SkillAgentCreated { get; set; }

    public string OwnerUName { get; set; }
    public string? OwnerName { get; set; }

    public string? LastUpdatedUName { get; set; }
    public string? UpdateName { get; set; }

    public string? SubOrgId { get; set; }
    public string? SubOrgName { get; set; }

    public string? MainOrgId { get; set; }
    public string? MainOrgName { get; set; }

    public string? ContactName { get; set; }
    public string? Gender { get; set; }

    public string? Remark { get; set; }
}

public class SrCallbackResponse
{
    public string? Code { get; set; }
    public string? Summary { get; set; }
    public string? Detail { get; set; }
    public string? SrReferenceLink { get; set; }

    public  string? SrOpened { get; set; }
    public  string? SrClosed { get; set; }

    public string? SrRequireCallBack { get; set; }
    public  string? Created { get; set; }

    public string? ANumber { get; set; }

    public string? ChannelName { get; set; }
    public string? SrTypeName { get; set; }
    public string? SrReference { get; set; }
    public string? SrStatusName { get; set; }

    public string? CreatedUname { get; set; }
    public string? CreatorName { get; set; }
    public int SkillAgentCreated { get; set; }

    public string? OwnerUname { get; set; }
    public string? OwnerName { get; set; }

    public string? LastUpdatedUname { get; set; }
    public string? UpdaterName { get; set; }

    public string? SubOrgId { get; set; }
    public string? SubOrgName { get; set; }

    public string? MainOrgId { get; set; }
    public string? MainOrgName { get; set; }

    public string? ContactName { get; set; }
}