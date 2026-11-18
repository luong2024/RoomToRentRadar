namespace RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Retrieves similar available rooms for an unavailable or viewable listing (US02 AC3, BR-LST-10, getSimilarRooms).
/// </summary>
public interface IGetSimilarRoomsUseCase
{
    Task<IReadOnlyList<RoomCardDto>> ExecuteAsync(string roomId, string? campusId, CancellationToken ct = default);
}
