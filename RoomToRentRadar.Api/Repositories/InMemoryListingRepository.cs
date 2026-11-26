using System.Collections.Concurrent;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Api.Services.Search;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Repositories;

public sealed class InMemoryListingRepository : IListingRepository, ILandlordListingRepository
{
    private readonly Lock _sync = new();
    private readonly ConcurrentDictionary<string, ListingDocument> _byId;
    private readonly List<ListingDocument> _canonical;

    public InMemoryListingRepository()
        : this(ListingSeedData.Create())
    {
    }

    public InMemoryListingRepository(IEnumerable<ListingDocument> listings)
    {
        var all = listings.ToList();
        _byId = new ConcurrentDictionary<string, ListingDocument>(
            all.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        _canonical = all
            .Where(l => IsCanonicalId(l.Id))
            .ToList();
    }

    public Task<ListingDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var listing);
        return Task.FromResult(listing);
    }

    public Task<IReadOnlyList<ListingDocument>> GetAllCanonicalAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ListingDocument>>(_canonical.ToList());
        }
    }

    public Task<ListingDocument> UpsertAsync(ListingDocument listing, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _byId[listing.Id] = listing;
            UpdateCanonicalList(listing);

            // Đồng bộ alias room-ictu-XXX <-> room-2026-XXX nếu có
            var aliasId = GetAliasId(listing.Id);
            if (aliasId is not null && _byId.TryGetValue(aliasId, out var aliasDoc))
            {
                aliasDoc.Status = listing.Status;
                aliasDoc.IsVerified = listing.IsVerified;
                aliasDoc.ModerationStatus = listing.ModerationStatus;
                aliasDoc.UpdatedAt = listing.UpdatedAt;
            }

            return Task.FromResult(listing);
        }
    }

    /// <summary>US05 / T05.2 — tin công khai (Available + đã xác minh) của một cơ sở, dùng cho tìm theo vùng vẽ.</summary>
    public Task<IReadOnlyList<ListingDocument>> GetPublicListingsAsync(
        string campusId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<ListingDocument> items = _canonical
                .Where(l =>
                    string.Equals(l.CampusId, campusId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(l.Status, "Available", StringComparison.OrdinalIgnoreCase)
                    && l.IsVerified)
                .ToList();

            return Task.FromResult(items);
        }
    }

    public Task<IReadOnlyList<ListingDocument>> GetSimilarCandidatesAsync(
        string campusId,
        string excludeId,
        CancellationToken cancellationToken = default)
    {
        var excludeCanonical = NormalizeToCanonicalId(excludeId);

        lock (_sync)
        {
            IReadOnlyList<ListingDocument> items = _canonical
                .Where(l =>
                    string.Equals(l.CampusId, campusId, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(l.Id, excludeCanonical, StringComparison.OrdinalIgnoreCase)
                    && l.IsPublic())
                .ToList();

            return Task.FromResult(items);
        }
    }

    public Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> SearchAsync(
        ListingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        List<ListingDocument> snapshot;
        lock (_sync)
        {
            snapshot = _canonical.ToList();
        }

        var matched = ListingSorter.Apply(ApplySearchFilters(snapshot, query), query.SortBy).ToList();
        var paging = ListingPaging.From(query);
        IReadOnlyList<ListingDocument> page = matched.Skip(paging.Skip).Take(paging.PageSize).ToList();

        return Task.FromResult((page, matched.Count));
    }

    /// <summary>US04 / T04.1 — lọc theo campus, giá, diện tích, loại phòng, khoảng cách (chỉ tin công khai).</summary>
    private static IEnumerable<ListingDocument> ApplySearchFilters(IEnumerable<ListingDocument> listings, ListingSearchQuery query) =>
        listings.Where(l =>
            l.IsPublic()
            && string.Equals(l.CampusId, query.CampusId, StringComparison.OrdinalIgnoreCase)
            && (query.MinRent is not { } minRent || l.Rent >= minRent)
            && (query.MaxRent is not { } maxRent || l.Rent <= maxRent)
            && (query.MinArea is not { } minArea || l.Area >= minArea)
            && (query.MaxArea is not { } maxArea || l.Area <= maxArea)
            && (string.IsNullOrWhiteSpace(query.RoomType)
                || string.Equals(l.RoomType, query.RoomType, StringComparison.OrdinalIgnoreCase))
            && (query.MaxDistanceMeters is not { } maxDistance || l.DistanceMeters <= maxDistance));

    public Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> GetPagedAsync(
        ListingQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        List<ListingDocument> snapshot;
        lock (_sync)
        {
            snapshot = _canonical.ToList();
        }

        IQueryable<ListingDocument> query = snapshot
            .AsQueryable()
            .Where(l =>
                (string.IsNullOrWhiteSpace(request.CampusId)
                 || string.Equals(l.CampusId, request.CampusId, StringComparison.OrdinalIgnoreCase))
                && string.Equals(l.Status, "Available", StringComparison.OrdinalIgnoreCase)
                && l.IsVerified);

        var totalCount = query.Count();

        var sortKey = request.SortBy?.Trim().ToLowerInvariant();
        var isAsc = sortKey switch
        {
            "price_desc" or "rent_desc" or "area_desc" => false,
            "price_asc" or "rent_asc" or "area_asc" => true,
            _ => request.IsAscending
        };

        IOrderedQueryable<ListingDocument> ordered = (sortKey, isAsc) switch
        {
            ("price" or "rent" or "price_asc" or "rent_asc", true) => query.OrderBy(l => l.Rent).ThenBy(l => l.DistanceMeters),
            ("price" or "rent" or "price_desc" or "rent_desc", false) => query.OrderByDescending(l => l.Rent).ThenBy(l => l.DistanceMeters),
            ("area" or "area_asc", true) => query.OrderBy(l => l.Area).ThenBy(l => l.DistanceMeters),
            ("area" or "area_desc", false) => query.OrderByDescending(l => l.Area).ThenBy(l => l.DistanceMeters),
            ("updatedat", true) => query.OrderBy(l => l.UpdatedAt),
            ("updatedat", false) => query.OrderByDescending(l => l.UpdatedAt),
            (_, false) => query.OrderByDescending(l => l.DistanceMeters).ThenBy(l => l.Rent),
            _ => query.OrderBy(l => l.DistanceMeters).ThenBy(l => l.Rent)
        };

        var pageNumber = request.EffectivePage;
        var pageSize = Math.Clamp(request.PageSize, 1, ListingQueryRequest.MaxPageSize);

        var items = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<ListingDocument>)items, totalCount));
    }

    /// <summary>US08 / T08.2 — thêm tin mới của chủ trọ (vào cả danh sách canonical để T08.3 thấy trong hàng đợi duyệt).</summary>
    public Task AddAsync(ListingDocument listing, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_byId.TryAdd(listing.Id, listing))
            {
                throw new InvalidOperationException($"Listing '{listing.Id}' already exists.");
            }

            UpdateCanonicalList(listing);
        }

        return Task.CompletedTask;
    }

    /// <summary>US08 / T08.2 — rollback khi upload ảnh lúc tạo tin thất bại.</summary>
    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var removed = _byId.TryRemove(id, out _);
            _canonical.RemoveAll(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(removed);
        }
    }

    /// <summary>
    /// Bản ghi canonical = mọi id trừ alias <c>room-ictu-XXX</c> (seed <c>room-2026-XXX</c> + tin chủ trọ mới đăng).
    /// </summary>
    private static bool IsCanonicalId(string id) =>
        !id.StartsWith("room-ictu-", StringComparison.OrdinalIgnoreCase);

    private void UpdateCanonicalList(ListingDocument listing)
    {
        if (!IsCanonicalId(listing.Id))
        {
            return;
        }

        int existingIndex = _canonical.FindIndex(l =>
            string.Equals(l.Id, listing.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _canonical[existingIndex] = listing;
        }
        else
        {
            _canonical.Add(listing);
        }
    }

    private static string? GetAliasId(string id)
    {
        if (id.StartsWith("room-2026-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id["room-2026-".Length..], out var n))
        {
            return $"room-ictu-{n:000}";
        }

        if (id.StartsWith("room-ictu-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id["room-ictu-".Length..], out var m))
        {
            return $"room-2026-{m:000}";
        }

        return null;
    }

    private static string NormalizeToCanonicalId(string id)
    {
        if (id.StartsWith("room-ictu-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id["room-ictu-".Length..], out var n))
        {
            return $"room-2026-{n:000}";
        }

        return id;
    }
}
