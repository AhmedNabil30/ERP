using System.Globalization;

namespace Kaff.Domain.Common;

/// <summary>
/// A rate held as a fraction — 20% is stored as 0.20.
/// </summary>
/// <remarks>
/// This type exists to kill one specific bug: the 20-versus-0.20 confusion. spec.md is written in
/// percent (hold 20%, advance 25%, تشوينات 75%, subcontractor retention 5%) while every formula
/// multiplies by a fraction. Construction is explicit on both sides —
/// <see cref="FromPercent"/> or <see cref="FromFraction"/> — so a bare number can never be mistaken
/// for the other convention.
/// </remarks>
public readonly record struct Percentage : IComparable<Percentage>, IComparable
{
    /// <summary>Fractions are stored at decimal(18,6): 0.000001 resolves to one ten-thousandth of a percent.</summary>
    public const int Scale = 6;

    public static readonly Percentage Zero = new(0m);

    private Percentage(decimal fraction)
    {
        decimal normalised = decimal.Round(fraction, Scale, MidpointRounding.AwayFromZero);

        if (normalised < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "A rate must not be negative.");
        }

        Fraction = normalised;
    }

    /// <summary>The rate as a fraction. 20% is 0.20.</summary>
    public decimal Fraction { get; }

    /// <summary>The rate expressed in percent. 0.20 is 20.</summary>
    public decimal Percent => Fraction * 100m;

    public bool IsZero => Fraction == 0m;

    /// <summary>Builds from a percent figure as written in spec.md — <c>FromPercent(20m)</c> for 20%.</summary>
    public static Percentage FromPercent(decimal percent) => new(percent / 100m);

    /// <summary>Builds from a fraction — <c>FromFraction(0.20m)</c> for 20%.</summary>
    public static Percentage FromFraction(decimal fraction) => new(fraction);

    /// <summary>Applies the rate to an amount. <c>hold = Percentage.FromPercent(20).Of(workValue)</c>.</summary>
    public Money Of(Money amount) => amount * Fraction;

    /// <summary>Applies <c>1 + rate</c> to an amount — the markup and supervision form in spec.md §4.2 and §5.2.</summary>
    public Money Uplift(Money amount) => amount * (1m + Fraction);

    public static Percentage operator +(Percentage left, Percentage right) => new(left.Fraction + right.Fraction);

    public static bool operator <(Percentage left, Percentage right) => left.Fraction < right.Fraction;

    public static bool operator >(Percentage left, Percentage right) => left.Fraction > right.Fraction;

    public static bool operator <=(Percentage left, Percentage right) => left.Fraction <= right.Fraction;

    public static bool operator >=(Percentage left, Percentage right) => left.Fraction >= right.Fraction;

    public static Percentage Add(Percentage left, Percentage right) => left + right;

    public int CompareTo(Percentage other) => Fraction.CompareTo(other.Fraction);

    public int CompareTo(object? obj) => obj is Percentage other
        ? CompareTo(other)
        : throw new ArgumentException($"Object must be of type {nameof(Percentage)}.", nameof(obj));

    public override string ToString() => Percent.ToString("0.####", CultureInfo.InvariantCulture) + "%";
}
