using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Services.Interactions;

public interface IUndoService
{
    /// <summary>
    /// Shows a timed undo affordance for a removed favourite and restores it
    /// when the user takes the offer.
    /// </summary>
    Task OfferRemovalUndoAsync(FavouriteRoomDto item, CancellationToken cancellationToken = default);
}
