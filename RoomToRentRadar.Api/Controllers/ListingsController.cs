using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class ListingsController(IListingDetailService listingDetailService) : ControllerBase
{
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListingDetailDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var detail = await listingDetailService.GetByIdAsync(id, isAuthenticated, cancellationToken);

        if (detail is null)
        {
            return NotFound(new { message = $"Listing '{id}' was not found." });
        }

        return Ok(detail);
    }
}
