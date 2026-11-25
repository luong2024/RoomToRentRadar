using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Profile;

/// <summary><c>updateMyProfile</c> (BR-CMP-02, BR-I18N-02, UC-24, UC-25).</summary>
public interface IUpdateMyProfileUseCase
{
    /// <param name="userId">Taken from the access token only, never from the request body (anti-IDOR).</param>
    Task<UserProfileDto> ExecuteAsync(string userId, UpdateMyProfileRequest request, CancellationToken ct = default);
}
