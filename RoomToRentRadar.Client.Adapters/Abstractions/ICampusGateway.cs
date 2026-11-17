using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Campus reference data (Campuses tag of <c>docs/api/openapi.yaml</c>, BR-CMP-01).</summary>
public interface ICampusGateway
{
    [ApiOperation("listCampuses")]
    Task<IReadOnlyList<CampusDto>> ListCampusesAsync(CancellationToken ct = default);
}