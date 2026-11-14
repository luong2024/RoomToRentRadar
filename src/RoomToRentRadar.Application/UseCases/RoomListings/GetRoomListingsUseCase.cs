namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;

/// <summary>
/// Implements the use case for browsing verified rental rooms near a campus (US01).
/// </summary>
public sealed class GetRoomListingsUseCase : IGetRoomListingsUseCase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

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

        // AC3: Cache-aside pattern for read-heavy listing query
        var normalizedSortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "distance" : query.SortBy.Trim().ToLowerInvariant();
        var normalizedSortOrder = string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder.Trim().ToLowerInvariant();
        var cacheKey = $"rooms:campus:{query.CampusId}:p{query.Page}:s{query.PageSize}:sort{normalizedSortBy}_{normalizedSortOrder}";

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

        // AC1: Sort by distance from the selected campus by default
        var pageItems = dtos.ToList();
        var result = new PagedResult<RoomListingDto>(pageItems, query.Page, query.PageSize, listingsPage.TotalCount);

        // Store in cache
        await _cacheService.SetAsync(cacheKey, result, CacheTtl, cancellationToken);

        return result;
    }

    private static void ValidateQuery(GetRoomListingsQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.CampusId))
        {
            throw new ValidationException("CampusId", "Campus ID is required.");
        }

        if (query.Page < 1)
        {
            throw new ValidationException("Page", "Page number must be at least 1.");
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
