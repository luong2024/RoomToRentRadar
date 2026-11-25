using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the user and saves immediately. Returns false when the unique email index
    /// rejects a concurrent insert, and leaves the context clean so the request can map a conflict.
    /// </summary>
    Task<bool> TryAddAsync(User user, CancellationToken cancellationToken);
}
