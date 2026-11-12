namespace RoomToRentRadar.Domain.ValueObjects;

using RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Immutable value object representing a geographic point with latitude and longitude.
/// </summary>
public readonly record struct GeoLocation
{
    private const double EarthRadiusKm = 6371.0;

    public double Latitude { get; }
    public double Longitude { get; }

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude is < -90.0 or > 90.0 || longitude is < -180.0 or > 180.0)
        {
            throw new InvalidLocationException(latitude, longitude);
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Calculates distance in kilometers to another location using the Haversine formula.
    /// </summary>
    public double DistanceToKm(GeoLocation other)
    {
        var dLat = DegreesToRadians(other.Latitude - Latitude);
        var dLon = DegreesToRadians(other.Longitude - Longitude);

        var lat1 = DegreesToRadians(Latitude);
        var lat2 = DegreesToRadians(other.Latitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(EarthRadiusKm * c, 2);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
