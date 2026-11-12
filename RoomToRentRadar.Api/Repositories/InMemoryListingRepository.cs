using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Models;

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
