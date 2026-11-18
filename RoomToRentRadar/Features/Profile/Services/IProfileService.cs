using RoomToRentRadar.Features.Profile.Models;

namespace RoomToRentRadar.Features.Profile.Services;

public interface IProfileService
{
    Task<ProfileApiDto?> GetMyProfileAsync(CancellationToken cancellationToken = default);
    Task<ProfileApiDto?> UpdateAsync(ProfileApiDto profile, string? newAvatarData, CancellationToken cancellationToken = default);
}
