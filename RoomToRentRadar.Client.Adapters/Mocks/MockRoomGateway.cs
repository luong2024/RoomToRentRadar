using System.Net;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks.SampleData;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Client.Adapters.Mocks;

/// <summary>
/// Deterministic in-memory <see cref="IRoomGateway"/> for Debug builds (PLAN Q6/Q11, AC-4).
/// SearchRoomsAsync mirrors SearchRoomsUseCase (validation, campus resolution, filters, the four
/// sorts with Id tie-break, paging) over the seed-mirroring SampleRooms, so mock and real API
/// results are identical — guarded by MockSeedDriftTests and MockVsRealApiParityTests.
/// </summary>
public sealed class MockRoomGateway : IRoomGateway
{
    private const double MotorbikeSpeedKmPerHour = 30.0;
    private const double WalkingSpeedKmPerHour = 5.0;

    public Task<PagedResult<RoomCardDto>> SearchRoomsAsync(RoomSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sort = NormalizeSort(request.Sort);
        ValidateFilters(request.MinRentVnd, request.MaxRentVnd, request.MinAreaM2, request.MaxAreaM2, request.MaxDistanceKm);
        var campus = ResolveCampus(request.CampusId);
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = Math.Clamp(request.PageSize, 1, RoomSearchRequest.MaxPageSize);

        var ranked = RankRooms(campus, request, sort);
        var items = BuildPage(ranked, pageNumber, pageSize);
        return Task.FromResult(new PagedResult<RoomCardDto>(items, ranked.Count, pageNumber, pageSize));
    }

    public Task<AreaSearchResultDto> SearchRoomsInAreaAsync(AreaSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sort = NormalizeSort(request.Sort);
        ValidateFilters(request.MinRentVnd, request.MaxRentVnd, request.MinAreaM2, request.MaxAreaM2, request.MaxDistanceKm);
        var campus = ResolveCampus(request.CampusId);
        var criteria = new RoomSearchRequest(
            request.CampusId, request.MinRentVnd, request.MaxRentVnd, request.MinAreaM2, request.MaxAreaM2,
            request.RoomTypes, request.MaxDistanceKm, sort);
        var items = RankRooms(campus, criteria, sort)
            .Where(match => Contains(request.Polygon, match.Room.Latitude, match.Room.Longitude))
            .Select(ToCard)
            .ToList();
        return Task.FromResult(new AreaSearchResultDto(items, items.Count, false));
    }

