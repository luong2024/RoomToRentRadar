using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public sealed class AuthService(AppDbContext dbContext)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    public async Task<LoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return null;
        }

        var (token, tokenHash) = TokenGenerator.Create();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(SessionLifetime);

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse(token, expiresAt, user.DisplayName);
    }

    public async Task<User?> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = TokenGenerator.Hash(token);
        var now = DateTime.UtcNow;

        return await dbContext.AuthSessions
            .Where(session => session.TokenHash == tokenHash && session.ExpiresAt > now)
            .Select(session => session.User)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = TokenGenerator.Hash(token);
        var session = await dbContext.AuthSessions
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            return;
        }

        dbContext.AuthSessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
