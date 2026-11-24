using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Services.Favourites;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class FavouritesController(IFavouriteService favouriteService) : ControllerBase
{
    public const string UserIdHeaderName = "X-User-Id";

    /// <summary>Thêm, xóa hoặc đảo trạng thái yêu thích một phòng trọ (US06 T06.1).</summary>
    [HttpPost("toggle")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ToggleFavouriteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ToggleFavouriteResponse>> Toggle(
        [FromBody] ToggleFavouriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        try
        {
            var result = await favouriteService.ToggleAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (FavouriteValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Yêu cầu không hợp lệ");
        }
        catch (FavouriteListingNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }

    /// <summary>Lấy danh sách phòng yêu thích kèm giá thuê và trạng thái hiện tại mới nhất.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FavouritesListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FavouritesListResponse>> GetFavourites(
        [FromQuery] bool includeUnavailable = true,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        try
        {
            var result = await favouriteService.GetFavouritesAsync(userId, includeUnavailable, cancellationToken);
            return Ok(result);
        }
        catch (FavouriteValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Yêu cầu không hợp lệ");
        }
    }

    /// <summary>Đồng bộ danh sách yêu thích từ thiết bị/offline lên tài khoản (Account Sync).</summary>
    [HttpPost("sync")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SyncFavouritesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SyncFavouritesResponse>> Sync(
        [FromBody] SyncFavouritesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        try
        {
            var result = await favouriteService.SyncAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (FavouriteValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Dữ liệu đồng bộ không hợp lệ");
        }
    }

    /// <summary>Xóa nhanh một phòng khỏi danh sách yêu thích.</summary>
    [HttpDelete("{listingId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ToggleFavouriteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ToggleFavouriteResponse>> Remove(
        string listingId,
        CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        try
        {
            var result = await favouriteService.ToggleAsync(
                userId,
                new ToggleFavouriteRequest { ListingId = listingId, Action = "Remove" },
                cancellationToken);
            return Ok(result);
        }
        catch (FavouriteValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Yêu cầu không hợp lệ");
        }
    }

    private bool TryResolveUserId(out string userId, out ActionResult? unauthorizedResult)
    {
        var claimId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
        var headerId = Request?.Headers[UserIdHeaderName].FirstOrDefault();
        var raw = !string.IsNullOrWhiteSpace(claimId) ? claimId : headerId;

        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Length > 100)
        {
            userId = string.Empty;
            unauthorizedResult = Problem(
                detail: $"Thiếu thông tin người dùng (cần JWT claim hoặc header '{UserIdHeaderName}' <= 100 ký tự).",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Chưa xác thực người dùng");
            return false;
        }

        userId = raw.Trim();
        unauthorizedResult = null;
        return true;
    }
}
