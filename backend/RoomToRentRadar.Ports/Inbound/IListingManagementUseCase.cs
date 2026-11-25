using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Inbound;

public interface IListingManagementUseCase
{
    Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(
        Guid ownerId,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<ListingWriteResult<ManagedListingDto>> CreateAsync(
        Guid ownerId,
        string defaultLandlordName,
        ListingWriteCommand command,
        CancellationToken cancellationToken);

    Task<ListingWriteResult<ManagedListingDto>> UpdateAsync(
        Guid ownerId,
        Guid listingId,
        ListingWriteCommand command,
        CancellationToken cancellationToken);

    Task<ListingWriteResult<ManagedListingDto>> PublishAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ListingWriteResult<ManagedListingDto>> UnpublishAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ListingWriteError?> DeleteAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken);

    Task<ManagedListingDto?> GetManagedAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken);
}

public interface IHouseRulesUseCase
{
    Task<ListingWriteResult<HouseRulesDto>> UpdateAsync(
        Guid ownerId,
        Guid listingId,
        UpdateHouseRulesRequest request,
        CancellationToken cancellationToken);
}
