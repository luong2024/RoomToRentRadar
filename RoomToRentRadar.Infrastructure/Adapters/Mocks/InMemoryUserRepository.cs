using System.Collections.Concurrent;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _usersById = new(StringComparer.Ordinal);
    private readonly object _writeLock = new();

    public Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_usersById.TryGetValue(id, out var user) ? Clone(user) : null);
    }

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var target = normalizedEmail.Trim().ToLowerInvariant();
        var user = _usersById.Values.FirstOrDefault(u =>
            string.Equals(u.Email.Value, target, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user == null ? null : Clone(user));
    }

    public Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = _usersById.Values.FirstOrDefault(u =>
            u.RefreshTokens.Any(t => string.Equals(t.TokenHash, refreshTokenHash, StringComparison.Ordinal)));

        return Task.FromResult(user == null ? null : Clone(user));
    }

    public Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var target = normalizedEmail.Trim().ToLowerInvariant();
        var exists = _usersById.Values.Any(u =>
            string.Equals(u.Email.Value, target, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_writeLock)
        {
            if (_usersById.Values.Any(u =>
                    string.Equals(u.Email.Value, user.Email.Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateEmailException(user.Email.Value);
            }

            _usersById[user.Id] = Clone(user);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_writeLock)
        {
            _usersById[user.Id] = Clone(user);
        }

        return Task.CompletedTask;
    }

    private static User Clone(User source)
    {
        var clonedTokens = source.RefreshTokens.Select(t => new RefreshTokenSession(
            tokenHash: t.TokenHash,
            jwtId: t.JwtId,
            createdAtUtc: t.CreatedAtUtc,
            expiresAtUtc: t.ExpiresAtUtc,
            revokedAtUtc: t.RevokedAtUtc,
            replacedByTokenHash: t.ReplacedByTokenHash));

        return User.Reconstitute(
            id: source.Id,
            email: source.Email.Value,
            fullName: source.FullName,
            passwordHash: source.PasswordHash,
            role: source.Role,
            authProvider: source.AuthProvider,
            ssoSubjectId: source.SsoSubjectId,
            phoneNumber: source.PhoneNumber,
            studentCode: source.StudentCode,
            isVerified: source.IsVerified,
            failedLoginCount: source.FailedLoginCount,
            lockoutEndUtc: source.LockoutEndUtc,
            createdAtUtc: source.CreatedAtUtc,
            updatedAtUtc: source.UpdatedAtUtc,
            refreshTokens: clonedTokens);
    }
}
