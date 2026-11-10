using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Contracts.Responses.Audit;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Audit;

public interface IGetAuditLogsUseCase
{
    Task<PagedResult<AuditLogDto>> ExecuteAsync(AuditLogQueryRequest request, CancellationToken ct = default);
}

