using ContactTogetherApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ContactTogetherApi.Data;

/// <summary>
/// Hand-written model configuration, kept out of the scaffolded file so a re-scaffold does not lose it.
/// </summary>
public partial class ApplicationDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // These tables have no primary key in the database, so the scaffolder maps them as keyless
        // and EF refuses to insert into them. Declare the key EF needs to track new rows.
        //
        // TblEmployee.Id is unique in the data. TblAccount.Id is not (legacy duplicates exist), so
        // only read TblAccount with AsNoTracking or projections - a tracking query over duplicate
        // ids would collapse them into one entity.
        modelBuilder.Entity<TblEmployee>().HasKey(e => e.Id);
        modelBuilder.Entity<TblAccount>().HasKey(e => e.Id);
    }
}
