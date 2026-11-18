using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/routing")]
[Produces("application/json")]
public sealed class RoutingController : ControllerBase
{
    private readonly IRoutingService _routingService;

    public RoutingController(IRoutingService routingService)
    {
        _routingService = routingService ?? throw new ArgumentNullException(nameof(routingService));
    }

    /// <summary>
    /// Calculates a driving route between two explicitly supplied coordinates.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> CalculateRoute([FromBody] RouteRequest? request, CancellationToken cancellationToken)
    {
        var route = await _routingService.GetRouteAsync(request, cancellationToken);
        return Ok(route);
    }
}
