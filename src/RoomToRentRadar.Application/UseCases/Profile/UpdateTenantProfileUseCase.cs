using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
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
        var now = _timeProvider.GetUtcNow();
        var profile = await _repository.GetByUserIdAsync(userId, cancellationToken)
            ?? UserProfile.CreateDefault(userId, UserRole.Student, now);

        var phone = PhoneNumber.CreateOptional(command.PhoneNumber);
        profile.UpdateBasicInfo(command.FullName, phone, command.AvatarUrl, command.Bio, now);

        var emergencyPhone = PhoneNumber.CreateOptional(command.EmergencyContactPhone);
        var gender = Enum.TryParse<Gender>(command.Gender, true, out var g) ? g : Gender.Unspecified;

        var tenantDetails = new TenantDetails(
            command.StudentId,
            command.University ?? "ICTU",
            command.Faculty,
            command.DateOfBirth,
            gender,
            command.EmergencyContactName,
            emergencyPhone,
            command.MaxBudget);

        profile.UpdateTenantDetails(tenantDetails, now);
        await _repository.UpdateAsync(profile, cancellationToken);

        return GetMyProfileUseCase.MapToDto(profile);
    }
}