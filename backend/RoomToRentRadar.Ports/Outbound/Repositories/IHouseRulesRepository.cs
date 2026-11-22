using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IHouseRulesRepository
{
    Task<bool> IsOwnerAsync(Guid listingId, Guid ownerId, CancellationToken cancellationToken);

    Task<ListingHouseRules?> FindAsync(Guid listingId, CancellationToken cancellationToken);

    void Add(ListingHouseRules rules);

    void AddVersion(ListingHouseRuleVersion version);
}
