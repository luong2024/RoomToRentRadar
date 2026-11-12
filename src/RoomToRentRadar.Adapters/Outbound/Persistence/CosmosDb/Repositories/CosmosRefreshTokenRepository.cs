using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ICosmosDbContext _dbContext;
    private readonly CosmosUnitOfWork _unitOfWork;

    public CosmosRefreshTokenRepository(ICosmosDbContext dbContext, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _dbContext = dbContext;
        _unitOfWork = (CosmosUnitOfWork)unitOfWork;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var tracked = _unitOfWork.GetTrackedRefreshTokenByHash(tokenHash);
        if (tracked is not null)
        {
            return tracked;
        }

        if (_dbContext.RefreshTokens is InMemoryCosmosContainer<RefreshTokenDocument> inMemory)
        {
            var match = await inMemory.QueryAsync(
                t => string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal),
                cancellationToken);
            var first = match.FirstOrDefault();
            if (first is null)
            {
                return null;
            }

            _unitOfWork.TrackRefreshTokenLoaded(first.Id, first.ETag);
            return MapToDomain(first);
        }

        var query = new QueryDefinition("SELECT * FROM c WHERE c.tokenHash = @tokenHash")
            .WithParameter("@tokenHash", tokenHash);

        var results = await _dbContext.RefreshTokens.QueryAsync(query, cancellationToken);
        var doc = results.FirstOrDefault();
        if (doc is null)
        {
            return null;
        }

        _unitOfWork.TrackRefreshTokenLoaded(doc.Id, doc.ETag);
        return MapToDomain(doc);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetByFamilyAsync(
        Guid tokenFamily,
        CancellationToken cancellationToken = default)
    {
        var familyString = tokenFamily.ToString();

        IReadOnlyList<RefreshTokenDocument> docs;

        if (_dbContext.RefreshTokens is InMemoryCosmosContainer<RefreshTokenDocument> inMemory)
        {
            docs = await inMemory.QueryAsync(
                t => string.Equals(t.TokenFamily, familyString, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
        }
        else
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.tokenFamily = @tokenFamily")
                .WithParameter("@tokenFamily", familyString);

            docs = await _dbContext.RefreshTokens.QueryAsync(query, cancellationToken);
        }

        foreach (var doc in docs)
        {
            _unitOfWork.TrackRefreshTokenLoaded(doc.Id, doc.ETag);
        }

        var dbTokens = docs.Select(MapToDomain).ToList();

        // Merge with any tracked tokens in unit of work
        var trackedTokens = _unitOfWork.GetTrackedRefreshTokensByFamily(tokenFamily);
        var combined = new Dictionary<Guid, RefreshToken>();

        foreach (var token in dbTokens)
        {
            combined[token.Id] = token;
        }

        foreach (var tracked in trackedTokens)
        {
            combined[tracked.Id] = tracked;
        }

        return combined.Values.ToList();
    }

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _unitOfWork.RegisterNewRefreshToken(refreshToken);
        return Task.CompletedTask;
    }

    public void Update(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _unitOfWork.RegisterDirtyRefreshToken(refreshToken);
    }

    public static RefreshToken MapToDomain(RefreshTokenDocument doc)
    {
        return RefreshToken.Reconstitute(
            Guid.Parse(doc.Id),
            Guid.Parse(doc.UserId),
            doc.TokenHash,
            Guid.Parse(doc.TokenFamily),
            doc.CreatedAt,
            doc.ExpiresAt,
            doc.RevokedAt,
            string.IsNullOrEmpty(doc.ReplacedByTokenId) ? null : Guid.Parse(doc.ReplacedByTokenId),
            doc.RevocationReason);
    }
}
