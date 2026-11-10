using RoomToRentRadar.Application.UseCases.Auth;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Auth;

[TestFixture]
public class LoginUserUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private IPasswordHasherPort _passwordHasher = null!;
    private IJwtTokenServicePort _jwtTokenService = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private LoginUserUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasherPort>();
        _jwtTokenService = Substitute.For<IJwtTokenServicePort>();
        _auditLogger = Substitute.For<ISecurityAuditLoggerPort>();

        _jwtTokenService.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _jwtTokenService.GenerateRawRefreshToken().Returns("raw-refresh-token");
        _jwtTokenService.ComputeTokenHash("raw-refresh-token").Returns("hashed-refresh-token");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<DateTime>())
            .Returns(ci => new GeneratedAccessToken("jwt-access-token", "jti-002", ci.Arg<DateTime>().AddMinutes(30)));

        _sut = new LoginUserUseCase(_userRepository, _passwordHasher, _jwtTokenService, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ValidCredentials_ResetsFailedCountAndIssuesTokens()
    {
        var user = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "stored-password-hash",
            fullName: "Nguyen Van Sinh Vien",
            role: UserRole.Student,
            phoneNumber: "0912345678",
            studentCode: "DTC225180",
            utcNow: DateTime.UtcNow.AddDays(-1));

        _userRepository.GetByEmailAsync("student@ictu.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("StrongPass123", "stored-password-hash").Returns(true);

        var result = await _sut.ExecuteAsync(new LoginRequest("student@ictu.edu.vn", "StrongPass123"));

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("jwt-access-token"));
            Assert.That(result.RefreshToken, Is.EqualTo("raw-refresh-token"));
            Assert.That(user.FailedLoginCount, Is.EqualTo(0));
        });

        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_WrongPassword_IncrementsFailedLoginCountAndThrowsInvalidCredentialsException()
    {
        var user = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "stored-password-hash",
            fullName: "Nguyen Van Sinh Vien",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: "DTC225180",
            utcNow: DateTime.UtcNow.AddDays(-1));

        _userRepository.GetByEmailAsync("student@ictu.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("WrongPassword1", "stored-password-hash").Returns(false);

        Assert.That(
            async () => await _sut.ExecuteAsync(new LoginRequest("student@ictu.edu.vn", "WrongPassword1")),
            Throws.TypeOf<InvalidCredentialsException>());

        Assert.That(user.FailedLoginCount, Is.EqualTo(1));
        _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_FifthConsecutiveWrongPassword_LocksAccountAndThrowsAccountLockedException()
    {
        var utcNow = DateTime.UtcNow;
        var user = User.Reconstitute(
            id: "user-1",
            email: "student@ictu.edu.vn",
            fullName: "Student",
            passwordHash: "stored-password-hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: "DTC225180",
            isVerified: true,
            failedLoginCount: 4,
            lockoutEndUtc: null,
            createdAtUtc: utcNow.AddDays(-1),
            updatedAtUtc: utcNow.AddMinutes(-1));

        _userRepository.GetByEmailAsync("student@ictu.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("WrongPassword5", "stored-password-hash").Returns(false);

        Assert.That(
            async () => await _sut.ExecuteAsync(new LoginRequest("student@ictu.edu.vn", "WrongPassword5")),
            Throws.TypeOf<AccountLockedException>());

        Assert.That(user.FailedLoginCount, Is.EqualTo(5));
        Assert.That(user.LockoutEndUtc, Is.Not.Null);
    }
}

