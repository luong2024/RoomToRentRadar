namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound port for invalidating cached room listings when room mutations occur (status change, verification, price update).
/// Follows Interface Segregation Principle: write use cases depend on this abstraction without depending on read use cases.
/// </summary>
public interface IRoomListingCacheInvalidator
{
    Task InvalidateCampusAsync(
        string campusId,
        CancellationToken cancellationToken = default);
}
