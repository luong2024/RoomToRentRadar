using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.Api.Security;

public sealed class HttpContextUserContextAdapter : ICurrentUserContextPort, ICurrentUserContext
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
}
