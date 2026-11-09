using Kaff.Domain.DailyLogs;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-613: the distance a check-in is measured by, and the threshold that decides "far".</summary>
public sealed class CheckInTests
{
    [Fact]
    public void Haversine_gives_about_111_km_for_one_degree_of_latitude_and_zero_for_the_same_point()
    {
        GeoDistance.Metres(30d, 31d, 31d, 31d).Should().BeApproximately(111_195d, 100d);
        GeoDistance.Metres(30.0444, 31.2357, 30.0444, 31.2357).Should().Be(0d);
    }

    [Fact]
    public void The_threshold_is_500_metres_and_exactly_500_is_not_far()
    {
        CheckInDistanceRule rule = new RulingSet().CheckInDistance;

        rule.ThresholdMetres.Should().Be(500d);
        rule.IsFar(500d).Should().BeFalse();
        rule.IsFar(500.01d).Should().BeTrue();
    }

    [Theory]
    [InlineData(90d, 180d, true)]
    [InlineData(-90d, -180d, true)]
    [InlineData(90.1d, 0d, false)]
    [InlineData(0d, 180.1d, false)]
    public void Coordinates_outside_the_planet_are_not_a_location(double latitude, double longitude, bool valid) =>
        GeoDistance.IsValid(latitude, longitude).Should().Be(valid);
}
