namespace RoomToRentRadar.Application.Ports.Outbound;

using RoomToRentRadar.Domain.Entities;

/// <summary>
/// Outbound port for accessing and querying university campus data.
/// </summary>
public interface ICampusRepository
{
    Task<Campus?> GetByIdAsync(string campusId, CancellationToken cancellationToken = default);

    Task AddAsync(Campus campus, CancellationToken cancellationToken = default);
}
