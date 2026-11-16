using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Infrastructure.Api;
using System.Diagnostics;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Fetches paginated room listings from the backend's verified ICTU campus catalog.
/// </summary>
public sealed class RoomListingsService : IRoomListingsService
{
    private readonly IApiClient _apiClient;
    private readonly bool _enableDebugFallback;
    private const string IctuCampusId = "campus-ictu";

    public RoomListingsService(IApiClient apiClient, bool? enableDebugFallback = null)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
#if DEBUG
        _enableDebugFallback = enableDebugFallback ?? true;
#else
        _enableDebugFallback = enableDebugFallback ?? false;
#endif
    }

    public async Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
        int page,
        int pageSize,
        RoomSortOption sortBy = RoomSortOption.Newest,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

#if DEBUG
        // Android surfaces a failed socket as a JavaProxyThrowable before the awaited
        // request can be handled. Keep the listings screen fully usable while debugging
        // without a reachable local Cosmos-backed API.
        if (_enableDebugFallback && Debugger.IsAttached)
        {
            return GetFallbackMockPage(page, pageSize, sortBy, search);
        }
#endif

        try
        {
            var (sortByQuery, sortOrderQuery) = GetSortQuery(sortBy);
            var endpoint = $"api/v1/rooms?campusId={IctuCampusId}&page={page}&pageSize={pageSize}&sortBy={sortByQuery}&sortOrder={sortOrderQuery}";
            var response = await _apiClient.GetAsync<PagedResponse<RoomListingApiItem>>(endpoint, cancellationToken);

            if (response is null)
            {
                return new PagedResponse<RoomListItemModel>([], page, pageSize, 0);
            }

            var items = response.Items.Select(MapListing).ToList();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                items = items.Where(item => item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                            item.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
                             .ToList();
            }

            return new PagedResponse<RoomListItemModel>(items, response.Page, response.PageSize, response.TotalCount);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            ex is HttpRequestException or TaskCanceledException)
        {
            // The screen remains usable during local development when the API or Cosmos emulator is offline.
            return GetFallbackMockPage(page, pageSize, sortBy, search);
        }
    }

    public async Task<RoomListItemModel?> GetRoomByIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        var trimmedId = roomId.Trim();
        var endpoint = $"api/rooms/{Uri.EscapeDataString(trimmedId)}";

#if DEBUG
        if (_enableDebugFallback && Debugger.IsAttached)
        {
            return GetFallbackRoomById(trimmedId);
        }
