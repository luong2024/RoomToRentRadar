using NSubstitute;
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
    private IPasswordHasherPort _passwordHasher = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private LoginWithGoogleUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _googleVerifier = Substitute.For<IGoogleAuthVerifierPort>();
        _jwtTokenService = Substitute.For<IJwtTokenServicePort>();
        _passwordHasher = Substitute.For<IPasswordHasherPort>();
        _auditLogger = Substitute.For<ISecurityAuditLoggerPort>();

        _jwtTokenService.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _jwtTokenService.GenerateRawRefreshToken().Returns("google-refresh-token");
        _jwtTokenService.ComputeTokenHash("google-refresh-token").Returns("hashed-google-refresh-token");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<DateTime>())
            .Returns(ci => new GeneratedAccessToken("google-jwt-access-token", "jti-google-1", ci.Arg<DateTime>().AddMinutes(30)));

        _passwordHasher.HashPassword(Arg.Any<string>()).Returns(ci => "hashed-" + ci.Arg<string>());
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(ci =>
            ci.ArgAt<string>(1) == "hashed-" + ci.ArgAt<string>(0) || ci.ArgAt<string>(1) == "hash");

        _sut = new LoginWithGoogleUseCase(_userRepository, _googleVerifier, _jwtTokenService, _passwordHasher, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ValidStudentTokenWithPassword_ProvisionsVerifiedStudentAndIssuesTokens()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-student-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student.ictu@ictu.edu.vn",
                FullName: "Sinh Vien Google",
                SubjectId: "google-sub-student-1",
                EmailVerified: true,
                HostedDomain: "ictu.edu.vn"));

        _userRepository.GetByEmailAsync("student.ictu@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _sut.ExecuteAsync(new GoogleLoginRequest(
            IdToken: "valid-google-student-token",
            Role: "Student",
            Password: "Password123!"));

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
                u.IsVerified &&
                u.PasswordHash == "hashed-Password123!"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_StudentInitialRegistrationWithoutPassword_ThrowsPasswordRequiredException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-student-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student.ictu@ictu.edu.vn",
                FullName: "Sinh Vien Google",
                SubjectId: "google-sub-student-1",
                EmailVerified: true,
                HostedDomain: "ictu.edu.vn"));

        _userRepository.GetByEmailAsync("student.ictu@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var ex = Assert.ThrowsAsync<PasswordRequiredException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-student-token", "Student", Password: null)));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_StudentWithNonIctuEmail_ThrowsValidationException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-personal-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student@gmail.com",
                FullName: "Personal Student",
                SubjectId: "google-sub-2",
                EmailVerified: true,
                HostedDomain: null));

        _userRepository.GetByEmailAsync("student@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var ex = Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-personal-token", "Student", Password: "Password123!")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_StudentWithIctuEmailButInvalidHostedDomain_ThrowsValidationException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-token-wrong-hd", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student@ictu.edu.vn",
                FullName: "Student Fake Domain",
                SubjectId: "google-sub-fake-hd",
                EmailVerified: true,
                HostedDomain: "otherdomain.com"));

        _userRepository.GetByEmailAsync("student@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var ex = Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-token-wrong-hd", "Student", Password: "Password123!")));

        Assert.That(ex.Message, Does.Contain("verified hosted domain 'ictu.edu.vn'"));
    }

    [Test]
    public async Task ExecuteAsync_ValidLandlordTokenWithPhoneAndPassword_ProvisionsUnverifiedLandlord()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "landlord@gmail.com",
                FullName: "Chu Tro Google",
                SubjectId: "google-sub-landlord-1",
                EmailVerified: true,
                HostedDomain: null));

        _userRepository.GetByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _sut.ExecuteAsync(new GoogleLoginRequest(
            IdToken: "valid-google-landlord-token",
            Role: "Landlord",
            PhoneNumber: "0912345678",
            HouseName: "Tro Co Hoa",
            HouseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen",
            Password: "Password123!"));

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("google-jwt-access-token"));
            Assert.That(result.User.Email, Is.EqualTo("landlord@gmail.com"));
            Assert.That(result.User.Role, Is.EqualTo("Landlord"));
            Assert.That(result.User.AuthProvider, Is.EqualTo("Google"));
            Assert.That(result.User.PhoneNumber, Is.EqualTo("0912345678"));
            Assert.That(result.User.HouseName, Is.EqualTo("Tro Co Hoa"));
            Assert.That(result.User.HouseAddress, Is.EqualTo("So 12 ngo 34 Z115, TP Thai Nguyen"));
            Assert.That(result.User.IsVerified, Is.False);
        });

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Role == UserRole.Landlord &&
                u.AuthProvider == AuthProvider.Google &&
                !u.IsVerified &&
                u.PasswordHash == "hashed-Password123!"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_LandlordWithoutPhone_ThrowsPhoneNumberRequiredException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "landlord@gmail.com",
                FullName: "Chu Tro Google",
                SubjectId: "google-sub-landlord-1",
                EmailVerified: true,
                HostedDomain: null));

        _userRepository.GetByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var ex = Assert.ThrowsAsync<PhoneNumberRequiredException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest(
                IdToken: "valid-google-landlord-token",
                Role: "Landlord",
                PhoneNumber: null,
                HouseName: "Tro Co Hoa",
                HouseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen",
                Password: "Password123!")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_LandlordWithoutHouseName_ThrowsHouseNameRequiredException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "landlord@gmail.com",
                FullName: "Chu Tro Google",
                SubjectId: "google-sub-landlord-1",
                EmailVerified: true,
                HostedDomain: null));

        _userRepository.GetByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var ex = Assert.ThrowsAsync<HouseNameRequiredException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest(
                IdToken: "valid-google-landlord-token",
                Role: "Landlord",
                PhoneNumber: "0912345678",
                HouseName: null,
                HouseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen",
                Password: "Password123!")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_LandlordWithoutHouseAddress_ThrowsHouseAddressRequiredException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "landlord@gmail.com",
                FullName: "Chu Tro Google",
                SubjectId: "google-sub-landlord-1",
                EmailVerified: true,
                HostedDomain: null));

        _userRepository.GetByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var ex = Assert.ThrowsAsync<HouseAddressRequiredException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest(
                IdToken: "valid-google-landlord-token",
                Role: "Landlord",
                PhoneNumber: "0912345678",
                HouseName: "Tro Co Hoa",
                HouseAddress: null,
                Password: "Password123!")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_ExistingLocalUserWithoutCurrentPassword_ThrowsCurrentPasswordRequiredException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "existing@gmail.com",
                FullName: "Existing Landlord",
                SubjectId: "google-sub-existing",
                EmailVerified: true,
                HostedDomain: null));

        var existingUser = User.RegisterLocal(
            email: "existing@gmail.com",
            passwordHash: "hash",
            fullName: "Existing Landlord",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-1),
            houseName: "Tro Co Hoa",
            houseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen");

        _userRepository.GetByEmailAsync("existing@gmail.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var ex = Assert.ThrowsAsync<CurrentPasswordRequiredException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-token", "Landlord", CurrentPassword: null)));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_ExistingLocalUserWithWrongCurrentPassword_ThrowsInvalidCredentialsException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "existing@gmail.com",
                FullName: "Existing Landlord",
                SubjectId: "google-sub-existing",
                EmailVerified: true,
                HostedDomain: null));

        var existingUser = User.RegisterLocal(
            email: "existing@gmail.com",
            passwordHash: "hash",
            fullName: "Existing Landlord",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-1),
            houseName: "Tro Co Hoa",
            houseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen");

        _userRepository.GetByEmailAsync("existing@gmail.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        _passwordHasher.VerifyPassword("wrong-pass", "hash").Returns(false);

        var ex = Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-token", "Landlord", CurrentPassword: "wrong-pass")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task ExecuteAsync_ExistingLocalUserWithValidCurrentPassword_LinksGoogleAndLogsIn()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-google-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "existing@gmail.com",
                FullName: "Existing Landlord",
                SubjectId: "google-sub-existing",
                EmailVerified: true,
                HostedDomain: null));

        var existingUser = User.RegisterLocal(
            email: "existing@gmail.com",
            passwordHash: "hash",
            fullName: "Existing Landlord",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-1),
            houseName: "Tro Co Hoa",
            houseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen");

        _userRepository.GetByEmailAsync("existing@gmail.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        _passwordHasher.VerifyPassword("correct-pass", "hash").Returns(true);

        var result = await _sut.ExecuteAsync(new GoogleLoginRequest("valid-google-token", "Landlord", CurrentPassword: "correct-pass"));

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
        var ex = Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("some-token", role)));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_ExistingAdminUser_ThrowsForbiddenRoleException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-admin-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "admin@ictu.edu.vn",
                FullName: "Administrator",
                SubjectId: "google-sub-admin",
                EmailVerified: true,
                HostedDomain: "ictu.edu.vn"));

        var adminUser = User.Reconstitute(
            id: "admin-1",
            email: "admin@ictu.edu.vn",
            fullName: "Administrator",
            passwordHash: "hash",
            role: UserRole.Admin,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: null,
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: DateTime.UtcNow.AddDays(-10),
            updatedAtUtc: DateTime.UtcNow.AddDays(-10));

        _userRepository.GetByEmailAsync("admin@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns(adminUser);

        var ex = Assert.ThrowsAsync<ForbiddenRoleException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-admin-token", "Student")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_ExistingLandlordRequestingStudentRole_ThrowsValidationException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-landlord-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "chutro@ictu.edu.vn",
                FullName: "Landlord Account",
                SubjectId: "google-sub-landlord",
                EmailVerified: true,
                HostedDomain: "ictu.edu.vn"));

        var landlordUser = User.RegisterLocal(
            email: "chutro@ictu.edu.vn",
            passwordHash: "hash",
            fullName: "Landlord Account",
            role: UserRole.Landlord,
            phoneNumber: "0912345678",
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-10),
            houseName: "Tro Chu Tro",
            houseAddress: "So 12 ngo 34 Z115, TP Thai Nguyen");

        _userRepository.GetByEmailAsync("chutro@ictu.edu.vn", Arg.Any<CancellationToken>())
            .Returns(landlordUser);

        var ex = Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-landlord-token", "Student")));

        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void ExecuteAsync_ExistingStudentWithNonIctuEmail_ThrowsValidationException()
    {
        _googleVerifier.VerifyIdTokenAsync("valid-personal-token", Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload(
                Email: "student@gmail.com",
                FullName: "Personal Student",
                SubjectId: "google-sub-personal",
                EmailVerified: true,
                HostedDomain: null));

        var studentUser = User.RegisterLocal(
            email: "student@gmail.com",
            passwordHash: "hash",
            fullName: "Personal Student",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: null,
            utcNow: DateTime.UtcNow.AddDays(-10));

        _userRepository.GetByEmailAsync("student@gmail.com", Arg.Any<CancellationToken>())
            .Returns(studentUser);

        var ex = Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _sut.ExecuteAsync(new GoogleLoginRequest("valid-personal-token", "Student")));

        Assert.That(ex, Is.Not.Null);
    }
}
