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
    private readonly Dictionary<Guid, string?> _userEtags = [];
    private readonly Dictionary<Guid, string?> _refreshTokenEtags = [];

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

    public void TrackUserLoaded(string id, string? etag)
    {
        if (Guid.TryParse(id, out var guid))
        {
            _userEtags[guid] = etag;
        }
    }

    public void TrackRefreshTokenLoaded(string id, string? etag)
    {
        if (Guid.TryParse(id, out var guid))
        {
            _refreshTokenEtags[guid] = etag;
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

    public User? GetTrackedUserByExternalLogin(string normalizedProvider, string normalizedSubject)
    {
        var match = _dirtyUsers.Values
            .Concat(_addedUsers.Values)
            .FirstOrDefault(u => u.ExternalLogins.Any(el =>
                string.Equals(el.Provider, normalizedProvider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(el.ProviderSubject, normalizedSubject, StringComparison.Ordinal)));

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
        // 1. Persist added users with global unique email & external login guarantees via dedicated index containers
        foreach (var user in _addedUsers.Values.ToList())
        {
            var normalizedEmail = user.Email.Value.ToLowerInvariant();
            var emailIndexDoc = new EmailIndexDocument
            {
                Id = normalizedEmail,
                UserId = user.Id.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            // 1a. Reserve global email uniqueness
            try
            {
                await _dbContext.EmailIndex.CreateItemAsync(emailIndexDoc, emailIndexDoc.Id, cancellationToken);
                if (_inTransaction)
                {
                    _rollbackActions.Add(() => _dbContext.EmailIndex.DeleteItemAsync(emailIndexDoc.Id, emailIndexDoc.Id, CancellationToken.None));
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new DuplicateEmailException(ex);
            }

            // 1b. Reserve global external login uniqueness across user partitions
            var createdExternalIndexIds = new List<string>();
            try
            {
                foreach (var el in user.ExternalLogins)
                {
                    var extIndexDoc = new ExternalLoginIndexDocument
                    {
                        Id = ExternalLoginIndexDocument.BuildId(el.Provider, el.ProviderSubject),
                        UserId = user.Id.ToString(),
                        Provider = el.Provider,
                        ProviderSubject = el.ProviderSubject,
                        CreatedAt = el.CreatedAt
                    };

                    await _dbContext.ExternalLoginIndex.CreateItemAsync(extIndexDoc, extIndexDoc.Id, cancellationToken);
                    createdExternalIndexIds.Add(extIndexDoc.Id);

                    if (_inTransaction)
                    {
                        var extIdToDelete = extIndexDoc.Id;
                        _rollbackActions.Add(() => _dbContext.ExternalLoginIndex.DeleteItemAsync(extIdToDelete, extIdToDelete, CancellationToken.None));
                    }
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                await CleanupCreatedIndexesAsync(emailIndexDoc.Id, createdExternalIndexIds);
                throw new ExternalIdentityConflictException(
                    "This external identity is already linked to another user.",
                    ex);
            }
            catch
            {
                await CleanupCreatedIndexesAsync(emailIndexDoc.Id, createdExternalIndexIds);
                throw;
            }

            // 1c. Create the user document
            var doc = MapUserToDocument(user);
            try
            {
                var created = await _dbContext.Users.CreateItemAsync(doc, doc.Id, cancellationToken);
                _userEtags[user.Id] = created.ETag;

                if (_inTransaction)
                {
                    var idToDelete = doc.Id;
                    _rollbackActions.Add(() => _dbContext.Users.DeleteItemAsync(idToDelete, idToDelete, CancellationToken.None));
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                await CleanupCreatedIndexesAsync(emailIndexDoc.Id, createdExternalIndexIds);
                throw new DuplicateEmailException(ex);
            }
            catch
            {
                await CleanupCreatedIndexesAsync(emailIndexDoc.Id, createdExternalIndexIds);
                throw;
            }
        }
        _addedUsers.Clear();

        // 2. Persist modified users with Optimistic Concurrency Control (ETag)
        foreach (var user in _dirtyUsers.Values.ToList())
        {
            var doc = MapUserToDocument(user);
            _userEtags.TryGetValue(user.Id, out var expectedEtag);

            var previous = await _dbContext.Users.ReadItemAsync(doc.Id, doc.Id, cancellationToken);
            if (_inTransaction && previous is not null)
            {
                _rollbackActions.Add(() => _dbContext.Users.UpsertItemAsync(previous, previous.Id, CancellationToken.None));
            }

            // Reserve any newly added external logins for this existing user
            var previousExternalKeys = previous?.ExternalLogins
                .Select(el => ExternalLoginIndexDocument.BuildId(el.Provider, el.ProviderSubject))
                .ToHashSet(StringComparer.Ordinal) ?? [];

            var newlyCreatedExternalIndexIds = new List<string>();
            try
            {
                foreach (var el in user.ExternalLogins)
                {
                    var extId = ExternalLoginIndexDocument.BuildId(el.Provider, el.ProviderSubject);
                    if (previousExternalKeys.Contains(extId))
                    {
                        continue;
                    }

                    var extIndexDoc = new ExternalLoginIndexDocument
                    {
                        Id = extId,
                        UserId = user.Id.ToString(),
                        Provider = el.Provider,
                        ProviderSubject = el.ProviderSubject,
                        CreatedAt = el.CreatedAt
                    };

                    await _dbContext.ExternalLoginIndex.CreateItemAsync(extIndexDoc, extIndexDoc.Id, cancellationToken);
                    newlyCreatedExternalIndexIds.Add(extIndexDoc.Id);

                    if (_inTransaction)
                    {
                        var extIdToDelete = extIndexDoc.Id;
                        _rollbackActions.Add(() => _dbContext.ExternalLoginIndex.DeleteItemAsync(extIdToDelete, extIdToDelete, CancellationToken.None));
                    }
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                await CleanupCreatedIndexesAsync(null, newlyCreatedExternalIndexIds);
                throw new ExternalIdentityConflictException(
                    "This external identity is already linked to another user.",
                    ex);
            }
            catch
            {
                await CleanupCreatedIndexesAsync(null, newlyCreatedExternalIndexIds);
                throw;
            }

            try
            {
                var replaced = await _dbContext.Users.ReplaceItemAsync(
                    doc,
                    doc.Id,
                    doc.Id,
                    expectedEtag,
                    cancellationToken);

                _userEtags[user.Id] = replaced.ETag;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                await CleanupCreatedIndexesAsync(null, newlyCreatedExternalIndexIds);
                throw new ConcurrencyConflictException(
                    "User state was concurrently modified by another operation. Please retry.",
                    ex);
            }
            catch
            {
                await CleanupCreatedIndexesAsync(null, newlyCreatedExternalIndexIds);
                throw;
            }
        }
        _dirtyUsers.Clear();

        // 3. Persist added refresh tokens
        foreach (var token in _addedRefreshTokens.Values.ToList())
        {
            var doc = MapRefreshTokenToDocument(token);
            try
            {
                var created = await _dbContext.RefreshTokens.CreateItemAsync(doc, doc.Id, cancellationToken);
                _refreshTokenEtags[token.Id] = created.ETag;

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

        // 4. Persist modified refresh tokens with Optimistic Concurrency Control (ETag)
        foreach (var token in _dirtyRefreshTokens.Values.ToList())
        {
            var doc = MapRefreshTokenToDocument(token);
            _refreshTokenEtags.TryGetValue(token.Id, out var expectedEtag);

            if (_inTransaction)
            {
                var previous = await _dbContext.RefreshTokens.ReadItemAsync(doc.Id, doc.Id, cancellationToken);
                if (previous is not null)
                {
                    _rollbackActions.Add(() => _dbContext.RefreshTokens.UpsertItemAsync(previous, previous.Id, CancellationToken.None));
                }
            }

            try
            {
                var replaced = await _dbContext.RefreshTokens.ReplaceItemAsync(
                    doc,
                    doc.Id,
                    doc.Id,
                    expectedEtag,
                    cancellationToken);

                _refreshTokenEtags[token.Id] = replaced.ETag;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                throw new InvalidRefreshTokenException(
                    "Refresh token has been concurrently modified or already rotated. Session invalidation enforced.");
            }
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

    private async Task CleanupCreatedIndexesAsync(string? emailIndexId, IEnumerable<string> externalLoginIndexIds)
    {
        foreach (var extId in externalLoginIndexIds)
        {
            try
            {
                await _dbContext.ExternalLoginIndex.DeleteItemAsync(extId, extId, CancellationToken.None);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        if (!string.IsNullOrEmpty(emailIndexId))
        {
            try
            {
                await _dbContext.EmailIndex.DeleteItemAsync(emailIndexId, emailIndexId, CancellationToken.None);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    private async Task RollbackAsync(int fromIndex)
    {
        _addedUsers.Clear();
        _dirtyUsers.Clear();
        _addedRefreshTokens.Clear();
        _dirtyRefreshTokens.Clear();
        _userEtags.Clear();
        _refreshTokenEtags.Clear();

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
            IsVerified = user.IsVerified,
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
