using System;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Utilities;

/// <summary>
/// Tính khoảng cách đường chim bay theo công thức Haversine và ước tính thời gian dự phòng khi dịch vụ định tuyến lỗi (US09 AC3).
/// </summary>
public static class HaversineDistanceHelper
{
    public const double EarthRadiusKm = 6371.0;

    public static double CalculateStraightLineKm(double lat1, double lng1, double lat2, double lng2)
    {
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLng = DegreesToRadians(lng2 - lng1);

        double rLat1 = DegreesToRadians(lat1);
        double rLat2 = DegreesToRadians(lat2);

        double sinLat = Math.Sin(dLat / 2.0);
        double sinLng = Math.Sin(dLng / 2.0);

        double a = (sinLat * sinLat) + (Math.Cos(rLat1) * Math.Cos(rLat2) * sinLng * sinLng);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0.0, 1.0 - a)));

        return EarthRadiusKm * c;
    }

    public static int EstimateMotorbikeMinutes(double distanceKm)
    {
        if (distanceKm <= 0)
            return 1;

        return Math.Max(1, (int)Math.Round((distanceKm / 28.0) * 60.0));
    }

    public static int EstimateBusMinutes(double distanceKm)
    {
        if (distanceKm <= 0)
            return 2;

        return Math.Max(2, (int)Math.Round((distanceKm / 18.0) * 60.0) + 3);
    }

    public static int EstimateWalkMinutes(double distanceKm)
    {
        if (distanceKm <= 0)
            return 1;

        return Math.Max(1, (int)Math.Round((distanceKm / 4.8) * 60.0));
    }

    public static CommuteEstimateDto BuildFallbackEstimate(
        string listingId,
        CampusDestinationOption campus,
        double roomLat,
        double roomLng)
    {
        double straightKm = Math.Round(CalculateStraightLineKm(roomLat, roomLng, campus.Latitude, campus.Longitude), 2);

        return new CommuteEstimateDto
        {
            ListingId = listingId,
            CampusId = campus.CampusId,
            CampusName = campus.CampusName,
            MotorbikeMinutes = EstimateMotorbikeMinutes(straightKm),
            BusMinutes = EstimateBusMinutes(straightKm),
            WalkMinutes = EstimateWalkMinutes(straightKm),
            RouteDistanceKm = straightKm,
            IsFallbackStraightLine = true,
            StraightLineKm = straightKm
        };
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
