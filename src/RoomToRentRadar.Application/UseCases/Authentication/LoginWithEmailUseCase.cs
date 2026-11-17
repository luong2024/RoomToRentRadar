using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Authentication;

public sealed class LoginWithEmailUseCase : ILoginWithEmailUseCase
{
    private const string GenericInvalidCredentialsMessage = "Invalid email or password.";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public LoginWithEmailUseCase(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthenticationResult> ExecuteAsync(
        LoginWithEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new RequestValidationException("email", "Email address is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new RequestValidationException("password", "Password is required.");
        }

        var email = EmailAddress.Create(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !user.HasLocalPassword)
        {
            throw new AuthenticationFailedException(GenericInvalidCredentialsMessage);
        }

        if (!_passwordHasher.VerifyPassword(command.Password, user.PasswordHash!))
        {
            throw new AuthenticationFailedException(GenericInvalidCredentialsMessage);
        }

        user.EnsureActive();

        var utcNow = _timeProvider.GetUtcNow();

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            user.RecordSuccessfulLogin(utcNow);
            _userRepository.Update(user);

            var accessToken = _tokenService.GenerateAccessToken(user, utcNow);
            var refreshToken = _tokenService.GenerateRefreshToken(utcNow);

            var refreshTokenEntity = RefreshToken.Create(
                user.Id,
                refreshToken.TokenHash,
                Guid.NewGuid(),
                utcNow,
                refreshToken.ExpiresAt);

            await _refreshTokenRepository.AddAsync(refreshTokenEntity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new AuthenticationResult(
                user.Id,
                user.Email.Value,
                user.Role.ToRoleName(),
                accessToken.Token,
                accessToken.ExpiresAt,
                refreshToken.RawToken,
                refreshToken.ExpiresAt);
        }, cancellationToken);
    }
}
