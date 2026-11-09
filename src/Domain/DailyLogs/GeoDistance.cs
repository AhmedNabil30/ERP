namespace Kaff.Domain.DailyLogs;

/// <summary>Great-circle distance between two points on the earth, in metres.</summary>
public static class GeoDistance
{
    private const double EarthRadiusMetres = 6_371_000d;

    /// <summary>True when the pair is a real place: latitude within 90 degrees and longitude within 180.</summary>
    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= -90d and <= 90d && longitude is >= -180d and <= 180d;

    /// <summary>The haversine distance. Coordinates are degrees; this is not a money calculation.</summary>
    public static double Metres(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180d;

        double deltaLat = Rad(latitudeB - latitudeA);
        double deltaLng = Rad(longitudeB - longitudeA);
        double a = (Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d))
            + (Math.Cos(Rad(latitudeA)) * Math.Cos(Rad(latitudeB)) * Math.Sin(deltaLng / 2d) * Math.Sin(deltaLng / 2d));

        return 2d * EarthRadiusMetres * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }
}
