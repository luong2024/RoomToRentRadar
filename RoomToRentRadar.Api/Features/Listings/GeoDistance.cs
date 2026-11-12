namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Inclusive WGS84 bounding box, in degrees, that contains every point within the
/// requested radius of the centre.
/// </summary>
public readonly record struct GeoBoundingBox(
    double MinLat,
    double MaxLat,
    double MinLng,
    double MaxLng);

/// <summary>
/// Pure geodesy helpers used by the radius filter: a haversine distance and the
/// bounding box that narrows the SQL candidate set before the exact distance check.
/// </summary>
public static class GeoDistance
{
    /// <summary>Mean earth radius in kilometres.</summary>
    public const double EarthRadiusKm = 6371.0088;

    /// <summary>Kilometres per degree of latitude on a spherical earth.</summary>
    public const double KmPerDegreeLatitude = 111.32;

    private const double RadiansPerDegree = Math.PI / 180d;

    /// <summary>Great-circle distance in kilometres between two WGS84 positions.</summary>
    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        var deltaLat = (lat2 - lat1) * RadiansPerDegree;
        var deltaLng = (lng2 - lng1) * RadiansPerDegree;

        var sinLat = Math.Sin(deltaLat / 2d);
        var sinLng = Math.Sin(deltaLng / 2d);

        var a = sinLat * sinLat
            + Math.Cos(lat1 * RadiansPerDegree) * Math.Cos(lat2 * RadiansPerDegree) * sinLng * sinLng;

        // Clamp so rounding can never push the square root outside the asin domain.
        return 2d * EarthRadiusKm * Math.Asin(Math.Min(1d, Math.Sqrt(Math.Max(0d, a))));
    }

    /// <summary>
    /// Bounding box of every position within <paramref name="radiusKm"/> of the
    /// centre. The longitude delta grows towards the poles and is clamped so the
    /// bounds stay inside [-180, 180]; the latitude delta is clamped to [-90, 90].
    /// </summary>
    public static GeoBoundingBox BoundingBox(double lat, double lng, double radiusKm)
    {
        var latDelta = radiusKm / KmPerDegreeLatitude;

        var cosLat = Math.Cos(lat * RadiansPerDegree);
        var lngDelta = cosLat <= 1e-9
            // At the poles a small radius spans every longitude.
            ? 180d
            : Math.Min(180d, radiusKm / (KmPerDegreeLatitude * cosLat));

        return new GeoBoundingBox(
            Math.Max(-90d, lat - latDelta),
            Math.Min(90d, lat + latDelta),
            Math.Max(-180d, lng - lngDelta),
            Math.Min(180d, lng + lngDelta));
    }
}
