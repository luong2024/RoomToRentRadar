using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Utilities;

namespace RoomToRentRadar.Services;

/// <summary>
/// Cung cấp danh sách cơ sở đích quanh ICTU/Thái Nguyên và dữ liệu ước tính offline/fallback (US09 T09.2).
/// </summary>
public sealed class MockCommuteService : ICommuteService
{
    private const double DefaultRoomLatitude = 21.5822;
    private const double DefaultRoomLongitude = 105.8091;

    private static readonly IReadOnlyList<CampusDestinationOption> CampusDestinations =
    [
        new("ictu-main", "Cổng chính ICTU (Z115)", 21.5850, 105.8066),
        new("ictu-dorm", "Ký túc xá ĐH CNTT&TT", 21.5868, 105.8045),
        new("tnu-center", "Đại học Thái Nguyên (Trung tâm)", 21.5889, 105.8144),
        new("tuaf-main", "ĐH Nông Lâm Thái Nguyên", 21.5945, 105.8082)
    ];

    public IReadOnlyList<CampusDestinationOption> GetCampusDestinations() => CampusDestinations;

    public Task<CommuteEstimateDto> GetCommuteEstimateAsync(
        string listingId,
        CampusDestinationOption campus,
        double? roomLat = null,
        double? roomLng = null,
        bool forceFallback = false,
        CancellationToken cancellationToken = default)
    {
        double lat = roomLat ?? DefaultRoomLatitude;
        double lng = roomLng ?? DefaultRoomLongitude;

        if (forceFallback || string.Equals(listingId, "room-fallback", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HaversineDistanceHelper.BuildFallbackEstimate(listingId, campus, lat, lng));
        }

        double straightKm = HaversineDistanceHelper.CalculateStraightLineKm(lat, lng, campus.Latitude, campus.Longitude);
        double roadKm = Math.Max(0.2, straightKm * 1.25);

        var dto = new CommuteEstimateDto
        {
            ListingId = listingId,
            CampusId = campus.CampusId,
            CampusName = campus.CampusName,
            MotorbikeMinutes = HaversineDistanceHelper.EstimateMotorbikeMinutes(roadKm),
            BusMinutes = HaversineDistanceHelper.EstimateBusMinutes(roadKm),
            WalkMinutes = HaversineDistanceHelper.EstimateWalkMinutes(roadKm),
            RouteDistanceKm = Math.Round(roadKm, 2),
            IsFallbackStraightLine = false,
            StraightLineKm = Math.Round(straightKm, 2)
        };

        return Task.FromResult(dto);
    }
}
