using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace ContactTogetherApi.Data;

/// <summary>
/// SQL functions callable from LINQ queries. Hand-written, kept out of the scaffolded file.
/// </summary>
public partial class ApplicationDbContext
{
    /// <summary>
    /// Parses one of the legacy <c>varchar</c> date columns (month first, e.g. <c>11/4/2026 9:05</c>)
    /// into a <see cref="DateTime"/> inside SQL, as <c>TRY_CONVERT(datetime, value, 101)</c>. A value
    /// that does not parse becomes <c>NULL</c> instead of failing the query. Only usable in LINQ.
    /// </summary>
    public static DateTime? ParseLegacyDate(string? value) =>
        throw new NotSupportedException($"{nameof(ParseLegacyDate)} can only be used in LINQ to Entities queries.");

    private static void ConfigureFunctions(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(ApplicationDbContext).GetMethod(nameof(ParseLegacyDate))!)
            .HasTranslation(args => new SqlFunctionExpression(
                "TRY_CONVERT",
                [new SqlFragmentExpression("datetime"), args[0], new SqlFragmentExpression("101")],
                nullable: true,
                argumentsPropagateNullability: [false, true, false],
                // EF wants the non-nullable type here even though the expression can be NULL.
                typeof(DateTime),
                typeMapping: null));
    }
}
