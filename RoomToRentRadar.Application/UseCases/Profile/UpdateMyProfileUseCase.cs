using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Profile;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class UpdateMyProfileUseCase : IUpdateMyProfileUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly ICampusRepository _campusRepository;

    public UpdateMyProfileUseCase(
        IUserRepository userRepository,
        ICampusRepository campusRepository)
    {
        _userRepository = userRepository;
        _campusRepository = campusRepository;
    }

    public async Task<UserProfileDto> ExecuteAsync(
        string userId,
        UpdateMyProfileRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainValidationException("User ID is required.");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        await EnsureCampusExistsAsync(request.SelectedCampusId, ct);

        var changed = user.UpdateProfile(
            request.FullName,
            request.PhoneNumber,
            request.SelectedCampusId,
            request.PreferredLanguage,
            DateTime.UtcNow);

        if (changed)
        {
            await _userRepository.UpdateAsync(user, ct);
        }

        return UserMapper.ToProfileDto(user);
    }

    // BR-CMP-02: reject an unknown campus here, otherwise the Users -> Campuses foreign key fails as a 500.
    private async Task EnsureCampusExistsAsync(string? campusId, CancellationToken ct)
    {
        // A missing value means "unchanged"; a blank value is rejected by the aggregate.
        if (string.IsNullOrWhiteSpace(campusId))
        {
            return;
        }

        var trimmedId = campusId.Trim();
        var campus = await _campusRepository.GetByIdAsync(trimmedId, ct);
        if (campus == null || !string.Equals(campus.Id, trimmedId, StringComparison.Ordinal))
        {
            throw new DomainValidationException(
                $"Campus with ID '{trimmedId}' not found.", ProblemCodes.CampusNotFound);
        }
    }
}
