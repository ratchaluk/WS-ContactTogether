using System.Globalization;
using ContactTogetherApi.Reports.Export;

namespace ContactTogetherApi.Reports;

/// <summary>
/// Title and columns of each exported report. Column order and headers follow the legacy sample
/// files in docs/report01.pdf–report06.pdf; Report 07 has no sample and uses Report 01's columns.
/// </summary>
public static class ReportLayouts
{
    // Report 06 header groups. Declared first: static fields initialise in order.
    private static readonly string[] KiddingCall = ["1. Kidding Call"];
    private static readonly string[] WrongNumber = ["4.Wrong Number(โทรผิด)"];
    private static readonly string[] OutOfScope = ["5. Out Of Scope"];
    private static readonly string[] OutOfScopeStress = ["5. Out Of Scope", "ระบายความเครียด"];

    public static readonly ReportLayout<ServiceRequestReportDto> Report01 = new()
    {
        Code = "Report01",
        Title = "รายละเอียดข้อมูล SR สำหรับ Supervisor ทุกสังกัด",
        RowNumber = true,
        Columns = ServiceRequestColumns(),
    };

    public static readonly ReportLayout<SrCallbackResponse> Report02 = new()
    {
        Code = "Report02",
        Title = "รายละเอียดข้อมูล SR สำหรับ Supervisor ประเภทสายหลุด",
        RowNumber = true,
        Columns = new ColumnSet<SrCallbackResponse>()
            .DateTime("Opened", r => ReportValues.ParseIso(r.SrOpened), 1.2f)
            .Text("SR", r => r.Code)
            .Text("Type", r => r.SrTypeName, 0.8f)
            .Text("a_number", r => r.ANumber, 0.9f)
            .Text("หน่วยงานหลัก", r => r.MainOrgName)
            .Text("หน่วยงานรอง", r => r.SubOrgName)
            .Text("Summary", r => r.Summary, 1.3f)
            .Text("Description", r => r.Detail, 2f)
            .Text("Status", r => r.SrStatusName, 0.8f)
            .Text("ที่มาข้อมูล", r => r.SrReference, 0.8f)
            .Text("Link ที่มาข้อมูล", r => r.SrReferenceLink, 1.4f, breakAnywhere: true)
            .DateTime("Closed", r => ReportValues.ParseIso(r.SrClosed), 1.2f)
            .Text("require_callback", r => ReportValues.RequireCallBack(r.SrRequireCallBack), 0.7f)
            .Text("Created_by_Id", r => r.CreatedUname, 1f)
            .Text("Created_by", r => r.CreatorName)
            .Text("Skill", r => ReportValues.Skill(r.SkillAgentCreated), 0.5f)
            .Text("Owner_Id", r => r.OwnerUname, 0.9f)
            .Text("Owner", r => r.OwnerName)
            .Text("Last_updated_by_Id", r => r.LastUpdatedUname, 1.1f)
            .Text("Last_updated_by", r => r.UpdaterName)
            .Text("Contact_Name", r => ReportValues.Trim(r.ContactName)),
    };

