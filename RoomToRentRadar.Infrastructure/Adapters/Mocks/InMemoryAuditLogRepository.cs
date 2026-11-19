using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly List<AuditLogEntry> _logs = new();
    private readonly object _lock = new();

    public Task InsertAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _logs.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<PagedResult<AuditLogEntry>> QueryAsync(AuditLogQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var query = _logs.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                query = query.Where(l => string.Equals(l.UserId, request.UserId.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.Action))
            {
                query = query.Where(l => string.Equals(l.Action, request.Action.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.EntityType))
            {
                query = query.Where(l => string.Equals(l.EntityType, request.EntityType.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (request.FromUtc.HasValue)
            {
                query = query.Where(l => l.TimestampUtc >= request.FromUtc.Value);
            }

            if (request.ToUtc.HasValue)
            {
                query = query.Where(l => l.TimestampUtc <= request.ToUtc.Value);
            }

            var ordered = query.OrderByDescending(l => l.TimestampUtc).ToList();
            var totalCount = ordered.Count;

            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var offset = (pageNumber - 1) * pageSize;

            var items = ordered.Skip(offset).Take(pageSize).ToList();

            return Task.FromResult(new PagedResult<AuditLogEntry>(items, totalCount, pageNumber, pageSize));
        }
    }
}

