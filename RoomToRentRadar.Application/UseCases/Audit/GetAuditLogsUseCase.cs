using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Contracts.Responses.Audit;
using RoomToRentRadar.Ports.Inbound.UseCases.Audit;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Audit;

public sealed class GetAuditLogsUseCase : IGetAuditLogsUseCase
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsUseCase(IAuditLogRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<PagedResult<AuditLogDto>> ExecuteAsync(AuditLogQueryRequest request, CancellationToken ct = default)
    {
        var effectiveRequest = request ?? new AuditLogQueryRequest();
        var pagedEntries = await _repository.QueryAsync(effectiveRequest, ct);

        var dtos = pagedEntries.Items.Select(entry => new AuditLogDto(
            Id: entry.Id,
            UserId: entry.UserId,
            UserEmail: entry.UserEmail,
            Role: entry.Role,
            Action: entry.Action,
            EntityType: entry.EntityType,
            EntityId: entry.EntityId,
            DetailsJson: entry.DetailsJson,
            IpAddress: entry.IpAddress,
            UserAgent: entry.UserAgent,
            StatusCode: entry.StatusCode,
            DurationMs: entry.DurationMs,
            IsSuccess: entry.IsSuccess,
            ErrorMessage: entry.ErrorMessage,
            TimestampUtc: entry.TimestampUtc
        )).ToList();

        return new PagedResult<AuditLogDto>(
            dtos,
            pagedEntries.TotalCount,
            pagedEntries.PageNumber,
            pagedEntries.PageSize);
    }
}
