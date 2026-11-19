using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Auth;

public sealed class RevokeTokenUseCase : IRevokeTokenUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenServicePort _jwtTokenService;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public RevokeTokenUseCase(
        IUserRepository userRepository,
        IJwtTokenServicePort jwtTokenService,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    public async Task ExecuteAsync(string userId, RevokeTokenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainValidationException("Authenticated user ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new DomainValidationException("Refresh token is required.");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        var tokenHash = _jwtTokenService.ComputeTokenHash(request.RefreshToken);
        user.RevokeRefreshToken(tokenHash, DateTime.UtcNow);

        await _userRepository.UpdateAsync(user, ct);
        _auditLogger.LogTokenRevoked(user.Id, "UserInitiatedLogout");
    }
}
