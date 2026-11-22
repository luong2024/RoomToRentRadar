using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.UseCases.Listings;

public sealed class HouseRulesService(
    IHouseRulesRepository repository,
    IUnitOfWork unitOfWork) : IHouseRulesUseCase
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
                ListingWriteErrorCodes.InvalidHouseRules,
                errors);
        }

        if (!await repository.IsOwnerAsync(listingId, ownerId, cancellationToken))
        {
            return ListingWriteResult<HouseRulesDto>.Failure(ListingWriteErrorCodes.NotFound);
        }

        var rules = await repository.FindAsync(listingId, cancellationToken);

        var now = DateTime.UtcNow;
        var nextVersion = (rules?.Version ?? 0) + 1;

        if (rules is null)
        {
            rules = new ListingHouseRules { ListingId = listingId };
            repository.Add(rules);
        }

        rules.QuietHours = input.QuietHours;
        rules.GuestPolicy = input.GuestPolicy;
        rules.CookingRules = input.CookingRules;
        rules.ParkingRules = input.ParkingRules;
        rules.Version = nextVersion;
        rules.UpdatedAt = now;
        rules.UpdatedByUserId = ownerId;

        repository.AddVersion(new ListingHouseRuleVersion
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ListingWriteResult<HouseRulesDto>.Success(new HouseRulesDto(
            input.QuietHours,
            input.GuestPolicy,
            input.CookingRules,
            input.ParkingRules,
            nextVersion,
            now));
    }
}
