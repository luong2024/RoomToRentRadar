using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Policies;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Use case for finding available similar rooms near an unavailable listing per docs/business-analysis.md BR-LST-10.
/// </summary>
public sealed class GetSimilarRoomsUseCase : IGetSimilarRoomsUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomSearchReadModel _searchReadModel;

    public GetSimilarRoomsUseCase(IRoomRepository roomRepository, IRoomSearchReadModel searchReadModel)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _searchReadModel = searchReadModel ?? throw new ArgumentNullException(nameof(searchReadModel));
    }

    public async Task<IReadOnlyList<RoomCardDto>> ExecuteAsync(string roomId, string? campusId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Array.Empty<RoomCardDto>();
        }

        var trimmedId = roomId.Trim();
        var targetRoom = await _roomRepository.GetByIdAsync(trimmedId, ct);
        if (targetRoom == null || !targetRoom.IsPubliclyVisible)
        {
            return Array.Empty<RoomCardDto>();
        }

        return await _searchReadModel.GetSimilarRoomsAsync(
            targetRoomId: targetRoom.Id,
            targetMonthlyRentVnd: targetRoom.MonthlyRentVnd,
            targetLatitude: targetRoom.Latitude,
            targetLongitude: targetRoom.Longitude,
            roomType: targetRoom.RoomType,
            limit: SimilarRoomsPolicy.MaxSimilarRooms,
            ct: ct);
    }
}
