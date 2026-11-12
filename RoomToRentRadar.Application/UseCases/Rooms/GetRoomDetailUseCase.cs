using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly ICurrentUserContextPort _currentUserContext;

    public GetRoomDetailUseCase(IRoomRepository roomRepository, ICurrentUserContextPort currentUserContext)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    private static readonly Dictionary<string, (double Lat, double Lng)> Campuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ICTU"] = (21.5851, 105.8066),
        ["TNUS"] = (21.5887, 105.80979),
        ["SFL"] = (21.5866, 105.8116),
        ["TUEBA"] = (21.5937, 105.8048),
        ["TUAF"] = (21.5794, 105.8277),
        ["TNUE"] = (21.5822, 105.8170)
    };

    public async Task<RoomDetailDto?> ExecuteAsync(string roomId, CancellationToken ct = default, string? campusId = null)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var trimmedId = roomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedId, ct);
        if (room == null)
        {
            return null;
        }

        // BR-LST-04a: Listings pending review, rejected, or suspended return 404 to non-owner/public callers
        if (!room.IsPubliclyVisible)
        {
            return null;
        }

        // BR-AUTH-07 / US02 AC4: Only signed-in users can see the landlord's phone number
        var landlordPhone = _currentUserContext.IsAuthenticated ? room.LandlordPhoneNumber : null;

        // BR-CMP-03: Dynamic campus distance calculation
        var campusKey = string.IsNullOrWhiteSpace(campusId) ? "ICTU" : campusId.Trim();
        if (!Campuses.TryGetValue(campusKey, out var campusCoords))
        {
            campusCoords = Campuses["ICTU"];
        }

        var distanceKm = CalculateDistance(campusCoords.Lat, campusCoords.Lng, room.Latitude, room.Longitude);
        var effectiveDistance = distanceKm > 0 ? distanceKm : room.DistanceToCampusKm;

        // AC1 & AC2: Full details mapped through RoomMapper
        return RoomMapper.ToDetailDto(room, landlordPhone, effectiveDistance);
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        if (lat1 == 0 && lon1 == 0 || lat2 == 0 && lon2 == 0)
        {
            return 0.0;
        }
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(earthRadiusKm * c, 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
