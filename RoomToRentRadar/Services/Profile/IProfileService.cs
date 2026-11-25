using RoomToRentRadar.Models.Profile;

namespace RoomToRentRadar.Services.Profile;

public interface IProfileService
{
    Task<ProfileApiDto?> GetMyProfileAsync(CancellationToken cancellationToken = default);
    Task<ProfileApiDto?> UpdateAsync(ProfileApiDto profile, string? newAvatarData, CancellationToken cancellationToken = default);
}
