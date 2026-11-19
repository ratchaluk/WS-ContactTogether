using ContactTogetherApi.Data;
using ContactTogetherApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Helper;

/// <summary>Account reads shared by <c>AccountController</c> and <c>ServicesController</c>.</summary>
public static class AccountQueries
{
    /// <summary>
    /// One account with its gender name and enabled contact details, in the shape
    /// <c>GET /Account/GetAccounts</c> returns, or <c>null</c> when <paramref name="accountId"/> is empty or
    /// matches no row. <c>IsEnable</c> is returned but not filtered on, so an old service still shows who called.
    /// </summary>
    public static async Task<AccountSearchResponse?> LoadAccountAsync(
        this ApplicationDbContext db,
        string? accountId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return null;
        }

        // TblAccount.Id has legacy duplicates with identical data, so take any one of them.
        var account = await (
            from a in db.TblAccounts.AsNoTracking()
            join gender in db.TblGenders.AsNoTracking() on a.GenderId equals gender.Id into genders
            from gender in genders.DefaultIfEmpty()
            where a.Id == accountId
            select new AccountSearchResponse
            {
                Id = a.Id,
                SalutationTh = a.SalutationTh,
                FirstnameTh = a.FirstnameTh,
                LastnameTh = a.LastnameTh,
                SalutationEn = a.SalutationEn,
                FirstnameEn = a.FirstnameEn,
                LastnameEn = a.LastnameEn,
                GenderId = a.GenderId,
                Gender = gender != null ? gender.NameTh : null,
                Birthdate = a.Birthdate,
                Address = a.Address,
                AreaId = a.AreaId,
                Zipcode = a.Zipcode,
                Remark = a.Remark,
                IsScret = a.IsScret,
                IsEnable = a.IsEnable,
                Created = a.Created,
            }).FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        account.FullName = string.Join(' ', new[] { account.SalutationTh, account.FirstnameTh, account.LastnameTh }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        account.Contacts = await db.TblAccountDetails.AsNoTracking()
            .Where(d => d.AccountId == accountId && d.IsEnable == "T")
            .OrderBy(d => d.DetailType)
            .ThenBy(d => d.Detail)
            .Select(d => new AccountContactResponse { Id = d.Id, DetailType = d.DetailType, Detail = d.Detail })
            .ToListAsync(cancellationToken);

        return account;
    }
}
