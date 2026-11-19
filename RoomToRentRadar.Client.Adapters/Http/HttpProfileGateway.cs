using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>updateMyProfile arrives with T13.4; task-id stub until then (PLAN §5.4).</summary>
public sealed class HttpProfileGateway : IProfileGateway
{
    public HttpProfileGateway(HttpClient httpClient, ILogger<HttpProfileGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<UserProfileDto> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken ct = default) =>
        Pending.For<UserProfileDto>("T13.4", "updateMyProfile");
}