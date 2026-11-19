using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Auth;

public sealed class RefreshTokenUseCase : IRefreshTokenUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenServicePort _jwtTokenService;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public RefreshTokenUseCase(
        IUserRepository userRepository,
        IJwtTokenServicePort jwtTokenService,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    public async Task<AuthTokensDto> ExecuteAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new DomainValidationException("Refresh token is required.");
        }

        var currentHash = _jwtTokenService.ComputeTokenHash(request.RefreshToken);
        var user = await _userRepository.GetByRefreshTokenHashAsync(currentHash, ct);
        if (user == null)
        {
            throw new InvalidTokenException("Refresh token was not found.");
        }

        var utcNow = DateTime.UtcNow;
        user.EnsureNotLockedOut(utcNow);

        var accessToken = _jwtTokenService.GenerateAccessToken(user, utcNow);
        var newRawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var newRefreshTokenHash = _jwtTokenService.ComputeTokenHash(newRawRefreshToken);

        RefreshTokenSession rotatedSession;
        try
        {
            rotatedSession = user.RotateRefreshToken(
                currentTokenHash: currentHash,
                newTokenHash: newRefreshTokenHash,
                newJwtId: accessToken.JwtId,
                utcNow: utcNow,
                lifetime: _jwtTokenService.RefreshTokenLifetime);
        }
        catch (InvalidTokenException)
        {
            try
            {
                await _userRepository.UpdateAsync(user, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Concurrency conflict during token revocation - ignore to preserve the original InvalidTokenException
            }
            throw;
        }

        try
        {
            await _userRepository.UpdateAsync(user, ct);
        }
        catch (ConcurrencyConflictException)
        {
            _auditLogger.LogTokenRevoked(user.Id, "Token refresh concurrency conflict");
            throw new InvalidTokenException("Refresh token was already used or rotated concurrently. Please re-authenticate.");
        }

        _auditLogger.LogTokenRefreshed(user.Id, accessToken.JwtId);

        return new AuthTokensDto(
            AccessToken: accessToken.Token,
            RefreshToken: newRawRefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: rotatedSession.ExpiresAtUtc,
            User: UserMapper.ToProfileDto(user));
    }
}
