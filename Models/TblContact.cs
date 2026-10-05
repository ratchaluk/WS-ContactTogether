using System;
using System.Collections.Generic;

namespace ContactTogetherApi.Models;

public partial class TblContact
{
    public string? Id { get; set; }

    public string? CategoryId { get; set; }

    public string? ContactDetail { get; set; }

    public string? ContactStart { get; set; }

    public string? ContactEnd { get; set; }

    public string? MenuIvr { get; set; }

    public string? ChannelId { get; set; }

    public string? CreatedBy { get; set; }
}
