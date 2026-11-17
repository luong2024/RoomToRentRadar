using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.UseCases.Authentication;

public sealed class RefreshSessionUseCase : IRefreshSessionUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RefreshSessionUseCase(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthenticationResult> ExecuteAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new RequestValidationException("refreshToken", "Refresh token is required.");
        }

        var tokenHash = _tokenService.HashRefreshToken(command.RefreshToken);
        var utcNow = _timeProvider.GetUtcNow();

        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existingToken is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (existingToken.IsRevoked)
        {
            await RevokeCompromisedTokenFamilyAsync(existingToken.TokenFamily, utcNow, cancellationToken);
            throw new InvalidRefreshTokenException(
                "Refresh token has already been used or revoked. The session family has been invalidated.");
        }

        if (existingToken.IsExpired(utcNow))
        {
            throw new InvalidRefreshTokenException("Refresh token has expired.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _userRepository.GetByIdAsync(existingToken.UserId, ct)
                ?? throw new InvalidRefreshTokenException();

            user.EnsureActive();

            var newAccessToken = _tokenService.GenerateAccessToken(user, utcNow);
            var newRefreshToken = _tokenService.GenerateRefreshToken(utcNow);

            var replacementTokenEntity = RefreshToken.Create(
                user.Id,
                newRefreshToken.TokenHash,
                existingToken.TokenFamily,
                utcNow,
                newRefreshToken.ExpiresAt);

            existingToken.ReplaceWith(replacementTokenEntity.Id, utcNow);

            _refreshTokenRepository.Update(existingToken);
            await _refreshTokenRepository.AddAsync(replacementTokenEntity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new AuthenticationResult(
                user.Id,
                user.Email.Value,
                user.Role.ToRoleName(),
                newAccessToken.Token,
                newAccessToken.ExpiresAt,
                newRefreshToken.RawToken,
                newRefreshToken.ExpiresAt);
        }, cancellationToken);
    }

    private async Task RevokeCompromisedTokenFamilyAsync(
        Guid tokenFamily,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var familyTokens = await _refreshTokenRepository.GetByFamilyAsync(tokenFamily, ct);
            var anyUpdated = false;

            foreach (var familyToken in familyTokens)
            {
                if (!familyToken.IsRevoked)
                {
                    familyToken.Revoke(utcNow, "CompromisedTokenFamilyReplay");
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
