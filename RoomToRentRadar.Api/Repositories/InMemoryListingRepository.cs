using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Repositories;

public sealed class InMemoryListingRepository : IListingRepository
{
    private readonly IReadOnlyDictionary<string, ListingDocument> _byId;
    private readonly IReadOnlyList<ListingDocument> _canonical;

    public InMemoryListingRepository()
        : this(ListingSeedData.Create())
    {
    }

    public InMemoryListingRepository(IEnumerable<ListingDocument> listings)
    {
        var all = listings.ToList();
        _byId = all.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);
        _canonical = all
            .Where(l => l.Id.StartsWith("room-2026-", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public Task<ListingDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var listing);
        return Task.FromResult(listing);
    }

    public Task<IReadOnlyList<ListingDocument>> GetSimilarCandidatesAsync(
        string campusId,
        string excludeId,
        CancellationToken cancellationToken = default)
    {
        var excludeCanonical = NormalizeToCanonicalId(excludeId);

        IReadOnlyList<ListingDocument> items = _canonical
            .Where(l =>
                string.Equals(l.CampusId, campusId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(l.Id, excludeCanonical, StringComparison.OrdinalIgnoreCase)
                && string.Equals(l.Status, "Available", StringComparison.OrdinalIgnoreCase)
                && l.IsVerified)
            .ToList();

        return Task.FromResult(items);
    }

    public Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> SearchAsync(
        ListingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ListingDocument> filtered = _canonical
            .Where(l =>
                string.Equals(l.CampusId, query.CampusId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(l.Status, "Available", StringComparison.OrdinalIgnoreCase)
                && l.IsVerified);

        if (query.MinRent is { } minRent)
        {
            filtered = filtered.Where(l => l.Rent >= minRent);
        }

        if (query.MaxRent is { } maxRent)
        {
            filtered = filtered.Where(l => l.Rent <= maxRent);
        }

        if (query.MinArea is { } minArea)
        {
            filtered = filtered.Where(l => l.Area >= minArea);
        }

        if (query.MaxArea is { } maxArea)
        {
            filtered = filtered.Where(l => l.Area <= maxArea);
        }

        if (!string.IsNullOrWhiteSpace(query.RoomType))
        {
            filtered = filtered.Where(l =>
                string.Equals(l.RoomType, query.RoomType, StringComparison.OrdinalIgnoreCase));
        }

        if (query.MaxDistanceMeters is { } maxDistance)
        {
            filtered = filtered.Where(l => l.DistanceMeters <= maxDistance);
        }

        filtered = (query.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" or "rent" or "price_asc" => filtered.OrderBy(l => l.Rent).ThenBy(l => l.DistanceMeters),
            "price_desc" or "rent_desc" => filtered.OrderByDescending(l => l.Rent).ThenBy(l => l.DistanceMeters),
            "area" or "area_desc" => filtered.OrderByDescending(l => l.Area).ThenBy(l => l.DistanceMeters),
            _ => filtered.OrderBy(l => l.DistanceMeters).ThenBy(l => l.Rent)
        };

        var list = filtered.ToList();
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var page = list
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(((IReadOnlyList<ListingDocument>)page, list.Count));
    }

    public Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> GetPagedAsync(
        ListingQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ListingDocument> query = _canonical
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
