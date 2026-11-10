using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
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

    public async Task<AuthTokensDto> ExecuteAsync(IctuSsoLoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw new DomainValidationException("ICTU SSO ID token is required.");
        }

        var payload = await _ssoVerifier.VerifyIdTokenAsync(request.IdToken, ct);
        var email = EmailAddress.Create(payload.Email);
        if (!email.IsIctuStudentEmail())
        {
            _auditLogger.LogLoginFailed(email.Value, "NonIctuSsoDomain");
            throw new DomainValidationException($"ICTU Student SSO requires an '{EmailAddress.IctuDomain}' email address.");
        }

        var utcNow = DateTime.UtcNow;
        var existingUser = await _userRepository.GetByEmailAsync(email.Value, ct);
        User user;
        var isNewUser = false;

        if (existingUser == null)
        {
            user = User.RegisterIctuSso(
                email: email.Value,
                fullName: payload.FullName,
                ssoSubjectId: payload.SubjectId,
                studentCode: payload.StudentCode,
                utcNow: utcNow);
            isNewUser = true;
        }
        else
        {
            existingUser.EnsureNotLockedOut(utcNow);
            existingUser.LinkIctuSso(payload.SubjectId, payload.StudentCode, utcNow);
            existingUser.RecordLoginSuccess(utcNow);
            user = existingUser;
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user, utcNow);
        var rawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var refreshTokenHash = _jwtTokenService.ComputeTokenHash(rawRefreshToken);
        var refreshSession = user.IssueRefreshToken(
            refreshTokenHash,
            accessToken.JwtId,
            utcNow,
            _jwtTokenService.RefreshTokenLifetime);

        if (isNewUser)
        {
            await _userRepository.AddAsync(user, ct);
            _auditLogger.LogUserRegistered(
                user.Id,
                user.Email.Value,
                user.Role.ToString(),
                AuthProvider.IctuSso.ToString(),
                user.IsVerified);
        }
        else
        {
            await _userRepository.UpdateAsync(user, ct);
        }

        _auditLogger.LogLoginSucceeded(user.Id, user.Email.Value, user.Role.ToString(), AuthProvider.IctuSso.ToString());

        return new AuthTokensDto(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: refreshSession.ExpiresAtUtc,
            User: UserMapper.ToProfileDto(user));
    }
}
