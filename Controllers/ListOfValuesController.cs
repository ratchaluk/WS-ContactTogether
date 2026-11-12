using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace ContactTogetherApi.Controllers;

/// <summary>รายการข้อมูลอ้างอิง (อ่านอย่างเดียว) สำหรับ drop-down ในหน้าจอ</summary>
[ApiController]
[Route("[controller]")]
public class ListOfValuesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ListOfValuesController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>คืนรายการช่องทาง (<c>TblChannel</c>) ที่เปิดใช้งาน เป็น id และชื่อภาษาไทย</summary>
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

    /// <summary>คืนรายการประเภทบริการ (<c>TblCategory</c>) ที่เปิดใช้งาน</summary>
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
    /// คืนจังหวัด อำเภอ และตำบลที่เปิดใช้งานทั้งหมดในครั้งเดียว สำหรับ drop-down ที่อยู่ 3 ตัวที่ผูกกัน
    /// แต่ละรายการมี id ของระดับแม่ติดมาด้วย เลือกตำบลแล้วเติมอำเภอและจังหวัดได้ เลือกอำเภอแล้วเติมจังหวัดได้
    /// รายการที่ระดับแม่ถูกปิดใช้งานจะไม่ถูกส่งมา
    /// </summary>
    /// <remarks>
    /// <para>
    /// แคชฝั่ง server: เก็บ JSON ที่บีบอัดไว้แล้ว (Brotli และ gzip) ใน <see cref="IMemoryCache"/> นาน
    /// <see cref="AddressDatasetCacheDuration"/> จึง query DB เพียงครั้งเดียวต่อช่วงเวลานั้น
    /// และไม่ต้องบีบอัดใหม่ทุก request
    /// </para>
    /// <para>
    /// แคชฝั่ง browser: ส่ง <c>Cache-Control: private</c> พร้อม ETag (ค่า <c>version</c>) ถ้า client ส่ง
    /// <c>If-None-Match</c> ที่ตรงกันมา จะได้ 304 แบบไม่มี body
    /// </para>
    /// </remarks>
    [HttpGet("GetAddressDataset")]
    [Authorize]
    [ProducesResponseType(typeof(AddressDatasetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAddressDataset(CancellationToken cancellationToken)
    {
        // The cached entry does not depend on this request, so a cancelled request must not cancel
        // building it for everyone else.
        var dataset = await _cache.GetOrCreateAsync(AddressDatasetCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AddressDatasetCacheDuration;
            return BuildCachedAddressDataset(await LoadAddressDatasetAsync(CancellationToken.None));
        });

        // private: the endpoint needs a token, so shared caches (proxies) must not keep the response.
        Response.Headers.CacheControl = $"private, max-age={(int)AddressDatasetClientMaxAge.TotalSeconds}";
        Response.Headers.Vary = HeaderNames.AcceptEncoding;

        var (body, encoding) = PickEncoding(dataset!, Request.Headers.AcceptEncoding);
        if (encoding is not null)
        {
            Response.Headers.ContentEncoding = encoding;
        }

        // Weak ETag: the br, gzip and plain bodies differ in bytes but carry the same data. File()
        // answers 304 itself when If-None-Match matches.
        return File(body, "application/json; charset=utf-8", lastModified: null,
            entityTag: new EntityTagHeaderValue($"\"{dataset!.Version}\"", isWeak: true));
    }

    private const string AddressDatasetCacheKey = "ListOfValues:AddressDataset";

    /// <summary>ระยะเวลาที่ server เก็บ dataset ไว้ใน memory ก่อน query DB ใหม่</summary>
    private static readonly TimeSpan AddressDatasetCacheDuration = TimeSpan.FromHours(1);

    /// <summary>ระยะเวลาที่ browser ใช้ของเดิมได้โดยไม่ต้องถาม server หลังจากนั้นจะถามด้วย ETag</summary>
    private static readonly TimeSpan AddressDatasetClientMaxAge = TimeSpan.FromMinutes(10);

    private sealed record CachedAddressDataset(string Version, byte[] Json, byte[] Brotli, byte[] Gzip);

    private static CachedAddressDataset BuildCachedAddressDataset(AddressDatasetResponse response)
    {
        // Same camelCase naming as the MVC JSON output.
        var json = JsonSerializer.SerializeToUtf8Bytes(response, JsonSerializerOptions.Web);
        return new CachedAddressDataset(response.Version, json, Compress(json, brotli: true), Compress(json, brotli: false));
    }

    private static byte[] Compress(byte[] data, bool brotli)
    {
        using var output = new MemoryStream();
        using (Stream stream = brotli
                   ? new BrotliStream(output, CompressionLevel.SmallestSize)
                   : new GZipStream(output, CompressionLevel.SmallestSize))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>เลือก Brotli ถ้า client รับได้ ไม่งั้น gzip ไม่งั้นส่งแบบไม่บีบอัด (ข้ามตัวที่ระบุ <c>q=0</c>)</summary>
    private static (byte[] Body, string? Encoding) PickEncoding(CachedAddressDataset dataset, StringValues acceptEncoding)
    {
        var accepted = StringWithQualityHeaderValue.TryParseList(acceptEncoding, out var values)
            ? values.Where(v => v.Quality is not 0).Select(v => v.Value.Value).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        if (accepted.Contains("br"))
        {
            return (dataset.Brotli, "br");
        }

        return accepted.Contains("gzip") ? (dataset.Gzip, "gzip") : (dataset.Json, null);
    }

    private async Task<AddressDatasetResponse> LoadAddressDatasetAsync(CancellationToken cancellationToken)
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

        return response;
    }

    private static int ParseId(string id) => int.Parse(id.Trim(), CultureInfo.InvariantCulture);

    /// <summary>เลขฐานสิบหก 16 หลักแรกของ SHA-256 ที่คำนวณจากทั้งสามรายการ จึงเปลี่ยนทุกครั้งที่ข้อมูลเปลี่ยน</summary>
    private static string ComputeVersion(AddressDatasetResponse response)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new { response.Province, response.District, response.Subdistrict });
        return Convert.ToHexStringLower(SHA256.HashData(json))[..16];
    }

    // Per-level address endpoints: the UI loads one level at a time as the user picks, instead of the
    // whole GetAddressDataset up front. Same filtering as GetAddressDataset: only enabled rows, and a
    // child whose parent is disabled is left out. An unknown or disabled parent id gives an empty list.

    /// <summary>คืนจังหวัดที่เปิดใช้งานทั้งหมด สำหรับ drop-down ที่อยู่ตัวแรก</summary>
    [HttpGet("GetProvince")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ProvinceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProvince(CancellationToken cancellationToken)
    {
        var provinces = await _db.TblProvinces.AsNoTracking()
            .Where(p => p.IsEnable == "T")
            .OrderBy(p => p.ProvinceName)
            .Select(p => new { p.ProvinceId, p.ProvinceName })
            .ToListAsync(cancellationToken);

        return Ok(provinces.Select(p => new ProvinceResponse
        {
            Id = ParseId(p.ProvinceId),
            Name = p.ProvinceName,
        }));
    }

    /// <summary>คืนอำเภอที่เปิดใช้งานของจังหวัดที่ระบุ (<c>TblProvince.ProvinceId</c>)</summary>
    [HttpGet("GetDistrict")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<DistrictResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDistrict([FromQuery, Required] int? provinceId, CancellationToken cancellationToken)
    {
        var id = provinceId!.Value.ToString(CultureInfo.InvariantCulture);

        var districts = await (
            from d in _db.TblDistricts.AsNoTracking()
            join p in _db.TblProvinces.AsNoTracking() on d.ProvinceId equals p.ProvinceId
            where d.ProvinceId == id && d.IsEnable == "T" && p.IsEnable == "T"
            orderby d.DistrictName
            select new { d.DistrictId, d.ProvinceId, d.DistrictName })
            .ToListAsync(cancellationToken);

        return Ok(districts.Select(d => new DistrictResponse
        {
            Id = ParseId(d.DistrictId),
            ProvinceId = ParseId(d.ProvinceId),
            Name = d.DistrictName,
        }));
    }

    /// <summary>
    /// คืนตำบลที่เปิดใช้งานของอำเภอที่ระบุ (<c>TblDistrict.DistrictId</c>) พร้อมรหัสไปรษณีย์ของแต่ละตำบล
    /// รหัสไปรษณีย์ผูกกับตำบล ไม่ใช่อำเภอ เพราะประมาณ 1 ใน 5 ของอำเภอมีหลายรหัส
    /// หน้าจอจึงเติมรหัสไปรษณีย์ตอนเลือกตำบล
    /// </summary>
    [HttpGet("GetSubdistrict")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<SubdistrictResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSubdistrict([FromQuery, Required] int? districtId, CancellationToken cancellationToken)
    {
        var id = districtId!.Value.ToString(CultureInfo.InvariantCulture);

        var subdistricts = await (
            from s in _db.TblSubDistricts.AsNoTracking()
            join d in _db.TblDistricts.AsNoTracking() on s.DistrictId equals d.DistrictId
            join p in _db.TblProvinces.AsNoTracking() on d.ProvinceId equals p.ProvinceId
            where s.DistrictId == id && s.IsEnable == "T" && d.IsEnable == "T" && p.IsEnable == "T"
            orderby s.SubDistrictName
            select new { s.SubDistrictId, s.DistrictId, s.SubDistrictName, s.ZipCode })
            .ToListAsync(cancellationToken);

        return Ok(subdistricts.Select(s => new SubdistrictResponse
        {
            Id = ParseId(s.SubDistrictId),
            DistrictId = ParseId(s.DistrictId),
            Name = s.SubDistrictName,
            Zipcode = s.ZipCode,
        }));
    }

    /// <summary>คืนรายการเพศ (<c>TblGender</c>) ที่เปิดใช้งาน สำหรับ drop-down เพศ</summary>
    [HttpGet("GetGender")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<GenderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetGender(CancellationToken cancellationToken)
    {
        var genders = await _db.TblGenders
            .AsNoTracking()
            .Where(g => g.IsEnable == "T")
            .OrderBy(g => g.NameTh)
            .Select(g => new GenderResponse
            {
                Id = g.Id,
                NameTh = g.NameTh,
                NameEn = g.NameEn,
                IsDefault = g.IsDefault,
            })
            .ToListAsync(cancellationToken);

        return Ok(genders);
    }

    /// <summary>คืนรายการบทบาท (<c>TblRole</c>) ที่เปิดใช้งาน สำหรับ drop-down บทบาท</summary>
    [HttpGet("GetRole")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<RoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRole(CancellationToken cancellationToken)
    {
        var roles = await _db.TblRoles
            .AsNoTracking()
            .Where(r => r.IsEnable == "T")
            .OrderBy(r => r.NameTh)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                NameTh = r.NameTh,
                NameEn = r.NameEn,
                LevelSecretId = r.LevelSecretId,
                IsDefault = r.IsDefault,
                IsAdmin = r.IsAdmin,
            })
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    /// <summary>
    /// คืนประเภทการรับสาย (<c>TblCategory</c> ที่ <c>CategoryType = "CONTACT"</c>) เป็นต้นไม้
    /// ระดับแรกคือกลุ่ม Service / Non Service ระดับที่สองคือ radio ระดับที่สามคือ drop-down ข้าง radio
    /// ค่าที่บันทึกลง <c>TblContact.CategoryId</c> คือ id ของรายการสุดท้ายที่เลือก (ตัว radio เองถ้าไม่มี drop-down)
    /// </summary>
    [HttpGet("GetContactType")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ContactTypeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetContactType(CancellationToken cancellationToken)
    {
        // About 20 rows, so load them all at once and build the tree in memory. They are sorted by
        // NameTh in SQL (the DB's Thai collation), and ToLookup keeps that order within each parent.
        var rows = await _db.TblCategories
            .AsNoTracking()
            .Where(c => c.CategoryType == "CONTACT" && c.IsEnable == "T")
            .OrderBy(c => c.NameTh)
            .Select(c => new CategoryRow(c.Id, c.RefId, c.NameTh, c.NameEn))
            .ToListAsync(cancellationToken);

        // Roots point RefId at themselves. They are sorted by Id instead, so Service (…A100001) comes
        // before Non Service (…A100002), as on the screen. A row whose parent is disabled or missing is
        // never reached from a root, so it is left out.
        var roots = rows.Where(r => r.RefId == r.Id).OrderBy(r => r.Id, StringComparer.Ordinal);
        var childrenOf = rows.Where(r => r.RefId != r.Id).ToLookup(r => r.RefId);
        var visited = new HashSet<string>();

        List<ContactTypeResponse> BuildNodes(IEnumerable<CategoryRow> source)
        {
            var nodes = new List<ContactTypeResponse>();
            foreach (var row in source)
            {
                // Guards against a RefId cycle in the data.
                if (!visited.Add(row.Id))
                {
                    continue;
                }

                nodes.Add(new ContactTypeResponse
                {
                    Id = row.Id,
                    NameTh = row.NameTh,
                    NameEn = row.NameEn,
                    Children = BuildNodes(childrenOf[row.Id]),
                });
            }

            return nodes;
        }

        return Ok(BuildNodes(roots));
    }

    private sealed record CategoryRow(string Id, string RefId, string NameTh, string? NameEn);
}
