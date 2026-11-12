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

public sealed class LoginWithGoogleUseCase : ILoginWithGoogleUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IGoogleAuthVerifierPort _googleVerifier;
    private readonly IJwtTokenServicePort _jwtTokenService;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public LoginWithGoogleUseCase(
        IUserRepository userRepository,
        IGoogleAuthVerifierPort googleVerifier,
        IJwtTokenServicePort jwtTokenService,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _googleVerifier = googleVerifier;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    public async Task<AuthTokensDto> ExecuteAsync(GoogleLoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw new DomainValidationException("Google ID token is required.");
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role) ||
            (role != UserRole.Student && role != UserRole.Landlord))
        {
            throw new DomainValidationException("Google sign-in is only permitted for Student and Landlord roles.");
        }

        var payload = await _googleVerifier.VerifyIdTokenAsync(request.IdToken, ct);
        var email = EmailAddress.Create(payload.Email);
        var utcNow = DateTime.UtcNow;

        var existingUser = await _userRepository.GetByEmailAsync(email.Value, ct);
        User user;
        var isNewUser = false;

        if (existingUser != null)
        {
            existingUser.EnsureNotLockedOut(utcNow);
            existingUser.LinkGoogle(payload.SubjectId, request.PhoneNumber, utcNow);
            existingUser.RecordLoginSuccess(utcNow);
            user = existingUser;
        }
        else
        {
            if (role == UserRole.Student && !email.IsIctuStudentEmail())
            {
                _auditLogger.LogLoginFailed(email.Value, "NonIctuGoogleDomain");
                throw new DomainValidationException($"Student Google login requires an '{EmailAddress.IctuDomain}' email address.");
            }

            if (role == UserRole.Landlord && string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                throw new DomainValidationException("Phone number is required for Landlord registration.");
            }

            user = User.RegisterGoogle(
                email: email.Value,
                fullName: payload.FullName,
                googleSubjectId: payload.SubjectId,
                role: role,
                phoneNumber: request.PhoneNumber,
                utcNow: utcNow);
            isNewUser = true;
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
                AuthProvider.Google.ToString(),
                user.IsVerified);
        }
        else
        {
            await _userRepository.UpdateAsync(user, ct);
        }

        _auditLogger.LogLoginSucceeded(user.Id, user.Email.Value, user.Role.ToString(), AuthProvider.Google.ToString());

        return new AuthTokensDto(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: refreshSession.ExpiresAtUtc,
            User: UserMapper.ToProfileDto(user));
    }
}