#endif

        try
        {
            return await _apiClient.GetAsync<RoomListItemModel>(endpoint, cancellationToken);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            ex is HttpRequestException or TaskCanceledException)
        {
            return GetFallbackRoomById(trimmedId);
        }
    }

    private static (string SortBy, string SortOrder) GetSortQuery(RoomSortOption sortBy) =>
        sortBy switch
        {
            RoomSortOption.PriceAsc => ("price", "asc"),
            RoomSortOption.PriceDesc => ("price", "desc"),
            RoomSortOption.DistanceAsc => ("distance", "asc"),
            _ => ("distance", "asc")
        };

    private static RoomListItemModel MapListing(RoomListingApiItem listing) => new()
    {
        Id = listing.Id,
        Title = listing.Title,
        Price = listing.MonthlyRent,
        AreaSqm = listing.AreaSquareMeters,
        Address = listing.Address,
        DistanceToCampusKm = listing.DistanceToCampusKm,
        ThumbnailUrl = listing.ThumbnailUrl,
        IsVerified = listing.IsVerified,
        Amenities = []
    };


    private static PagedResponse<RoomListItemModel> GetFallbackMockPage(
        int page,
        int pageSize,
        RoomSortOption sortBy,
        string? searchTerm)
    {
        IEnumerable<RoomListItemModel> query = MockRooms;

        // Filter the full mock catalog before paging, mirroring server-side search semantics.
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r => MatchesSearch(r, searchTerm));
        }

        query = sortBy switch
        {
            RoomSortOption.PriceAsc => query.OrderBy(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.PriceDesc => query.OrderByDescending(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.DistanceAsc => query.OrderBy(r => r.DistanceToCampusKm).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var ordered = query.ToList();
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResponse<RoomListItemModel>(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: ordered.Count);
    }

    private static RoomListItemModel? GetFallbackRoomById(string roomId) =>
        MockRooms.FirstOrDefault(room => string.Equals(room.Id, roomId, StringComparison.Ordinal));

    private static bool MatchesSearch(RoomListItemModel room, string term)
    {
        if (room.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            room.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return room.Amenities?.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static readonly IReadOnlyList<RoomListItemModel> MockRooms =
    [
        new()
        {
            Id = "room-ictu-001",
            Title = "Newly built ensuite room near the ICTU main gate",
            Price = 1_800_000m,
            AreaSqm = 22,
            Address = "Lane 12, Z115 Road, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.3,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Air conditioning", "Water heater", "Flexible hours", "Fingerprint lock"]
        },
        new()
        {
            Id = "room-ictu-002",
            Title = "Bright balcony studio in the Quyet Thang staff area",
            Price = 2_300_000m,
            AreaSqm = 28,
            Address = "Z115 Road, Goc Mit Hamlet, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.6,
            ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 9, 14, 30, 0, TimeSpan.FromHours(7)),
            Amenities = ["Furnished", "Balcony", "Shared washer"]
        },
        new()
        {
            Id = "room-ictu-003",
            Title = "Budget student room with a spacious loft",
            Price = 1_200_000m,
            AreaSqm = 18,
            Address = "Tan Duc Hamlet, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.8,
            ThumbnailUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 9, 9, 15, 0, TimeSpan.FromHours(7)),
            Amenities = ["Loft", "Water heater", "Spacious parking"]
        },
        new()
        {
            Id = "room-ictu-004",
            Title = "One-bedroom mini apartment near Diem Hen junction",
            Price = 2_800_000m,
            AreaSqm = 32,
            Address = "Tan Thinh Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.2,
            ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 8, 18, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Air conditioning", "Refrigerator", "Separate kitchen"]
        },
        new()
        {
            Id = "room-ictu-005",
            Title = "Secure room with 24/7 cameras behind the ICTU dormitory",
            Price = 1_500_000m,
            AreaSqm = 20,
            Address = "Lane 45, Z115 Road, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.4,
            ThumbnailUrl = "https://images.unsplash.com/photo-1554995207-c18c203602cb?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["24/7 cameras", "Water heater", "High-speed Wi-Fi"]
        },
        new()
        {
            Id = "room-ictu-006",
            Title = "Spacious shared room for 2–3 people near ICTU",
            Price = 2_000_000m,
            AreaSqm = 26,
            Address = "Group 5, Tan Thinh Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 7, 16, 45, 0, TimeSpan.FromHours(7)),
            Amenities = ["Two beds", "Air conditioning", "Double study desk"]
        },
        new()
        {
            Id = "room-ictu-007",
            Title = "Bright second-floor ensuite room with residential utility rates",
            Price = 1_650_000m,
            AreaSqm = 21,
            Address = "Z115 Road, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1513694203232-719a280e022f?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 7, 8, 20, 0, TimeSpan.FromHours(7)),
            Amenities = ["Residential utilities", "Water heater", "Large windows"]
        },
        new()
        {
            Id = "room-ictu-008",
            Title = "Freshly renovated room, 300m from the ICTU side gate",
            Price = 1_400_000m,
            AreaSqm = 19,
            Address = "Hong Thai Hamlet, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.3,
            ThumbnailUrl = "https://images.unsplash.com/photo-1540518614846-7ede433c4573?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Bed and wardrobe", "Water heater", "No live-in landlord"]
        },
        new()
        {
            Id = "room-ictu-009",
            Title = "Premium studio with elevator near Tan Thinh lake",
            Price = 2_600_000m,
            AreaSqm = 30,
            Address = "Group 8, Tan Thinh Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.4,
            ThumbnailUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 5, 11, 30, 0, TimeSpan.FromHours(7)),
            Amenities = ["Elevator", "Air conditioning", "Refrigerator", "Keycard lock"]
        },
        new()
        {
            Id = "room-ictu-010",
            Title = "Quiet single room for final-year students",
            Price = 1_350_000m,
            AreaSqm = 17,
            Address = "Lane 88, Z115 Road, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.7,
            ThumbnailUrl = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 4, 19, 10, 0, TimeSpan.FromHours(7)),
            Amenities = ["Quiet area", "Work desk", "Water heater"]
        },
        new()
        {
            Id = "room-ictu-011",
            Title = "Room with a private drying area and kitchen shelf",
            Price = 1_750_000m,
            AreaSqm = 23,
            Address = "Extended Quang Trung Road, Thinh Dan Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.9,
            ThumbnailUrl = "https://images.unsplash.com/photo-1536376072261-38c75010e6c9?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 3, 13, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Private kitchen", "Drying area", "Air conditioning"]
        },
        new()
        {
            Id = "room-ictu-012",
            Title = "Ensuite room with AC and furniture near the student market",
            Price = 1_900_000m,
            AreaSqm = 24,
            Address = "Z115 Student Market, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Air conditioning", "Water heater", "Near market and bus stop"]
        },
        new()
        {
            Id = "room-ictu-013",
            Title = "Affordable room for new ICTU students with free parking",
            Price = 1_100_000m,
            AreaSqm = 16,
            Address = "Goc Mit Hamlet, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.9,
            ThumbnailUrl = "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Free parking", "Wi-Fi", "Private bathroom"]
        },
        new()
        {
            Id = "room-ictu-014",
            Title = "Two-bed serviced apartment near ICTU",
            Price = 2_450_000m,
            AreaSqm = 29,
            Address = "Tan Thinh Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.6,
            ThumbnailUrl = "https://images.unsplash.com/photo-1560185127-6ed189bf02f4?w=600&q=80",
            IsVerified = false,
            CreatedAt = new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.FromHours(7)),
            Amenities = ["Furnished", "Washer", "Fingerprint lock"]
        }
    ];
}

internal sealed class RoomListingApiItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal MonthlyRent { get; init; }
    public double AreaSquareMeters { get; init; }
    public double DistanceToCampusKm { get; init; }
    public bool IsVerified { get; init; }
    public string Address { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
}
