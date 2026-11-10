using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IAuditLogRepository
{
    Task InsertAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<PagedResult<AuditLogEntry>> QueryAsync(AuditLogQueryRequest request, CancellationToken ct = default);
}

