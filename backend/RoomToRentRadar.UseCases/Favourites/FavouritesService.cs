using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.UseCases.Favourites;

public sealed class FavouritesService(
    IListingFavouriteRepository favouriteRepository,
    IListingLookupRepository listingRepository,
    IUnitOfWork unitOfWork) : IFavouritesUseCase
{
    public Task<IReadOnlyList<FavouriteRoomDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => favouriteRepository.GetByUserAsync(userId, cancellationToken);

    public async Task<bool> AddAsync(
        Guid userId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        if (!await listingRepository.ExistsAsync(listingId, cancellationToken))
        {
            return false;
        }

        if (!await favouriteRepository.ExistsAsync(userId, listingId, cancellationToken))
        {
            await favouriteRepository.AddAsync(
                new ListingFavourite
                {
                    UserId = userId,
                    ListingId = listingId,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public Task RemoveAsync(Guid userId, Guid listingId, CancellationToken cancellationToken)
        => favouriteRepository.RemoveAsync(userId, listingId, cancellationToken);
}
