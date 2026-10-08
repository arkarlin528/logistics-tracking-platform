namespace Logistics.Domain.Reference;

/// <summary>
/// Simple geometry for simulated positions. Longitudes take the shorter way round the globe,
/// so Los Angeles → Port Klang crosses the Pacific instead of the Atlantic.
/// </summary>
public static class GeoMath
{
    public static (double Latitude, double Longitude) Interpolate(
        double fromLat, double fromLng, double toLat, double toLng, double progress)
    {
        var lat = fromLat + (toLat - fromLat) * progress;
        var lng = NormalizeLongitude(fromLng + LongitudeDelta(fromLng, toLng) * progress);
        return (Math.Round(lat, 4), Math.Round(lng, 4));
    }

    /// <summary>Flat-map distance in degrees. Fine for measuring progress along one leg; not for real-world km.</summary>
    public static double Distance(double fromLat, double fromLng, double toLat, double toLng)
    {
        var dLat = toLat - fromLat;
        var dLng = LongitudeDelta(fromLng, toLng);
        return Math.Sqrt(dLat * dLat + dLng * dLng);
    }

    /// <summary>Signed difference in (-180, 180]: the shorter direction from one longitude to another.</summary>
    public static double LongitudeDelta(double fromLng, double toLng)
    {
        var delta = (toLng - fromLng) % 360;
        if (delta > 180) delta -= 360;
        if (delta <= -180) delta += 360;
        return delta;
    }

    public static double NormalizeLongitude(double lng)
    {
        var result = (lng + 180) % 360;
        if (result < 0) result += 360;
        return result - 180;
    }
}
