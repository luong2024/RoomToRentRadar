using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.SqliteDataAccess;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => dbContext.Users.FirstOrDefaultAsync(
            user => user.Email == normalizedEmail,
            cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
        => await dbContext.Users.AddAsync(user, cancellationToken);
}
