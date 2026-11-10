using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Contracts.Responses.Audit;
using RoomToRentRadar.Ports.Inbound.UseCases.Audit;

namespace RoomToRentRadar.Application.UseCases.Audit;

public sealed class GetAuditLogsUseCase : IGetAuditLogsUseCase
{
    public Task<PagedResult<AuditLogDto>> ExecuteAsync(AuditLogQueryRequest request, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}

