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
        // TblEmployee.Id and TblContact.ID are unique in the data (TblContact checked on 2026-11-12:
        // 1,216 rows, no duplicate or null ID). TblAccount.Id is not (legacy duplicates exist), so
        // only read TblAccount with AsNoTracking or projections - a tracking query over duplicate
        // ids would collapse them into one entity.
        modelBuilder.Entity<TblEmployee>().HasKey(e => e.Id);
        modelBuilder.Entity<TblAccount>().HasKey(e => e.Id);
        modelBuilder.Entity<TblContact>().HasKey(e => e.Id);

        // TblActivity lost its (ServiceId, Line) primary key on 2026-11-23 and gained a
        // uniqueidentifier Id defaulting to newsequentialid() (the table was empty, so no duplicates).
        // Leave Id unset when inserting: the scaffolded HasDefaultValueSql makes the database fill it.
        modelBuilder.Entity<TblActivity>().HasKey(e => e.Id);

        // TblService needs no entry here: its Id has been the database primary key (PK_TblService)
        // since 2026-11-26, so the scaffolded model declares it.

        // Every insert gets a UUID primary key: any entity whose key is a single string column is
        // assigned one by GuidStringValueGenerator when it is Add()ed with the key left null. A key
        // set explicitly is kept. Composite keys (TblReopenedLog), Guid keys (TblActivity) and
        // keyless tables are skipped.
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

        ConfigureFunctions(modelBuilder);
    }
}
