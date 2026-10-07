using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace ContactTogetherApi.Data;

/// <summary>
/// Generates string primary keys as UUIDs in the schema's existing format: 32 upper-case hex
/// characters, no dashes (<c>Guid.ToString("N")</c>).
/// </summary>
public sealed class GuidStringValueGenerator : ValueGenerator<string>
{
    public static string NewId() => Guid.NewGuid().ToString("N").ToUpperInvariant();

    // The value is final, not a placeholder replaced by the database on SaveChanges.
    public override bool GeneratesTemporaryValues => false;

    public override string Next(EntityEntry entry) => NewId();
}
