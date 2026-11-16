using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Campuses;

/// <summary>
/// Inbound port for listing all campuses ordered by default campus first, then by name (BR-CMP-01).
/// </summary>
public interface IListCampusesUseCase
{
    Task<IReadOnlyList<CampusDto>> ExecuteAsync(CancellationToken ct = default);
}
