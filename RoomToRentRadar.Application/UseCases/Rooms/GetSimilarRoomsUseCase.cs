using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Policies;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Use case for finding available similar rooms near an unavailable listing per docs/business-analysis.md BR-LST-10.
/// Target room proximity is used to filter candidates (<= 2 km), while distanceKm in returned cards
/// represents distance to the requested campus per OpenAPI getSimilarRooms schema (M2 / BR-SRCH-03).
/// </summary>
public sealed class GetSimilarRoomsUseCase : IGetSimilarRoomsUseCase
{
    private const double KmPerDegreeLatitude = 110.574;
    private const double KmPerDegreeLongitudeAtEquator = 111.320;

    private readonly IRoomRepository _roomRepository;
    private readonly IRoomSearchReadModel _searchReadModel;
    private readonly ICampusRepository _campusRepository;

    public GetSimilarRoomsUseCase(
        IRoomRepository roomRepository,
        IRoomSearchReadModel searchReadModel,
        ICampusRepository campusRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _searchReadModel = searchReadModel ?? throw new ArgumentNullException(nameof(searchReadModel));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
    }

    public async Task<IReadOnlyList<RoomCardDto>?> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        // BR-CMP-01..03: Validate / resolve campus. Unknown campusId throws DomainValidationException (mapped to 400 CAMPUS_NOT_FOUND).
        var campus = await ResolveCampusAsync(campusId, ct).ConfigureAwait(false);

        var trimmedId = roomId.Trim();
        var targetRoom = await _roomRepository.GetByIdAsync(trimmedId, ct).ConfigureAwait(false);
        if (targetRoom == null || !targetRoom.IsPubliclyVisible)
        {
            return null;
        }

        var minRent = (long)Math.Round(targetRoom.MonthlyRentVnd * (1.0m - SimilarRoomsPolicy.PriceToleranceRatio));
        var maxRent = (long)Math.Round(targetRoom.MonthlyRentVnd * (1.0m + SimilarRoomsPolicy.PriceToleranceRatio));

        var deltaLat = SimilarRoomsPolicy.MaxDistanceKm / KmPerDegreeLatitude;
        var deltaLng = SimilarRoomsPolicy.MaxDistanceKm / (KmPerDegreeLongitudeAtEquator * Math.Cos(targetRoom.Latitude * Math.PI / 180.0));

        var filter = new RoomSearchQueryFilter(
            MinRentVnd: minRent,
            MaxRentVnd: maxRent,
            MinAreaM2: null,
            MaxAreaM2: null,
            RoomTypes: null,
            MinLat: targetRoom.Latitude - deltaLat,
            MaxLat: targetRoom.Latitude + deltaLat,
            MinLng: targetRoom.Longitude - deltaLng,
            MaxLng: targetRoom.Longitude + deltaLng);

        var candidates = await _searchReadModel.QueryCandidatesAsync(filter, ct).ConfigureAwait(false) ?? Array.Empty<RoomSearchCandidate>();

        var ranked = new List<(RoomSearchCandidate Candidate, double DistanceToTargetKm, double DistanceToCampusKm)>();
        foreach (var c in candidates)
        {
            if (c is null || string.Equals(c.Id, targetRoom.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (c.Status != ListingStatus.Available)
            {
                continue;
            }

            if (c.MonthlyRentVnd < minRent || c.MonthlyRentVnd > maxRent)
            {
                continue;
            }

            var distanceToTarget = HaversineDistanceCalculator.CalculateDistanceKm(
                targetRoom.Latitude, targetRoom.Longitude, c.Latitude, c.Longitude);

            if (!double.IsFinite(distanceToTarget) || distanceToTarget > SimilarRoomsPolicy.MaxDistanceKm)
            {
                continue;
            }

            var distanceToCampus = HaversineDistanceCalculator.CalculateDistanceKm(
                campus.Latitude, campus.Longitude, c.Latitude, c.Longitude);

            ranked.Add((c, distanceToTarget, distanceToCampus));
        }

        var sameType = ranked.Where(x => x.Candidate.RoomType == targetRoom.RoomType).ToList();
        var selected = sameType.Count >= SimilarRoomsPolicy.MinResultsBeforeRelaxingRoomType
            ? sameType
            : ranked;

        return selected
            .OrderBy(x => x.DistanceToTargetKm)
            .ThenBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .Take(SimilarRoomsPolicy.MaxSimilarRooms)
            .Select(x => new RoomCardDto(
                Id: x.Candidate.Id,
                Title: x.Candidate.Title,
                MonthlyRentVnd: x.Candidate.MonthlyRentVnd,
                AreaM2: x.Candidate.AreaM2,
                RoomType: x.Candidate.RoomType,
                DistanceKm: x.DistanceToCampusKm,
                IsVerified: x.Candidate.IsVerified,
                Status: x.Candidate.Status,
                CoverThumbUrl: x.Candidate.CoverThumbUrl,
                Latitude: x.Candidate.Latitude,
                Longitude: x.Candidate.Longitude,
                PublishedAtUtc: x.Candidate.PublishedAtUtc,
                IsFavourite: false
            ))
            .ToList();
    }

    private async Task<Campus> ResolveCampusAsync(string? campusId, CancellationToken ct)
    {
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
