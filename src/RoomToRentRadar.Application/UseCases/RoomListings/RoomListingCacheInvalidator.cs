namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Ports.Outbound;

public sealed class RoomListingCacheInvalidator(ICacheService cacheService) : IRoomListingCacheInvalidator
{
    private static long _sequence;

    internal static string VersionKey(string campusId) => $"rooms:campus:{campusId}:version";

    public Task InvalidateCampusAsync(string campusId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campusId))
        {
            return Task.CompletedTask;
        }

        var version = $"{DateTimeOffset.UtcNow.Ticks}_{Interlocked.Increment(ref _sequence)}";
        return cacheService.SetAsync(VersionKey(campusId), version, TimeSpan.FromDays(7), cancellationToken);
    }
}
