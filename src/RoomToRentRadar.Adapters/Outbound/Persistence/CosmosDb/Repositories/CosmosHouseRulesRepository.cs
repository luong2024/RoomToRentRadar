using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosHouseRulesRepository : IHouseRulesRepository
{
    private readonly ICosmosDbContext _dbContext;

    public CosmosHouseRulesRepository(ICosmosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<RoomHouseRules?> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var normalizedRoomId = roomId.Trim();
        var docId = RoomHouseRules.BuildId(normalizedRoomId);
        var doc = await _dbContext.HouseRules.ReadItemAsync(docId, normalizedRoomId, cancellationToken);
        return doc?.ToCurrentDomain();
    }

    public async Task<IReadOnlyList<HouseRulesRevision>> GetRevisionsByRoomIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return [];
        }

        var normalizedRoomId = roomId.Trim();
        var docs = await _dbContext.HouseRules.QueryAsync(
            d => string.Equals(d.RoomId, normalizedRoomId, StringComparison.Ordinal) &&
                 string.Equals(d.DocumentType, RoomHouseRulesDocument.RevisionDocumentType, StringComparison.Ordinal),
            cancellationToken);

        return docs
            .OrderByDescending(d => d.Version)
            .Select(d => d.ToRevisionDomain())
            .ToList();
    }

    public async Task SaveAsync(
        RoomHouseRules houseRules,
        HouseRulesRevision newRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(houseRules);
        ArgumentNullException.ThrowIfNull(newRevision);

        var currentDoc = RoomHouseRulesDocument.FromCurrentDomain(houseRules);
        var revisionDoc = RoomHouseRulesDocument.FromRevisionDomain(newRevision);

        try
        {
            if (houseRules.CurrentVersion == 1 && string.IsNullOrWhiteSpace(houseRules.ETag))
            {
                await _dbContext.HouseRules.CreateItemAsync(currentDoc, houseRules.RoomId, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(houseRules.ETag))
            {
                await _dbContext.HouseRules.ReplaceItemAsync(
                    currentDoc,
                    currentDoc.Id,
                    houseRules.RoomId,
                    houseRules.ETag,
                    cancellationToken);
            }
            else
            {
                await _dbContext.HouseRules.UpsertItemAsync(currentDoc, houseRules.RoomId, cancellationToken);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            throw new ConcurrencyConflictException(
                $"House rules for room '{houseRules.RoomId}' were modified concurrently. Please refresh and retry.");
        }

        await _dbContext.HouseRules.CreateItemAsync(revisionDoc, newRevision.RoomId, cancellationToken);
    }
}
