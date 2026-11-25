using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosListingReportRepository : IListingReportRepository
{
    private readonly ICosmosDbContext _dbContext;

    public CosmosListingReportRepository(ICosmosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ListingReport?> GetByIdAsync(
        string reportId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var doc = await _dbContext.ListingReports.ReadItemAsync(reportId.Trim(), roomId.Trim(), cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<bool> HasActiveReportByUserAsync(
        string roomId,
        Guid reporterUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || reporterUserId == Guid.Empty)
        {
            return false;
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.roomId = @roomId AND c.reporterUserId = @reporterUserId AND (c.status = @pendingStatus OR c.status = @inReviewStatus)")
            .WithParameter("@roomId", roomId.Trim())
            .WithParameter("@reporterUserId", reporterUserId.ToString())
            .WithParameter("@pendingStatus", (int)ReportStatus.Pending)
            .WithParameter("@inReviewStatus", (int)ReportStatus.InReview);

        var matches = await _dbContext.ListingReports.QueryAsync(query, cancellationToken);
        return matches.Count > 0;
    }

    public async Task AddAsync(ListingReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        var doc = ListingReportDocument.FromDomain(report);
        await _dbContext.ListingReports.CreateItemAsync(doc, report.RoomId, cancellationToken);
    }

    public async Task UpdateAsync(ListingReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        var doc = ListingReportDocument.FromDomain(report);

        try
        {
            await _dbContext.ListingReports.ReplaceItemAsync(
                doc,
                doc.Id,
                doc.RoomId,
                report.ETag,
                cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            throw new ConcurrencyConflictException(
                $"Listing report '{report.Id}' was modified concurrently. Please refresh and retry.");
        }
    }

    public async Task<(IReadOnlyList<ListingReport> Items, int TotalCount)> GetModerationQueueAsync(
        ReportStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = status.HasValue
            ? new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", (int)status.Value)
            : new QueryDefinition("SELECT * FROM c");

        var docs = await _dbContext.ListingReports.QueryAsync(query, cancellationToken);

        // FIFO ordering: oldest pending reports appear first in moderation queue
        var ordered = docs
            .OrderBy(d => d.CreatedAtUtc)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => d.ToDomain())
            .ToList();

        return (pagedItems, totalCount);
    }
}
