using Microsoft.Extensions.Logging;
using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Policies;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Returns full room details for US02 (getRoomDetail).
/// Campus coordinates come from <see cref="CampusRegistry"/> (single source of truth per BR-CMP-03).
/// Distance is calculated by <see cref="HaversineCalculator"/> (single source of truth per BR-SRCH-03).
/// Phone masking follows BR-AUTH-07: null for anonymous callers.
/// Visibility follows BR-LST-04a: PendingReview / Rejected / Suspended return null (→ 404 at API layer).
/// </summary>
public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly ICurrentUserContextPort _currentUserContext;
    private readonly ILogger<GetRoomDetailUseCase> _logger;

    public GetRoomDetailUseCase(
        IRoomRepository roomRepository,
        ICurrentUserContextPort currentUserContext,
        ILogger<GetRoomDetailUseCase>? logger = null)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GetRoomDetailUseCase>.Instance;
    }

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

        // BR-LST-04a: PendingReview, Rejected, Suspended return 404 to public callers
        if (!room.IsPubliclyVisible)
        {
            return null;
        }

        // BR-AUTH-07 / US02 AC4: Only signed-in users can see the landlord's phone number
        var landlordPhone = _currentUserContext.IsAuthenticated ? room.LandlordPhoneNumber : null;

        // BR-CMP-03: Use CampusRegistry as the single source of campus coordinates.
        // If campusId is null/empty → default to ICTU (documented default campus).
        // If campusId is provided but unknown → log a warning and fall back to ICTU (graceful degradation).
        (double Lat, double Lng) campusCoords;
        if (string.IsNullOrWhiteSpace(campusId))
        {
            campusCoords = CampusRegistry.GetDefaultCoordinates();
        }
        else if (!CampusRegistry.TryGetCoordinates(campusId.Trim(), out campusCoords))
        {
            _logger.LogWarning(
                "Unknown campusId '{CampusId}' requested for room '{RoomId}'. Falling back to default campus '{DefaultCampusId}'.",
                campusId, trimmedId, CampusRegistry.DefaultCampusId);
            campusCoords = CampusRegistry.GetDefaultCoordinates();
        }

        // M2: Use HaversineCalculator (Domain service) — no inline duplication
        var distanceKm = HaversineCalculator.DistanceKm(campusCoords.Lat, campusCoords.Lng, room.Latitude, room.Longitude);
        var effectiveDistance = distanceKm > 0 ? distanceKm : room.DistanceToCampusKm;

        // AC1 & AC2: Full details mapped through RoomMapper
        return RoomMapper.ToDetailDto(room, landlordPhone, effectiveDistance);
    }
}
