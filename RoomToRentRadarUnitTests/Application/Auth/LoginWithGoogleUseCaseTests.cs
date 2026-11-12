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
public class LoginWithGoogleUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private IGoogleAuthVerifierPort _googleVerifier = null!;
    private IJwtTokenServicePort _jwtTokenService = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private LoginWithGoogleUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _googleVerifier = Substitute.For<IGoogleAuthVerifierPort>();
        _jwtTokenService = Substitute.For<IJwtTokenServicePort>();
        _auditLogger = Substitute.For<ISecurityAuditLoggerPort>();

        _jwtTokenService.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _jwtTokenService.GenerateRawRefreshToken().Returns("google-refresh-token");
        _jwtTokenService.ComputeTokenHash("google-refresh-token").Returns("hashed-google-refresh-token");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<DateTime>())
            .Returns(ci => new GeneratedAccessToken("google-jwt-access-token", "jti-google-1", ci.Arg<DateTime>().AddMinutes(30)));

        _sut = new LoginWithGoogleUseCase(_userRepository, _googleVerifier, _jwtTokenService, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ValidStudentToken_ProvisionsVerifiedStudentAndIssuesTokens()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-student-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student.ictu@ictu.edu.vn",
                FullName: "Sinh Vien Google",
                SubjectId: "google-sub-student-1"));

        _userRepository.GetByEmailAsync("student.ictu@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-student-token", "Student"));

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("google-jwt-access-token"));
            Assert.That(result.User.Email, Is.EqualTo("student.ictu@ictu.edu.vn"));
            Assert.That(result.User.Role, Is.EqualTo("Student"));
            Assert.That(result.User.AuthProvider, Is.EqualTo("Google"));
            Assert.That(result.User.IsVerified, Is.True);
        });

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Role == UserRole.Student &&
                u.AuthProvider == AuthProvider.Google &&
                u.IsVerified),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_StudentWithNonIctuEmail_ThrowsValidationException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-personal-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student@gmail.com",
                FullName: "Personal Student",
                SubjectId: "google-sub-2"));

        _userRepository.GetByEmailAsync("student@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-personal-token", "Student")));
    }

    [Test]
    public async Task ExecuteAsync_ValidLandlordTokenWithPhone_ProvisionsUnverifiedLandlord()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "landlord@gmail.com",
                FullName: "Chu Tro Google",
                SubjectId: "google-sub-landlord-1"));

        _userRepository.GetByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _sut.ExecuteAsync(new GoogleLoginRequest(
            IdToken: "valid-google-landlord-token",
            Role: "Landlord",
            PhoneNumber: "0912345678"));

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("google-jwt-access-token"));
            Assert.That(result.User.Email, Is.EqualTo("landlord@gmail.com"));
            Assert.That(result.User.Role, Is.EqualTo("Landlord"));
            Assert.That(result.User.AuthProvider, Is.EqualTo("Google"));
            Assert.That(result.User.PhoneNumber, Is.EqualTo("0912345678"));
            Assert.That(result.User.IsVerified, Is.False);
        });

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Role == UserRole.Landlord &&
                u.AuthProvider == AuthProvider.Google &&
                !u.IsVerified),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_LandlordWithoutPhone_ThrowsValidationException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "landlord@gmail.com",
                FullName: "Chu Tro Google",
                SubjectId: "google-sub-landlord-1"));

        _userRepository.GetByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.ExecuteAsync(new GoogleLoginRequest(
                IdToken: "valid-google-landlord-token",
                Role: "Landlord",
                PhoneNumber: null)));
    }

    [Test]
    public async Task ExecuteAsync_ExistingUser_LinksGoogleAndLogsIn()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "existing@gmail.com",
                FullName: "Existing Landlord",
                SubjectId: "google-sub-existing"));

        var existingUser = User.RegisterLocal(
            email: "existing@gmail.com",
            passwordHash: "hash",
            fullName: "Existing Landlord",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-1));

        _userRepository.GetByEmailAsync("existing@gmail.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var result = await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-token", "Landlord"));

        Assert.Multiple(() =>
        {
            Assert.That(result.User.Email, Is.EqualTo("existing@gmail.com"));
            Assert.That(existingUser.AuthProvider, Is.EqualTo(AuthProvider.Google));
            Assert.That(existingUser.SsoSubjectId, Is.EqualTo("google-sub-existing"));
        });

        await _userRepository.Received(1).UpdateAsync(existingUser, Arg.Any<CancellationToken>());
    }

    [TestCase("Admin")]
    [TestCase("InvalidRole")]
    [TestCase("")]
    public void ExecuteAsync_InvalidRole_ThrowsValidationException(string role)
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.ExecuteAsync(new GoogleLoginRequest("some-token", role)));
    }
}
