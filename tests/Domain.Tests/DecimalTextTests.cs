using Kaff.Domain.Common;

namespace Kaff.Domain.Tests;

/// <summary>
/// decisions.md D-135: the one grammar every decimal crosses the wire through.
/// </summary>
public sealed class DecimalTextTests
{
    [Theory]
    [InlineData("1234.5678")]
    [InlineData("-12.5")]
    [InlineData("0.150000")]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("100")]
    [InlineData("12345678901234.5678")]
    public void Accepts_the_wire_grammar(string text)
    {
        DecimalText.TryParse(text, out decimal _).Should().BeTrue();
    }

    [Fact]
    public void A_seventeen_digit_value_round_trips_exactly()
    {
        // decisions.md D-135's own measured failure: this exact value was corrupted to
        // "…4.5680" when it crossed a JavaScript double (VRF-FIXTURE-011). Text must not lose it.
        DecimalText.TryParse("12345678901234.5678", out decimal value).Should().BeTrue();

        value.Should().Be(12345678901234.5678m);
        value.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("12345678901234.5678");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1.5")]
    [InlineData("1.5 ")]
    [InlineData("+1.5")]
    [InlineData("1.5e2")]
    [InlineData("1E-4")]
    [InlineData("0x1A")]
    [InlineData("1,234.5")]
    [InlineData("١٢٫٥")]
    [InlineData("1.2.3")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("--1")]
    [InlineData(null)]
    public void Refuses_everything_outside_the_grammar(string? text)
    {
        DecimalText.TryParse(text, out decimal _).Should().BeFalse();
    }
}
