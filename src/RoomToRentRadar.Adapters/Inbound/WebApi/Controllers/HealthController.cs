using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "RoomToRentRadar.Api",
            timestampUtc = DateTimeOffset.UtcNow
        });
    }
}
