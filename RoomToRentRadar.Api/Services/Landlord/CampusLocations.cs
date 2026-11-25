namespace RoomToRentRadar.Api.Services.Landlord;

/// <summary>Tọa độ campus (GeoJSON [lng, lat]) và khoảng cách Haversine tới campus.</summary>
public static class CampusLocations
{
    private const double EarthRadiusMeters = 6_371_000;

    private static readonly Dictionary<string, double[]> Campuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ictu-main"] = [105.8066, 21.5850]
    };

    public static double[] Get(string? campusId) =>
        campusId is not null && Campuses.TryGetValue(campusId, out var point) ? point : Campuses["ictu-main"];

    public static double HaversineMeters(double[] from, double[] to)
    {
        double lat1 = ToRad(from[1]), lat2 = ToRad(to[1]);
        double dLat = lat2 - lat1, dLng = ToRad(to[0] - from[0]);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                   + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180;
}
