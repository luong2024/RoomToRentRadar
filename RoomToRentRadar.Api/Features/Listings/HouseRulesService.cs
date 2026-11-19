using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class HouseRulesService(AppDbContext dbContext)
{
    public async Task<ListingWriteResult<HouseRulesDto>> UpdateAsync(
        Guid ownerId,
        Guid listingId,
        UpdateHouseRulesRequest request,
        CancellationToken cancellationToken)
    {
        if (!HouseRulesValidator.TryValidate(request, out var input, out var errors))
        {
            return ListingWriteResult<HouseRulesDto>.Failure(
                ListingWriteProblemFactory.InvalidHouseRulesProblem(errors));
        }

        var isOwner = await dbContext.Listings
            .AnyAsync(x => x.Id == listingId && x.OwnerId == ownerId, cancellationToken);
        if (!isOwner)
        {
            return ListingWriteResult<HouseRulesDto>.Failure(
                ListingWriteProblemFactory.NotFoundProblem());
        }

        var rules = await dbContext.ListingHouseRules
            .SingleOrDefaultAsync(x => x.ListingId == listingId, cancellationToken);

        var now = DateTime.UtcNow;
        var nextVersion = (rules?.Version ?? 0) + 1;

        if (rules is null)
        {
            rules = new ListingHouseRules { ListingId = listingId };
            dbContext.ListingHouseRules.Add(rules);
        }

        rules.QuietHours = input.QuietHours;
        rules.GuestPolicy = input.GuestPolicy;
        rules.CookingRules = input.CookingRules;
        rules.ParkingRules = input.ParkingRules;
        rules.Version = nextVersion;
        rules.UpdatedAt = now;
        rules.UpdatedByUserId = ownerId;

        dbContext.ListingHouseRuleVersions.Add(new ListingHouseRuleVersion
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            Version = nextVersion,
            QuietHours = input.QuietHours,
            GuestPolicy = input.GuestPolicy,
            CookingRules = input.CookingRules,
            ParkingRules = input.ParkingRules,
            CreatedAt = now,
            CreatedByUserId = ownerId
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return ListingWriteResult<HouseRulesDto>.Success(new HouseRulesDto(
            input.QuietHours,
            input.GuestPolicy,
            input.CookingRules,
            input.ParkingRules,
            nextVersion,
            now));
    }
}
