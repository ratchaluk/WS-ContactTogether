namespace ContactTogetherApi.Dtos;

/// <summary>หนึ่งรายการของ <c>GET /ListOfValues/GetServiceTitle</c> (<c>TblServiceTitles</c>)</summary>
public class ServiceTitleResponse
{
    /// <summary>
    /// <c>TblServiceTitles.Id</c> เป็นตัวพิมพ์ใหญ่ รูปแบบเดียวกับที่ <c>TblService.ServiceTitleId</c> เก็บ
    /// จึงเทียบกับ <c>serviceTitleId</c> ใน response ของ Services ได้ตรง ๆ
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string ServiceTitleText { get; set; } = string.Empty;
}
