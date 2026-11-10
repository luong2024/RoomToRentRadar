using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Auth;

public sealed class LoginWithIctuSsoUseCase : ILoginWithIctuSsoUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IIctuSsoVerifierPort _ssoVerifier;
    private readonly IJwtTokenServicePort _jwtTokenService;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public LoginWithIctuSsoUseCase(
        IUserRepository userRepository,
        IIctuSsoVerifierPort ssoVerifier,
        IJwtTokenServicePort jwtTokenService,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _ssoVerifier = ssoVerifier;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    public Task<AuthTokensDto> ExecuteAsync(IctuSsoLoginRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

