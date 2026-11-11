namespace ContactTogetherApi.Dtos;

// Responses of the per-level address endpoints (GetProvince / GetDistrict / GetSubdistrict). The ids
// are nvarchar in the schema but always whole numbers, so they are numbers here, the same as in
// AddressDatasetResponse.

/// <summary><c>TblProvince.ProvinceId</c> / <c>ProvinceName</c>.</summary>
public class ProvinceResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary><c>TblDistrict.DistrictId</c> / <c>ProvinceId</c> / <c>DistrictName</c>.</summary>
public class DistrictResponse
{
    public int Id { get; set; }

    public int ProvinceId { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary><c>TblSubDistrict.SubDistrictId</c> / <c>DistrictId</c> / <c>SubDistrictName</c> / <c>ZipCode</c>.</summary>
public class SubdistrictResponse
{
    public int Id { get; set; }

    public int DistrictId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Zipcode { get; set; } = string.Empty;
}
