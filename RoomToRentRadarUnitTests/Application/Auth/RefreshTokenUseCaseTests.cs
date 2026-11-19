using RoomToRentRadar.Application.UseCases.Auth;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Auth;

[TestFixture]
public class RefreshTokenUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private IJwtTokenServicePort _jwtTokenService = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private RefreshTokenUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _jwtTokenService = Substitute.For<IJwtTokenServicePort>();
        _auditLogger = Substitute.For<ISecurityAuditLoggerPort>();

        _jwtTokenService.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _jwtTokenService.ComputeTokenHash(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");
        _jwtTokenService.GenerateRawRefreshToken().Returns("new-raw-refresh-token");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<DateTime>())
            .Returns(ci => new GeneratedAccessToken("new-jwt-access-token", "new-jti", ci.Arg<DateTime>().AddMinutes(30)));

        _sut = new RefreshTokenUseCase(_userRepository, _jwtTokenService, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ActiveRefreshToken_RotatesTokenAndReturnsNewTokenPair()
    {
        var utcNow = DateTime.UtcNow;
        var activeToken = new RefreshTokenSession(
            tokenHash: "hash:valid-refresh-token",
            jwtId: "old-jti",
            createdAtUtc: utcNow.AddHours(-1),
            expiresAtUtc: utcNow.AddDays(6));

        var user = User.Reconstitute(
            id: "user-1",
            email: "student@ictu.edu.vn",
            fullName: "Student",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: "DTC01",
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: utcNow.AddDays(-2),
            updatedAtUtc: utcNow.AddHours(-1),
            refreshTokens: new[] { activeToken });

        _userRepository.GetByRefreshTokenHashAsync("hash:valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _sut.ExecuteAsync(new RefreshTokenRequest("valid-refresh-token"));

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("new-jwt-access-token"));
            Assert.That(result.RefreshToken, Is.EqualTo("new-raw-refresh-token"));
            Assert.That(activeToken.IsRevoked, Is.True);
            Assert.That(activeToken.ReplacedByTokenHash, Is.EqualTo("hash:new-raw-refresh-token"));
        });

        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_ExpiredOrRevokedRefreshToken_ThrowsInvalidTokenException()
    {
        var utcNow = DateTime.UtcNow;
        var expiredToken = new RefreshTokenSession(
            tokenHash: "hash:expired-token",
            jwtId: "old-jti",
            createdAtUtc: utcNow.AddDays(-8),
            expiresAtUtc: utcNow.AddDays(-1));

        var user = User.Reconstitute(
            id: "user-1",
            email: "student@ictu.edu.vn",
            fullName: "Student",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: "DTC01",
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: utcNow.AddDays(-10),
            updatedAtUtc: utcNow.AddDays(-8),
            refreshTokens: new[] { expiredToken });

        _userRepository.GetByRefreshTokenHashAsync("hash:expired-token", Arg.Any<CancellationToken>())
            .Returns(user);

        Assert.ThrowsAsync<InvalidTokenException>(
            async () => await _sut.ExecuteAsync(new RefreshTokenRequest("expired-token")));
    }

    [Test]
    public async Task ExecuteAsync_ConcurrencyConflictDuringUpdate_ThrowsInvalidTokenExceptionAndLogsAudit()
    {
        var utcNow = DateTime.UtcNow;
        var activeToken = new RefreshTokenSession(
            tokenHash: "hash:concurrent-token",
            jwtId: "active-jti",
            createdAtUtc: utcNow.AddHours(-1),
            expiresAtUtc: utcNow.AddDays(7));

        var user = User.Reconstitute(
            id: "user-concurrent",
            email: "student@ictu.edu.vn",
            fullName: "Student",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: "DTC01",
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: utcNow.AddDays(-1),
            updatedAtUtc: utcNow.AddHours(-1),
            refreshTokens: new[] { activeToken },
            concurrencyToken: "old-token-version");

        _userRepository.GetByRefreshTokenHashAsync("hash:concurrent-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.UpdateAsync(user, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConcurrencyConflictException("Conflict")));

        Assert.ThrowsAsync<InvalidTokenException>(
            async () => await _sut.ExecuteAsync(new RefreshTokenRequest("concurrent-token")));

        _auditLogger.Received(1).LogTokenRevoked("user-concurrent", Arg.Is<string>(s => s.Contains("concurrency conflict")));
    }
}

