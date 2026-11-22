using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class HouseRulesRepository(AppDbContext dbContext) : IHouseRulesRepository
{
    public async Task<bool> IsOwnerAsync(
        Guid listingId,
        Guid ownerId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(x => x.Id == listingId && x.OwnerId == ownerId, cancellationToken);

    public async Task<ListingHouseRules?> FindAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.ListingHouseRules
            .SingleOrDefaultAsync(x => x.ListingId == listingId, cancellationToken);

    public void Add(ListingHouseRules rules) => dbContext.ListingHouseRules.Add(rules);

    public void AddVersion(ListingHouseRuleVersion version) => dbContext.ListingHouseRuleVersions.Add(version);
}
