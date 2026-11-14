namespace RoomToRentRadar.Application.Ports.Inbound;

using RoomToRentRadar.Application.DTOs;

/// <summary>
/// Inbound port for browsing verified rental room listings near a student campus (US01).
/// </summary>
public interface IGetRoomListingsUseCase
{
    Task<PagedResult<RoomListingDto>> ExecuteAsync(
        GetRoomListingsQuery query,
        CancellationToken cancellationToken = default);
}
