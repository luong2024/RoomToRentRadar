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
public class LoginWithIctuSsoUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private IIctuSsoVerifierPort _ssoVerifier = null!;
    private IJwtTokenServicePort _jwtTokenService = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private LoginWithIctuSsoUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _ssoVerifier = Substitute.For<IIctuSsoVerifierPort>();
        _jwtTokenService = Substitute.For<IJwtTokenServicePort>();
        _auditLogger = Substitute.For<ISecurityAuditLoggerPort>();

        _jwtTokenService.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _jwtTokenService.GenerateRawRefreshToken().Returns("sso-refresh-token");
        _jwtTokenService.ComputeTokenHash("sso-refresh-token").Returns("hashed-sso-refresh-token");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<DateTime>())
            .Returns(ci => new GeneratedAccessToken("sso-jwt-access-token", "jti-sso-1", ci.Arg<DateTime>().AddMinutes(30)));

        _sut = new LoginWithIctuSsoUseCase(_userRepository, _ssoVerifier, _jwtTokenService, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ValidIctuSsoTokenForNewStudent_ProvisionsVerifiedStudentAndIssuesTokens()
    {
        _ssoVerifier.VerifyIdTokenAsync("valid-ictu-oidc-token", Arg.Any<CancellationToken>())
            .Returns(new IctuSsoUserPayload(
                Email: "dtc225180@ictu.edu.vn",
                FullName: "Nguyen Van Sinh Vien",
                SubjectId: "oidc-sub-225180",
                StudentCode: "DTC225180"));

        _userRepository.GetByEmailAsync("dtc225180@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _sut.ExecuteAsync(new IctuSsoLoginRequest("valid-ictu-oidc-token"));

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("sso-jwt-access-token"));
            Assert.That(result.User.Email, Is.EqualTo("dtc225180@ictu.edu.vn"));
            Assert.That(result.User.Role, Is.EqualTo("Student"));
            Assert.That(result.User.AuthProvider, Is.EqualTo("IctuSso"));
            Assert.That(result.User.IsVerified, Is.True);
        });

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Role == UserRole.Student &&
                u.AuthProvider == AuthProvider.IctuSso &&
                u.IsVerified),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ExistingStudentAccount_LinksIctuSsoAndUpdatesUser()
    {
        var existingStudent = User.RegisterLocal(
            email: "dtc225180@ictu.edu.vn",
            passwordHash: "hash",
            fullName: "Nguyen Van Sinh Vien",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-5));

        _ssoVerifier.VerifyIdTokenAsync("valid-ictu-oidc-token", Arg.Any<CancellationToken>())
            .Returns(new IctuSsoUserPayload(
                Email: "dtc225180@ictu.edu.vn",
                FullName: "Nguyen Van Sinh Vien",
                SubjectId: "oidc-sub-225180",
                StudentCode: "DTC225180"));

        _userRepository.GetByEmailAsync("dtc225180@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns(existingStudent);

        var result = await _sut.ExecuteAsync(new IctuSsoLoginRequest("valid-ictu-oidc-token"));

        Assert.Multiple(() =>
        {
            Assert.That(result.User.StudentCode, Is.EqualTo("DTC225180"));
            Assert.That(existingStudent.SsoSubjectId, Is.EqualTo("oidc-sub-225180"));
        });

        await _userRepository.Received(1).UpdateAsync(existingStudent, Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_NonIctuEmailDomain_ThrowsDomainValidationException()
    {
        _ssoVerifier.VerifyIdTokenAsync("gmail-oidc-token", Arg.Any<CancellationToken>())
            .Returns(new IctuSsoUserPayload(
                Email: "outsider@gmail.com",
                FullName: "Outsider",
                SubjectId: "sub-999"));

        Assert.That(
            async () => await _sut.ExecuteAsync(new IctuSsoLoginRequest("gmail-oidc-token")),
            Throws.TypeOf<DomainValidationException>());
    }
}

