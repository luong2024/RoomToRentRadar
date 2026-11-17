using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Authentication;

public sealed class LoginWithGoogleUseCase : ILoginWithGoogleUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IGoogleIdentityVerifier _googleIdentityVerifier;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public LoginWithGoogleUseCase(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IGoogleIdentityVerifier googleIdentityVerifier,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _googleIdentityVerifier = googleIdentityVerifier;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthenticationResult> ExecuteAsync(
        LoginWithGoogleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.IdToken))
        {
            throw new RequestValidationException("idToken", "Google ID token is required.");
        }

        var verifiedIdentity = await _googleIdentityVerifier.VerifyIdTokenAsync(command.IdToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(verifiedIdentity.Subject))
        {
            throw new InvalidGoogleTokenException("Verified Google ID token is missing the subject identifier.");
        }

        if (!verifiedIdentity.EmailVerified)
        {
            throw new InvalidGoogleTokenException("Google account email address is not verified.");
        }

        var email = EmailAddress.Create(verifiedIdentity.Email);
        var utcNow = _timeProvider.GetUtcNow();

        var existingUser = await _userRepository.GetByExternalLoginAsync(
            UserExternalLogin.GoogleProvider,
            verifiedIdentity.Subject,
            cancellationToken);

        if (existingUser is not null)
        {
            existingUser.EnsureActive();

            return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                existingUser.RecordSuccessfulLogin(utcNow);
                _userRepository.Update(existingUser);

                return await IssueTokensForUserAsync(existingUser, utcNow, ct);
            }, cancellationToken);
        }

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ExternalIdentityConflictException();
        }

        if (string.IsNullOrWhiteSpace(command.Role))
        {
            throw new RequestValidationException(
                "role",
                "Role (STUDENT or LANDLORD) is required when registering with Google for the first time.");
        }

        var role = UserRoleExtensions.ParseSelfRegistrationRole(command.Role);

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var newUser = User.RegisterGoogleUser(email, verifiedIdentity.Subject, role, utcNow);
            await _userRepository.AddAsync(newUser, ct);

            return await IssueTokensForUserAsync(newUser, utcNow, ct);
        }, cancellationToken);
    }

    private async Task<AuthenticationResult> IssueTokensForUserAsync(
        User user,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user, utcNow);
        var refreshToken = _tokenService.GenerateRefreshToken(utcNow);

        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            refreshToken.TokenHash,
            Guid.NewGuid(),
            utcNow,
            refreshToken.ExpiresAt);

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthenticationResult(
            user.Id,
            user.Email.Value,
            user.Role.ToRoleName(),
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.RawToken,
            refreshToken.ExpiresAt);
    }
}