    public static readonly ReportLayout<SrCallbackResponse> Report03 = new()
    {
        Code = "Report03",
        Title = "รายละเอียดข้อมูล SR สำหรับ Supervisor ประเภท Require CallBack",
        RowNumber = true,
        Columns = new ColumnSet<SrCallbackResponse>()
            .DateTime("Opened", r => ReportValues.ParseIso(r.SrOpened), 1.2f)
            .Text("SR", r => r.Code)
            .Text("Type", r => r.SrTypeName, 0.8f)
            .Text("a_number", r => r.ANumber, 0.9f)
            .Text("หน่วยงานหลัก", r => r.MainOrgName)
            .Text("หน่วยงานรอง", r => r.SubOrgName)
            .Text("Summary", r => r.Summary, 1.3f)
            .Text("Description", r => r.Detail, 2f)
            .Text("Status", r => r.SrStatusName, 0.8f)
            .Text("ที่มาข้อมูล", r => r.SrReference, 0.8f)
            .Text("Link ที่มาข้อมูล", r => r.SrReferenceLink, 1.4f, breakAnywhere: true)
            .DateTime("Closed", r => ReportValues.ParseIso(r.SrClosed), 1.2f)
            .Text("Created_by_Id", r => r.CreatedUname, 1f)
            .Text("Created_by", r => r.CreatorName)
            .Text("Skill", r => ReportValues.Skill(r.SkillAgentCreated), 0.5f)
            .Text("Owner_Id", r => r.OwnerUname, 0.9f)
            .Text("Owner", r => r.OwnerName)
            .Text("Last_updated_by_Id", r => r.LastUpdatedUname, 1.1f)
            .Text("Last_updated_by", r => r.UpdaterName)
            .Text("Contact_No", r => r.ContactNo, 0.9f),
    };

    public static readonly ReportLayout<SrCallbackResponse> Report04 = new()
    {
        Code = "Report04",
        Title = "รายละเอียดข้อมูล SR สำหรับ Supervisor แยกตามหน่วยงาน",
        RowNumber = true,
        Columns = new ColumnSet<SrCallbackResponse>()
            .DateTime("Opened", r => ReportValues.ParseIso(r.SrOpened), 1.2f)
            .Text("SR", r => r.Code)
            .Text("Type", r => r.SrTypeName, 0.8f)
            .Text("a_number", r => r.ANumber, 0.9f)
            .Text("หน่วยงาน", r => r.SubOrgName, 1.2f)
            .Text("Summary", r => r.Summary, 1.5f)
            .Text("Description", r => r.Detail, 2.2f)
            .Text("Status", r => r.SrStatusName, 0.8f)
            .Text("ที่มาข้อมูล", r => r.SrReference, 0.8f)
            .Text("Link ที่มาข้อมูล", r => r.SrReferenceLink, 1.4f, breakAnywhere: true)
            .DateTime("Closed", r => ReportValues.ParseIso(r.SrClosed), 1.2f)
            .Text("Created_by_Id", r => r.CreatedUname, 1f)
            .Text("Created_by", r => r.CreatorName)
            .Text("Skill", r => ReportValues.Skill(r.SkillAgentCreated), 0.5f)
            .Text("require_callback", r => ReportValues.RequireCallBack(r.SrRequireCallBack), 0.7f)
            .Text("Owner_Id", r => r.OwnerUname, 0.9f)
            .Text("Owner", r => r.OwnerName)
            .Text("Last_updated_by_Id", r => r.LastUpdatedUname, 1.1f)
            .Text("Last_updated_by", r => r.UpdaterName)
            .Text("Contact_Name", r => ReportValues.Trim(r.ContactName)),
    };

    public static readonly ReportLayout<ClaimContactDailyReportDto> Report05 = new()
    {
        Code = "Report05",
        Title = "Admin-รายงานสรุปจำนวนข้อมูลประเภท Non-Service (ตามการเลือกใช้บริการของประชาชน กด 2 Claim)",
        TotalsRow = true,
        Paper = PdfPaper.A4Landscape,
        PdfFontSize = 8f,
        Columns = new ColumnSet<ClaimContactDailyReportDto>()
            .Text("วันที่", r => ReportValues.ThaiShortDate(r.Contact_Date), 1.1f)
            .Number("Bad Signal", r => r.Bad_Signal)
            .Number("Kidding Call\nจิตไม่ปกติ", r => r.Kidding_Abnormal)
            .Number("Kidding Call\nเด็กโทรเล่น", r => r.Kidding_Child)
            .Number("Kidding Call\nโทรด่าหยาบ", r => r.Kidding_Rude)
            .Number("Kidding Call\nเสียงเงียบ", r => r.Kidding_Silent)
            .Number("Flood", r => r.Flood, 0.7f)
            .Number("Lost\nConnection", r => r.Lost_Connection)
            .Number("Wrong Number\nThe Pizza\nCompany", r => r.Wrong_Number_Pizza)
            .Number("Wrong\nNumber\nอื่นๆ", r => r.Wrong_Number_Other)
            .Number("ระบาย\nความเครียด\nด้านสังคม", r => r.Stress_Social, 1.25f)
            .Number("ระบาย\nความเครียด\nด้านเศรษฐกิจ", r => r.Stress_Economic, 1.25f)
            .Number("ระบาย\nความเครียด\nด้านการเมือง", r => r.Stress_Political, 1.25f)
            .Number("ระบาย\nความเครียด\nด้านกฎหมาย", r => r.Stress_Legal, 1.25f)
            .Number("ระบาย\nความเครียด\nด้าน\nทรัพยากรธรรมชาติ", r => r.Stress_Natural_Resources, 1.6f)
            .Number("ขอคำปรึกษา\nเจ้าหน้าที่", r => r.Consult_Officer, 1.2f)
            .Number("รวม", r => r.Total, 0.8f),
    };

