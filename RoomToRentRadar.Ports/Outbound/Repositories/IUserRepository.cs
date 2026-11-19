using RoomToRentRadar.Domain.Aggregates;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}

