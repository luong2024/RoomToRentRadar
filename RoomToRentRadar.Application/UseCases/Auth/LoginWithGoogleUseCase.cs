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
    private readonly IPasswordHasherPort _passwordHasher;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public LoginWithGoogleUseCase(
        IUserRepository userRepository,
        IGoogleAuthVerifierPort googleVerifier,
        IJwtTokenServicePort jwtTokenService,
        IPasswordHasherPort passwordHasher,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _googleVerifier = googleVerifier;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
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

        // BR-AUTH-04: Student role domain and hd enforcement
        if (role == UserRole.Student)
        {
            if (!email.IsIctuStudentEmail() || !string.Equals(payload.HostedDomain, "ictu.edu.vn", StringComparison.OrdinalIgnoreCase))
            {
                _auditLogger.LogLoginFailed(email.Value, "InvalidIctuHostedDomain");
                throw new DomainValidationException($"Student Google login requires an '{EmailAddress.IctuDomain}' email address with verified hosted domain 'ictu.edu.vn'.");
            }
        }

        var existingUser = await _userRepository.GetByEmailAsync(email.Value, ct);
        User user;
        var isNewUser = false;

        if (existingUser != null)
        {
            if (existingUser.Role == UserRole.Admin)
            {
                _auditLogger.LogLoginFailed(email.Value, "AdminGoogleSignInForbidden");
                throw new ForbiddenRoleException("Admin accounts cannot sign in or link via Google authentication.");
            }

            if (existingUser.Role != role)
            {
                _auditLogger.LogLoginFailed(email.Value, "RoleMismatch");
                throw new DomainValidationException($"Account exists with role '{existingUser.Role}', which does not match requested role '{role}'.");
            }

            if (existingUser.Role == UserRole.Student && !email.IsIctuStudentEmail())
            {
                _auditLogger.LogLoginFailed(email.Value, "NonIctuGoogleDomain");
                throw new DomainValidationException($"Student Google login requires an '{EmailAddress.IctuDomain}' email address.");
            }

            if (existingUser.Role == UserRole.Landlord &&
                string.IsNullOrWhiteSpace(existingUser.PhoneNumber) &&
                string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                throw new PhoneNumberRequiredException();
            }

            if (existingUser.Role == UserRole.Landlord &&
                string.IsNullOrWhiteSpace(existingUser.HouseName) &&
                string.IsNullOrWhiteSpace(request.HouseName))
            {
                throw new HouseNameRequiredException();
            }

            if (existingUser.Role == UserRole.Landlord &&
                string.IsNullOrWhiteSpace(existingUser.HouseAddress) &&
                string.IsNullOrWhiteSpace(request.HouseAddress))
            {
                throw new HouseAddressRequiredException();
            }

            // Anti Pre-Hijacking: if account already exists with a password and is not yet linked to Google,
            // require current password confirmation on the first link
            if (!string.IsNullOrWhiteSpace(existingUser.PasswordHash) && string.IsNullOrWhiteSpace(existingUser.SsoSubjectId))
            {
                if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                {
                    throw new CurrentPasswordRequiredException("Current password is required to link this Google account.");
                }

                if (!_passwordHasher.VerifyPassword(request.CurrentPassword, existingUser.PasswordHash))
                {
                    _auditLogger.LogLoginFailed(email.Value, "InvalidCurrentPasswordForGoogleLink");
                    throw new InvalidCredentialsException("Current password verification failed.");
                }
            }

            existingUser.EnsureNotLockedOut(utcNow);
            existingUser.LinkGoogle(
                payload.SubjectId,
                request.PhoneNumber,
                utcNow,
                houseName: request.HouseName,
                houseAddress: request.HouseAddress);
            existingUser.RecordLoginSuccess(utcNow);
            user = existingUser;
        }
        else
        {
            if (role == UserRole.Landlord && string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                throw new PhoneNumberRequiredException();
            }

            if (role == UserRole.Landlord && string.IsNullOrWhiteSpace(request.HouseName))
            {
                throw new HouseNameRequiredException();
            }

            if (role == UserRole.Landlord && string.IsNullOrWhiteSpace(request.HouseAddress))
            {
                throw new HouseAddressRequiredException();
            }

            // Dual login password creation on initial Google registration
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new PasswordRequiredException("Password creation is required for initial Google registration.");
            }

            User.ValidatePlainPasswordComplexity(request.Password);
            var passwordHash = _passwordHasher.HashPassword(request.Password);

            user = User.RegisterGoogle(
                email: email.Value,
                fullName: payload.FullName,
                googleSubjectId: payload.SubjectId,
                role: role,
                phoneNumber: request.PhoneNumber,
                utcNow: utcNow,
                passwordHash: passwordHash,
                houseName: request.HouseName,
                houseAddress: request.HouseAddress);
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
