using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Common;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class UpdateTenantProfileUseCase : IUpdateTenantProfileUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly TimeProvider _timeProvider;

    public UpdateTenantProfileUseCase(IUserProfileRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateTenantCommand command, CancellationToken cancellationToken = default)
    {
        UserProfile.ValidateAvatarUrl(command.AvatarUrl);

        var now = _timeProvider.GetUtcNow();
        var existingProfile = await _repository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var isNewProfile = existingProfile is null;

        var profile = existingProfile
            ?? UserProfile.CreateDefault(
                userId,
                UserRole.Student,
                now);

        var phone = PhoneNumber.CreateOptional(command.PhoneNumber);
        profile.UpdateBasicInfo(command.FullName, phone, command.AvatarUrl, command.Bio, now);

        var existingTenant = profile.Tenant;
        var emergencyPhone = command.EmergencyContactPhone is null
            ? existingTenant?.EmergencyContactPhone
            : PhoneNumber.CreateOptional(command.EmergencyContactPhone);

        var gender = existingTenant?.Gender ?? Gender.Unspecified;
        if (!string.IsNullOrWhiteSpace(command.Gender))
        {
            if (Enum.TryParse<Gender>(command.Gender, true, out var g) && Enum.IsDefined(typeof(Gender), g))
            {
                gender = g;
            }
            else
            {
                throw new ProfileValidationException($"Invalid gender value '{command.Gender}'. Supported values are Unspecified, Male, Female, Other.");
            }
        }

        if (command.DateOfBirth.HasValue)
        {
            var today = BusinessTime.ToBusinessDate(now);
            if (command.DateOfBirth.Value > today)
            {
                throw new ProfileValidationException("Date of birth cannot be in the future.");
            }
        }

        if (command.MaxBudget.HasValue && command.MaxBudget.Value < 0)
        {
            throw new ProfileValidationException("Maximum budget cannot be negative.");
        }

        var tenantDetails = new TenantDetails(
            ResolveOptional(command.StudentId, command.IsStudentIdSpecified, existingTenant?.StudentId),
            ResolveOptional(command.University, command.IsUniversitySpecified, existingTenant?.University ?? "ICTU"),
            ResolveOptional(command.Faculty, command.IsFacultySpecified, existingTenant?.Faculty),
            command.DateOfBirth ?? existingTenant?.DateOfBirth,
            gender,
            command.EmergencyContactName ?? existingTenant?.EmergencyContactName,
            emergencyPhone,
            ResolveOptional(command.MaxBudget, command.IsMaxBudgetSpecified, existingTenant?.MaxBudget));

        profile.UpdateTenantDetails(tenantDetails, now);

        if (isNewProfile)
        {
            // Profile chưa tồn tại: chỉ được INSERT.
            // Nếu request khác tạo trước, AddAsync sẽ trả conflict.
            await _repository.AddAsync(profile, cancellationToken);
        }
        else
        {
            // Profile đã tồn tại: bắt buộc cập nhật có ETag.
            await _repository.UpdateAsync(profile, cancellationToken);
        }

        return GetMyProfileUseCase.MapToDto(profile);
    }

    private static T? ResolveOptional<T>(T? value, bool isSpecified, T? existingValue)
        where T : class => isSpecified || value is not null ? value : existingValue;

    private static decimal? ResolveOptional(decimal? value, bool isSpecified, decimal? existingValue) =>
        isSpecified || value.HasValue ? value : existingValue;
}
