using System.Reflection;
using System.Text.RegularExpressions;
using Kaff.Domain.Common;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// KAFF-600 AC-600-J on the engineer's own read. <c>GET .../daily-logs/mine</c> returns <c>expenseDrafts</c>; the
/// engineer typed an amount on the draft (see <c>RulingSet.SiteExpenseDraftAmount</c>) and it must never come back.
/// </summary>
public sealed class GetMyLogMoneyFreeTests
{
    [Fact]
    public void The_engineers_own_log_response_has_no_money_member_at_any_depth()
    {
        var offenders = new List<string>();
        Walk(typeof(Kaff.Api.Features.DailyLogs.GetMyLog.Response), offenders, []);

        offenders.Should().BeEmpty("AC-600-J: no rate, amount, cost or balance on the engineer's screen");
    }

    private static void Walk(Type type, List<string> offenders, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (PropertyInfo property in type.GetProperties())
        {
            Type inner = property.PropertyType.IsGenericType
                ? property.PropertyType.GetGenericArguments()[0]
                : property.PropertyType;
            inner = Nullable.GetUnderlyingType(inner) ?? inner;

            if (inner == typeof(Money) || inner == typeof(Percentage)
                || Regex.IsMatch(property.Name, "cost|rate|price|amount|total|markup|balance", RegexOptions.IgnoreCase))
            {
                offenders.Add($"{type.Name}.{property.Name}");
            }

            if (inner.Namespace?.StartsWith("Kaff.Api", StringComparison.Ordinal) == true)
            {
                Walk(inner, offenders, seen);
            }
        }
    }
}
