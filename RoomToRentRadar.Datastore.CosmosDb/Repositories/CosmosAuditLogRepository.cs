using System.Text;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Datastore.CosmosDb.Context;
using RoomToRentRadar.Datastore.CosmosDb.Documents;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.CosmosDb.Repositories;

public sealed class CosmosAuditLogRepository : IAuditLogRepository
{
    private readonly CosmosDbContext _dbContext;

    public CosmosAuditLogRepository(CosmosDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task InsertAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var container = _dbContext.GetAuditLogsContainer();
        var document = MapToDocument(entry);

        await container.CreateItemAsync(
            document,
            new PartitionKey(document.UserId),
            cancellationToken: ct);
    }

    public async Task<PagedResult<AuditLogEntry>> QueryAsync(AuditLogQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var offset = (pageNumber - 1) * pageSize;

        var container = _dbContext.GetAuditLogsContainer();

        var whereClause = new StringBuilder("WHERE 1=1");
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            whereClause.Append(" AND (c.userId = @userId OR c.actualUserId = @userId)");
            parameters["@userId"] = request.UserId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            whereClause.Append(" AND c.action = @action");
            parameters["@action"] = request.Action.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            whereClause.Append(" AND c.entityType = @entityType");
            parameters["@entityType"] = request.EntityType.Trim();
        }

        if (request.FromUtc.HasValue)
        {
            whereClause.Append(" AND c.timestampUtc >= @fromUtc");
            parameters["@fromUtc"] = request.FromUtc.Value.ToUniversalTime();
        }

        if (request.ToUtc.HasValue)
        {
            whereClause.Append(" AND c.timestampUtc <= @toUtc");
            parameters["@toUtc"] = request.ToUtc.Value.ToUniversalTime();
        }

        // 1. Count query
        var countSql = $"SELECT VALUE COUNT(1) FROM c {whereClause}";
        var countDef = new QueryDefinition(countSql);
        foreach (var (k, v) in parameters)
        {
            countDef.WithParameter(k, v);
        }

        var totalCount = 0;
        using (var countIterator = container.GetItemQueryIterator<int>(countDef))
        {
            if (countIterator.HasMoreResults)
            {
                var response = await countIterator.ReadNextAsync(ct);
                totalCount = response.FirstOrDefault();
            }
        }

        if (totalCount == 0)
        {
            return new PagedResult<AuditLogEntry>(Array.Empty<AuditLogEntry>(), 0, pageNumber, pageSize);
        }

        // 2. Data query
        var dataSql = $"SELECT * FROM c {whereClause} ORDER BY c.timestampUtc DESC OFFSET @offset LIMIT @pageSize";
        var dataDef = new QueryDefinition(dataSql);
        foreach (var (k, v) in parameters)
        {
            dataDef.WithParameter(k, v);
        }
        dataDef.WithParameter("@offset", offset);
        dataDef.WithParameter("@pageSize", pageSize);

        var items = new List<AuditLogEntry>();
        using (var dataIterator = container.GetItemQueryIterator<AuditLogDocument>(dataDef))
        {
            while (dataIterator.HasMoreResults)
            {
                var response = await dataIterator.ReadNextAsync(ct);
                foreach (var doc in response)
                {
                    items.Add(MapToDomain(doc));
                }
            }
        }

        return new PagedResult<AuditLogEntry>(items, totalCount, pageNumber, pageSize);
    }

    private static AuditLogDocument MapToDocument(AuditLogEntry entry)
    {
        return new AuditLogDocument
        {
            Id = entry.Id,
            UserId = entry.GetEffectivePartitionKey(),
            ActualUserId = entry.UserId,
            UserEmail = entry.UserEmail,
            Role = entry.Role,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            DetailsJson = entry.DetailsJson,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            IsSuccess = entry.IsSuccess,
            ErrorMessage = entry.ErrorMessage,
            TimestampUtc = entry.TimestampUtc
        };
    }

    private static AuditLogEntry MapToDomain(AuditLogDocument doc)
    {
        var resolvedUserId = doc.ActualUserId ?? (doc.UserId.StartsWith("anonymous_") ? null : doc.UserId);

        return new AuditLogEntry(
            id: doc.Id,
            userId: resolvedUserId,
            userEmail: doc.UserEmail,
            role: doc.Role,
            action: doc.Action,
            entityType: doc.EntityType,
            entityId: doc.EntityId,
            detailsJson: doc.DetailsJson,
            ipAddress: doc.IpAddress,
            userAgent: doc.UserAgent,
            statusCode: doc.StatusCode,
            durationMs: doc.DurationMs,
            isSuccess: doc.IsSuccess,
            errorMessage: doc.ErrorMessage,
            timestampUtc: doc.TimestampUtc);
    }
}

