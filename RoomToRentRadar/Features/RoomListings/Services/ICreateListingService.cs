using RoomToRentRadar.Features.RoomListings.Models;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Service port for submitting listing creation requests to the backend.
/// </summary>
public interface ICreateListingService
{
    Task<CreateListingResult> CreateListingAsync(CreateListingRequest request, CancellationToken cancellationToken = default);
}
