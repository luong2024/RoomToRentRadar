using Microsoft.Extensions.Logging;
using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Returns full room details for US02 (getRoomDetail).
/// Author: Tran Thuy Duyen (T02.1 - US02).
/// Campus is resolved via <see cref="ICampusRepository"/> per BR-CMP-01..03 (M1).
/// Distance is calculated by <see cref="HaversineDistanceCalculator"/> rounded to 1 decimal place per BR-SRCH-03 (M5).
/// Phone masking follows BR-AUTH-07: null for anonymous callers.
/// Visibility follows BR-LST-04a: PendingReview / Rejected / Suspended return null (→ 404 at API layer).
/// </summary>
public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly ICurrentUserContextPort _currentUserContext;
    private readonly ILogger<GetRoomDetailUseCase> _logger;

    public GetRoomDetailUseCase(
        IRoomRepository roomRepository,
        ICampusRepository campusRepository,
        ICurrentUserContextPort currentUserContext,
        ILogger<GetRoomDetailUseCase>? logger = null)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GetRoomDetailUseCase>.Instance;
    }

    public async Task<RoomDetailDto?> ExecuteAsync(string roomId, CancellationToken ct = default, string? campusId = null)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        // BR-CMP-01..03: Validate / resolve campus first. Unknown campusId throws DomainValidationException (mapped to 400 CAMPUS_NOT_FOUND).
        var campus = await ResolveCampusAsync(campusId, ct).ConfigureAwait(false);

        var trimmedId = roomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedId, ct).ConfigureAwait(false);
        if (room == null)
        {
            return null;
        }

        // BR-LST-04a: PendingReview, Rejected, Suspended return 404 to public callers
        if (!room.IsDetailViewable)
        {
            return null;
        }

        // BR-AUTH-07 / US02 AC4: Only signed-in users can see the landlord's phone number
        var landlordPhone = _currentUserContext.IsAuthenticated ? room.LandlordPhoneNumber : null;

        // BR-SRCH-03 & M5: Distance in km from room to selected campus, rounded to 1 decimal place.
        var distanceKm = HaversineDistanceCalculator.CalculateDistanceKm(
            campus.Latitude, campus.Longitude, room.Latitude, room.Longitude);

        // AC1 & AC2: Full details mapped through RoomMapper
        return RoomMapper.ToDetailDto(room, landlordPhone, distanceKm);
    }

    private async Task<Campus> ResolveCampusAsync(string? campusId, CancellationToken ct)
    {
        // M1: Unknown campusId throws DomainValidationException (mapped to 400 CAMPUS_NOT_FOUND) instead of silent fallback
        if (!string.IsNullOrWhiteSpace(campusId))
        {
            var trimmedId = campusId.Trim();
            var campus = await _campusRepository.GetByIdAsync(trimmedId, ct).ConfigureAwait(false);
            return campus ?? throw new DomainValidationException(
                $"Campus with ID '{trimmedId}' not found.", ProblemCodes.CampusNotFound);
        }

        var defaultCampus = await _campusRepository.GetDefaultCampusAsync(ct).ConfigureAwait(false);
        return defaultCampus ?? throw new DomainValidationException(
            "Default campus is not configured.", ProblemCodes.CampusNotFound);
    }
}
