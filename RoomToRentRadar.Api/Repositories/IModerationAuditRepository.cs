using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public interface IModerationAuditRepository
{
    Task<ModerationAuditDocument> AddAsync(
        ModerationAuditDocument entry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModerationAuditDocument>> GetByListingIdAsync(
        string listingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ModerationAuditDocument>> GetLatestByListingIdsAsync(
        IEnumerable<string> listingIds,
        CancellationToken cancellationToken = default);
}
