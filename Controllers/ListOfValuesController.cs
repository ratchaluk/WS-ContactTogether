using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Helper;
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
    /// คืนหัวข้อเรื่อง (<c>TblServiceTitles</c>) ที่เปิดใช้งาน สำหรับ drop-down หัวข้อเรื่อง
    /// เรียงตามลำดับที่เพิ่มเข้าระบบ
    /// </summary>
    [HttpGet("GetServiceTitle")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ServiceTitleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetServiceTitle(CancellationToken cancellationToken)
    {
        var titles = await _db.TblServiceTitles
            .AsNoTracking()
            .Where(t => t.IsEnable == "T")
            .OrderBy(t => t.Created)
            .Select(t => new { t.Id, t.ServiceTitleText })
            .ToListAsync(cancellationToken);

        // Upper case, as TblService.ServiceTitleId stores it; a Guid would serialize in lower case.
        return Ok(titles.Select(t => new ServiceTitleResponse
        {
            Id = t.Id.ToString("D").ToUpperInvariant(),
            ServiceTitleText = t.ServiceTitleText,
        }));
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

    /// <summary>
    /// คืนรายการประเภทการติดต่อของผู้ติดต่อ (<c>TblAccountDetail.DetailType</c>) สำหรับ drop-down เบอร์/อีเมล
    /// ไม่มีตารางใน DB เป็นรายการคงที่ 5 ค่า ส่ง <c>id</c> กลับไปเป็น <c>detailType</c> ของ
    /// <c>POST /Account/AddNewAccount</c>
    /// </summary>
    [HttpGet("GetAccountDetailType")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<LookupItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetAccountDetailType() => Ok(AccountDetailTypes.Items);

    /// <summary>
    /// คืนพื้นที่ (<c>TblArea</c>) ที่เปิดใช้งานของชั้นที่ระบุ เป็นรายการเรียบ แถว "ไม่ระบุ" (<c>IsDefault = T</c>) ขึ้นก่อน แล้วเรียงตามชื่อ:
    /// <c>PROVINCE</c> = จังหวัด, <c>DISTRICT</c> = อำเภอ, <c>SUBDISTRICT</c> = ตำบล
    /// ส่ง <paramref name="refId"/> เพื่อเอาเฉพาะลูกของพื้นที่นั้น (อำเภอของจังหวัด หรือตำบลของอำเภอ)
    /// ส่ง <c>id</c> ที่เลือกเป็น <c>areaId</c> ของ <c>AddNewAccount</c> / <c>UpdateAccountById</c> / <c>AddNewService</c>
    /// </summary>
    [HttpGet("GetArea")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<AreaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetArea(
        [FromQuery, Required] string? areaType,
        [FromQuery] string? refId,
        CancellationToken cancellationToken)
    {
        // The DB compares case-sensitively and stores types and ids in upper case, so accept any case here.
        var type = areaType!.Trim().ToUpperInvariant();
        if (!AreaTypes.All.Contains(type))
        {
            ModelState.AddModelError(nameof(areaType),
                $"Must be one of {string.Join(", ", AreaTypes.All)}.");
            return ValidationProblem(ModelState);
        }

        var parentId = string.IsNullOrWhiteSpace(refId) ? null : refId.Trim().ToUpperInvariant();

        var query = _db.TblAreas
            .AsNoTracking()
            .Where(a => a.AreaType == type && a.IsEnable == "T");
        if (parentId is not null)
        {
            query = query.Where(a => a.RefId == parentId);
        }

        // The "--ไม่ระบุ…--" placeholder of each level is the IsDefault row. Thai collation ignores the dashes,
        // so it would land in the middle of the list; put it first, as a drop-down's default option.
        var areas = await query
            .OrderBy(a => a.IsDefault == "T" ? 0 : 1)
            .ThenBy(a => a.NameTh)
            .Select(a => new AreaResponse
            {
                Id = a.Id,
                RefId = a.RefId,
                NameTh = a.NameTh,
                NameEn = a.NameEn,
                AreaType = a.AreaType,
                Zipcode = a.Zipcode,
                IsDefault = a.IsDefault,
            })
            .ToListAsync(cancellationToken);

        return Ok(areas);
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

    /// <summary>
    /// คืนรายการที่มาข้อมูล (<c>TblReference</c>) ที่เปิดใช้งาน สำหรับ drop-down "ที่มาข้อมูล" ตอนเปิดเรื่อง
    /// ส่ง <c>id</c> ที่เลือกกลับไปเป็น <c>serviceReference</c> ของ <c>POST /Services/AddNewService</c>
    /// </summary>
    [HttpGet("GetReference")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ReferenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetReference(CancellationToken cancellationToken)
    {
        var references = await _db.TblReferences
            .AsNoTracking()
            .Where(r => r.IsEnable == "T")
            .OrderBy(r => r.NameTh)
            .Select(r => new ReferenceResponse
            {
                Id = r.Id,
                NameTh = r.NameTh,
                NameEn = r.NameEn,
                IsDefault = r.IsDefault,
            })
            .ToListAsync(cancellationToken);

        return Ok(references);
    }

    /// <summary>
    /// คืนรายการระดับ (<c>TblLevel</c>) ที่เปิดใช้งานของประเภทที่ระบุ เรียงตาม <c>Multiplier</c>
    /// <c>SEVERITY</c> = ความเร่งด่วน, <c>PRIORITY</c> = ความสำคัญ, <c>SECRET</c> = ชั้นความลับ
    /// ส่ง <c>id</c> ที่เลือกกลับไปเป็น <c>levelSeverityId</c> / <c>levelPriorityId</c> / <c>levelSecretId</c>
    /// ของ <c>POST /Services/AddNewService</c> ตามประเภท
    /// </summary>
    [HttpGet("GetLevel")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<LevelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLevel([FromQuery, Required] string? levelType, CancellationToken cancellationToken)
    {
        // The DB compares case-sensitively and stores the types in upper case, so accept any case here.
        var type = levelType!.Trim().ToUpperInvariant();
        if (!LevelTypes.All.Contains(type))
        {
            ModelState.AddModelError(nameof(levelType),
                $"Must be one of {string.Join(", ", LevelTypes.All)}.");
            return ValidationProblem(ModelState);
        }

        var levels = await _db.TblLevels
            .AsNoTracking()
            .Where(l => l.LevelType == type && l.IsEnable == "T")
            .OrderBy(l => l.Multiplier)
            .ThenBy(l => l.NameTh)
            .Select(l => new LevelResponse
            {
                Id = l.Id,
                NameTh = l.NameTh,
                NameEn = l.NameEn,
                Multiplier = l.Multiplier,
                IsDefault = l.IsDefault,
            })
            .ToListAsync(cancellationToken);

        return Ok(levels);
    }

    /// <summary>
    /// คืนสถานะ (<c>TblStatus</c>) ที่เปิดใช้งานของประเภทที่ระบุ เรียงตามชื่อ ค่าเริ่มต้นคือ
    /// <c>StatusType = "SERVICE"</c> (สถานะของเรื่อง) ส่ง id ที่เลือกเป็น <c>statusId</c> ของ
    /// <c>POST /Services/AddNewService</c> ได้เลย ส่ง <c>statusType=ACTIVITY</c> สำหรับสถานะของงาน
    /// </summary>
    [HttpGet("GetStatus")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<StatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus([FromQuery] string? statusType, CancellationToken cancellationToken)
    {
        // SERVICE unless the caller asks for another type. The DB compares case-sensitively and stores
        // the types in upper case, so accept any case here.
        var type = string.IsNullOrWhiteSpace(statusType)
            ? StatusTypes.Service
            : statusType.Trim().ToUpperInvariant();
        if (!StatusTypes.All.Contains(type))
        {
            ModelState.AddModelError(nameof(statusType),
                $"Must be one of {string.Join(", ", StatusTypes.All)}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await LoadStatusesAsync(type, cancellationToken));
    }

    /// <summary>
    /// คืนสถานะของงาน (<c>TblStatus</c> ที่ <c>StatusType = "ACTIVITY"</c>) ที่เปิดใช้งาน เรียงตามชื่อ
    /// ได้ผลเหมือน <c>GetStatus?statusType=ACTIVITY</c>
    /// </summary>
    [HttpGet("GetStatusByStatusTypeActivity")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<StatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatusByStatusTypeActivity(CancellationToken cancellationToken) =>
        Ok(await LoadStatusesAsync(StatusTypes.Activity, cancellationToken));

    private Task<List<StatusResponse>> LoadStatusesAsync(string type, CancellationToken cancellationToken) =>
        _db.TblStatuses
            .AsNoTracking()
            .Where(s => s.StatusType == type && s.IsEnable == "T")
            .OrderBy(s => s.NameTh)
            .Select(s => new StatusResponse
            {
                Id = s.Id,
                RefId = s.RefId,
                NameTh = s.NameTh,
                NameEn = s.NameEn,
                IsType = s.IsType,
                IsDefault = s.IsDefault,
            })
            .ToListAsync(cancellationToken);

    /// <summary>
    /// คืนหน่วยงาน (<c>TblOrganization</c>) ที่เปิดใช้งานทั้งหมด เรียงตามชื่อ เป็นต้นไม้ 2 ชั้นผ่าน <c>refId</c>:
    /// หน่วยงานหลัก (กระทรวง) มี <c>refId</c> เท่ากับ <c>id</c> ของตัวเอง ส่วนหน่วยงานย่อย (กรม) ชี้ไปที่กระทรวง
    /// ส่ง <c>id</c> ของหน่วยงานย่อยเป็น <c>organizationId</c> ของ <c>POST /Services/AddNewService</c>
    /// </summary>
    [HttpGet("GetOrganization")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<OrganizationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrganization(CancellationToken cancellationToken)
    {
        var organizations = await _db.TblOrganizations
            .AsNoTracking()
            .Where(o => o.IsEnable == "T")
            .OrderBy(o => o.NameTh)
            .Select(o => new OrganizationResponse
            {
                Id = o.Id,
                RefId = o.RefId,
                NameTh = o.NameTh,
                NameEn = o.NameEn,
            })
            .ToListAsync(cancellationToken);

        return Ok(organizations);
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
    /// คืนรายการพนักงาน (<c>TblEmployee</c>) ที่เปิดใช้งาน เป็น id และชื่อเต็มภาษาไทย (คำนำหน้า ชื่อ นามสกุล)
    /// สำหรับ drop-down เจ้าของเรื่อง ส่ง <c>id</c> เป็น <c>serviceOwner</c> ของ <c>GET /Services/SearchService</c>
    /// ไม่ส่งรหัสผ่านและข้อมูลส่วนตัวอื่นของพนักงาน
    /// </summary>
    [HttpGet("GetEmployee")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<LookupItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEmployee(CancellationToken cancellationToken)
    {
        var rows = await _db.TblEmployees
            .AsNoTracking()
            .Where(e => e.IsEnable == "T")
            .OrderBy(e => e.FirstnameTh)
            .ThenBy(e => e.LastnameTh)
            .Select(e => new { e.Id, e.SalutationTh, e.FirstnameTh, e.LastnameTh })
            .ToListAsync(cancellationToken);

        var employees = rows.Select(e => new LookupItemResponse
        {
            Id = e.Id ?? string.Empty,
            NameTh = FullNameTh(e.SalutationTh, e.FirstnameTh, e.LastnameTh),
        });

        return Ok(employees);
    }

    /// <summary>คำนำหน้า ชื่อ นามสกุล ภาษาไทย ต่อกันด้วยช่องว่าง ข้ามส่วนที่ไม่มีค่า</summary>
    private static string FullNameTh(string? salutation, string? firstname, string? lastname) =>
        string.Join(' ', new[] { salutation, firstname, lastname }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>
    /// คืนกลุ่มพนักงาน (ตาราง <c>TblUserGroup</c>) ที่เปิดใช้งานทั้งหมด เรียงตามชื่อ สำหรับ drop-down กลุ่มในฟอร์มเพิ่มงาน
    /// เป็น 2 ชั้นผ่าน <c>refId</c> เหมือน <c>GetOrganization</c>: กลุ่มหลักมี <c>refId</c> เท่ากับ <c>id</c> ของตัวเอง
    /// ส่วนกลุ่มย่อยชี้ไปที่กลุ่มหลัก กลุ่มย่อยที่กลุ่มหลักถูกปิดจะไม่ถูกส่งมา
    /// ส่ง <c>id</c> ที่เลือกเป็น <c>groupId</c> ของ <c>POST /Services/AddNewServiceOperations</c>
    /// และของ <c>GET /ListOfValues/GetEmployeeByGroup</c>
    /// </summary>
    [HttpGet("GetEmployeeGroup")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<EmployeeGroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEmployeeGroup(CancellationToken cancellationToken)
    {
        // A main group points RefGroupId at itself, so joining each group to the row its RefGroupId names
        // and requiring that row to be an enabled main group keeps the main groups (joined to themselves)
        // and drops sub-groups under a disabled one, which the legacy picker never showed.
        // Enable is char(1) 'T'/'F'.
        var groups = await (
            from g in _db.TblUserGroups.AsNoTracking()
            join parent in _db.TblUserGroups.AsNoTracking() on g.RefGroupId equals parent.GroupId
            where g.Enable == "T" && parent.Enable == "T" && parent.RefGroupId == parent.GroupId
            orderby g.GroupName
            select new { g.GroupId, g.RefGroupId, g.GroupName })
            .ToListAsync(cancellationToken);

        // Strings, because TblActivity.GroupId and TblEmployeeGroup.GroupId hold the id as text.
        return Ok(groups.Select(g => new EmployeeGroupResponse
        {
            Id = g.GroupId.ToString(CultureInfo.InvariantCulture),
            RefId = g.RefGroupId!.Value.ToString(CultureInfo.InvariantCulture),
            Name = g.GroupName ?? string.Empty,
        }));
    }

    /// <summary>
    /// คืนพนักงานที่เปิดใช้งานในกลุ่มที่ระบุ (<c>TblEmployeeGroup</c>) สำหรับ drop-down ผู้รับในฟอร์มเพิ่มงาน
    /// เป็น id และชื่อเต็มภาษาไทยแบบเดียวกับ <c>GetEmployee</c> เรียงตามชื่อ แล้วนามสกุล
    /// ส่ง <c>id</c> ที่เลือกเป็น <c>ownerId</c> ของ <c>POST /Services/AddNewServiceOperations</c>
    /// ได้เฉพาะสมาชิกของกลุ่มนั้นโดยตรง ไม่รวมกลุ่มย่อย กลุ่มที่ไม่มีอยู่หรือถูกปิดได้ <c>[]</c>
    /// </summary>
    [HttpGet("GetEmployeeByGroup")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<LookupItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEmployeeByGroup([FromQuery, Required] int? groupId, CancellationToken cancellationToken)
    {
        var id = groupId!.Value;
        var groupKey = id.ToString(CultureInfo.InvariantCulture);

        var groupEnabled = await _db.TblUserGroups.AsNoTracking()
            .AnyAsync(g => g.GroupId == id && g.Enable == "T", cancellationToken);
        if (!groupEnabled)
        {
            return Ok(Array.Empty<LookupItemResponse>());
        }

        // TblEmployeeGroup has no key, so a pair could appear twice; Distinct keeps one row per employee.
        var rows = await (
            from eg in _db.TblEmployeeGroups.AsNoTracking()
            join e in _db.TblEmployees.AsNoTracking() on eg.EmployeeId equals e.Id
            where eg.GroupId == groupKey && e.IsEnable == "T"
            select new { e.Id, e.SalutationTh, e.FirstnameTh, e.LastnameTh })
            .Distinct()
            .OrderBy(e => e.FirstnameTh)
            .ThenBy(e => e.LastnameTh)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(e => new LookupItemResponse
        {
            Id = e.Id ?? string.Empty,
            NameTh = FullNameTh(e.SalutationTh, e.FirstnameTh, e.LastnameTh),
        }));
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
