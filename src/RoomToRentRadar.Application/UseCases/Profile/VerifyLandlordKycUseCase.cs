using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.Profile;

public sealed class VerifyLandlordKycUseCase : IVerifyLandlordKycUseCase
{
    private readonly IUserProfileRepository _repository;
    private readonly IKycDocumentStorageService? _kycStorage;
    private readonly TimeProvider _timeProvider;

    public VerifyLandlordKycUseCase(
        IUserProfileRepository repository,
        IKycDocumentStorageService? kycStorage = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _kycStorage = kycStorage;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UserProfileDto> ExecuteAsync(VerifyLandlordKycCommand command, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByUserIdAsync(command.TargetUserId, cancellationToken)
            ?? throw new ProfileNotFoundException(command.TargetUserId);

        var now = _timeProvider.GetUtcNow();
        if (command.IsApproved)
            profile.ApproveKyc(now, command.Reason);
        else
            profile.RejectKyc(command.Reason ?? "Information did not match verification criteria.", now);

        await _repository.UpdateAsync(profile, cancellationToken);
        return await GetMyProfileUseCase.MapToDtoAsync(profile, _kycStorage, cancellationToken);
    }
}
