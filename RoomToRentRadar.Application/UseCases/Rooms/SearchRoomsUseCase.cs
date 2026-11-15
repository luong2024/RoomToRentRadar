using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// UC-05 (Search and Filter Listings) / <c>operationId: searchRooms</c> / BR-SRCH-01..06, BR-CMP-01..03.
/// Order: validate → resolve campus → build filter + bounding box → read model → Haversine → exact
/// distance filter → sort → paging → map. Only the CQRS read model is used (M2 / ADR-006).
/// </summary>
public sealed class SearchRoomsUseCase : ISearchRoomsUseCase
{
    // ADR-006 bounding box: km per degree of latitude / longitude at the equator.
    private const double KmPerDegreeLatitude = 110.574;
    private const double KmPerDegreeLongitudeAtEquator = 111.320;

    private readonly ICampusRepository _campusRepository;
    private readonly IRoomSearchReadModel _readModel;

    public SearchRoomsUseCase(ICampusRepository campusRepository, IRoomSearchReadModel readModel)
    {
        ArgumentNullException.ThrowIfNull(campusRepository);
        ArgumentNullException.ThrowIfNull(readModel);
        _campusRepository = campusRepository;
        _readModel = readModel;
    }

    public async Task<PagedResult<RoomCardDto>> ExecuteAsync(RoomSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // BR-SRCH-02: whitelist validation must run before any repository/read model call.
        var sort = NormalizeSort(request.Sort);
        ValidateRequest(request);

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = RequestedPageSize(request.PageSize);

        var campus = await ResolveCampusAsync(request.CampusId, ct).ConfigureAwait(false);
        var originLatitude = campus.Latitude;
        var originLongitude = campus.Longitude;

        var filter = BuildQueryFilter(request, originLatitude, originLongitude);
        var candidates = await _readModel.QueryCandidatesAsync(filter, ct).ConfigureAwait(false) ?? Array.Empty<RoomSearchCandidate>();

        var matches = new List<RankedRoom>();
        foreach (var candidate in candidates)
        {
            // Defence in depth: the read model filters on status, the use case never trusts that.
            if (candidate is null || candidate.Status != ListingStatus.Available)
            {
                continue;
            }

            if (request.RoomTypes is { Count: > 0 } roomTypes && !roomTypes.Contains(candidate.RoomType))
            {
                continue;
            }

            var distanceKm = HaversineDistanceCalculator.CalculateDistanceKm(
                originLatitude, originLongitude, candidate.Latitude, candidate.Longitude);

            // BR-SRCH-03 / ADR-006: non-finite coordinates cannot be located, so they are dropped;
            // the bounding box is only a pre-filter, this is the exact cut.
            if (!double.IsFinite(distanceKm))
            {
                continue;
            }

            if (request.MaxDistanceKm.HasValue && distanceKm > request.MaxDistanceKm.Value)
            {
                continue;
            }

            matches.Add(new RankedRoom(candidate, distanceKm));
        }

        var ordered = SortMatches(matches, sort);
        var totalCount = ordered.Count;
        var items = BuildPage(ordered, pageNumber, pageSize);

        return new PagedResult<RoomCardDto>(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Ruling 21: only a missing (<c>null</c>) sort means the default. Any provided value is trimmed and
    /// then matched exactly, case-sensitively (lower case), so <c>"DISTANCE"</c>, <c>""</c> or blanks are invalid.
    /// </summary>
    /// <remarks>
    /// Do not "clean this up" into <c>string.IsNullOrWhiteSpace(sort)</c>: per ruling 21 only <c>null</c> is the
    /// default, while <c>""</c> / whitespace-only is invalid input (<c>INVALID_SORT_OPTION</c>). Using
    /// IsNullOrWhiteSpace would silently turn those invalid values into a default sort and break the ruling.
    /// </remarks>
    private static string NormalizeSort(string? sort)
    {
        if (sort is null)
        {
            return RoomSearchRequest.DefaultSort;
        }

        var normalized = sort.Trim();
        if (!RoomSearchRequest.AllowedSorts.Contains(normalized, StringComparer.Ordinal))
        {
            // BR-SRCH-02 → HTTP 400
            throw new DomainValidationException(
                "Invalid sort option. Allowed values: distance, price_asc, price_desc, newest.",
                "INVALID_SORT_OPTION");
        }

        return normalized;
    }

    private static void ValidateRequest(RoomSearchRequest request)
    {
        ValidateRentRange(request.MinRentVnd, request.MaxRentVnd);
        ValidateAreaRange(request.MinAreaM2, request.MaxAreaM2);
        ValidateDistance(request.MaxDistanceKm);
    }

    private static void ValidateRentRange(long? minRentVnd, long? maxRentVnd)
    {
        if (minRentVnd is < 0 || maxRentVnd is < 0)
        {
            throw new DomainValidationException("Rent filters cannot be negative.", "INVALID_PRICE_RANGE");
        }

        if (minRentVnd.HasValue && maxRentVnd.HasValue && minRentVnd.Value > maxRentVnd.Value)
        {
            throw new DomainValidationException("MinRentVnd cannot be greater than MaxRentVnd.", "INVALID_PRICE_RANGE");
        }
    }

    private static void ValidateAreaRange(double? minAreaM2, double? maxAreaM2)
    {
        if ((minAreaM2.HasValue && !IsValidArea(minAreaM2.Value)) ||
            (maxAreaM2.HasValue && !IsValidArea(maxAreaM2.Value)))
        {
            throw new DomainValidationException(
                "Area filters must be finite numbers greater than or equal to 0.", "INVALID_AREA_RANGE");
        }

        if (minAreaM2.HasValue && maxAreaM2.HasValue && minAreaM2.Value > maxAreaM2.Value)
        {
            throw new DomainValidationException("MinAreaM2 cannot be greater than MaxAreaM2.", "INVALID_AREA_RANGE");
        }
    }

    private static bool IsValidArea(double areaM2) => double.IsFinite(areaM2) && areaM2 >= 0;

    private static void ValidateDistance(double? maxDistanceKm)
    {
        if (!maxDistanceKm.HasValue)
        {
            return;
        }

        var distance = maxDistanceKm.Value;
        if (!double.IsFinite(distance) ||
            distance < RoomSearchRequest.MinDistanceKm ||
            distance > RoomSearchRequest.MaxDistanceKmAllowed)
        {
            throw new DomainValidationException(
                $"MaxDistanceKm must be between {RoomSearchRequest.MinDistanceKm} and {RoomSearchRequest.MaxDistanceKmAllowed} km.",
                "INVALID_DISTANCE_RANGE");
        }
    }

    /// <summary>
    /// Ruling 21: an explicit page size is clamped to <c>1..MaxPageSize</c>; the default (20) only applies
    /// when the caller omits the value (record default of <see cref="RoomSearchRequest"/>).
    /// </summary>
    private static int RequestedPageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            return 1;
        }

        return pageSize > RoomSearchRequest.MaxPageSize ? RoomSearchRequest.MaxPageSize : pageSize;
    }

