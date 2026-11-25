using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Services.Api;

public interface IProfileClient
{
    Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpdateDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default);
}
