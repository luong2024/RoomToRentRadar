using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Policies;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Use case for finding available similar rooms near an unavailable listing per docs/business-analysis.md BR-LST-10.
/// </summary>
public sealed class GetSimilarRoomsUseCase : IGetSimilarRoomsUseCase
{
    private const double KmPerDegreeLatitude = 110.574;
    private const double KmPerDegreeLongitudeAtEquator = 111.320;

    private readonly IRoomRepository _roomRepository;
    private readonly IRoomSearchReadModel _searchReadModel;

    public GetSimilarRoomsUseCase(IRoomRepository roomRepository, IRoomSearchReadModel searchReadModel)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _searchReadModel = searchReadModel ?? throw new ArgumentNullException(nameof(searchReadModel));
    }

    public async Task<IReadOnlyList<RoomCardDto>> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Array.Empty<RoomCardDto>();
        }

        var trimmedId = roomId.Trim();
        var targetRoom = await _roomRepository.GetByIdAsync(trimmedId, ct);
        if (targetRoom == null || !targetRoom.IsPubliclyVisible)
        {
            return Array.Empty<RoomCardDto>();
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

        var candidates = await _searchReadModel.QueryCandidatesAsync(filter, ct);

        var ranked = new List<(RoomSearchCandidate Candidate, double DistanceKm)>();
        foreach (var c in candidates)
        {
            if (string.Equals(c.Id, targetRoom.Id, StringComparison.OrdinalIgnoreCase))
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

            var distance = HaversineDistanceCalculator.CalculateDistanceKm(
                targetRoom.Latitude, targetRoom.Longitude, c.Latitude, c.Longitude);

            if (!double.IsFinite(distance) || distance > SimilarRoomsPolicy.MaxDistanceKm)
            {
                continue;
            }

            ranked.Add((c, Math.Round(distance, 1, MidpointRounding.AwayFromZero)));
        }

        var sameType = ranked.Where(x => x.Candidate.RoomType == targetRoom.RoomType).ToList();
        var selected = sameType.Count >= SimilarRoomsPolicy.MinResultsBeforeRelaxingRoomType
            ? sameType
            : ranked;

        return selected
            .OrderBy(x => x.DistanceKm)
            .ThenBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .Take(SimilarRoomsPolicy.MaxSimilarRooms)
            .Select(x => new RoomCardDto(
                Id: x.Candidate.Id,
                Title: x.Candidate.Title,
                MonthlyRentVnd: x.Candidate.MonthlyRentVnd,
                AreaM2: x.Candidate.AreaM2,
                RoomType: x.Candidate.RoomType,
                DistanceKm: x.DistanceKm,
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
}
