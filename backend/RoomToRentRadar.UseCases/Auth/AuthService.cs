using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.UseCases.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IAuthSessionRepository authSessionRepository,
    IRegistrationChallengeRepository registrationChallengeRepository,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IOtpGenerator otpGenerator,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork) : IAuthUseCase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    public async Task<BeginRegistrationResult> BeginRegistrationAsync(
        BeginRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        if (!UserRoleWire.IsSelfService(command.Role))
        {
            return BeginRegistrationResult.RoleNotAllowed();
        }

        var email = User.NormalizeEmail(command.Email);
        var existing = await userRepository.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return BeginRegistrationResult.DuplicateEmail();
        }

        var now = DateTime.UtcNow;
        await registrationChallengeRepository.RemoveByEmailAsync(email, cancellationToken);
        await registrationChallengeRepository.RemoveExpiredAsync(now, cancellationToken);

        var (passwordHash, passwordSalt) = passwordHasher.Hash(command.Password);
        var code = otpGenerator.Generate();
        var (otpHash, otpSalt) = passwordHasher.Hash(code);

        var challenge = new RegistrationChallenge
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = command.DisplayName,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = command.Role,
            OtpHash = otpHash,
            OtpSalt = otpSalt,
            AttemptCount = 0,
            ExpiresAt = now.AddMinutes(AccountRules.OtpLifetimeMinutes),
            CreatedAt = now
        };

        await registrationChallengeRepository.AddAsync(challenge, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var delivered = await emailSender.SendRegistrationOtpAsync(
            new RegistrationOtpEmail(email, command.DisplayName, code, challenge.ExpiresAt),
            cancellationToken);
        if (!delivered)
        {
            registrationChallengeRepository.Remove(challenge);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return BeginRegistrationResult.EmailDeliveryFailed();
        }

        return BeginRegistrationResult.ChallengeCreated(new RegisterChallengeResponse(
            challenge.Id,
            challenge.Email,
            challenge.ExpiresAt));
    }

    public async Task<VerifyRegistrationResult> VerifyRegistrationAsync(
        VerifyRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        var challenge = await registrationChallengeRepository.FindByIdAsync(
            command.ChallengeId,
            cancellationToken);
        if (challenge is null)
        {
            return VerifyRegistrationResult.ChallengeNotFound();
        }

        var now = DateTime.UtcNow;
        if (challenge.ExpiresAt <= now)
        {
            registrationChallengeRepository.Remove(challenge);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return VerifyRegistrationResult.ChallengeExpired();
        }

        if (challenge.AttemptCount >= AccountRules.OtpMaxAttempts)
        {
            registrationChallengeRepository.Remove(challenge);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return VerifyRegistrationResult.AttemptsExceeded();
        }

        if (!passwordHasher.Verify(command.Code, challenge.OtpHash, challenge.OtpSalt))
        {
            var attemptCount = await registrationChallengeRepository.TryConsumeAttemptAsync(
                challenge.Id,
                AccountRules.OtpMaxAttempts,
                cancellationToken);

            if (attemptCount is null or >= AccountRules.OtpMaxAttempts)
            {
                registrationChallengeRepository.Remove(challenge);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return VerifyRegistrationResult.AttemptsExceeded();
            }

            return VerifyRegistrationResult.InvalidCode(AccountRules.OtpMaxAttempts - attemptCount.Value);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = challenge.Email,
            DisplayName = challenge.DisplayName,
            PasswordHash = challenge.PasswordHash,
            PasswordSalt = challenge.PasswordSalt,
            Role = challenge.Role,
            CreatedAt = now
        };

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (!await userRepository.TryAddAsync(user, cancellationToken))
        {
            registrationChallengeRepository.Remove(challenge);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return VerifyRegistrationResult.DuplicateEmail();
        }

        var response = await CreateSessionAsync(user, cancellationToken);
        registrationChallengeRepository.Remove(challenge);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return VerifyRegistrationResult.Verified(response);
    }

    public async Task<LoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByEmailAsync(
            User.NormalizeEmail(email),
            cancellationToken);

        if (user is null)
        {
            passwordHasher.VerifyDummy(password);
            return null;
        }

        if (!passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return null;
        }

        var response = await CreateSessionAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public Task<User?> AuthenticateAsync(string token, CancellationToken cancellationToken)
        => authSessionRepository.FindUserByTokenHashAsync(
            tokenGenerator.Hash(token),
            DateTime.UtcNow,
            cancellationToken);

    public async Task<UserProfileDto?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        return user is null ? null : ToProfile(user);
    }

    public async Task<UserProfileDto?> UpdateDisplayNameAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.DisplayName = displayName.Trim();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToProfile(user);
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        var session = await authSessionRepository.FindByTokenHashAsync(
            tokenGenerator.Hash(token),
            cancellationToken);
        if (session is null)
        {
            return;
        }

        authSessionRepository.Remove(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoginResponse> CreateSessionAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(SessionLifetime);
        var (token, tokenHash) = tokenGenerator.Create(
            new AuthTokenSubject(user.Id, user.Email, user.DisplayName, user.Role),
            expiresAt);

        await authSessionRepository.AddAsync(
            new AuthSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = now
            },
            cancellationToken);

        return new LoginResponse(
            token,
            expiresAt,
            user.DisplayName,
            UserRoleWire.ToWireValue(user.Role));
    }

    private static UserProfileDto ToProfile(User user)
        => new(
            user.Id,
            user.Email,
            user.DisplayName,
            UserRoleWire.ToWireValue(user.Role),
            user.CreatedAt);
}
