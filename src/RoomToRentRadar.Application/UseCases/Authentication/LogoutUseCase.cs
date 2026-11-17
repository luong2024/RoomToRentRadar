using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases.Authentication;

public sealed class LogoutUseCase : ILogoutUseCase
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public LogoutUseCase(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task ExecuteAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new RequestValidationException("refreshToken", "Refresh token is required.");
        }

        var tokenHash = _tokenService.HashRefreshToken(command.RefreshToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        if (!existingToken.IsRevoked)
        {
            var utcNow = _timeProvider.GetUtcNow();
            existingToken.Revoke(utcNow, "UserLogout");
            _refreshTokenRepository.Update(existingToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
