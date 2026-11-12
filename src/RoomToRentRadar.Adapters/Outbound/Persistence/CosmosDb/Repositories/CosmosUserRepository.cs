using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using User = RoomToRentRadar.Domain.Entities.User;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosUserRepository : IUserRepository
{
    private readonly ICosmosDbContext _dbContext;
    private readonly CosmosUnitOfWork _unitOfWork;

    public CosmosUserRepository(ICosmosDbContext dbContext, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _dbContext = dbContext;
        _unitOfWork = (CosmosUnitOfWork)unitOfWork;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tracked = _unitOfWork.GetTrackedUser(id);
        if (tracked is not null)
        {
            return tracked;
        }

        var idString = id.ToString();
        var doc = await _dbContext.Users.ReadItemAsync(idString, idString, cancellationToken);
        if (doc is null)
        {
            return null;
        }

        _unitOfWork.TrackUserLoaded(doc.Id, doc.ETag);
        return MapToDomain(doc);
    }

    public async Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        var normalized = email.Value.Trim().ToLowerInvariant();

        var tracked = _unitOfWork.GetTrackedUserByEmail(normalized);
        if (tracked is not null)
        {
            return tracked;
        }

        if (_dbContext.Users is InMemoryCosmosContainer<UserDocument> inMemory)
        {
            var match = await inMemory.QueryAsync(
                u => string.Equals(u.NormalizedEmail, normalized, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
            var first = match.FirstOrDefault();
            if (first is null)
            {
                return null;
            }

            _unitOfWork.TrackUserLoaded(first.Id, first.ETag);
            return MapToDomain(first);
        }

        var query = new QueryDefinition("SELECT * FROM c WHERE c.normalizedEmail = @email")
            .WithParameter("@email", normalized);

        var results = await _dbContext.Users.QueryAsync(query, cancellationToken);
        var doc = results.FirstOrDefault();
        if (doc is null)
        {
            return null;
        }

        _unitOfWork.TrackUserLoaded(doc.Id, doc.ETag);
        return MapToDomain(doc);
    }

    public async Task<User?> GetByExternalLoginAsync(
        string provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.Trim().ToUpperInvariant();
        var normalizedSubject = providerSubject.Trim();

        var tracked = _unitOfWork.GetTrackedUserByExternalLogin(normalizedProvider, normalizedSubject);
        if (tracked is not null)
        {
            return tracked;
        }

        // Prefer O(1) point read via ExternalLoginIndex container
        var indexId = ExternalLoginIndexDocument.BuildId(normalizedProvider, normalizedSubject);
        var indexDoc = await _dbContext.ExternalLoginIndex.ReadItemAsync(indexId, indexId, cancellationToken);
        if (indexDoc is not null && Guid.TryParse(indexDoc.UserId, out var indexedUserId))
        {
            return await GetByIdAsync(indexedUserId, cancellationToken);
        }

        if (_dbContext.Users is InMemoryCosmosContainer<UserDocument> inMemory)
        {
            var match = await inMemory.QueryAsync(
                u => u.ExternalLogins.Any(el =>
                    string.Equals(el.Provider, normalizedProvider, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(el.ProviderSubject, normalizedSubject, StringComparison.Ordinal)),
                cancellationToken);

            var first = match.FirstOrDefault();
            if (first is null)
            {
                return null;
            }

            _unitOfWork.TrackUserLoaded(first.Id, first.ETag);
            return MapToDomain(first);
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE ARRAY_CONTAINS(c.externalLogins, { \"provider\": @provider, \"providerSubject\": @providerSubject }, true)")
            .WithParameter("@provider", normalizedProvider)
            .WithParameter("@providerSubject", normalizedSubject);

        var results = await _dbContext.Users.QueryAsync(query, cancellationToken);
        var doc = results.FirstOrDefault();
        if (doc is null)
        {
            return null;
        }

        _unitOfWork.TrackUserLoaded(doc.Id, doc.ETag);
        return MapToDomain(doc);
    }

    public async Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        var user = await GetByEmailAsync(email, cancellationToken);
        return user is not null;
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _unitOfWork.RegisterNewUser(user);
        return Task.CompletedTask;
    }

    public void Update(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _unitOfWork.RegisterDirtyUser(user);
    }

    public static User MapToDomain(UserDocument doc)
    {
        var logins = doc.ExternalLogins.Select(el => UserExternalLogin.Reconstitute(
            Guid.Parse(el.Id),
            Guid.Parse(el.UserId),
            el.Provider,
            el.ProviderSubject,
            EmailAddress.Create(el.ProviderEmail),
            el.CreatedAt));

        return User.Reconstitute(
            Guid.Parse(doc.Id),
            EmailAddress.Create(doc.Email),
            doc.PasswordHash,
            UserRoleExtensions.ParseRole(doc.Role),
            doc.IsActive,
            doc.SecurityStamp,
            doc.CreatedAt,
            doc.UpdatedAt,
            doc.LastLoginAt,
            logins);
    }
}
