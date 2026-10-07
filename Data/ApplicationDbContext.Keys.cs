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

        // Every insert gets a UUID primary key: any entity whose key is a single string column is
        // assigned one by GuidStringValueGenerator when it is Add()ed with the key left null. A key
        // set explicitly is kept. Composite keys (TblActivity, TblReopenedLog) and keyless tables
        // are skipped - their keys are not ids.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(string))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(key.Properties[0].Name)
                    .HasValueGenerator<GuidStringValueGenerator>();
            }
        }
    }
}
