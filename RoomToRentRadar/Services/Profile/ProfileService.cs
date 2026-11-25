using RoomToRentRadar.Models.Profile;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Services.Profile;

public sealed class ProfileService(IApiClient apiClient) : IProfileService
{
    public Task<ProfileApiDto?> GetMyProfileAsync(CancellationToken cancellationToken = default) =>
        apiClient.GetAsync<ProfileApiDto>("api/v1/profiles/me", cancellationToken);

    public Task<ProfileApiDto?> UpdateAsync(ProfileApiDto profile, string? newAvatarData, CancellationToken cancellationToken = default)
    {
        var avatarUrl = newAvatarData ?? profile.AvatarUrl;
        return profile.Role.ToUpperInvariant() switch
        {
            "STUDENT" => apiClient.PutAsync<UpdateTenantProfileRequest, ProfileApiDto>("api/v1/profiles/me/tenant",
                new(profile.FullName, profile.PhoneNumber, avatarUrl, profile.Bio, profile.Tenant?.StudentId,
                    profile.Tenant?.University, profile.Tenant?.Faculty, null, null, null, null, profile.Tenant?.MaxBudget), cancellationToken),
            "LANDLORD" => apiClient.PutAsync<UpdateLandlordProfileRequest, ProfileApiDto>("api/v1/profiles/me/landlord",
                new(profile.FullName, profile.PhoneNumber, avatarUrl, profile.Bio, profile.Landlord?.BusinessName,
                    profile.Landlord?.Address, null, null, null, null, null, null, null), cancellationToken),
            "ADMIN" => apiClient.PutAsync<UpdateAdminProfileRequest, ProfileApiDto>("api/v1/profiles/me/admin",
                new(profile.FullName, profile.PhoneNumber, profile.Admin?.StaffCode ?? string.Empty,
                    profile.Admin?.Department ?? string.Empty, profile.Admin?.Title ?? string.Empty, avatarUrl, profile.Bio), cancellationToken),
            _ => throw new InvalidOperationException("The current account role is not supported.")
        };
    }
}
