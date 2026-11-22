using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
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

    public async Task<LoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByEmailAsync(
            User.NormalizeEmail(email),
            cancellationToken);

        if (user is null || !passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return null;
        }

        var (token, tokenHash) = tokenGenerator.Create();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(SessionLifetime);

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

        return new LoginResponse(token, expiresAt, user.DisplayName);
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
