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
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IUnitOfWork unitOfWork) : IAuthUseCase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    public async Task<RegisterUserResult> RegisterAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        if (!UserRoleWire.IsSelfService(command.Role))
        {
            return RegisterUserResult.RoleNotAllowed();
        }

        var email = User.NormalizeEmail(command.Email);
        var existing = await userRepository.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return RegisterUserResult.DuplicateEmail();
        }

        var (hash, salt) = passwordHasher.Hash(command.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = command.DisplayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = command.Role,
            CreatedAt = DateTime.UtcNow
        };

        if (!await userRepository.TryAddAsync(user, cancellationToken))
        {
            return RegisterUserResult.DuplicateEmail();
        }

        return RegisterUserResult.Created(new RegisterResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            UserRoleWire.ToWireValue(user.Role)));
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            token,
            expiresAt,
            user.DisplayName,
            UserRoleWire.ToWireValue(user.Role));
    }

    public Task<User?> AuthenticateAsync(string token, CancellationToken cancellationToken)
        => authSessionRepository.FindUserByTokenHashAsync(
            tokenGenerator.Hash(token),
            DateTime.UtcNow,
            cancellationToken);

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
}
