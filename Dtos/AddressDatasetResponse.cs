namespace ContactTogetherApi.Dtos;

/// <summary>
/// The whole province / district / subdistrict list for the three linked address drop-downs. The UI
/// loads it once and filters locally: a subdistrict's <see cref="SubdistrictItem.DistrictId"/> gives
/// its district, and that district's <see cref="DistrictItem.ProvinceId"/> gives its province.
/// </summary>
public class AddressDatasetResponse
{
    /// <summary>
    /// Changes only when the data changes (a hash of the content), so the UI can cache the dataset
    /// and reload it only when this differs from the cached copy.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    public List<ProvinceItem> Province { get; set; } = [];

    public List<DistrictItem> District { get; set; } = [];

    public List<SubdistrictItem> Subdistrict { get; set; } = [];

    /// <summary><c>TblProvince.ProvinceId</c> / <c>ProvinceName</c>.</summary>
    public class ProvinceItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary><c>TblDistrict.DistrictId</c> / <c>ProvinceId</c> / <c>DistrictName</c>.</summary>
    public class DistrictItem
    {
        public int Id { get; set; }

        public int ProvinceId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary><c>TblSubDistrict.SubDistrictId</c> / <c>DistrictId</c> / <c>SubDistrictName</c> / <c>ZipCode</c>.</summary>
    public class SubdistrictItem
    {
        public int Id { get; set; }

        public int DistrictId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Zipcode { get; set; } = string.Empty;
    }
}
