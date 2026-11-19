using System.Globalization;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Fetches paginated room listings via <see cref="IApiClient"/>, with a DEBUG-only fallback
/// to realistic ICTU campus room data when the backend API is unreachable.
/// </summary>
public sealed class RoomListingsService : IRoomListingsService
{
    public const string DefaultCampusId = "campus-ictu";
    public const double IctuCampusLatitude = 21.5852;
    public const double IctuCampusLongitude = 105.8073;

    private static readonly Dictionary<string, string[]> AmenityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aircon"] = ["Air conditioner", "Air conditioning", "aircon", "Điều hòa"],
        ["wifi"] = ["Wifi", "Wi-Fi"],
        ["parking"] = ["Parking", "parking", "Free parking", "để xe", "gửi xe", "giữ xe"],
        ["bathroom"] = ["Private bathroom", "bathroom", "Water heater", "khép kín", "Nóng lạnh"],
        ["washing"] = ["Washing machine", "washing", "Máy giặt"],
        ["kitchen"] = ["Kitchen", "kitchen", "Private kitchen", "Bếp"]
    };

    private readonly IApiClient _apiClient;
    private readonly bool _enableDebugFallback;

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
        RoomSortOption sortBy = RoomSortOption.DistanceAsc,
        string? search = null,
        RoomFilterCriteria? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var searchTerm = search?.Trim();
        var (sortByParam, sortOrderParam) = GetSortParameters(sortBy);
        var endpoint = $"api/v1/rooms?campusId={DefaultCampusId}&page={page}&pageSize={pageSize}&sortBy={sortByParam}&sortOrder={sortOrderParam}";

        try
        {
            var response = await _apiClient.GetAsync<PagedResponse<RoomListingApiDto>>(
                endpoint,
                cancellationToken);

            if (response is null)
            {
                return new PagedResponse<RoomListItemModel>([], page, pageSize, 0);
            }

            var mappedItems = (response.Items ?? [])
                .Select(MapDtoToModel)
                .ToList();

            return new PagedResponse<RoomListItemModel>(
                Items: mappedItems,
                Page: response.Page > 0 ? response.Page : page,
                PageSize: response.PageSize > 0 ? response.PageSize : pageSize,
                TotalCount: response.TotalCount);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException or InvalidOperationException or TaskCanceledException))
        {
            return GetFallbackMockPage(page, pageSize, sortBy, searchTerm, filter);
        }
    }

    public async Task<RoomListItemModel?> GetRoomByIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        var trimmedId = roomId.Trim();
        var endpoint = $"api/rooms/{Uri.EscapeDataString(trimmedId)}";

        try
        {
            var dto = await _apiClient.GetAsync<RoomListingApiDto>(endpoint, cancellationToken);
            return dto is null ? null : MapDtoToModel(dto);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException or InvalidOperationException or TaskCanceledException))
        {
            return MockRooms.FirstOrDefault(r =>
                string.Equals(r.Id, trimmedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<SearchRoomsResult> SearchRoomsAsync(
        RoomSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var queryString = criteria.ToQueryString();
        var endpoint = string.IsNullOrEmpty(queryString) ? "api/rooms" : $"api/rooms?{queryString}";

        try
        {
            var response = await _apiClient.GetAsync<SearchRoomsResult>(endpoint, cancellationToken);
            return response ?? new SearchRoomsResult([], 0, 0, criteria.PageNumber, []);
        }
        catch (Exception ex) when (
            _enableDebugFallback &&
            !cancellationToken.IsCancellationRequested &&
            (ex is HttpRequestException or InvalidOperationException or TaskCanceledException))
        {
            return GetFallbackMockSearchResult(criteria);
        }
    }

    private static (string SortBy, string SortOrder) GetSortParameters(RoomSortOption sortBy) =>
        sortBy switch
        {
            RoomSortOption.PriceAsc => ("price", "asc"),
            RoomSortOption.PriceDesc => ("price", "desc"),
            RoomSortOption.DistanceAsc => ("distance", "asc"),
            _ => ("distance", "asc")
        };

    private static RoomListItemModel MapDtoToModel(RoomListingApiDto dto)
    {
        var price = dto.MonthlyRent > 0 ? dto.MonthlyRent : (dto.Price ?? 0m);
        var area = dto.AreaSquareMeters > 0
            ? dto.AreaSquareMeters
            : (dto.AreaSqm ?? dto.Area ?? 0d);

        return new RoomListItemModel
        {
            Id = dto.Id,
            Title = dto.Title,
            Price = price,
            AreaSqm = area,
            Address = dto.Address,
            RoomType = !string.IsNullOrWhiteSpace(dto.RoomType) ? dto.RoomType : "private",
            District = !string.IsNullOrWhiteSpace(dto.District) ? dto.District : "Thai Nguyen City",
            DistanceToCampusKm = dto.DistanceToCampusKm,
            ThumbnailUrl = dto.ThumbnailUrl,
            IsVerified = dto.IsVerified,
            IsAvailable = dto.IsAvailable ?? true,
            Amenities = dto.Amenities is { Count: > 0 } ? dto.Amenities : []
        };
    }

    private static PagedResponse<RoomListItemModel> GetFallbackMockPage(
        int page,
        int pageSize,
        RoomSortOption sortBy,
        string? searchTerm,
        RoomFilterCriteria? filter)
    {
        IEnumerable<RoomListItemModel> query = MockRooms;

        // Filter the full mock catalog before paging, mirroring server-side search and filter semantics.
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r => MatchesSearch(r, searchTerm));
        }

        if (filter is not null)
        {
            query = query.Where(r => MatchesFilter(r, filter));
        }

        query = sortBy switch
        {
            RoomSortOption.PriceAsc => query.OrderBy(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.PriceDesc => query.OrderByDescending(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.DistanceAsc => query.OrderBy(r => r.DistanceToCampusKm).ThenBy(r => r.Id),
            _ => query.OrderBy(r => r.DistanceToCampusKm).ThenBy(r => r.Id)
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

    private static SearchRoomsResult GetFallbackMockSearchResult(RoomSearchCriteria criteria)
    {
        IEnumerable<RoomListItemModel> query = MockRooms;

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim();
            query = query.Where(r => MatchesSearch(r, keyword));
        }

        if (criteria.MinPrice.HasValue)
        {
            query = query.Where(r => r.Price >= criteria.MinPrice.Value);
        }

        if (criteria.MaxPrice.HasValue)
        {
            query = query.Where(r => r.Price <= criteria.MaxPrice.Value);
        }

        if (criteria.MinArea.HasValue)
        {
            query = query.Where(r => r.AreaSqm >= criteria.MinArea.Value);
        }

        if (criteria.MaxArea.HasValue)
        {
            query = query.Where(r => r.AreaSqm <= criteria.MaxArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.RoomType) && !string.Equals(criteria.RoomType, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.RoomType, criteria.RoomType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.RadiusInKm.HasValue)
        {
            query = query.Where(r => (r.DistanceToCampusKm ?? double.MaxValue) <= criteria.RadiusInKm.Value);
        }

        if (criteria.Amenities is { Count: > 0 })
        {
            foreach (var rawAmenity in criteria.Amenities)
            {
                if (!string.IsNullOrWhiteSpace(rawAmenity))
                {
                    var amenityKey = rawAmenity.Trim();
                    query = query.Where(r => MatchesAmenity(r, amenityKey));
                }
            }
        }

        var sortBy = criteria.SortBy ?? RoomSortOption.Newest;
        query = sortBy switch
        {
            RoomSortOption.PriceAsc => query.OrderBy(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.PriceDesc => query.OrderByDescending(r => r.Price).ThenBy(r => r.Id),
            RoomSortOption.DistanceAsc => query.OrderBy(r => r.DistanceToCampusKm ?? double.MaxValue).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
        };

        var list = query.ToList();
        var totalCount = list.Count;
        var pageSize = Math.Max(1, criteria.PageSize);
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        var pagedItems = list
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoomSummaryDto(
                r.Id,
                r.Title,
                r.Price,
                r.AreaSqm,
                r.RoomType,
                r.IsVerified,
                r.DistanceToCampusKm,
                r.Address,
                r.ThumbnailUrl,
                r.Amenities,
                r.CreatedAt))
            .ToList();

        var activeFilters = new List<string>();
        if (criteria.MinPrice.HasValue) activeFilters.Add("MinPrice");
        if (criteria.MaxPrice.HasValue) activeFilters.Add("MaxPrice");
        if (criteria.MinArea.HasValue) activeFilters.Add("MinArea");
        if (criteria.MaxArea.HasValue) activeFilters.Add("MaxArea");
        if (!string.IsNullOrWhiteSpace(criteria.RoomType)) activeFilters.Add("RoomType");
        if (!string.IsNullOrWhiteSpace(criteria.Keyword)) activeFilters.Add("Keyword");
        if (criteria.RadiusInKm.HasValue) activeFilters.Add("RadiusInKm");

        return new SearchRoomsResult(
            pagedItems,
            totalCount,
            totalPages,
            pageNumber,
            activeFilters);
    }

    private static bool MatchesSearch(RoomListItemModel room, string term)
    {
        if (room.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            room.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return room.Amenities?.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool MatchesFilter(RoomListItemModel room, RoomFilterCriteria filter)
    {
        if (filter.MinPrice.HasValue && room.Price < filter.MinPrice.Value)
        {
            return false;
        }

        if (filter.MaxPrice.HasValue && room.Price > filter.MaxPrice.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.RoomType) &&
            !string.Equals(room.RoomType, filter.RoomType.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Amenities is { Count: > 0 })
        {
            foreach (var rawAmenity in filter.Amenities)
            {
                if (!string.IsNullOrWhiteSpace(rawAmenity) && !MatchesAmenity(room, rawAmenity.Trim()))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool MatchesAmenity(RoomListItemModel room, string amenityKey)
    {
        var keywords = AmenityKeywords.TryGetValue(amenityKey, out var mapped)
            ? mapped
            : [amenityKey];

        if (room.Amenities?.Any(a => keywords.Any(k => a.Contains(k, StringComparison.OrdinalIgnoreCase))) == true)
        {
            return true;
        }

        return keywords.Any(k => room.Title.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly IReadOnlyList<RoomListItemModel> MockRooms =
    [
        new()
        {
            Id = "room-ictu-001",
            Title = "Newly built private room right next to ICTU main gate",
            Price = 1_800_000m,
            AreaSqm = 22,
            Address = "Alley 12 Z115 Street, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.3,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=600&q=80",
            IsVerified = false,
            Amenities = ["Air conditioner", "Water heater", "Private bathroom", "Fingerprint lock"]
        },
        new()
        {
            Id = "room-ictu-002",
            Title = "Breezy balcony studio in Quyet Thang faculty residential area",
            Price = 2_300_000m,
            AreaSqm = 28,
            Address = "Z115 Street, Goc Mit, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.6,
            ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=600&q=80",
            IsVerified = false,
            Amenities = ["Full furniture", "Balcony", "Washing machine"]
        },
        new()
        {
            Id = "room-ictu-003",
            Title = "Budget student room with spacious mezzanine",
            Price = 1_200_000m,
            AreaSqm = 18,
            Address = "Tan Duc, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.8,
            ThumbnailUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?w=600&q=80",
            IsVerified = false,
            Amenities = ["Mezzanine", "Water heater", "Parking"]
        },
        new()
        {
            Id = "room-ictu-004",
            Title = "1-bedroom mini apartment near Diem Hen junction",
            Price = 2_800_000m,
            AreaSqm = 32,
            Address = "Tan Thinh Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.2,
            ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=600&q=80",
            IsVerified = false,
            Amenities = ["Air conditioner", "Fridge", "Private kitchen"]
        },
        new()
        {
            Id = "room-ictu-005",
            Title = "Secure student room with 24/7 CCTV behind ICTU dorms",
            Price = 1_500_000m,
            AreaSqm = 20,
            Address = "Alley 45 Z115 Street, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.4,
            ThumbnailUrl = "https://images.unsplash.com/photo-1554995207-c18c203602cb?w=600&q=80",
            IsVerified = false,
            Amenities = ["24/7 CCTV", "Water heater", "Wi-Fi"]
        },
        new()
        {
            Id = "room-ictu-006",
            Title = "Spacious shared double room for 2–3 students near ICTU",
            Price = 2_000_000m,
            AreaSqm = 26,
            Address = "Group 5, Tan Thinh Ward, Thai Nguyen City",
            RoomType = "shared",
            DistanceToCampusKm = 1.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?w=600&q=80",
            IsVerified = false,
            Amenities = ["2 beds", "Air conditioner", "Double study desk"]
        },
        new()
        {
            Id = "room-ictu-007",
            Title = "Bright 2nd-floor en-suite room with standard utility rates",
            Price = 1_650_000m,
            AreaSqm = 21,
            Address = "Z115 Street, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1513694203232-719a280e022f?w=600&q=80",
            IsVerified = false,
            Amenities = ["Private bathroom", "Water heater", "Large window"]
        },
        new()
        {
            Id = "room-ictu-008",
            Title = "Freshly renovated clean room 300m from ICTU side gate",
            Price = 1_400_000m,
            AreaSqm = 19,
            Address = "Hong Thai, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.3,
            ThumbnailUrl = "https://images.unsplash.com/photo-1540518614846-7ede433c4573?w=600&q=80",
            IsVerified = false,
            Amenities = ["Bed & wardrobe", "Water heater", "Independent access"]
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
            Amenities = ["Elevator", "Air conditioner", "Fridge", "Keycard access"]
        },
        new()
        {
            Id = "room-ictu-010",
            Title = "Quiet single room ideal for senior students",
            Price = 1_350_000m,
            AreaSqm = 17,
            Address = "Alley 88 Z115 Street, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.7,
            ThumbnailUrl = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?w=600&q=80",
            IsVerified = false,
            Amenities = ["Quiet area", "Study desk", "Water heater"]
        },
        new()
        {
            Id = "room-ictu-011",
            Title = "Student room with private drying yard and kitchen counter",
            Price = 1_750_000m,
            AreaSqm = 23,
            Address = "Extended Quang Trung Road, Thinh Dan Ward, Thai Nguyen City",
            DistanceToCampusKm = 1.9,
            ThumbnailUrl = "https://images.unsplash.com/photo-1536376072261-38c75010e6c9?w=600&q=80",
            IsVerified = false,
            Amenities = ["Private kitchen", "Drying yard", "Air conditioner"]
        },
        new()
        {
            Id = "room-ictu-012",
            Title = "En-suite room with AC and furniture next to student market",
            Price = 1_900_000m,
            AreaSqm = 24,
            Address = "Z115 Student Market, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.5,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?w=600&q=80",
            IsVerified = false,
            Amenities = ["Air conditioner", "Water heater", "Near market & bus stop"]
        },
        new()
        {
            Id = "room-ictu-013",
            Title = "Affordable room for ICTU freshmen with free parking",
            Price = 1_100_000m,
            AreaSqm = 16,
            Address = "Goc Mit, Quyet Thang, Thai Nguyen City",
            DistanceToCampusKm = 0.9,
            ThumbnailUrl = "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?w=600&q=80",
            IsVerified = false,
            Amenities = ["Free parking", "Wi-Fi", "Private bathroom"]
        },
        new()
        {
            Id = "room-ictu-014",
            Title = "Serviced twin room near University of Sciences & ICTU",
            Price = 2_450_000m,
            AreaSqm = 29,
            Address = "Tan Thinh Ward, Thai Nguyen City",
            RoomType = "shared",
            DistanceToCampusKm = 1.6,
            ThumbnailUrl = "https://images.unsplash.com/photo-1560185127-6ed189bf02f4?w=600&q=80",
            IsVerified = false,
            Amenities = ["Full furniture", "Washing machine", "Fingerprint lock"]
        }
    ];
}
