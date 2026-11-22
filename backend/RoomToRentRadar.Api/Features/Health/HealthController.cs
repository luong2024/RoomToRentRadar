using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.SqliteDataAccess;

namespace RoomToRentRadar.Api.Features.Health;

[ApiController]
[Route("health")]
public sealed class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return Problem(
                detail: "The database is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(new HealthResponse("ok"));
    }
}

public sealed record HealthResponse(string Status);
