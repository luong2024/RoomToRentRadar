using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/listings")]
public sealed class ListingManagementController(
    ListingManagementService listingManagementService) : ControllerBase
{
    private const int MinimumPhotosOnCreate = 3;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoUploadLimits.MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(ManagedListingDto), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ManagedListingDto>> CreateListing(
        [FromForm] ListingWriteForm form,
        CancellationToken cancellationToken)
    {
        if (!ListingWriteValidator.TryValidate(
                form,
                MinimumPhotosOnCreate,
                requireCoordinates: true,
                out var input,
                out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        var result = await listingManagementService.CreateAsync(
            CurrentUserId,
            CurrentUserName,
            input,
            cancellationToken);

        return result.IsSuccess
            ? Created($"/api/v1/listings/{result.Value!.Id}/manage", result.Value)
            : ToProblem(result.Problem!);
    }

    [HttpGet("{id:guid}/manage")]
    [ProducesResponseType(typeof(ManagedListingDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ManagedListingDto>> GetManagedListing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var listing = await listingManagementService.GetManagedAsync(
            CurrentUserId,
            id,
            cancellationToken);

        return listing is null
            ? ToProblem(ListingWriteProblemFactory.NotFoundProblem())
            : Ok(listing);
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoUploadLimits.MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(ManagedListingDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ManagedListingDto>> UpdateListing(
        Guid id,
        [FromForm] ListingWriteForm form,
        CancellationToken cancellationToken)
    {
        if (!ListingWriteValidator.TryValidate(
                form,
                minimumPhotos: 0,
                requireCoordinates: false,
                out var input,
                out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        var result = await listingManagementService.UpdateAsync(
            CurrentUserId,
            id,
            input,
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Problem!);
    }

    [HttpPost("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(ManagedListingDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ManagedListingDto>> UnpublishListing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await listingManagementService.UnpublishAsync(
            CurrentUserId,
            id,
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Problem!);
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private string CurrentUserName
        => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
