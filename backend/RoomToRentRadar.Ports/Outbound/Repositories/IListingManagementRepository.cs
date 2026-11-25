using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IListingManagementRepository
{
    Task<IReadOnlyList<Listing>> GetMineAsync(
        Guid ownerId,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    /// <summary>Tracked listing owned by the user, including photos, amenities and rules.</summary>
    Task<Listing?> FindOwnedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken);

    /// <summary>Read-only listing owned by the user, including photos, amenities and rules.</summary>
    Task<Listing?> GetManagedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken);

    void Add(Listing listing);

    void AddPhotoRange(IReadOnlyList<ListingPhoto> photos);

    Task<bool> HasModerationAuditLogsAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task CleanupExternalReferencesAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    void Remove(Listing listing);

    void RemovePhotoRange(IEnumerable<ListingPhoto> photos);
}
