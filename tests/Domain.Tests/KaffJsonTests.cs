using System.Text.Json;
using Kaff.Domain.Common;
using Kaff.Domain.Common.Serialization;

namespace Kaff.Domain.Tests;

/// <summary>
/// decisions.md D-151 — <see cref="Percentage"/> crosses the wire as the fraction, as a JSON string,
/// in both directions.
/// </summary>
public sealed class KaffJsonTests
{
    [Fact]
    public void Percentage_round_trips_as_a_fraction_string()
    {
        Percentage markup = Percentage.FromFraction(0.1275m);

        string json = JsonSerializer.Serialize(markup, KaffJson.Options);

        json.Should().Be("\"0.1275\"");

        Percentage read = JsonSerializer.Deserialize<Percentage>(json, KaffJson.Options);

        read.Should().Be(markup);
    }

    [Theory]
    [InlineData("\"+0.05\"")]
    [InlineData("\"5e-2\"")]
    [InlineData("\"٠٫٠٥\"")]
    public void A_string_outside_the_wire_grammar_is_refused(string json)
    {
        Action act = () => JsonSerializer.Deserialize<Percentage>(json, KaffJson.Options);

        act.Should().Throw<JsonException>();
    }
}
