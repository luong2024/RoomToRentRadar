using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface IPhotoGalleryApiService
{
    [Get("/api/v1/listings/{listingId}/photos")]
    Task<PhotoGalleryResponse> GetGalleryAsync(
        string listingId,
        CancellationToken cancellationToken = default);
}
