using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// UC-05 (Search and Filter Listings) / <c>operationId: searchRooms</c> / BR-SRCH-01..06, BR-CMP-01..03.
/// Task 2 fills in the behaviour; Phase 0 keeps only the dependency shape so the solution compiles.
/// </summary>
public sealed class SearchRoomsUseCase : ISearchRoomsUseCase
{
    private readonly ICampusRepository _campusRepository;
    private readonly IRoomSearchReadModel _readModel;

    public SearchRoomsUseCase(ICampusRepository campusRepository, IRoomSearchReadModel readModel)
    {
        ArgumentNullException.ThrowIfNull(campusRepository);
        ArgumentNullException.ThrowIfNull(readModel);
        _campusRepository = campusRepository;
        _readModel = readModel;
    }

    public Task<PagedResult<RoomCardDto>> ExecuteAsync(RoomSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        throw new NotImplementedException("SearchRoomsUseCase is implemented in Task 2 (Giai đoạn 2).");
    }
}
