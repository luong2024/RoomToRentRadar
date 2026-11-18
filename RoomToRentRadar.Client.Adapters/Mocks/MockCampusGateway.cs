using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks.SampleData;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Mocks;

/// <summary>Deterministic in-memory <see cref="ICampusGateway"/> for Debug builds: the eight seed campuses (PLAN Q6/Q11).</summary>
public sealed class MockCampusGateway : ICampusGateway
{
    public Task<IReadOnlyList<CampusDto>> ListCampusesAsync(CancellationToken ct = default) =>
        Task.FromResult(SampleCampuses.All);
}