using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Ports.Inbound.UseCases.Campuses;

namespace RoomToRentRadar.Api.Controllers;

/// <summary>
/// Reference data endpoints for university campuses (BR-CMP-01..03, <c>docs/erd.md</c> §4.1).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class CampusesController : ControllerBase
{
    private readonly IListCampusesUseCase _listCampusesUseCase;

    public CampusesController(IListCampusesUseCase listCampusesUseCase)
    {
        ArgumentNullException.ThrowIfNull(listCampusesUseCase);
        _listCampusesUseCase = listCampusesUseCase;
    }

    /// <summary>
    /// Campus reference list (BR-CMP-01, operationId: listCampuses). Open to guests.
    /// Returns campuses ordered with the default campus (ICTU) first, then by name.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CampusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CampusDto>>> ListCampuses(CancellationToken ct = default)
    {
        var campuses = await _listCampusesUseCase.ExecuteAsync(ct);
        return Ok(campuses);
    }
}
