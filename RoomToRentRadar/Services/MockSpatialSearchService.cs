using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Utilities;

namespace RoomToRentRadar.Services;

public sealed class MockSpatialSearchService : ISpatialSearchService
{
    private static readonly IReadOnlyList<SpatialSearchPinDto> SeedPins = BuildSeedPins();

    public async Task<SpatialSearchResponse> SearchInPolygonAsync(
        IReadOnlyList<double[]> closedGeoJsonRing,
        string sortBy = "distance",
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        var matched = SeedPins
            .Where(pin => GeoPolygonHelper.IsPointInPolygon(pin.Latitude, pin.Longitude, closedGeoJsonRing))
            .ToList();

        return new SpatialSearchResponse
        {
            TotalCount = matched.Count,
            Items = matched
        };
    }

    private static IReadOnlyList<SpatialSearchPinDto> BuildSeedPins()
    {
        // Tọa độ mẫu quanh cổng ĐH CNTT&TT ICTU (21.5850, 105.8066)
        var offsets = new (double dLat, double dLng, string street, string type, decimal rent)[]
        {
            ( 0.0012,  0.0010, "Đường Z115", "Khép kín", 1_600_000m),
            (-0.0015,  0.0018, "Ngõ 23 Z115", "Studio", 2_200_000m),
            ( 0.0022, -0.0014, "Xã Quyết Thắng", "Phòng trọ", 1_300_000m),
            (-0.0020, -0.0016, "P. Tân Thịnh", "Mini Apartment", 2_500_000m),
            ( 0.0008, -0.0024, "Đường Quang Trung", "Khép kín", 1_800_000m),
            ( 0.0030,  0.0022, "Ngõ 82 Quang Trung", "Studio", 2_100_000m),
            (-0.0009,  0.0031, "Tổ 4 Tân Thịnh", "Phòng trọ", 1_400_000m),
            ( 0.0017,  0.0004, "Cổng sau ICTU", "Khép kín", 1_700_000m)
        };

        var list = new List<SpatialSearchPinDto>(offsets.Length);
        for (int i = 0; i < offsets.Length; i++)
        {
            var o = offsets[i];
            list.Add(new SpatialSearchPinDto
            {
                Id = $"room-map-{i + 1:000}",
                CampusId = "ictu-main",
                Title = $"Phòng {o.type} #{i + 1:00} - {o.street}",
                Rent = o.rent,
                Area = 18 + (i % 4) * 3,
                DistanceMeters = 200 + i * 110,
                RoomType = o.type,
                Address = $"{o.street}, TP. Thái Nguyên",
                IsVerified = true,
                Latitude = 21.5850 + o.dLat,
                Longitude = 105.8066 + o.dLng
            });
        }

        return list;
    }
}