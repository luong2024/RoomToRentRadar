using RoomToRentRadar.Application.UseCases.Auth;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Auth;

[TestFixture]
public class RevokeTokenUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private IJwtTokenServicePort _jwtTokenService = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private RevokeTokenUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _jwtTokenService = Substitute.For<IJwtTokenServicePort>();
        _auditLogger = Substitute.For<ISecurityAuditLoggerPort>();

        _jwtTokenService.ComputeTokenHash(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");
        _sut = new RevokeTokenUseCase(_userRepository, _jwtTokenService, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ValidActiveToken_MarksTokenRevokedAndUpdatesRepository()
    {
        var utcNow = DateTime.UtcNow;
        var session = new RefreshTokenSession(
            tokenHash: "hash:active-refresh-token",
            jwtId: "jti-1",
            createdAtUtc: utcNow.AddHours(-2),
            expiresAtUtc: utcNow.AddDays(5));

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
            updatedAtUtc: utcNow.AddHours(-2),
            refreshTokens: new[] { session });

        _userRepository.GetByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);

        await _sut.ExecuteAsync("user-1", new RevokeTokenRequest("active-refresh-token"));

        Assert.That(session.IsRevoked, Is.True);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        _auditLogger.Received(1).LogTokenRevoked("user-1", Arg.Any<string>());
    }

    [Test]
    public void ExecuteAsync_UnknownToken_ThrowsInvalidTokenException()
    {
        var utcNow = DateTime.UtcNow;
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
            updatedAtUtc: utcNow.AddHours(-2));

        _userRepository.GetByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);

        Assert.That(
            async () => await _sut.ExecuteAsync("user-1", new RevokeTokenRequest("non-existent-token")),
            Throws.TypeOf<InvalidTokenException>());
    }
}

