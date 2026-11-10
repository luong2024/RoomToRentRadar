using System.Collections.Concurrent;
using RoomToRentRadar.Datastore.CosmosDb.Documents;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Infrastructure.Repositories;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, UserDocument> _usersById = new(StringComparer.Ordinal);
    private readonly object _writeLock = new();

    public Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_usersById.TryGetValue(id, out var doc)
            ? CosmosUserRepository.MapToDomain(doc)
            : null);
    }

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var target = normalizedEmail.Trim().ToLowerInvariant();
        var doc = _usersById.Values.FirstOrDefault(u =>
            string.Equals(u.NormalizedEmail, target, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(doc == null ? null : CosmosUserRepository.MapToDomain(doc));
    }

    public Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var doc = _usersById.Values.FirstOrDefault(u =>
            u.RefreshTokens.Any(t => string.Equals(t.TokenHash, refreshTokenHash, StringComparison.Ordinal)));

        return Task.FromResult(doc == null ? null : CosmosUserRepository.MapToDomain(doc));
    }

    public Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var target = normalizedEmail.Trim().ToLowerInvariant();
        var exists = _usersById.Values.Any(u =>
            string.Equals(u.NormalizedEmail, target, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_writeLock)
        {
            if (_usersById.Values.Any(u =>
                    string.Equals(u.NormalizedEmail, user.Email.Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateEmailException(user.Email.Value);
            }

            _usersById[user.Id] = CosmosUserRepository.MapToDocument(user);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_writeLock)
        {
            _usersById[user.Id] = CosmosUserRepository.MapToDocument(user);
        }

        return Task.CompletedTask;
    }
}

