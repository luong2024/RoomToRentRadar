namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IRoomListingVisibilityWriter
{
    /// <summary>Hides existing public projections and returns the affected campus IDs.</summary>
    Task<IReadOnlyList<string>> HideAsync(string roomId, CancellationToken cancellationToken = default);
}
