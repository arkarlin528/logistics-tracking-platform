using Logistics.Domain.Reference;

namespace Logistics.Domain.Tests;

public class GeoMathTests
{
    [Fact]
    public void Halfway_between_nearby_ports_is_the_midpoint()
    {
        var (lat, lng) = GeoMath.Interpolate(13.0, 100.0, 1.0, 104.0, 0.5);

        Assert.Equal(7.0, lat);
        Assert.Equal(102.0, lng);
    }

    [Fact]
    public void Los_Angeles_to_Port_Klang_crosses_the_Pacific()
    {
        // LA is at -118.26, Port Klang at 101.39: going east is 219.65 degrees, west is 140.35.
        var (_, lng) = GeoMath.Interpolate(33.73, -118.26, 3.0, 101.39, 0.5);

        Assert.True(lng is < -150 or > 150, $"Expected a mid-Pacific longitude, got {lng}");
    }

    [Theory]
    [InlineData(170, -170, 20)]
    [InlineData(-170, 170, -20)]
    [InlineData(10, 20, 10)]
    [InlineData(0, 180, 180)]
    public void Longitude_delta_takes_the_shorter_way(double from, double to, double expected)
    {
        Assert.Equal(expected, GeoMath.LongitudeDelta(from, to), precision: 6);
    }

    [Theory]
    [InlineData(190, -170)]
    [InlineData(-190, 170)]
    [InlineData(45, 45)]
    public void Longitudes_are_normalized_into_range(double input, double expected)
    {
        Assert.Equal(expected, GeoMath.NormalizeLongitude(input), precision: 6);
    }
}
