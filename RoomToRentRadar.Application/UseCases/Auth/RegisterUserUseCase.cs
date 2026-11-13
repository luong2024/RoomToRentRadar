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

public sealed class RegisterUserUseCase : IRegisterUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherPort _passwordHasher;
    private readonly IJwtTokenServicePort _jwtTokenService;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public RegisterUserUseCase(
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

    public async Task<AuthTokensDto> ExecuteAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = EmailAddress.Create(request.Email);
        var role = ParsePublicRole(request.Role);
        User.ValidatePlainPasswordComplexity(request.Password);

        if (await _userRepository.ExistsByEmailAsync(email.Value, ct))
        {
            throw new DuplicateEmailException(email.Value);
        }

        var utcNow = DateTime.UtcNow;
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = User.RegisterLocal(
            email: email.Value,
            passwordHash: passwordHash,
            fullName: request.FullName,
            role: role,
            phoneNumber: request.PhoneNumber,
            studentCode: request.StudentCode,
            utcNow: utcNow,
            houseName: request.HouseName);

        var accessToken = _jwtTokenService.GenerateAccessToken(user, utcNow);
        var rawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var refreshTokenHash = _jwtTokenService.ComputeTokenHash(rawRefreshToken);
        var refreshSession = user.IssueRefreshToken(
            refreshTokenHash,
            accessToken.JwtId,
            utcNow,
            _jwtTokenService.RefreshTokenLifetime);

        await _userRepository.AddAsync(user, ct);
        _auditLogger.LogUserRegistered(
            user.Id,
            user.Email.Value,
            user.Role.ToString(),
            user.AuthProvider.ToString(),
            user.IsVerified);

        return new AuthTokensDto(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: refreshSession.ExpiresAtUtc,
            User: UserMapper.ToProfileDto(user));
    }

    private static UserRole ParsePublicRole(string rawRole)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            throw new DomainValidationException("Role is required ('Student' or 'Landlord').");
        }

        if (!Enum.TryParse<UserRole>(rawRole.Trim(), ignoreCase: true, out var parsedRole) ||
            !Enum.IsDefined(typeof(UserRole), parsedRole))
        {
            throw new DomainValidationException($"Role '{rawRole}' is invalid. Supported public roles are 'Student' and 'Landlord'.");
        }

        if (parsedRole != UserRole.Student && parsedRole != UserRole.Landlord)
        {
            throw new ForbiddenRoleException("Public self-registration is only permitted for Student and Landlord roles.");
        }

        return parsedRole;
    }
}
