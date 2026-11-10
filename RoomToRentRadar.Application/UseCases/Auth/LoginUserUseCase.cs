using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Auth;

public sealed class LoginUserUseCase : ILoginUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherPort _passwordHasher;
    private readonly IJwtTokenServicePort _jwtTokenService;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public LoginUserUseCase(
        IUserRepository userRepository,
        IPasswordHasherPort passwordHasher,
        IJwtTokenServicePort jwtTokenService,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    public async Task<AuthTokensDto> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new DomainValidationException("Email and password are required.");
        }

        var email = EmailAddress.Create(request.Email);
        var utcNow = DateTime.UtcNow;

        var user = await _userRepository.GetByEmailAsync(email.Value, ct);
        if (user == null)
        {
            _auditLogger.LogLoginFailed(email.Value, "AccountNotFound");
            throw new InvalidCredentialsException("Invalid email or password.");
        }

        user.EnsureNotLockedOut(utcNow);

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            _auditLogger.LogLoginFailed(email.Value, "SsoOnlyAccount");
            throw new InvalidCredentialsException("This account uses ICTU Student SSO. Please sign in with ICTU SSO.");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.RecordLoginFailure(utcNow);
            await _userRepository.UpdateAsync(user, ct);

            if (user.IsLockedOut(utcNow))
            {
                _auditLogger.LogAccountLockedOut(user.Id, user.Email.Value, user.LockoutEndUtc!.Value);
                throw new AccountLockedException(user.LockoutEndUtc!.Value);
            }

            _auditLogger.LogLoginFailed(email.Value, "PasswordMismatch");
            throw new InvalidCredentialsException("Invalid email or password.");
        }

        user.RecordLoginSuccess(utcNow);

        var accessToken = _jwtTokenService.GenerateAccessToken(user, utcNow);
        var rawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var refreshTokenHash = _jwtTokenService.ComputeTokenHash(rawRefreshToken);
        var refreshSession = user.IssueRefreshToken(
            refreshTokenHash,
            accessToken.JwtId,
            utcNow,
            _jwtTokenService.RefreshTokenLifetime);

        await _userRepository.UpdateAsync(user, ct);
        _auditLogger.LogLoginSucceeded(user.Id, user.Email.Value, user.Role.ToString(), AuthProvider.Local.ToString());

        return new AuthTokensDto(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: refreshSession.ExpiresAtUtc,
            User: UserMapper.ToProfileDto(user));
    }
}
