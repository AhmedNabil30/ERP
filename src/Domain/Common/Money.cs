using System.Globalization;

namespace Kaff.Domain.Common;

/// <summary>
/// A monetary amount. CLAUDE.md: "Money is a <c>Money</c> value object wrapping <c>decimal</c>,
/// not a bare <c>decimal</c> passed around." There is no implicit conversion from <c>decimal</c>
/// on purpose — every monetary value must be constructed deliberately.
/// </summary>
/// <remarks>
/// <para>
/// Currency is deliberately NOT part of this type. spec.md §1 puts multi-currency out of scope:
/// "currency field exists, conversion logic does not". The currency lives on the entity that owns
/// the amount (Account, Project), and postings are rejected at the database when the two sides
/// disagree. Putting a currency inside Money would imply a conversion capability that must not exist.
/// </para>
/// <para>
/// Storage scale is 4 decimal places, matching <c>decimal(18,4)</c> in the database
/// (spec.md §6.1). Nothing may widen this.
/// </para>
/// </remarks>
public readonly record struct Money : IComparable<Money>, IComparable
{
    /// <summary>Decimal places retained in storage. spec.md §6.1 mandates decimal(18,4).</summary>
    public const int Scale = 4;

    /// <summary>
    /// Rounding applied when normalising to <see cref="Scale"/>.
    /// AwayFromZero is the Egyptian commercial convention (half up on positive amounts).
    /// This is flagged as an open question — see decisions.md D-008.
    /// </summary>
    public const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    public static readonly Money Zero = new(0m);

    public Money(decimal amount)
    {
        decimal normalised = decimal.Round(amount, Scale, Rounding);

        if (Math.Abs(normalised) >= MaxMagnitude)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Amount exceeds the decimal(18,4) range the database can store.");
        }

        Amount = normalised;
    }

    /// <summary>18 total digits minus 4 fractional digits leaves 14 integral digits.</summary>
    private const decimal MaxMagnitude = 100_000_000_000_000m;

    public decimal Amount { get; }

    public bool IsZero => Amount == 0m;

    public bool IsPositive => Amount > 0m;

    public bool IsNegative => Amount < 0m;

    public static Money From(decimal amount) => new(amount);

    public Money Abs() => new(Math.Abs(Amount));

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    public static Money operator -(Money value) => new(-value.Amount);

    public static Money operator *(Money left, decimal factor) => new(left.Amount * factor);

    public static Money operator *(decimal factor, Money right) => new(right.Amount * factor);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Multiply(Money left, decimal factor) => left * factor;

    public static Money Negate(Money value) => -value;

    public static Money Sum(IEnumerable<Money> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        decimal total = 0m;
        foreach (Money value in values)
        {
            total += value.Amount;
        }

        return new Money(total);
    }

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public int CompareTo(object? obj) => obj is Money other
        ? CompareTo(other)
        : throw new ArgumentException($"Object must be of type {nameof(Money)}.", nameof(obj));

    public override string ToString() => Amount.ToString("0.0000", CultureInfo.InvariantCulture);
}
