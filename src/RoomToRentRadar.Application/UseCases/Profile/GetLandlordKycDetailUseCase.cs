using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class GetLandlordKycDetailUseCase : IGetLandlordKycDetailUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly IKycDocumentStorageService? _kycStorage;

    public GetLandlordKycDetailUseCase(
        IUserProfileRepository repository,
        IKycDocumentStorageService? kycStorage = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _kycStorage = kycStorage;
    }

    public async Task<AdminLandlordKycDto> ExecuteAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByUserIdAsync(targetUserId, cancellationToken)
            ?? throw new ProfileNotFoundException(targetUserId);

        if (profile.Role != UserRole.Landlord || profile.Landlord is null)
        {
            throw new ProfileRoleMismatchException($"User '{targetUserId}' does not have a landlord profile.");
        }

        return await GetPendingLandlordsUseCase.MapToAdminKycDtoAsync(profile, _kycStorage, cancellationToken);
    }
}
