namespace RoomToRentRadar.Domain.Services;

public static class HaversineDistanceCalculator
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Great-circle distance in km, rounded to 2 decimals. Returns <see cref="double.NaN"/>
    /// when any argument is NaN or infinite so callers can reject the request (H3) instead of
    /// serializing an invalid number.
    /// </summary>
    public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        if (!double.IsFinite(lat1) || !double.IsFinite(lon1) || !double.IsFinite(lat2) || !double.IsFinite(lon2))
        {
            return double.NaN;
        }

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        // H3: floating point noise can push `a` slightly outside [0, 1] (antipodal points),
        // which would make 1 - a negative and produce NaN inside Sqrt.
        var clampedA = Math.Clamp(a, 0.0, 1.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(clampedA), Math.Sqrt(Math.Max(0.0, 1.0 - clampedA)));

        return Math.Round(EarthRadiusKm * c, 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
