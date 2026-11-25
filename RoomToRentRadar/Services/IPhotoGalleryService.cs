using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

public interface IPhotoGalleryService
{
    Task<IReadOnlyList<PhotoGalleryItemModel>> GetPhotosAsync(
        string listingId,
        CancellationToken cancellationToken = default);
}
