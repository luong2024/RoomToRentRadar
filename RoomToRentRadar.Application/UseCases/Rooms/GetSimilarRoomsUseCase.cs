namespace RoomToRentRadar.Application.UseCases.Rooms;

using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Retrieves similar available rooms for an unavailable or viewable listing (US02 AC3, BR-LST-10, getSimilarRooms).
/// Rent within ±20%, distance to target room ≤ 2.0 km, same room type preferred (fallback if &lt; 2 found), max 5 rooms.
/// </summary>
public sealed class GetSimilarRoomsUseCase : IGetSimilarRoomsUseCase
{
    private const double MaxDistanceToTargetKm = 2.0;
    private const double RentToleranceRatio = 0.20;
    private const int MaxResults = 5;

    private readonly IRoomRepository _roomRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly IRoomSearchReadModel _readModel;

    public GetSimilarRoomsUseCase(
        IRoomRepository roomRepository,
        ICampusRepository campusRepository,
        IRoomSearchReadModel readModel)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
    }

    public async Task<IReadOnlyList<RoomCardDto>> ExecuteAsync(
        string roomId,
        string? campusId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new DomainValidationException("Room ID is required.", ProblemCodes.RoomNotFound);
        }

        var targetRoom = await _roomRepository.GetByIdAsync(roomId.Trim(), ct);
        if (targetRoom == null)
        {
            throw new DomainValidationException($"Room with ID '{roomId}' was not found.", ProblemCodes.RoomNotFound);
        }

        Campus campus;
        if (string.IsNullOrWhiteSpace(campusId))
        {
            campus = await _campusRepository.GetDefaultCampusAsync(ct)
                ?? throw new InvalidOperationException("Default campus (ICTU) is not configured.");
        }
        else
        {
            campus = await _campusRepository.GetByIdAsync(campusId.Trim(), ct)
                ?? throw new DomainValidationException($"Campus with ID '{campusId}' was not found.", ProblemCodes.CampusNotFound);
        }

        // BR-LST-10: Rent within ±20%
        var minRent = (long)Math.Round(targetRoom.MonthlyRentVnd * (1.0 - RentToleranceRatio));
        var maxRent = (long)Math.Round(targetRoom.MonthlyRentVnd * (1.0 + RentToleranceRatio));

        // 2 km bounding box prefilter for SQLite query pushdown
        const double deltaLat = 0.025;
        const double deltaLng = 0.025;

        var filter = new RoomSearchQueryFilter(
            MinRentVnd: minRent,
            MaxRentVnd: maxRent,
            MinAreaM2: null,
            MaxAreaM2: null,
            MinLat: targetRoom.Latitude - deltaLat,
            MaxLat: targetRoom.Latitude + deltaLat,
            MinLng: targetRoom.Longitude - deltaLng,
            MaxLng: targetRoom.Longitude + deltaLng);

        var candidates = await _readModel.QueryCandidatesAsync(filter, ct);

        // Filter and calculate distance to target room
        var qualified = new List<(RoomSearchCandidate Candidate, double DistanceToTargetKm)>();
        foreach (var c in candidates)
        {
            if (string.Equals(c.Id, targetRoom.Id, StringComparison.Ordinal))
            {
                continue; // Exclude target room itself
            }

            if (c.Status != ListingStatus.Available || !c.IsVerified)
            {
                continue;
            }

            if (c.MonthlyRentVnd < minRent || c.MonthlyRentVnd > maxRent)
            {
                continue;
            }

            var distanceToTarget = HaversineDistanceCalculator.CalculateDistanceKm(
                targetRoom.Latitude, targetRoom.Longitude, c.Latitude, c.Longitude);

            if (distanceToTarget <= MaxDistanceToTargetKm)
            {
                qualified.Add((c, distanceToTarget));
            }
        }

        // BR-LST-10: Same room type preferred; if fewer than 2 found, relax room type filter
        var sameType = qualified
            .Where(x => x.Candidate.RoomType == targetRoom.RoomType)
            .OrderBy(x => x.DistanceToTargetKm)
            .ThenBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .ToList();

        List<(RoomSearchCandidate Candidate, double DistanceToTargetKm)> selected;
        if (sameType.Count >= 2)
        {
            selected = sameType.Take(MaxResults).ToList();
        }
        else
        {
            selected = qualified
                .OrderBy(x => x.DistanceToTargetKm)
                .ThenBy(x => x.Candidate.Id, StringComparer.Ordinal)
                .Take(MaxResults)
                .ToList();
        }

        // Map to RoomCardDto (distanceKm is relative to the requested campus per OpenAPI schema)
        return selected
            .Select(x => RoomMapper.ToCardDto(x.Candidate, campus))
            .ToList();
    }
}
