using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);
}
