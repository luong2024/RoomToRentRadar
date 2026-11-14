namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;

/// <summary>
/// Implements the use case for browsing verified rental rooms near a campus (US01).
/// </summary>
public sealed class GetRoomListingsUseCase : IGetRoomListingsUseCase, IRoomListingCacheInvalidator
{
    // Reduced TTL from 10 minutes to 2 minutes for faster convergence with real-time updates
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private static readonly HashSet<string> AllowedSortBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "distance",
        "price",
        "area"
    };

    private static readonly HashSet<string> AllowedSortOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    private readonly IRoomListingRepository _roomListingRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly ICacheService _cacheService;

    public GetRoomListingsUseCase(
        IRoomListingRepository roomListingRepository,
        ICampusRepository campusRepository,
        ICacheService cacheService)
    {
        _roomListingRepository = roomListingRepository;
        _campusRepository = campusRepository;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<RoomListingDto>> ExecuteAsync(
        GetRoomListingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        // AC3: Cache-aside pattern with campus versioning for instant invalidation
        var normalizedSortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "distance" : query.SortBy.Trim().ToLowerInvariant();
        var normalizedSortOrder = string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder.Trim().ToLowerInvariant();
        var campusVersion = await GetCampusCacheVersionAsync(query.CampusId, cancellationToken);
        var cacheKey = $"rooms:campus:{query.CampusId}:v{campusVersion}:p{query.Page}:s{query.PageSize}:sort{normalizedSortBy}_{normalizedSortOrder}";

        var cachedResult = await _cacheService.GetAsync<PagedResult<RoomListingDto>>(cacheKey, cancellationToken);
        if (cachedResult is not null)
        {
            return cachedResult;
        }

        var campus = await _campusRepository.GetByIdAsync(query.CampusId, cancellationToken);
        if (campus is null)
        {
            throw new CampusNotFoundException(query.CampusId);
        }

        // AC4: Only listings with status Available and Verified
        var listingsPage = await _roomListingRepository.GetAvailableVerifiedRoomsByCampusAsync(
            query.CampusId,
            campus.Location,
            query.Page,
            query.PageSize,
            normalizedSortBy,
            normalizedSortOrder == "desc",
            cancellationToken);

        // Enforce domain rule filter in application layer for verification
        var eligibleListings = listingsPage.Items.Where(l => l.IsDisplayable).ToList();

        // AC1 & AC2: Map each card with title, monthly rent, area, distance to campus, verification badge
        var dtos = eligibleListings.Select(listing => new RoomListingDto(
            Id: listing.Id,
            Title: listing.Title,
            MonthlyRent: listing.MonthlyRent.Amount,
            Currency: listing.MonthlyRent.Currency,
            AreaSquareMeters: listing.AreaSquareMeters,
            DistanceToCampusKm: listing.Location.DistanceToKm(campus.Location),
            IsVerified: listing.VerificationStatus == VerificationStatus.Verified,
            Address: listing.Address,
            ThumbnailUrl: listing.ThumbnailUrl,
            CampusId: listing.CampusId));

        var pageItems = dtos.ToList();

        var result = new PagedResult<RoomListingDto>(pageItems, query.Page, query.PageSize, listingsPage.TotalCount);

        // Store in cache
        await _cacheService.SetAsync(cacheKey, result, CacheTtl, cancellationToken);

        return result;
    }

    /// <summary>
    /// Implements IRoomListingCacheInvalidator for outbound cache invalidation from write use cases.
    /// Instantly invalidates all cached listing pages for a given campus by incrementing its version counter.
    /// </summary>
    public async Task InvalidateCampusAsync(string campusId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campusId))
        {
            return;
        }

        var versionKey = $"rooms:campus:{campusId}:version";
        var currentVersionStr = await _cacheService.GetAsync<string>(versionKey, cancellationToken);
        var nextVersion = int.TryParse(currentVersionStr, out var v) ? v + 1 : 2;
        await _cacheService.SetAsync(versionKey, nextVersion.ToString(), TimeSpan.FromDays(7), cancellationToken);
    }

    private async Task<string> GetCampusCacheVersionAsync(string campusId, CancellationToken cancellationToken)
    {
        var versionKey = $"rooms:campus:{campusId}:version";
        var version = await _cacheService.GetAsync<string>(versionKey, cancellationToken);
        if (string.IsNullOrEmpty(version))
        {
            version = "1";
            await _cacheService.SetAsync(versionKey, version, TimeSpan.FromDays(7), cancellationToken);
        }
        return version;
    }

    private static void ValidateQuery(GetRoomListingsQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.CampusId))
        {
            throw new ValidationException("CampusId", "Campus ID is required.");
        }

        // Bound page range to prevent integer overflow and runaway OFFSET calculations
        if (query.Page is < 1 or > 100000)
        {
            throw new ValidationException("Page", "Page number must be between 1 and 100,000.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ValidationException("PageSize", "Page size must be between 1 and 100.");
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !AllowedSortBy.Contains(query.SortBy.Trim()))
        {
            throw new ValidationException("SortBy", "SortBy must be one of: distance, price, area.");
        }

        if (!string.IsNullOrWhiteSpace(query.SortOrder) && !AllowedSortOrder.Contains(query.SortOrder.Trim()))
        {
            throw new ValidationException("SortOrder", "SortOrder must be either 'asc' or 'desc'.");
        }
    }
}
