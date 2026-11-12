using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;

namespace RoomToRentRadar.IntegrationTests.TestControllers;

[ApiController]
[Route("api/v1/test-rbac")]
public sealed class RbacProbeController : ControllerBase
{
    [HttpGet("authenticated")]
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    public IActionResult GetAuthenticated() =>
        Ok(new
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            role = User.FindFirstValue(ClaimTypes.Role)
        });

    [HttpGet("student")]
    [Authorize(Policy = AuthorizationPolicies.StudentOnly)]
    public IActionResult GetStudentResource() =>
        Ok(new { policy = AuthorizationPolicies.StudentOnly });

    [HttpGet("landlord")]
    [Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
    public IActionResult GetLandlordResource() =>
        Ok(new { policy = AuthorizationPolicies.LandlordOnly });

    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public IActionResult GetAdminResource() =>
        Ok(new { policy = AuthorizationPolicies.AdminOnly });
}