    public static readonly ReportLayout<NonServiceContactDailyReportDto> Report06 = new()
    {
        Code = "Report06",
        Title = "Admin-รายงานสรุปจำนวนข้อมูลประเภท Non-Service (ตามการเลือกใช้บริการของประชาชน Information)",
        TotalsRow = true,
        Paper = PdfPaper.A4Landscape,
        PdfFontSize = 8f,
        Columns = new ColumnSet<NonServiceContactDailyReportDto>()
            .Text("วันที่", r => ReportValues.LegacyDayMonthBe(r.Contact_Date), 1.8f)
            .Number("จิตไม่\nปกติ", r => r.Kidding_Abnormal, groups: KiddingCall)
            .Number("เด็กโทร\nเล่น", r => r.Kidding_Child, groups: KiddingCall)
            .Number("โทรด่า\nหยาบคาย", r => r.Kidding_Rude, groups: KiddingCall)
            .Number("เสียง\nเงียบ", r => r.Kidding_Silent, groups: KiddingCall)
            .Number("Flood", r => r.Flood, 0.8f, KiddingCall)
            .Number("รวม\nKidding\nCall", r => r.Kidding_Total, groups: KiddingCall)
            .Number("2.Bad Signal\n(สัญญาณไม่\nชัดเจน)", r => r.Bad_Signal, 1.3f)
            .Number("3. Lost\nConnection\n(สายหลุด)", r => r.Lost_Connection, 1.3f)
            .Number("The Pizza\nCompany", r => r.Wrong_Number_Pizza, 1.1f, WrongNumber)
            .Number("อื่นๆ", r => r.Wrong_Number_Other, groups: WrongNumber)
            .Number("ขอ\nคำปรึกษา\nเจ้าหน้าที่", r => r.Consult_Officer, 1.1f, OutOfScope)
            .Number("ด้าน\nสังคม", r => r.Stress_Social, groups: OutOfScopeStress)
            .Number("ด้าน\nเศรษฐกิจ", r => r.Stress_Economic, groups: OutOfScopeStress)
            .Number("ด้าน\nการเมือง", r => r.Stress_Political, groups: OutOfScopeStress)
            .Number("ด้าน\nกฎหมาย", r => r.Stress_Legal, groups: OutOfScopeStress)
            .Number("ด้าน\nทรัพยากร\nธรรมชาติ", r => r.Stress_Natural_Resources, 1.2f, OutOfScopeStress)
            .Number("รวม\nOut of Scope", r => r.Out_Of_Scope_Total, 1.1f, OutOfScope)
            .Number("รวม\nNonservice", r => r.Total, 1.4f),
    };

    public static readonly ReportLayout<ServiceRequestReportDto> Report07 = new()
    {
        Code = "Report07",
        Title = "รายละเอียดข้อมูล SR สำหรับ Supervisor แยกตามประเภทบริการ",
        RowNumber = true,
        Columns = ServiceRequestColumns(),
    };

