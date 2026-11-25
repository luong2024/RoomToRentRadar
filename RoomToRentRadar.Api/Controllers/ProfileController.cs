using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Profile;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly IUpdateMyProfileUseCase _updateMyProfileUseCase;

    public ProfileController(IUpdateMyProfileUseCase updateMyProfileUseCase)
    {
        _updateMyProfileUseCase = updateMyProfileUseCase;
    }

    [HttpPatch]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileDto>> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest request,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var profile = await _updateMyProfileUseCase.ExecuteAsync(userId, request, ct);
        return Ok(profile);
    }
}
