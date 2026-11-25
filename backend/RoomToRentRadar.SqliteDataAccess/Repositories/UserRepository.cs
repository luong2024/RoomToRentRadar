using Microsoft.Data.Sqlite;
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

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception, user))
        {
            dbContext.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsDuplicateEmail(DbUpdateException exception, User user)
        => exception.InnerException is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 }
            && exception.Entries.Any(entry => ReferenceEquals(entry.Entity, user));
}
