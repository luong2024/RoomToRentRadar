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
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.roomId = @roomId AND c.documentType = @documentType")
            .WithParameter("@roomId", normalizedRoomId)
            .WithParameter("@documentType", RoomHouseRulesDocument.RevisionDocumentType);

        var docs = await _dbContext.HouseRules.QueryAsync(query, cancellationToken);

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

        CosmosBatchOperation<RoomHouseRulesDocument> currentOp;
        if (houseRules.CurrentVersion == 1 && string.IsNullOrWhiteSpace(houseRules.ETag))
        {
            currentOp = new CosmosBatchOperation<RoomHouseRulesDocument>(
                CosmosBatchOperationType.Create,
                currentDoc,
                currentDoc.Id);
        }
        else if (!string.IsNullOrWhiteSpace(houseRules.ETag))
        {
            currentOp = new CosmosBatchOperation<RoomHouseRulesDocument>(
                CosmosBatchOperationType.Replace,
                currentDoc,
                currentDoc.Id,
                houseRules.ETag);
        }
        else
        {
            currentOp = new CosmosBatchOperation<RoomHouseRulesDocument>(
                CosmosBatchOperationType.Upsert,
                currentDoc,
                currentDoc.Id);
        }

        var revisionOp = new CosmosBatchOperation<RoomHouseRulesDocument>(
            CosmosBatchOperationType.Create,
            revisionDoc,
            revisionDoc.Id);

        try
        {
            await _dbContext.HouseRules.ExecuteBatchAsync(
                houseRules.RoomId,
                [currentOp, revisionOp],
                cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            throw new ConcurrencyConflictException(
                $"House rules for room '{houseRules.RoomId}' were modified concurrently. Please refresh and retry.");
        }
    }
}
