using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Services.Interactions;

public sealed class UndoService(IFavouritesService favouritesService, IUndoPrompt prompt) : IUndoService
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(5);

    public async Task OfferRemovalUndoAsync(
        FavouriteRoomDto item,
        CancellationToken cancellationToken = default)
    {
        var undoRequested = await prompt.ShowAsync(
            $"Đã bỏ lưu \"{item.Title}\".",
            "Hoàn tác",
            UndoWindow,
            cancellationToken);

        if (undoRequested)
        {
            await favouritesService.RestoreAsync(item, cancellationToken);
        }
    }
}
