using System.Globalization;
using System.Text.RegularExpressions;

namespace Kaff.Domain.Common;

/// <summary>
/// The one grammar every decimal crosses the wire through, per decisions.md D-135. Shared by
/// <c>KaffJson</c>'s converters and by <c>KAFF-200</c>'s Excel import (D-136) for a text money cell.
/// </summary>
/// <remarks>
/// ASCII only: an optional leading <c>-</c>, one or more digits, an optional <c>.</c> and more
/// digits. No leading <c>+</c>, no exponent, no thousands separator, no whitespace, no Arabic-Indic
/// digit (<c>٫</c>), and no digit outside ASCII. <see cref="decimal"/>'s own range (roughly 28-29
/// significant digits) is what refuses a value with too many digits — this grammar does not repeat
/// that limit as a second check.
/// </remarks>
public static partial class DecimalText
{
    [GeneratedRegex(@"^-?[0-9]+(\.[0-9]+)?$")]
    private static partial Regex Grammar();

    /// <summary>
    /// Parses <paramref name="text"/> under the wire grammar. Refuses anything the regex does not
    /// match before <see cref="decimal.TryParse(string, NumberStyles, IFormatProvider, out decimal)"/>
    /// ever sees it, so a value <c>decimal.TryParse</c> alone would accept — leading <c>+</c>, an
    /// exponent, a thousands separator — is refused here instead.
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = default;

        if (string.IsNullOrEmpty(text) || !Grammar().IsMatch(text))
        {
            return false;
        }

        return decimal.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }
}