    public Task<ComparisonDto> CompareRoomsAsync(IReadOnlyList<string> roomIds, string? campusId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomIds);
        var campus = ResolveCampus(campusId);
        var rooms = roomIds.Select(id =>
        {
            var room = FindRoom(id);
            return new CompareRoomDto(
                room.Id, room.Title, null, ListingStatus.Available, room.MonthlyRentVnd, room.DepositVnd,
                room.AreaM2, room.RoomType, Distance(campus, room), AmenitiesFor(room),
                SafetyScore(room), room.IsSafetyVerified, EstimatedMonthlyTotal(room));
        }).ToList();
        return Task.FromResult(new ComparisonDto(campus.Id, rooms));
    }

    public Task<RoomDetailDto> GetRoomDetailAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        var room = FindRoom(roomId);
        var campus = ResolveCampus(campusId);
        var detail = new RoomDetailDto(
            room.Id, room.Title, room.Description, room.RoomType, ListingStatus.Available, true,
            room.MonthlyRentVnd, room.DepositVnd, room.AreaM2, room.MaxOccupants, room.Furnishing, room.AvailableFrom,
            new AddressDto(room.AddressLine, room.Ward, room.District),
            new GeoPointDto(room.Latitude, room.Longitude),
            Distance(campus, room),
            new UtilitiesView(
                room.ElectricityPriceVndPerKwh, room.WaterPriceVnd, room.WaterPriceUnit,
                room.InternetFeeVnd, room.ParkingFeeVnd, room.OtherFeesNote, EstimatedMonthlyTotal(room)),
            AmenitiesFor(room),
            new SafetyDto(SafetyScore(room), room.IsSafetyVerified,
                [SafetyFeature.Cctv, SafetyFeature.SecureEntrance, SafetyFeature.WellLitAccess]),
            null,
            Array.Empty<PhotoDto>(),
            new LandlordContactDto("seed-landlord", "Seed Landlord", true,
                new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc), null, null),
            false,
            room.PublishedAtUtc,
            room.LastUpdatedAtUtc);
        return Task.FromResult(detail);
    }

    public Task<IReadOnlyList<RoomCardDto>> GetSimilarRoomsAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        var room = FindRoom(roomId);
        var campus = ResolveCampus(campusId);
        var similar = SampleRooms.All
            .Where(candidate => candidate.Id != room.Id)
            .Select(candidate => new RankedRoom(candidate, Distance(campus, candidate)))
            .OrderBy(match => match.Room.RoomType == room.RoomType ? 0 : 1)
            .ThenBy(match => match.DistanceKm)
            .ThenBy(match => match.Room.Id, StringComparer.Ordinal)
            .Take(4)
            .Select(ToCard)
            .ToList();
        return Task.FromResult<IReadOnlyList<RoomCardDto>>(similar);
    }

    public Task<TravelTimesDto> GetRoomTravelTimesAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        var room = FindRoom(roomId);
        var campus = ResolveCampus(campusId);
        var distanceKm = Distance(campus, room);
        var distanceMeters = (int)Math.Round(distanceKm * 1000);
        var items = new List<TravelTimeItemDto>
        {
            new(TravelMode.Motorbike, distanceMeters, (int)Math.Round(distanceKm / MotorbikeSpeedKmPerHour * 3600), true),
            new(TravelMode.Walking, distanceMeters, (int)Math.Round(distanceKm / WalkingSpeedKmPerHour * 3600), true)
        };
        return Task.FromResult(new TravelTimesDto(campus.Id, distanceKm, true, items));
    }

    private static List<RankedRoom> RankRooms(CampusDto campus, RoomSearchRequest request, string sort)
    {
        var matches = new List<RankedRoom>();
        foreach (var room in SampleRooms.All)
        {
            if (request.MinRentVnd is { } minRent && room.MonthlyRentVnd < minRent)
            {
                continue;
            }

            if (request.MaxRentVnd is { } maxRent && room.MonthlyRentVnd > maxRent)
            {
                continue;
            }

            if (request.MinAreaM2 is { } minArea && room.AreaM2 < minArea)
            {
                continue;
            }

            if (request.MaxAreaM2 is { } maxArea && room.AreaM2 > maxArea)
            {
                continue;
            }

            if (request.RoomTypes is { Count: > 0 } roomTypes && !roomTypes.Contains(room.RoomType))
            {
                continue;
            }

            var distanceKm = Distance(campus, room);
            if (!double.IsFinite(distanceKm))
            {
                continue;
            }

            if (request.MaxDistanceKm is { } maxDistance && distanceKm > maxDistance)
            {
                continue;
            }

            matches.Add(new RankedRoom(room, distanceKm));
        }

        IOrderedEnumerable<RankedRoom> ordered = sort switch
        {
            "price_asc" => matches.OrderBy(match => match.Room.MonthlyRentVnd),
            "price_desc" => matches.OrderByDescending(match => match.Room.MonthlyRentVnd),
            "newest" => matches.OrderByDescending(match => match.Room.PublishedAtUtc),
            _ => matches.OrderBy(match => match.DistanceKm)
        };
        return ordered.ThenBy(match => match.Room.Id, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<RoomCardDto> BuildPage(List<RankedRoom> ordered, int pageNumber, int pageSize)
    {
        var skip = (long)(pageNumber - 1) * pageSize;
        if (skip >= ordered.Count)
        {
            return Array.Empty<RoomCardDto>();
        }

        return ordered.Skip((int)skip).Take(pageSize).Select(ToCard).ToList();
    }

    private static RoomCardDto ToCard(RankedRoom match) => new(
        match.Room.Id, match.Room.Title, match.Room.MonthlyRentVnd, match.Room.AreaM2, match.Room.RoomType,
        match.DistanceKm, true, ListingStatus.Available, null, match.Room.Latitude, match.Room.Longitude,
        match.Room.PublishedAtUtc, false);

    // Ruling 21: only null means the default sort; "" / blanks / wrong case are INVALID_SORT_OPTION.
    private static string NormalizeSort(string? sort)
    {
        if (sort is null)
        {
            return RoomSearchRequest.DefaultSort;
        }

        var normalized = sort.Trim();
        if (!RoomSearchRequest.AllowedSorts.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ApiException(
                "Invalid sort option. Allowed values: distance, price_asc, price_desc, newest.",
                HttpStatusCode.BadRequest, ProblemCodes.InvalidSortOption);
        }

        return normalized;
    }

    private static void ValidateFilters(long? minRent, long? maxRent, double? minArea, double? maxArea, double? maxDistance)
    {
        if (minRent is < 0 || maxRent is < 0)
        {
            throw new ApiException("Rent filters cannot be negative.", HttpStatusCode.BadRequest, ProblemCodes.InvalidPriceRange);
        }

        if (minRent.HasValue && maxRent.HasValue && minRent.Value > maxRent.Value)
        {
            throw new ApiException("MinRentVnd cannot be greater than MaxRentVnd.", HttpStatusCode.BadRequest, ProblemCodes.InvalidPriceRange);
        }

        if ((minArea.HasValue && (!double.IsFinite(minArea.Value) || minArea.Value < 0))
            || (maxArea.HasValue && (!double.IsFinite(maxArea.Value) || maxArea.Value < 0)))
        {
            throw new ApiException("Area filters must be finite numbers greater than or equal to 0.", HttpStatusCode.BadRequest, ProblemCodes.InvalidAreaRange);
        }

        if (minArea.HasValue && maxArea.HasValue && minArea.Value > maxArea.Value)
        {
            throw new ApiException("MinAreaM2 cannot be greater than MaxAreaM2.", HttpStatusCode.BadRequest, ProblemCodes.InvalidAreaRange);
        }

        if (maxDistance.HasValue
            && (!double.IsFinite(maxDistance.Value)
                || maxDistance.Value < RoomSearchRequest.MinDistanceKm
                || maxDistance.Value > RoomSearchRequest.MaxDistanceKmAllowed))
        {
            throw new ApiException(
                $"MaxDistanceKm must be between {RoomSearchRequest.MinDistanceKm} and {RoomSearchRequest.MaxDistanceKmAllowed} km.",
                HttpStatusCode.BadRequest, ProblemCodes.InvalidDistanceRange);
        }
    }

    private static CampusDto ResolveCampus(string? campusId)
    {
        if (string.IsNullOrWhiteSpace(campusId))
        {
            return SampleCampuses.Default;
        }

        var trimmed = campusId.Trim();
        return SampleCampuses.Find(trimmed)
            ?? throw new ApiException($"Campus with ID '{trimmed}' not found.", HttpStatusCode.BadRequest, ProblemCodes.CampusNotFound);
    }

    private static SampleRoom FindRoom(string roomId) =>
        SampleRooms.Find(roomId)
            ?? throw new ApiException($"Room with ID '{roomId}' not found.", HttpStatusCode.NotFound, ProblemCodes.RoomNotFound);

    private static double Distance(CampusDto campus, SampleRoom room) =>
        HaversineDistanceCalculator.CalculateDistanceKm(campus.Latitude, campus.Longitude, room.Latitude, room.Longitude);

    private static IReadOnlyList<Amenity> AmenitiesFor(SampleRoom room)
    {
        var amenities = new List<Amenity>
        {
            Amenity.Wifi, Amenity.Bed, Amenity.Wardrobe, Amenity.Desk, Amenity.Window, Amenity.MotorbikeParking
        };
        if (room.Furnishing == Furnishing.Full)
        {
            amenities.AddRange([Amenity.AirConditioner, Amenity.WaterHeater, Amenity.Fridge]);
        }

        return amenities;
    }

    private static int SafetyScore(SampleRoom room) => room.IsSafetyVerified ? 80 : 60;

    // Fixed mock estimate: rent plus the flat monthly fees; metered usage is not simulated.
    private static long EstimatedMonthlyTotal(SampleRoom room) =>
        room.MonthlyRentVnd + room.InternetFeeVnd + room.ParkingFeeVnd + room.WaterPriceVnd;

    private static bool Contains(IReadOnlyList<GeoPointDto> polygon, double latitude, double longitude)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var current = polygon[i];
            var previous = polygon[j];
            if (current.Longitude > longitude != previous.Longitude > longitude
                && latitude < (previous.Latitude - current.Latitude) * (longitude - current.Longitude)
                    / (previous.Longitude - current.Longitude) + current.Latitude)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private readonly record struct RankedRoom(SampleRoom Room, double DistanceKm);
}