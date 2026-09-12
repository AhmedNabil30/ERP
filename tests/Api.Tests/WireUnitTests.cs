using System.Reflection;

namespace Kaff.Api.Tests;

/// <summary>
/// decisions.md D-151 — the fix for <c>V-38-H</c>: a rate's unit is carried by the type
/// <c>Percentage</c>, never by a bare <c>decimal</c>. This is the test that stops the next one.
/// </summary>
public sealed class WireUnitTests
{
    /// <summary>
    /// Money, not a rate, despite the name — decisions.md D-151 §4. <c>BaseSellRate</c> is a price
    /// (spec.md §4.2); <c>DayRate</c> (currently unreachable through any route, D-153 §1) is one too.
    /// </summary>
    private static readonly HashSet<string> MoneyNamedLikeARate = ["BaseSellRate", "DayRate"];

    [Fact]
    public void Every_rate_on_the_wire_is_typed_Percentage()
    {
        Type[] wireTypes = [.. typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null
                           && type.Namespace.StartsWith("Kaff.Api.Features.", StringComparison.Ordinal))
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                           || type.Name.EndsWith("Response", StringComparison.Ordinal))];

        wireTypes.Should().NotBeEmpty(
            "V-38-K — an enumeration over nothing would pass by having swept nothing");

        List<string> offenders = [];

        foreach (Type type in wireTypes)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool looksLikeARate = property.Name.EndsWith("Rate", StringComparison.Ordinal)
                    || property.Name.EndsWith("Markup", StringComparison.Ordinal)
                    || property.Name.EndsWith("Percentage", StringComparison.Ordinal);

                if (!looksLikeARate || MoneyNamedLikeARate.Contains(property.Name))
                {
                    continue;
                }

                Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if (propertyType == typeof(decimal))
                {
                    offenders.Add($"{type.FullName}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a rate crossing the wire as a bare decimal carries no unit — decisions.md D-151");
    }
}
