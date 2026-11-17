using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.UseCases.Authentication;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Tests.Authentication;

public class TokenLifecycleUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private InMemoryFakeUserRepository _userRepository = null!;
    private InMemoryFakeRefreshTokenRepository _refreshTokenRepository = null!;
    private FakeTokenService _tokenService = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private FixedTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryFakeUserRepository();
        _refreshTokenRepository = new InMemoryFakeRefreshTokenRepository();
        _tokenService = new FakeTokenService();
        _unitOfWork = new FakeUnitOfWork();
        _timeProvider = new FixedTimeProvider(FixedNow);
    }

    [Test]
    public async Task RefreshSession_WhenTokenActiveAndUserActive_ShouldRotateTokenWithinSameFamilyInSingleTransaction()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var family = Guid.NewGuid();
        var rawInitialToken = "initial-refresh-token";
        var initialTokenEntity = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawInitialToken),
            family,
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(initialTokenEntity);

        var rotateTime = FixedNow.AddMinutes(10);
        _timeProvider.AdvanceTo(rotateTime);

        var useCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await useCase.ExecuteAsync(new RefreshSessionCommand(rawInitialToken));

        var newTokenEntity = await _refreshTokenRepository.GetByTokenHashAsync(
            _tokenService.HashRefreshToken(result.RefreshToken));

        Assert.Multiple(() =>
        {
            Assert.That(result.UserId, Is.EqualTo(user.Id));
            Assert.That(result.AccessToken, Is.EqualTo("access-token-1"));
            Assert.That(result.RefreshToken, Is.EqualTo("raw-refresh-1"));
            Assert.That(initialTokenEntity.IsRevoked, Is.True);
            Assert.That(initialTokenEntity.RevokedAt, Is.EqualTo(rotateTime));
            Assert.That(newTokenEntity, Is.Not.Null);
            Assert.That(initialTokenEntity.ReplacedByTokenId, Is.EqualTo(newTokenEntity!.Id));
            Assert.That(newTokenEntity.TokenFamily, Is.EqualTo(family));
            Assert.That(newTokenEntity.IsActive(rotateTime), Is.True);
            Assert.That(_unitOfWork.TransactionCallCount, Is.EqualTo(1));
            Assert.That(_unitOfWork.SaveChangesCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RefreshSession_WhenRevokedTokenIsReplayed_ShouldRevokeEntireTokenFamilyAndReject()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var family = Guid.NewGuid();
        var rawTokenGen1 = "refresh-gen-1";
        var tokenGen1 = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawTokenGen1),
            family,
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(tokenGen1);

        var useCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        // Rotate Gen 1 -> Gen 2 legitimately
        _timeProvider.AdvanceTo(FixedNow.AddMinutes(5));
        var gen2Result = await useCase.ExecuteAsync(new RefreshSessionCommand(rawTokenGen1));
        var tokenGen2 = await _refreshTokenRepository.GetByTokenHashAsync(
            _tokenService.HashRefreshToken(gen2Result.RefreshToken));

        Assert.That(tokenGen2!.IsActive(FixedNow.AddMinutes(6)), Is.True);

        // Replay Gen 1 (already rotated/revoked!) -> must revoke Gen 2 as well!
        _timeProvider.AdvanceTo(FixedNow.AddMinutes(10));
        Assert.ThrowsAsync<InvalidRefreshTokenException>(async () =>
            await useCase.ExecuteAsync(new RefreshSessionCommand(rawTokenGen1)));

        Assert.Multiple(() =>
        {
            Assert.That(tokenGen2.IsRevoked, Is.True);
            Assert.That(tokenGen2.RevocationReason, Is.EqualTo("CompromisedTokenFamilyReplay"));
        });
    }

    [Test]
    public async Task RefreshSession_WhenTokenExpired_ShouldThrowInvalidRefreshTokenException()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var rawToken = "expired-refresh-token";
        var expiredToken = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawToken),
            Guid.NewGuid(),
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(expiredToken);

        _timeProvider.AdvanceTo(FixedNow.AddDays(8));

        var useCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(async () =>
            await useCase.ExecuteAsync(new RefreshSessionCommand(rawToken)));
    }

    [Test]
    public async Task RefreshSession_WhenUserInactive_ShouldThrowUserInactiveException()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("inactive@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        user.Deactivate(FixedNow.AddMinutes(1));
        await _userRepository.AddAsync(user);

        var rawToken = "active-refresh-token";
        var tokenEntity = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawToken),
            Guid.NewGuid(),
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(tokenEntity);

        var useCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<UserInactiveException>(async () =>
            await useCase.ExecuteAsync(new RefreshSessionCommand(rawToken)));
    }

    [Test]
    public async Task Logout_ShouldRevokeRefreshTokenAndPreventSubsequentRefresh()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("logout@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var rawToken = "logout-refresh-token";
        var tokenEntity = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawToken),
            Guid.NewGuid(),
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(tokenEntity);

        var logoutUseCase = new LogoutUseCase(
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        await logoutUseCase.ExecuteAsync(new LogoutCommand(rawToken));

        Assert.That(tokenEntity.IsRevoked, Is.True);

        var refreshUseCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(async () =>
            await refreshUseCase.ExecuteAsync(new RefreshSessionCommand(rawToken)));
    }
}
