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
        if (!IsValid(latitude, longitude))
        {
            throw new InvalidLocationException(latitude, longitude);
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeoLocation? FromOptional(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null)
        {
            return null;
        }

        if (latitude is not double lat || longitude is not double lon)
        {
            throw new InvalidLocationException();
        }

        return new GeoLocation(lat, lon);
    }

    internal static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude) && latitude is >= -90 and <= 90 &&
        double.IsFinite(longitude) && longitude is >= -180 and <= 180;

    /// <summary>
    /// Calculates distance in kilometers to another location using the Haversine formula,
    /// rounded to 2 decimal places for user-facing display.
    /// </summary>
    public double DistanceToKm(GeoLocation other)
    {
        return Math.Round(ExactDistanceToKm(other), 2);
    }

    /// <summary>
    /// Calculates high-precision unrounded distance in kilometers to another location
    /// to avoid premature rounding collisions during distance comparison and sorting.
    /// </summary>
    public double ExactDistanceToKm(GeoLocation other)
    {
        var dLat = DegreesToRadians(other.Latitude - Latitude);
        var dLon = DegreesToRadians(other.Longitude - Longitude);

        var lat1 = DegreesToRadians(Latitude);
        var lat2 = DegreesToRadians(other.Latitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