    /// <summary>Report 01 columns (also used by Report 07, which shares the DTO and has no sample).</summary>
    private static ColumnSet<ServiceRequestReportDto> ServiceRequestColumns() =>
        new ColumnSet<ServiceRequestReportDto>()
            .DateTime("Opened", r => ReportValues.ParseIso(r.SrOpened), 1.2f)
            .Text("SR", r => r.Code)
            .Text("Type", r => r.SrTypeName, 0.8f)
            .Text("a_number", r => r.ANumber, 0.9f)
            .Text("หน่วยงาน", r => r.MainOrgName)
            .Text("หน่วยงานรอง", r => r.SubOrgName)
            .Text("Summary", r => r.Summary, 1.2f)
            .Text("Description", r => r.Detail, 2f)
            .Text("Channel", r => r.ChannelName, 0.7f)
            .Text("Status", r => r.SrStatusName, 0.8f)
            .Text("ที่มาข้อมูล", r => r.SrReference, 0.8f)
            .Text("Link ที่มาข้อมูล", r => r.SrReferenceLink, 1.3f, breakAnywhere: true)
            .DateTime("Closed", r => ReportValues.ParseIso(r.SrClosed), 1.2f)
            .Text("require", r => ReportValues.RequireCallBack(r.SrRequireCallBack), 0.7f)
            .Text("Created_by_Id", r => r.CreatedUName, 1f)
            .Text("Created_by", r => r.CreaterName)
            .Text("Skill", r => ReportValues.Skill(r.SkillAgentCreated), 0.5f)
            .Text("Owner_Id", r => r.OwnerUName, 0.9f)
            .Text("Owner", r => r.OwnerName)
            .Text("Last_updated_by_Id", r => r.LastUpdatedUName, 1.1f)
            .Text("Last_updated_by", r => r.UpdateName)
            .Text("Contact_Name", r => ReportValues.Trim(r.ContactName))
            .Text("Gender", r => r.Gender, 0.6f)
            .Text("Remark", r => r.Remark);
}

/// <summary>Value conversions that reproduce what the legacy reports printed.</summary>
public static class ReportValues
{
    private static readonly string[] ThaiMonthsShort =
        ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];

    // As printed by the legacy Report 06 ("June", "July" in full, the others abbreviated).
    private static readonly string[] LegacyEnglishMonths =
        ["Jan", "Feb", "Mar", "Apr", "May", "June", "July", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>The DTOs carry SrOpened/SrClosed as "yyyy-MM-ddTHH:mm:ss" strings; "" means no date.</summary>
    public static DateTime? ParseIso(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>TblService.Call_Back: Y = callback wanted, D = dropped call (Report 02), anything else = not wanted.</summary>
    public static string RequireCallBack(string? callBack) => callBack?.Trim() switch
    {
        "Y" => "ต้องการ",
        "D" => "สายหลุด",
        _ => "ไม่ต้องการ",
    };

    /// <summary>1 = created by a Claim agent (printed as "Info"), 2 = blank.</summary>
    public static string Skill(int skillAgentCreated) => skillAgentCreated == 1 ? "Info" : string.Empty;

    public static string? Trim(string? value) => value?.Trim();

    /// <summary>Report 05 date, e.g. "24 ก.พ. 57" (Buddhist year, two digits).</summary>
    public static string ThaiShortDate(DateTime date) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{date.Day} {ThaiMonthsShort[date.Month - 1]} {(date.Year + 543) % 100:00}");

    /// <summary>Report 06 date, e.g. "10 - Feb - 2557" (Buddhist year).</summary>
    public static string LegacyDayMonthBe(DateOnly? date) =>
        date is { } d
            ? string.Create(CultureInfo.InvariantCulture, $"{d.Day} - {LegacyEnglishMonths[d.Month - 1]} - {d.Year + 543}")
            : string.Empty;
}
