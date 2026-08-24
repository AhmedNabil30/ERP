using System.Globalization;
using System.Text;

namespace Kaff.Domain.Common;

/// <summary>
/// A phone number together with its normalised form.
/// </summary>
/// <remarks>
/// spec.md deduplicates three master records by phone: Client (§2), Worker (§10) and — by the same
/// "exactly one record" rule — Employee. Deduplication only works if every record normalises the
/// same way, so normalisation lives here and the unique index in the database is built on
/// <see cref="Normalised"/>, never on the entered text.
///
/// Normalisation: Arabic-Indic digits folded to ASCII, all non-digits removed, a leading
/// international prefix reduced to a national number. Egyptian mobile numbers are stored in
/// national form (01XXXXXXXXX).
/// </remarks>
public readonly record struct PhoneNumber
{
    public const int MaxLength = 32;

    private const string EgyptCountryCode = "20";

    private PhoneNumber(string entered, string normalised)
    {
        Entered = entered;
        Normalised = normalised;
    }

    /// <summary>The text exactly as the user typed it. Kept for display and for support calls.</summary>
    public string Entered { get; }

    /// <summary>Digits only, national form. This is what the unique index is built on.</summary>
    public string Normalised { get; }

    public static Result<PhoneNumber> Create(string? entered)
    {
        if (string.IsNullOrWhiteSpace(entered))
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("phone.required", "errors.phone.required"));
        }

        string trimmed = entered.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("phone.too_long", "errors.phone.too_long"));
        }

        string normalised = Normalise(trimmed);

        if (normalised.Length < 7)
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("phone.too_short", "errors.phone.too_short"));
        }

        return Result.Success(new PhoneNumber(trimmed, normalised));
    }

    /// <summary>Rehydrates a stored value. For persistence only — it performs no validation.</summary>
    public static PhoneNumber FromStorage(string entered, string normalised) => new(entered, normalised);

    private static string Normalise(string value)
    {
        var digits = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            // CharUnicodeInfo maps Arabic-Indic (٠-٩) and Eastern Arabic-Indic (۰-۹) digits too.
            int digit = CharUnicodeInfo.GetDecimalDigitValue(c);
            if (digit >= 0)
            {
                digits.Append(digit.ToString(CultureInfo.InvariantCulture));
            }
        }

        string result = digits.ToString();

        // +20 10 1234 5678 and 0020 10 1234 5678 both reduce to 01012345678.
        if (result.StartsWith("00" + EgyptCountryCode, StringComparison.Ordinal))
        {
            result = "0" + result[(2 + EgyptCountryCode.Length)..];
        }
        else if (result.StartsWith(EgyptCountryCode, StringComparison.Ordinal) && result.Length > 10)
        {
            result = "0" + result[EgyptCountryCode.Length..];
        }

        return result;
    }

    public override string ToString() => Entered;
}
