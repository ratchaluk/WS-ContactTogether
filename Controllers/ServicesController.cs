using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using ContactTogetherApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ContactTogetherApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ServicesController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the enabled, not-yet-closed <c>TblService</c> rows owned by the calling employee. The
    /// employee is identified by the token's <c>sub</c> claim.
    /// </summary>
    [HttpGet("GetInProcessServices")]
    [ProducesResponseType(typeof(IEnumerable<ServiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInProcessServices(CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot identify the employee."));
        }

        var services = await ToResponses(_db.TblServices.AsNoTracking()
                .Where(service => service.OwnerId == employeeId
                    && (service.DateClosed == null || service.DateClosed == "")
                    && service.IsEnable == "T"))
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    /// <summary>
    /// Returns the enabled <c>TblService</c> rows that have an open, enabled <c>TblActivity</c>
    /// assigned to the calling employee, whoever owns the service itself. The employee is identified
    /// by the token's <c>sub</c> claim.
    /// </summary>
    [HttpGet("GetPendingServices")]
    [ProducesResponseType(typeof(IEnumerable<ServiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPendingServices(CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeId))
        {
            return Unauthorized(new MessageResponse(
                StatusCodes.Status401Unauthorized,
                "Token has no sub claim; cannot identify the employee."));
        }

        // A service can carry several such activities; filtering with Any() returns it once.
        var services = await ToResponses(_db.TblServices.AsNoTracking()
                .Where(service => service.IsEnable == "T"
                    && _db.TblActivities.Any(activity => activity.ServiceId == service.Id
                        && activity.OwnerId == employeeId
                        && activity.DateClosed == null
                        && activity.IsEnable == "T")))
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    /// <summary>
    /// Adds each service's status name and orders the rows by <c>Code</c>. TblService is keyless, so
    /// callers pass an untracked query. Status_ID has no FK constraint, hence the manual left join.
    /// </summary>
    private IQueryable<ServiceResponse> ToResponses(IQueryable<TblService> services) =>
            from service in services
            join status in _db.TblStatuses.AsNoTracking() on service.StatusId equals status.Id into statuses
            from status in statuses.DefaultIfEmpty()
            orderby service.Code
            select new ServiceResponse
            {
                Id = service.Id,
                Code = service.Code,
                CategoryId = service.CategoryId,
                StatusId = service.StatusId,
                StatusNameTh = status != null ? status.NameTh : null,
                StatusNameEn = status != null ? status.NameEn : null,
                OwnerId = service.OwnerId,
                DateOpened = service.DateOpened,
                DateClosed = service.DateClosed,
                ChannelIncomingId = service.ChannelIncomingId,
                CallBack = service.CallBack,
                ChannelOutgoingId = service.ChannelOutgoingId,
                AccountId = service.AccountId,
                Summary = service.Summary,
                Detail = service.Detail,
                ServiceArea = service.ServiceArea,
                OrganizationId = service.OrganizationId,
                OnScene = service.OnScene,
                ServiceReference = service.ServiceReference,
                ServiceReferenceLink = service.ServiceReferenceLink,
                AreaId = service.AreaId,
                Remark = service.Remark,
                LevelSeverityId = service.LevelSeverityId,
                LevelPriorityId = service.LevelPriorityId,
                LevelSecretId = service.LevelSecretId,
                IsEnable = service.IsEnable,
                Created = service.Created,
                CreatedBy = service.CreatedBy,
                Updated = service.Updated,
                UpdatedBy = service.UpdatedBy,
            };
}
