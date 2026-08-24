using System.Text.Json;
using Kaff.Domain.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kaff.Infrastructure.Persistence.Converters;

/// <summary>
/// <see cref="Money"/> to <c>decimal</c>.
/// </summary>
/// <remarks>
/// Precision is applied globally in <c>KaffDbContext.ConfigureConventions</c>, not per property.
/// CLAUDE.md requires decimal(18,4) on every money column "no exceptions", and a convention is the
/// only way to guarantee that a property added in a later session inherits it — a per-property
/// <c>HasPrecision</c> call is one a future session can forget, and EF Core truncates silently when
/// it is missing.
/// </remarks>
public sealed class MoneyConverter : ValueConverter<Money, decimal>
{
    public MoneyConverter()
        : base(money => money.Amount, amount => new Money(amount))
    {
    }
}

/// <summary><see cref="Percentage"/> to <c>decimal</c>, stored as a fraction at decimal(18,6).</summary>
public sealed class PercentageConverter : ValueConverter<Percentage, decimal>
{
    public PercentageConverter()
        : base(percentage => percentage.Fraction, fraction => Percentage.FromFraction(fraction))
    {
    }
}

/// <summary>
/// A read-only string list to a JSON document.
/// </summary>
/// <remarks>
/// Used for the audit record's changed-property list. Stored as <c>jsonb</c> so the trail stays
/// queryable, without pulling in provider-specific array mapping.
/// </remarks>
public sealed class StringListConverter : ValueConverter<IReadOnlyList<string>, string>
{
    public StringListConverter()
        : base(
            list => JsonSerializer.Serialize(list, JsonOptions),
            json => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>())
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
}

/// <summary>Change-tracking comparer for the string list, so EF detects edits to the collection.</summary>
public sealed class StringListComparer : ValueComparer<IReadOnlyList<string>>
{
    public StringListComparer()
        : base(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
            list => list.ToList())
    {
    }
}
