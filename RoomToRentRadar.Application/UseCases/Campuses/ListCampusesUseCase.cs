using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Ports.Inbound.UseCases.Campuses;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Campuses;

/// <summary>
/// Phase 0 stub for listing campuses (BR-CMP-01).
/// Real implementation with sorting and mapping comes in Phase 2 (GREEN).
/// </summary>
public sealed class ListCampusesUseCase : IListCampusesUseCase
{
    private readonly ICampusRepository _campusRepository;

    public ListCampusesUseCase(ICampusRepository campusRepository)
    {
        ArgumentNullException.ThrowIfNull(campusRepository);
        _campusRepository = campusRepository;
    }

    public Task<IReadOnlyList<CampusDto>> ExecuteAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException("Phase 0 stub for ListCampusesUseCase.");
    }
}
