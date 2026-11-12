using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class UpdateLandlordProfileUseCase : IUpdateLandlordProfileUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly TimeProvider _timeProvider;

    public UpdateLandlordProfileUseCase(IUserProfileRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateLandlordCommand command, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var profile = await _repository.GetByUserIdAsync(userId, cancellationToken)
            ?? UserProfile.CreateDefault(userId, UserRole.Landlord, now);

        var phone = PhoneNumber.CreateOptional(command.PhoneNumber);
        profile.UpdateBasicInfo(command.FullName, phone, command.AvatarUrl, command.Bio, now);

        var currentKyc = profile.Landlord?.VerificationStatus ?? KycVerificationStatus.Unverified;
        var isSubmittingKyc = !string.IsNullOrWhiteSpace(command.CitizenIdNumber) &&
                              !string.IsNullOrWhiteSpace(command.IdentityFrontPhotoUrl);

        var status = isSubmittingKyc && currentKyc != KycVerificationStatus.Verified
            ? KycVerificationStatus.Pending
            : currentKyc;

        var landlordDetails = new LandlordDetails(
            command.BusinessName,
            command.Address,
            command.CitizenIdNumber,
            command.CitizenIdIssueDate,
            command.IdentityFrontPhotoUrl,
            command.IdentityBackPhotoUrl,
            status,
            isSubmittingKyc ? "Submitted for review" : profile.Landlord?.VerificationNotes,
            profile.Landlord?.VerifiedAt,
            command.BankName,
            command.BankAccountNumber,
            command.BankAccountHolder,
            profile.Landlord?.Rating ?? 5.0,
            profile.Landlord?.ReviewCount ?? 0);

        profile.UpdateLandlordDetails(landlordDetails, now);
        await _repository.UpdateAsync(profile, cancellationToken);

        return GetMyProfileUseCase.MapToDto(profile);
    }
}