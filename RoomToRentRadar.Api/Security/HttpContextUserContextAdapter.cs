using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Api.Security;

/// <summary>
/// Adapts ASP.NET Core IHttpContextAccessor to the domain ICurrentUserContextPort (BR-AUTH-07).
/// </summary>
public sealed class HttpContextUserContextAdapter : ICurrentUserContextPort
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUserContextAdapter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ??
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Role =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
}
