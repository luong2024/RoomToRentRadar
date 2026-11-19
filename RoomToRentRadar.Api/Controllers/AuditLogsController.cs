using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Contracts.Responses.Audit;
using RoomToRentRadar.Ports.Inbound.UseCases.Audit;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Policy = AuthorizationPolicies.AdminPolicy)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IGetAuditLogsUseCase _getAuditLogsUseCase;

    public AuditLogsController(IGetAuditLogsUseCase getAuditLogsUseCase)
    {
        ArgumentNullException.ThrowIfNull(getAuditLogsUseCase);
        _getAuditLogsUseCase = getAuditLogsUseCase;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
        [FromQuery] AuditLogQueryRequest request,
        CancellationToken ct = default)
    {
        var result = await _getAuditLogsUseCase.ExecuteAsync(request, ct);
        return Ok(result);
    }
}

