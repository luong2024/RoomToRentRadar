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

        // Idempotency: If already revoked, return cleanly without re-executing or throwing.
        if (existingToken.IsRevoked)
        {
            return;
        }

        var utcNow = _timeProvider.GetUtcNow();

        // Atomicity & Token Family Scoping: Execute in transaction, revoking all active tokens
        // belonging exclusively to this token family. Independent sessions (different families) remain unaffected.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var familyTokens = await _refreshTokenRepository.GetByFamilyAsync(existingToken.TokenFamily, ct);
            var anyUpdated = false;

            if (!existingToken.IsRevoked)
            {
                existingToken.Revoke(utcNow, "UserLogout");
                _refreshTokenRepository.Update(existingToken);
                anyUpdated = true;
            }

            foreach (var familyToken in familyTokens)
            {
                if (!familyToken.IsRevoked)
                {
                    familyToken.Revoke(utcNow, "UserLogout");
                    _refreshTokenRepository.Update(familyToken);
                    anyUpdated = true;
                }
            }

            if (anyUpdated)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }, cancellationToken);
    }
}
