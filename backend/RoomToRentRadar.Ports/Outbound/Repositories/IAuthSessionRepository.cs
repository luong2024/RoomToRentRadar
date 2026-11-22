using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IAuthSessionRepository
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);

    Task<User?> FindUserByTokenHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<AuthSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Remove(AuthSession session);
}
