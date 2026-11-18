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

        Assert.Multiple(() =>
        {
            Assert.That(tokenEntity.IsRevoked, Is.True);
            Assert.That(tokenEntity.RevocationReason, Is.EqualTo("UserLogout"));
            Assert.That(_unitOfWork.TransactionCallCount, Is.EqualTo(1));
            Assert.That(_unitOfWork.SaveChangesCallCount, Is.EqualTo(1));
        });

        var refreshUseCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(async () =>
            await refreshUseCase.ExecuteAsync(new RefreshSessionCommand(rawToken)));
    }

    [Test]
    public async Task Logout_WhenMultipleSessionsExist_ShouldRevokeOnlySpecifiedTokenFamily_AndKeepIndependentSessionsActive()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("multisession@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        // Session 1 (Device A): Family 1
        var family1 = Guid.NewGuid();
        var rawTokenFam1Gen1 = "fam1-gen1";
        var tokenFam1Gen1 = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawTokenFam1Gen1),
            family1,
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(tokenFam1Gen1);

        // Rotate Session 1: Gen 1 -> Gen 2
        var rawTokenFam1Gen2 = "fam1-gen2";
        var tokenFam1Gen2 = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawTokenFam1Gen2),
            family1,
            FixedNow.AddHours(1),
            FixedNow.AddDays(7));
        tokenFam1Gen1.ReplaceWith(tokenFam1Gen2.Id, FixedNow.AddHours(1));
        await _refreshTokenRepository.AddAsync(tokenFam1Gen2);

        // Session 2 (Device B): Family 2 (Independent Session)
        var family2 = Guid.NewGuid();
        var rawTokenFam2Gen1 = "fam2-gen1";
        var tokenFam2Gen1 = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawTokenFam2Gen1),
            family2,
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(tokenFam2Gen1);

        var logoutUseCase = new LogoutUseCase(
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        // Logout using Session 1's active token
        await logoutUseCase.ExecuteAsync(new LogoutCommand(rawTokenFam1Gen2));

        Assert.Multiple(() =>
        {
            // Family 1 must be completely revoked
            Assert.That(tokenFam1Gen1.IsRevoked, Is.True, "Family 1 Gen 1 must be revoked");
            Assert.That(tokenFam1Gen2.IsRevoked, Is.True, "Family 1 Gen 2 must be revoked");
            Assert.That(tokenFam1Gen2.RevocationReason, Is.EqualTo("UserLogout"));

            // Family 2 (independent device session) MUST remain untouched and active!
            Assert.That(tokenFam2Gen1.IsRevoked, Is.False, "Family 2 on Device B must NOT be revoked");
            Assert.That(tokenFam2Gen1.IsActive(FixedNow.AddHours(2)), Is.True, "Family 2 must remain active");
        });

        // Verify Device B can still refresh normally
        var refreshUseCase = new RefreshSessionUseCase(
            _userRepository,
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var refreshResult = await refreshUseCase.ExecuteAsync(new RefreshSessionCommand(rawTokenFam2Gen1));
        Assert.That(refreshResult.AccessToken, Is.Not.Null);
    }

    [Test]
    public async Task Logout_WhenTokenAlreadyRevoked_ShouldBeIdempotentAndSucceedWithoutCallingSaveChanges()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("idempotent@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var rawToken = "already-revoked-token";
        var tokenEntity = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawToken),
            Guid.NewGuid(),
            FixedNow,
            FixedNow.AddDays(7));
        tokenEntity.Revoke(FixedNow.AddMinutes(5), "PreviousLogout");
        await _refreshTokenRepository.AddAsync(tokenEntity);

        var logoutUseCase = new LogoutUseCase(
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        // Calling logout on already-revoked token
        await logoutUseCase.ExecuteAsync(new LogoutCommand(rawToken));

        Assert.Multiple(() =>
        {
            Assert.That(tokenEntity.IsRevoked, Is.True);
            Assert.That(tokenEntity.RevocationReason, Is.EqualTo("PreviousLogout"));
            // Idempotency: no transaction or save changes should be triggered
            Assert.That(_unitOfWork.TransactionCallCount, Is.EqualTo(0));
            Assert.That(_unitOfWork.SaveChangesCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Logout_WhenTokenDoesNotExist_ShouldThrowInvalidRefreshTokenException()
    {
        var logoutUseCase = new LogoutUseCase(
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(async () =>
            await logoutUseCase.ExecuteAsync(new LogoutCommand("non-existent-token")));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Logout_WhenTokenIsNullOrWhitespace_ShouldThrowRequestValidationException(string? invalidToken)
    {
        var logoutUseCase = new LogoutUseCase(
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await logoutUseCase.ExecuteAsync(new LogoutCommand(invalidToken!)));
    }

    [Test]
    public async Task Logout_WhenPersistenceFails_ShouldPropagateExceptionAndNotLeaveInconsistentState()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("fail@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var rawToken = "persistence-fail-token";
        var tokenEntity = RefreshToken.Create(
            user.Id,
            _tokenService.HashRefreshToken(rawToken),
            Guid.NewGuid(),
            FixedNow,
            FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(tokenEntity);

        _unitOfWork.SimulatedSaveChangesException = new InvalidOperationException("Simulated CosmosDB commit failure");

        var logoutUseCase = new LogoutUseCase(
            _refreshTokenRepository,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await logoutUseCase.ExecuteAsync(new LogoutCommand(rawToken)));

        Assert.Multiple(() =>
        {
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.Message, Is.EqualTo("Simulated CosmosDB commit failure"));
            Assert.That(_unitOfWork.TransactionCallCount, Is.EqualTo(1));
        });
    }
}
