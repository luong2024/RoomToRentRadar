using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Services.Api;

public interface IModerationClient
{
    Task<ModerationListingQueueDto> GetPendingListingsAsync(
        int limit = 20,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(Guid listingId, CancellationToken cancellationToken = default);
}
