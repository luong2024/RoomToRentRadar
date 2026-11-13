using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class GetMyProfileUseCase : IGetMyProfileUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly IKycDocumentStorageService? _kycStorage;
    private readonly TimeProvider _timeProvider;

    public GetMyProfileUseCase(
        IUserProfileRepository repository,
        IKycDocumentStorageService? kycStorage = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _kycStorage = kycStorage;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            var parsedRole = UserRoleExtensions.ParseRole(role);
            var newProfile = UserProfile.CreateDefault(userId, parsedRole, _timeProvider.GetUtcNow());
            try
            {
                await _repository.AddAsync(newProfile, cancellationToken);
                existing = newProfile;
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent operation created the profile. Re-read the saved profile safely.
                existing = await _repository.GetByUserIdAsync(userId, cancellationToken)
                    ?? throw new ConcurrencyConflictException($"Failed to resolve concurrent profile creation for user '{userId}'.");
            }
        }

        return await MapToDtoAsync(existing, _kycStorage, cancellationToken);
    }

    public static async Task<UserProfileDto> MapToDtoAsync(
        UserProfile p,
        IKycDocumentStorageService? kycStorage = null,
        CancellationToken cancellationToken = default)
    {
        LandlordDetailsDto? landlordDto = null;
        if (p.Landlord is not null)
        {
            string? signedFrontUrl = null;
            string? signedBackUrl = null;

            if (kycStorage is not null)
            {
                if (!string.IsNullOrWhiteSpace(p.Landlord.IdentityFrontBlobName))
                {
                    signedFrontUrl = await kycStorage.GeneratePresignedReadUrlAsync(
                        p.Landlord.IdentityFrontBlobName,
                        TimeSpan.FromMinutes(15),
                        cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(p.Landlord.IdentityBackBlobName))
                {
                    signedBackUrl = await kycStorage.GeneratePresignedReadUrlAsync(
                        p.Landlord.IdentityBackBlobName,
                        TimeSpan.FromMinutes(15),
                        cancellationToken);
                }
            }

            landlordDto = new LandlordDetailsDto(
                p.Landlord.BusinessName,
                p.Landlord.Address,
                SensitiveDataMasker.Mask(p.Landlord.CitizenIdNumber),
                p.Landlord.CitizenIdIssueDate,
                signedFrontUrl,
                signedBackUrl,
                p.Landlord.VerificationStatus.ToString(),
                p.Landlord.VerificationNotes,
                p.Landlord.VerifiedAt,
                p.Landlord.BankName,
                SensitiveDataMasker.Mask(p.Landlord.BankAccountNumber),
                p.Landlord.BankAccountHolder,
                p.Landlord.Rating,
                p.Landlord.ReviewCount);
        }

        TenantDetailsDto? tenantDto = p.Tenant is null ? null : new TenantDetailsDto(
            p.Tenant.StudentId,
            p.Tenant.University,
            p.Tenant.Faculty,
            p.Tenant.DateOfBirth,
            p.Tenant.Gender.ToString(),
            p.Tenant.EmergencyContactName,
            p.Tenant.EmergencyContactPhone?.Value,
            p.Tenant.MaxBudget);

        AdminDetailsDto? adminDto = p.Admin is null ? null : new AdminDetailsDto(
            p.Admin.StaffCode,
            p.Admin.Department,
            p.Admin.Title);

        return new UserProfileDto(
            p.UserId,
            p.Role.ToRoleName(),
            p.FullName,
            p.PhoneNumber?.Value,
            p.AvatarUrl,
            p.Bio,
            tenantDto,
            landlordDto,
            adminDto);
    }

    public static UserProfileDto MapToDto(UserProfile p) =>
        MapToDtoAsync(p).GetAwaiter().GetResult();
}
