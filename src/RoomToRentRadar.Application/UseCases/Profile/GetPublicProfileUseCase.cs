using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class GetPublicProfileUseCase : IGetPublicProfileUseCase
{
    private readonly IUserProfileRepository _repository;

    public GetPublicProfileUseCase(IUserProfileRepository repository) => _repository = repository;

    public async Task<PublicProfileDto> ExecuteAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByUserIdAsync(targetUserId, cancellationToken)
            ?? throw new ProfileNotFoundException(targetUserId);

        return new PublicProfileDto(
            profile.UserId,
            profile.Role.ToRoleName(),
            profile.FullName,
            profile.AvatarUrl,
            profile.Bio,
            profile.Landlord?.BusinessName,
            profile.Tenant?.University,
            profile.Landlord?.Rating ?? 0.0,
            profile.Landlord?.ReviewCount ?? 0,
            profile.Landlord?.VerificationStatus == KycVerificationStatus.Verified);
    }
}