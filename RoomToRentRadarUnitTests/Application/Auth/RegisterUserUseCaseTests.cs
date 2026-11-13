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
public class RegisterUserUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private IPasswordHasherPort _passwordHasher = null!;
    private IJwtTokenServicePort _jwtTokenService = null!;
    private ISecurityAuditLoggerPort _auditLogger = null!;
    private RegisterUserUseCase _sut = null!;

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
            .Returns(ci => new GeneratedAccessToken("jwt-access-token", "jti-001", ci.Arg<DateTime>().AddMinutes(30)));

        _sut = new RegisterUserUseCase(_userRepository, _passwordHasher, _jwtTokenService, _auditLogger);
    }

    [Test]
    public async Task ExecuteAsync_ValidStudentRegistration_HashesPasswordAndCreatesVerifiedStudent()
    {
        _userRepository.ExistsByEmailAsync("student@ictu.edu.vn", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword("StrongPass123").Returns("pbkdf2-hashed-password");

        var request = new RegisterUserRequest(
            Email: "Student@ictu.edu.vn",
            Password: "StrongPass123",
            FullName: "Nguyen Van Sinh Vien",
            Role: "Student",
            PhoneNumber: "0912345678",
            StudentCode: "DTC225180");

        var result = await _sut.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("jwt-access-token"));
            Assert.That(result.RefreshToken, Is.EqualTo("raw-refresh-token"));
            Assert.That(result.User.Email, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(result.User.Role, Is.EqualTo("Student"));
            Assert.That(result.User.IsVerified, Is.True);
        });

        _passwordHasher.Received(1).HashPassword("StrongPass123");
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.Role == UserRole.Student &&
                u.IsVerified &&
                u.PasswordHash == "pbkdf2-hashed-password"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ValidLandlordRegistration_CreatesUnverifiedLandlordWaitingAdminApproval()
    {
        _userRepository.ExistsByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword("StrongPass123").Returns("pbkdf2-hashed-password");

        var request = new RegisterUserRequest(
            Email: "landlord@gmail.com",
            Password: "StrongPass123",
            FullName: "Tran Thi Chu Tro",
            Role: "Landlord",
            PhoneNumber: "0987654321",
            HouseName: "Nha tro Co Hoa");

        var result = await _sut.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.User.Role, Is.EqualTo("Landlord"));
            Assert.That(result.User.IsVerified, Is.False);
            Assert.That(result.User.PhoneNumber, Is.EqualTo("0987654321"));
            Assert.That(result.User.HouseName, Is.EqualTo("Nha tro Co Hoa"));
        });

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == UserRole.Landlord && !u.IsVerified),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_LandlordWithoutHouseName_ThrowsDomainValidationException()
    {
        _userRepository.ExistsByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>()).Returns(false);

        var request = new RegisterUserRequest(
            Email: "landlord@gmail.com",
            Password: "StrongPass123",
            FullName: "Tran Thi Chu Tro",
            Role: "Landlord",
            PhoneNumber: "0987654321",
            HouseName: null);

        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(request));
    }

    [Test]
    public async Task ExecuteAsync_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        _userRepository.ExistsByEmailAsync("existing@ictu.edu.vn", Arg.Any<CancellationToken>()).Returns(true);

        var request = new RegisterUserRequest(
            Email: "EXISTING@ictu.edu.vn",
            Password: "StrongPass123",
            FullName: "Duplicate User",
            Role: "Student");

        Assert.ThrowsAsync<DuplicateEmailException>(
            async () => await _sut.ExecuteAsync(request));
    }

    [Test]
    public void ExecuteAsync_AdminRoleSelfRegistration_ThrowsForbiddenRoleException()
    {
        var request = new RegisterUserRequest(
            Email: "hacker@ictu.edu.vn",
            Password: "StrongPass123",
            FullName: "Fake Admin",
            Role: "Admin");

        Assert.ThrowsAsync<ForbiddenRoleException>(
            async () => await _sut.ExecuteAsync(request));
    }

    [Test]
    public void ExecuteAsync_InvalidRoleIntegerString_ThrowsDomainValidationException()
    {
        var request = new RegisterUserRequest(
            Email: "attacker@ictu.edu.vn",
            Password: "StrongPass123",
            FullName: "Attacker",
            Role: "999");

        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(request));
    }

    [Test]
    public void ExecuteAsync_LandlordMissingPhoneNumber_ThrowsDomainValidationException()
    {
        _userRepository.ExistsByEmailAsync("landlord@gmail.com", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword("StrongPass123").Returns("pbkdf2-hashed-password");

        var request = new RegisterUserRequest(
            Email: "landlord@gmail.com",
            Password: "StrongPass123",
            FullName: "Landlord No Phone",
            Role: "Landlord",
            PhoneNumber: null);

        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(request));
    }
}

