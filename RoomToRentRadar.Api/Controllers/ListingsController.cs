using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class ListingsController(
    IListingService listingService,
    IListingDetailService listingDetailService,
    IListingSearchService listingSearchService) : ControllerBase
{
    /// <summary>GET /api/v1/listings?pageNumber=1&amp;pageSize=10&amp;sortBy=distance|rent|area&amp;isAscending=true (US01 T01.1)</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ListingCardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ListingCardDto>>> GetListings(
        [FromQuery] ListingQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await listingService.GetListingsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromQuery] ListingSearchQuery query,
        CancellationToken cancellationToken)
    {
        var result = await listingSearchService.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

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
