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

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, user))
        {
            dbContext.Entry(user).State = EntityState.Detached;

            // SQLite does not name the violated unique index, so confirm the email is the
            // conflicting column and let any other unique violation surface as a 500.
            var emailTaken = await dbContext.Users.AsNoTracking().AnyAsync(
                existing => existing.Email == user.Email,
                cancellationToken);
            if (emailTaken)
            {
                return false;
            }

            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, User user)
        => exception.InnerException is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 }
            && exception.Entries.Any(entry => ReferenceEquals(entry.Entity, user));
}
