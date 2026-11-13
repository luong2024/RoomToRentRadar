using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class GetPendingLandlordsUseCase : IGetPendingLandlordsUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly IKycDocumentStorageService? _kycStorage;

    public GetPendingLandlordsUseCase(
        IUserProfileRepository repository,
        IKycDocumentStorageService? kycStorage = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _kycStorage = kycStorage;
    }

    public async Task<IReadOnlyList<AdminLandlordKycDto>> ExecuteAsync(
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            throw new RequestValidationException(nameof(skip), "skip must be greater than or equal to 0.");
        }

        if (take < 1 || take > 100)
        {
            throw new RequestValidationException(nameof(take), "take must be between 1 and 100.");
        }

        var pendingProfiles = await _repository.GetPendingLandlordsAsync(skip, take, cancellationToken);
        var dtos = new List<AdminLandlordKycDto>(pendingProfiles.Count);

        foreach (var profile in pendingProfiles)
        {
            var dto = await MapToAdminKycDtoAsync(profile, _kycStorage, cancellationToken);
            dtos.Add(dto);
        }

        return dtos;
    }

    public static async Task<AdminLandlordKycDto> MapToAdminKycDtoAsync(
        UserProfile profile,
        IKycDocumentStorageService? kycStorage,
        CancellationToken cancellationToken)
    {
        string? signedFrontUrl = null;
        string? signedBackUrl = null;

        if (kycStorage is not null)
        {
            if (!string.IsNullOrWhiteSpace(profile.Landlord?.IdentityFrontBlobName))
            {
                signedFrontUrl = await kycStorage.GeneratePresignedReadUrlAsync(
                    profile.Landlord.IdentityFrontBlobName,
                    TimeSpan.FromMinutes(15),
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(profile.Landlord?.IdentityBackBlobName))
            {
                signedBackUrl = await kycStorage.GeneratePresignedReadUrlAsync(
                    profile.Landlord.IdentityBackBlobName,
                    TimeSpan.FromMinutes(15),
                    cancellationToken);
            }
        }

        return new AdminLandlordKycDto(
            UserId: profile.UserId,
            FullName: profile.FullName,
            PhoneNumber: profile.PhoneNumber?.Value,
            AvatarUrl: profile.AvatarUrl,
            BusinessName: profile.Landlord?.BusinessName,
            Address: profile.Landlord?.Address,
            CitizenIdNumber: profile.Landlord?.CitizenIdNumber,
            CitizenIdIssueDate: profile.Landlord?.CitizenIdIssueDate,
            IdentityFrontPhotoUrl: signedFrontUrl,
            IdentityBackPhotoUrl: signedBackUrl,
            VerificationStatus: profile.Landlord?.VerificationStatus.ToString() ?? "Unverified",
            VerificationNotes: profile.Landlord?.VerificationNotes,
            VerifiedAt: profile.Landlord?.VerifiedAt,
            CreatedAt: profile.CreatedAt,
            UpdatedAt: profile.UpdatedAt);
    }
}
