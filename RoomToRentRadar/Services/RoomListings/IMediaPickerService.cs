using RoomToRentRadar.Models.RoomListings;

namespace RoomToRentRadar.Services.RoomListings;

/// <summary>
/// Abstraction for picking multiple photos from device storage.
/// Allows pure unit testing without MAUI platform dependencies.
/// </summary>
public interface IMediaPickerService
{
    Task<IReadOnlyList<ListingPhotoItem>> PickPhotosAsync(CancellationToken cancellationToken = default);
}
