using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using User = RoomToRentRadar.Domain.Entities.User;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;

public sealed class CosmosUnitOfWork : IUnitOfWork
{
    private readonly ICosmosDbContext _dbContext;

    private readonly Dictionary<Guid, User> _addedUsers = [];
    private readonly Dictionary<Guid, User> _dirtyUsers = [];
    private readonly Dictionary<Guid, RefreshToken> _addedRefreshTokens = [];
    private readonly Dictionary<Guid, RefreshToken> _dirtyRefreshTokens = [];

    private readonly List<Func<Task>> _rollbackActions = [];
    private bool _inTransaction;

    public CosmosUnitOfWork(ICosmosDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public void RegisterNewUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _addedUsers[user.Id] = user;
    }

    public void RegisterDirtyUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!_addedUsers.ContainsKey(user.Id))
        {
            _dirtyUsers[user.Id] = user;
        }
    }

    public void RegisterNewRefreshToken(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _addedRefreshTokens[token.Id] = token;
    }

    public void RegisterDirtyRefreshToken(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (!_addedRefreshTokens.ContainsKey(token.Id))
        {
            _dirtyRefreshTokens[token.Id] = token;
        }
    }

    public User? GetTrackedUser(Guid id)
    {
        if (_dirtyUsers.TryGetValue(id, out var dirty))
        {
            return dirty;
        }

        if (_addedUsers.TryGetValue(id, out var added))
        {
            return added;
        }

        return null;
    }

    public User? GetTrackedUserByEmail(string normalizedEmail)
    {
        var match = _dirtyUsers.Values
            .Concat(_addedUsers.Values)
            .FirstOrDefault(u => string.Equals(u.Email.Value, normalizedEmail, StringComparison.OrdinalIgnoreCase));

        return match;
    }

    public RefreshToken? GetTrackedRefreshTokenByHash(string tokenHash)
    {
        var match = _dirtyRefreshTokens.Values
            .Concat(_addedRefreshTokens.Values)
            .FirstOrDefault(t => string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal));

        return match;
    }

    public IReadOnlyList<RefreshToken> GetTrackedRefreshTokensByFamily(Guid family)
    {
        return _dirtyRefreshTokens.Values
            .Concat(_addedRefreshTokens.Values)
            .Where(t => t.TokenFamily == family)
            .ToList();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Persist added users
        foreach (var user in _addedUsers.Values.ToList())
        {
            var doc = MapUserToDocument(user);
            try
            {
                await _dbContext.Users.CreateItemAsync(doc, doc.Id, cancellationToken);
                if (_inTransaction)
                {
                    var idToDelete = doc.Id;
                    _rollbackActions.Add(() => _dbContext.Users.DeleteItemAsync(idToDelete, idToDelete, CancellationToken.None));
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new DuplicateEmailException(ex);
            }
        }
        _addedUsers.Clear();

        // 2. Persist modified users
        foreach (var user in _dirtyUsers.Values.ToList())
        {
            var doc = MapUserToDocument(user);
            if (_inTransaction)
            {
                var previous = await _dbContext.Users.ReadItemAsync(doc.Id, doc.Id, cancellationToken);
                if (previous is not null)
                {
                    _rollbackActions.Add(() => _dbContext.Users.UpsertItemAsync(previous, previous.Id, CancellationToken.None));
                }
            }

            await _dbContext.Users.UpsertItemAsync(doc, doc.Id, cancellationToken);
        }
        _dirtyUsers.Clear();

        // 3. Persist added refresh tokens
        foreach (var token in _addedRefreshTokens.Values.ToList())
        {
            var doc = MapRefreshTokenToDocument(token);
            try
            {
                await _dbContext.RefreshTokens.CreateItemAsync(doc, doc.Id, cancellationToken);
                if (_inTransaction)
                {
                    var idToDelete = doc.Id;
                    _rollbackActions.Add(() => _dbContext.RefreshTokens.DeleteItemAsync(idToDelete, idToDelete, CancellationToken.None));
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"Duplicate token hash conflict for token '{token.TokenHash}'.", ex);
            }
        }
        _addedRefreshTokens.Clear();

        // 4. Persist modified refresh tokens
        foreach (var token in _dirtyRefreshTokens.Values.ToList())
        {
            var doc = MapRefreshTokenToDocument(token);
            if (_inTransaction)
            {
                var previous = await _dbContext.RefreshTokens.ReadItemAsync(doc.Id, doc.Id, cancellationToken);
                if (previous is not null)
                {
                    _rollbackActions.Add(() => _dbContext.RefreshTokens.UpsertItemAsync(previous, previous.Id, CancellationToken.None));
                }
            }

            await _dbContext.RefreshTokens.UpsertItemAsync(doc, doc.Id, cancellationToken);
        }
        _dirtyRefreshTokens.Clear();
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_inTransaction)
        {
            await operation(cancellationToken);
            return;
        }

        _inTransaction = true;
        var startRollbackIndex = _rollbackActions.Count;

        try
        {
            await operation(cancellationToken);
        }
        catch
        {
            await RollbackAsync(startRollbackIndex);
            throw;
        }
        finally
        {
            _inTransaction = false;
            _rollbackActions.Clear();
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_inTransaction)
        {
            return await operation(cancellationToken);
        }

        _inTransaction = true;
        var startRollbackIndex = _rollbackActions.Count;

        try
        {
            var result = await operation(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackAsync(startRollbackIndex);
            throw;
        }
        finally
        {
            _inTransaction = false;
            _rollbackActions.Clear();
        }
    }

    private async Task RollbackAsync(int fromIndex)
    {
        _addedUsers.Clear();
        _dirtyUsers.Clear();
        _addedRefreshTokens.Clear();
        _dirtyRefreshTokens.Clear();

        for (var i = _rollbackActions.Count - 1; i >= fromIndex; i--)
        {
            try
            {
                await _rollbackActions[i]();
            }
            catch
            {
                // Best-effort rollback
            }
        }
    }

    public static UserDocument MapUserToDocument(User user)
    {
        return new UserDocument
        {
            Id = user.Id.ToString(),
            Email = user.Email.Value,
            NormalizedEmail = user.Email.Value.ToLowerInvariant(),
            PasswordHash = user.PasswordHash,
            Role = user.Role.ToRoleName(),
            IsActive = user.IsActive,
            SecurityStamp = user.SecurityStamp,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            ExternalLogins = user.ExternalLogins.Select(el => new UserExternalLoginDocument
            {
                Id = el.Id.ToString(),
                UserId = el.UserId.ToString(),
                Provider = el.Provider,
                ProviderSubject = el.ProviderSubject,
                ProviderEmail = el.ProviderEmail.Value,
                CreatedAt = el.CreatedAt
            }).ToList()
        };
    }

    public static RefreshTokenDocument MapRefreshTokenToDocument(RefreshToken token)
    {
        return new RefreshTokenDocument
        {
            Id = token.Id.ToString(),
            UserId = token.UserId.ToString(),
            TokenHash = token.TokenHash,
            TokenFamily = token.TokenFamily.ToString(),
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            RevokedAt = token.RevokedAt,
            ReplacedByTokenId = token.ReplacedByTokenId?.ToString(),
            RevocationReason = token.RevocationReason
        };
    }
}
