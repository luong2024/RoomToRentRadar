using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class GetMyProfileUseCase : IGetMyProfileUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly TimeProvider _timeProvider;

    public GetMyProfileUseCase(IUserProfileRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            var parsedRole = UserRoleExtensions.ParseRole(role);
            existing = UserProfile.CreateDefault(userId, parsedRole, _timeProvider.GetUtcNow());
            await _repository.AddAsync(existing, cancellationToken);
        }

        return MapToDto(existing);
    }

    public static UserProfileDto MapToDto(UserProfile p) =>
        new(
            p.UserId,
            p.Role.ToRoleName(),
            p.FullName,
            p.PhoneNumber?.Value,
            p.AvatarUrl,
            p.Bio,
            p.Tenant is null ? null : new(
                p.Tenant.StudentId,
                p.Tenant.University,
                p.Tenant.Faculty,
                p.Tenant.DateOfBirth,
                p.Tenant.Gender.ToString(),
                p.Tenant.EmergencyContactName,
                p.Tenant.EmergencyContactPhone?.Value,
                p.Tenant.MaxBudget),
            p.Landlord is null ? null : new(
                p.Landlord.BusinessName,
                p.Landlord.Address,
                p.Landlord.CitizenIdNumber,
                p.Landlord.CitizenIdIssueDate,
                p.Landlord.IdentityFrontPhotoUrl,
                p.Landlord.IdentityBackPhotoUrl,
                p.Landlord.VerificationStatus.ToString(),
                p.Landlord.VerificationNotes,
                p.Landlord.VerifiedAt,
                p.Landlord.BankName,
                p.Landlord.BankAccountNumber,
                p.Landlord.BankAccountHolder,
                p.Landlord.Rating,
                p.Landlord.ReviewCount),
            p.Admin is null ? null : new(p.Admin.StaffCode, p.Admin.Department, p.Admin.Title));
}