    private async Task<Campus> ResolveCampusAsync(string? campusId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(campusId))
        {
            var trimmedId = campusId.Trim();
            var campus = await _campusRepository.GetByIdAsync(trimmedId, ct).ConfigureAwait(false);
            return campus ?? throw new DomainValidationException(
                $"Campus with ID '{trimmedId}' not found.", "CAMPUS_NOT_FOUND"); // BR-CMP-01..03 → HTTP 400
        }

        var defaultCampus = await _campusRepository.GetDefaultCampusAsync(ct).ConfigureAwait(false);
        return defaultCampus ?? throw new DomainValidationException(
            "Default campus is not configured.", "CAMPUS_NOT_FOUND");
    }

    private static RoomSearchQueryFilter BuildQueryFilter(RoomSearchRequest request, double originLatitude, double originLongitude)
    {
        double? minLat = null;
        double? maxLat = null;
        double? minLng = null;
        double? maxLng = null;

        if (request.MaxDistanceKm.HasValue)
        {
            var maxDistanceKm = request.MaxDistanceKm.Value;
            var deltaLat = maxDistanceKm / KmPerDegreeLatitude;
            var deltaLng = maxDistanceKm / (KmPerDegreeLongitudeAtEquator * Math.Cos(originLatitude * Math.PI / 180.0));

            minLat = originLatitude - deltaLat;
            maxLat = originLatitude + deltaLat;
            minLng = originLongitude - deltaLng;
            maxLng = originLongitude + deltaLng;
        }

        // Null/empty room type list means "no filter".
        var roomTypes = request.RoomTypes is { Count: > 0 } types ? types.ToArray() : null;

        return new RoomSearchQueryFilter(
            request.MinRentVnd,
            request.MaxRentVnd,
            request.MinAreaM2,
            request.MaxAreaM2,
            roomTypes,
            minLat,
            maxLat,
            minLng,
            maxLng);
    }

    /// <summary>BR-SRCH-04: primary key per sort option, ties always broken by <c>Id</c> (ordinal).</summary>
    private static IReadOnlyList<RankedRoom> SortMatches(List<RankedRoom> matches, string sort)
    {
        IOrderedEnumerable<RankedRoom> ordered = sort switch
        {
            "price_asc" => matches.OrderBy(match => match.Candidate.MonthlyRentVnd, Comparer<long>.Default),
            "price_desc" => matches.OrderByDescending(match => match.Candidate.MonthlyRentVnd, Comparer<long>.Default),
            "newest" => matches.OrderByDescending(match => match.Candidate.PublishedAtUtc ?? DateTime.MinValue),
            _ => matches.OrderBy(match => match.DistanceKm)
        };

        return ordered
            .ThenBy(match => match.Candidate.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<RoomCardDto> BuildPage(IReadOnlyList<RankedRoom> ordered, int pageNumber, int pageSize)
    {
        // long arithmetic so huge page numbers cannot overflow int. pageNumber is already clamped to
        // >= 1 before this call, so (pageNumber - 1) >= 0 and (long)(pageNumber - 1) * pageSize never overflows.
        var skip = (long)(pageNumber - 1) * pageSize;
        var count = ordered.Count;
        if (skip >= count)
        {
            return Array.Empty<RoomCardDto>();
        }

        var items = new List<RoomCardDto>(pageSize);
        // Comparing long index against long count keeps the loop condition readable; the (int) cast is only
        // reached when index < count, so it is always within the safe int range of the list.
        for (var index = skip; index < count && items.Count < pageSize; index++)
        {
            items.Add(ToCard(ordered[(int)index]));
        }

        return items;
    }

    private static RoomCardDto ToCard(RankedRoom match) => new(
        Id: match.Candidate.Id,
        Title: match.Candidate.Title,
        MonthlyRentVnd: match.Candidate.MonthlyRentVnd,
        AreaM2: match.Candidate.AreaM2,
        RoomType: match.Candidate.RoomType,
        DistanceKm: match.DistanceKm,
        IsVerified: match.Candidate.IsVerified,
        Status: match.Candidate.Status,
        CoverThumbUrl: match.Candidate.CoverThumbUrl,
        Latitude: match.Candidate.Latitude,
        Longitude: match.Candidate.Longitude,
        PublishedAtUtc: match.Candidate.PublishedAtUtc,
        IsFavourite: false); // anonymous search: US04, favourites are added client-side by US06

    private readonly record struct RankedRoom(RoomSearchCandidate Candidate, double DistanceKm);
}
