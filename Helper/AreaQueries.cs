using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Helper;

/// <summary>Area (<c>TblArea</c>) reads shared by <c>AccountQueries</c> and <c>ServicesController</c>.</summary>
public static class AreaQueries
{
    /// <summary>Province, district, sub-district: the deepest a path can be.</summary>
    private const int MaxDepth = 3;

    /// <summary>
    /// Walks <c>RefId</c> up from <paramref name="areaId"/> and returns the chain top level first
    /// (province, then district, then sub-district). A province points <c>RefId</c> at itself. Empty when
    /// <paramref name="areaId"/> is empty or not in <c>TblArea</c> (some legacy accounts hold plain text).
    /// One query per level: the table has thousands of rows, so it is not loaded whole.
    /// </summary>
    public static async Task<List<AreaResponse>> LoadAreaPathAsync(
        this ApplicationDbContext db,
        string? areaId,
        CancellationToken cancellationToken)
    {
        var path = new List<AreaResponse>();
        var currentId = areaId?.Trim();
        while (!string.IsNullOrEmpty(currentId) && path.Count < MaxDepth)
        {
            var id = currentId;
            var area = await db.TblAreas.AsNoTracking()
                .Where(a => a.Id == id)
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
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                break;
            }

            path.Add(area);
            currentId = area.RefId == area.Id ? null : area.RefId;
        }

        path.Reverse();
        return path;
    }
}
