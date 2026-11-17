using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Own profile updates (Profile tag of <c>docs/api/openapi.yaml</c>, BR-CMP-02, BR-I18N-02).</summary>
public interface IProfileGateway
{
    [ApiOperation("updateMyProfile")]
    Task<UserProfileDto> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken ct = default);
}