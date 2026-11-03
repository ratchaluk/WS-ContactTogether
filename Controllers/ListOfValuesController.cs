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
}
