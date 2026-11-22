using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class AuthSessionRepository(AppDbContext dbContext) : IAuthSessionRepository
{
    public async Task AddAsync(AuthSession session, CancellationToken cancellationToken)
        => await dbContext.AuthSessions.AddAsync(session, cancellationToken);

    public Task<User?> FindUserByTokenHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => dbContext.AuthSessions
            .Where(session => session.TokenHash == tokenHash && session.ExpiresAt > utcNow)
            .Select(session => session.User)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AuthSession?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
        => dbContext.AuthSessions.FirstOrDefaultAsync(
            session => session.TokenHash == tokenHash,
            cancellationToken);

    public void Remove(AuthSession session) => dbContext.AuthSessions.Remove(session);
}
