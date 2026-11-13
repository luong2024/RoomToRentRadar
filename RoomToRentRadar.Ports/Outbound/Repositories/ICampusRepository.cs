namespace RoomToRentRadar.Ports.Outbound.Repositories;

using RoomToRentRadar.Domain.Entities;

/// <summary>Campus reference data (BR-CMP-01..03, <c>docs/erd.md</c> §4.1).</summary>
public interface ICampusRepository
{
    Task<Campus?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Campus?> GetDefaultCampusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Campus>> GetAllAsync(CancellationToken ct = default);
}
