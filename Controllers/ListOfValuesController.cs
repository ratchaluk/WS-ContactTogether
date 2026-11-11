using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Controllers;

/// <summary>Read-only lookup lists that feed the drop-downs in the UI.</summary>
[ApiController]
[Route("[controller]")]
public class ListOfValuesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ListOfValuesController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the enabled rows of <c>TblChannel</c> as id + Thai name.</summary>
    [HttpGet("GetChannel")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<LookupItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChannel(CancellationToken cancellationToken)
    {
        // IsEnable is nvarchar(10), and the DB collation is Thai_100_CS_AI, so this matches "T" only.
        var channels = await _db.TblChannels
            .AsNoTracking()
            .Where(c => c.IsEnable == "T")
            .OrderBy(c => c.NameTh)
            .Select(c => new LookupItemResponse
            {
                Id = c.Id,
                NameTh = c.NameTh,
            })
            .ToListAsync(cancellationToken);

        return Ok(channels);
    }

    /// <summary>Returns the enabled rows of <c>TblCategory</c> as service types.</summary>
    [HttpGet("GetServiceType")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ServiceTypeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetServiceType(CancellationToken cancellationToken)
    {
        var serviceTypes = await _db.TblCategories
            .AsNoTracking()
            .Where(c => c.IsEnable == "T")
            .OrderBy(c => c.NameTh)
            .Select(c => new ServiceTypeResponse
            {
                Id = c.Id,
                RefId = c.RefId,
                NameTh = c.NameTh,
                NameEn = c.NameEn,
                CategoryType = c.CategoryType,
            })
            .ToListAsync(cancellationToken);

        return Ok(serviceTypes);
    }

    /// <summary>
    /// Returns every enabled province, district and subdistrict in one response, for the three linked
    /// address drop-downs. Each child carries its parent's id, so picking a subdistrict fills in its
    /// district and province, and picking a district fills in its province. A child whose parent is
    /// disabled is left out.
    /// </summary>
    [HttpGet("GetAddressDataset")]
    [Authorize]
    [ProducesResponseType(typeof(AddressDatasetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAddressDataset(CancellationToken cancellationToken)
    {
        var provinces = await _db.TblProvinces.AsNoTracking()
            .Where(p => p.IsEnable == "T")
            .OrderBy(p => p.ProvinceName)
            .Select(p => new { p.ProvinceId, p.ProvinceName })
            .ToListAsync(cancellationToken);

        var districts = await (
            from d in _db.TblDistricts.AsNoTracking()
            join p in _db.TblProvinces.AsNoTracking() on d.ProvinceId equals p.ProvinceId
            where d.IsEnable == "T" && p.IsEnable == "T"
            orderby d.DistrictName
            select new { d.DistrictId, d.ProvinceId, d.DistrictName })
            .ToListAsync(cancellationToken);

        var subdistricts = await (
            from s in _db.TblSubDistricts.AsNoTracking()
            join d in _db.TblDistricts.AsNoTracking() on s.DistrictId equals d.DistrictId
            join p in _db.TblProvinces.AsNoTracking() on d.ProvinceId equals p.ProvinceId
            where s.IsEnable == "T" && d.IsEnable == "T" && p.IsEnable == "T"
            orderby s.SubDistrictName
            select new { s.SubDistrictId, s.DistrictId, s.SubDistrictName, s.ZipCode })
            .ToListAsync(cancellationToken);

        // The ids are nvarchar in the schema but always whole numbers, so they are returned as numbers.
        var response = new AddressDatasetResponse
        {
            Province = provinces
                .Select(p => new AddressDatasetResponse.ProvinceItem
                {
                    Id = ParseId(p.ProvinceId),
                    Name = p.ProvinceName,
                })
                .ToList(),
            District = districts
                .Select(d => new AddressDatasetResponse.DistrictItem
                {
                    Id = ParseId(d.DistrictId),
                    ProvinceId = ParseId(d.ProvinceId),
                    Name = d.DistrictName,
                })
                .ToList(),
            Subdistrict = subdistricts
                .Select(s => new AddressDatasetResponse.SubdistrictItem
                {
                    Id = ParseId(s.SubDistrictId),
                    DistrictId = ParseId(s.DistrictId),
                    Name = s.SubDistrictName,
                    Zipcode = s.ZipCode,
                })
                .ToList(),
        };
        response.Version = ComputeVersion(response);

        return Ok(response);
    }

    private static int ParseId(string id) => int.Parse(id.Trim(), CultureInfo.InvariantCulture);

    /// <summary>The first 16 hex digits of a SHA-256 over the lists, so it changes whenever the data does.</summary>
    private static string ComputeVersion(AddressDatasetResponse response)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new { response.Province, response.District, response.Subdistrict });
        return Convert.ToHexStringLower(SHA256.HashData(json))[..16];
    }
}
