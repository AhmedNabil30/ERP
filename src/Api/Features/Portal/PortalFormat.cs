using System.Globalization;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Portal;

/// <summary>
/// How a number leaves the portal. Spec.md section 15 amendment (Karim, D-044 section 6): four decimals in storage and in every
/// calculation, two in anything a client sees, rounded at the last step. A portal response carries these strings and never a
/// <see cref="Money"/>, so a four-decimal figure cannot reach a client through a type that forgot to round.
/// </summary>
internal static class PortalFormat
{
    public static string Amount(Money value) =>
        decimal.Round(value.Amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    public static string? Amount(Money? value) => value is { } present ? Amount(present) : null;

    public static string Percent(Percentage value) =>
        decimal.Round(value.Percent, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    public static string? Percent(Percentage? value) => value is { } present ? Percent(present) : null;
}
