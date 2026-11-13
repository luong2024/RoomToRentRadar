using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class UpdateAdminProfileUseCase : IUpdateAdminProfileUseCase
{
    private readonly IUserProfileRepository _profileRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateAdminProfileUseCase(
        IUserProfileRepository profileRepository,
        IUserRepository userRepository,
        TimeProvider? timeProvider = null)
    {
        _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(Guid userId, UpdateAdminCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ProfileNotFoundException(userId);

        if (user.Role != UserRole.Admin)
        {
            throw new ProfileRoleMismatchException("User is not an authorized administrator in the system.");
        }

        user.EnsureActive();

        var now = _timeProvider.GetUtcNow();
        var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = UserProfile.CreateDefault(userId, user.Role, now);
            var phone = PhoneNumber.CreateOptional(command.PhoneNumber);
            profile.UpdateBasicInfo(command.FullName, phone, command.AvatarUrl, command.Bio, now);
            profile.UpdateAdminDetails(new AdminDetails(command.StaffCode, command.Department, command.Title), now);
            try
            {
                await _profileRepository.AddAsync(profile, cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
                if (profile is null)
                {
                    throw;
                }
                var phoneRetry = PhoneNumber.CreateOptional(command.PhoneNumber);
                var avatarRetry = command.AvatarUrl ?? profile.AvatarUrl;
                var bioRetry = command.Bio ?? profile.Bio;
                profile.UpdateBasicInfo(command.FullName, phoneRetry, avatarRetry, bioRetry, now);
                profile.UpdateAdminDetails(new AdminDetails(command.StaffCode, command.Department, command.Title), now);
                await _profileRepository.UpdateAsync(profile, cancellationToken);
            }
        }
        else
        {
            if (profile.Role != user.Role || profile.Role != UserRole.Admin)
            {
                throw new ProfileRoleMismatchException("Profile role does not match user identity role.");
            }

            var phone = PhoneNumber.CreateOptional(command.PhoneNumber);
            var avatarUrl = command.AvatarUrl ?? profile.AvatarUrl;
            var bio = command.Bio ?? profile.Bio;
            profile.UpdateBasicInfo(command.FullName, phone, avatarUrl, bio, now);
            profile.UpdateAdminDetails(new AdminDetails(command.StaffCode, command.Department, command.Title), now);
            await _profileRepository.UpdateAsync(profile, cancellationToken);
        }

        return GetMyProfileUseCase.MapToDto(profile);
    }
}
