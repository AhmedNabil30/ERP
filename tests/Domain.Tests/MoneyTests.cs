using Kaff.Domain.Common;

namespace Kaff.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Money_keeps_four_decimal_places()
    {
        var amount = new Money(1234.56789m);

        amount.Amount.Should().Be(1234.5679m);
    }

    [Fact]
    public void Money_addition_is_exact()
    {
        // The classic floating-point failure: 0.1 + 0.2 != 0.3 in binary. decimal makes it exact,
        // which is the whole reason CLAUDE.md forbids float and double anywhere near money.
        Money total = new Money(0.1m) + new Money(0.2m);

        total.Amount.Should().Be(0.3m);
    }

    [Fact]
    public void Money_rejects_amounts_the_database_cannot_store()
    {
        Action creating = () => _ = new Money(100_000_000_000_000m);

        creating.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Percentage_of_an_amount_matches_the_worked_example()
    {
        // spec.md §15, extract 1: 20% hold on 300,000 certified work is 60,000.
        Money hold = Percentage.FromPercent(20m).Of(new Money(300_000m));

        hold.Should().Be(new Money(60_000m));
    }

    [Fact]
    public void Percentage_distinguishes_twenty_from_zero_point_two()
    {
        Percentage.FromPercent(20m).Should().Be(Percentage.FromFraction(0.20m));
        Percentage.FromPercent(20m).Fraction.Should().Be(0.20m);
        Percentage.FromPercent(20m).Percent.Should().Be(20m);
    }

    [Theory]
    [InlineData("+20 100 123 4567", "01001234567")]
    [InlineData("0020 100 123 4567", "01001234567")]
    [InlineData("010 0123 4567", "01001234567")]
    [InlineData("٠١٠٠١٢٣٤٥٦٧", "01001234567")]
    public void Phone_numbers_normalise_to_one_deduplication_key(string entered, string expected)
    {
        // spec.md §2 deduplicates clients by phone; §10 deduplicates workers the same way.
        // Deduplication only works if every spelling of a number collapses to one key.
        Result<PhoneNumber> result = PhoneNumber.Create(entered);

        result.IsSuccess.Should().BeTrue();
        result.Value.Normalised.Should().Be(expected);
    }
}
