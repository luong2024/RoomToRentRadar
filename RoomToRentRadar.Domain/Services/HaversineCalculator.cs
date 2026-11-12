namespace RoomToRentRadar.Domain.Services;

/// <summary>
/// Domain service for Haversine great-circle distance calculation per BR-SRCH-03 and AGENTS §5.
/// Single authoritative implementation — no copy-pasting in Application or Infrastructure layers.
/// </summary>
public static class HaversineCalculator
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Calculates the Haversine distance in kilometres between two geographic coordinates.
    /// Returns 0.0 if either point is at the origin (0, 0) — guard for unset coordinates.
    /// </summary>
    /// <param name="lat1">Latitude of point 1 (degrees).</param>
    /// <param name="lon1">Longitude of point 1 (degrees).</param>
    /// <param name="lat2">Latitude of point 2 (degrees).</param>
    /// <param name="lon2">Longitude of point 2 (degrees).</param>
    /// <returns>Distance in kilometres, rounded to 2 decimal places.</returns>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        // Guard: treat (0, 0) as "unset" — avoids spurious distances from default-initialised coordinates
        if ((lat1 == 0.0 && lon1 == 0.0) || (lat2 == 0.0 && lon2 == 0.0))
        {
            return 0.0;
        }

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        // Clamp a to [0, 1] to defend against floating-point rounding producing values
        // marginally outside [0, 1], which would cause Math.Sqrt to return NaN.
        a = Math.Clamp(a, 0.0, 1.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return Math.Round(EarthRadiusKm * c, 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
