using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

[ApiController]
[Authorize(Policy = AuthPolicies.Landlord)]
[Route("api/v1/listings")]
public sealed class ListingManagementController(
    IListingManagementUseCase listingManagementUseCase,
    IHouseRulesUseCase houseRulesUseCase) : ControllerBase
{
    private const int MinimumPhotosOnCreate = 3;

    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<ManagedListingDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ManagedListingDto>>> GetMyListings(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 100)] int limit = 50,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0)
        => Ok(await listingManagementUseCase.GetMineAsync(CurrentUserId, limit, offset, cancellationToken));

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

        var command = ToCommand(input);
        ListingWriteResult<ManagedListingDto> result;
        try
        {
            result = await listingManagementUseCase.CreateAsync(
                CurrentUserId,
                CurrentUserName,
                command,
                cancellationToken);
        }
        finally
        {
            DisposeUploads(command);
        }

        return result.IsSuccess
            ? Created($"/api/v1/listings/{result.Value!.Id}/manage", result.Value)
            : ToProblem(result.Error!);
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
        var listing = await listingManagementUseCase.GetManagedAsync(
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

        var command = ToCommand(input);
        ListingWriteResult<ManagedListingDto> result;
        try
        {
            result = await listingManagementUseCase.UpdateAsync(
                CurrentUserId,
                id,
                command,
                cancellationToken);
        }
        finally
        {
            DisposeUploads(command);
        }

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    [HttpPut("{id:guid}/house-rules")]
    [ProducesResponseType(typeof(HouseRulesDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<HouseRulesDto>> UpdateHouseRules(
        Guid id,
        [FromBody] UpdateHouseRulesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await houseRulesUseCase.UpdateAsync(
            CurrentUserId,
            id,
            request,
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
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
        var result = await listingManagementUseCase.UnpublishAsync(
            CurrentUserId,
            id,
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    private static ListingWriteCommand ToCommand(ListingWriteInput input) => new(
        input.Title,
        input.Address,
        input.Price,
        input.Deposit,
        input.AreaM2,
        input.RoomType,
        input.Description,
        input.Bedrooms,
        input.Lat,
        input.Lng,
        input.LandlordName,
        input.LandlordPhone,
        input.Photos
            .Select(file => new PhotoUpload(
                file.FileName,
                file.ContentType,
                file.Length,
                file.OpenReadStream()))
            .ToList());

    private static void DisposeUploads(ListingWriteCommand command)
    {
        foreach (var photo in command.Photos)
        {
            photo.Content.Dispose();
        }
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private string CurrentUserName
        => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    private ObjectResult ToProblem(ListingWriteError error)
        => ToProblem(ToProblemDetails(error));

    private static ProblemDetails ToProblemDetails(ListingWriteError error) => error.Code switch
    {
        ListingWriteErrorCodes.NotFound => ListingWriteProblemFactory.NotFoundProblem(),
        ListingWriteErrorCodes.InvalidPhoto => ListingWriteProblemFactory.InvalidPhotoProblem(),
        ListingWriteErrorCodes.PhotoLimitExceeded => ListingWriteProblemFactory.PhotoLimitExceededProblem(),
        ListingWriteErrorCodes.InvalidListingTransition => ListingWriteProblemFactory.InvalidListingTransitionProblem(),
        ListingWriteErrorCodes.InvalidHouseRules => ListingWriteProblemFactory.InvalidHouseRulesProblem(
            error.ValidationErrors!),
        _ => throw new InvalidOperationException($"Unmapped listing write error code '{error.Code}'.")
    };

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
