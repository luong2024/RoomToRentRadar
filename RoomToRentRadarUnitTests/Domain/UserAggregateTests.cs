using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class UserAggregateTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public void RegisterLocal_AsStudent_CreatesVerifiedStudentAccount()
    {
        var user = User.RegisterLocal(
            email: "Student@ictu.edu.vn",
            passwordHash: "hashed-password",
            fullName: "Nguyen Van Sinh Vien",
            role: UserRole.Student,
            phoneNumber: "0912345678",
            studentCode: "dtc225180",
            utcNow: FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(user.Id, Is.Not.Empty);
            Assert.That(user.Email.Value, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(user.Role, Is.EqualTo(UserRole.Student));
            Assert.That(user.AuthProvider, Is.EqualTo(AuthProvider.Local));
            Assert.That(user.IsVerified, Is.True);
            Assert.That(user.StudentCode, Is.EqualTo("DTC225180"));
        });
    }

    [Test]
    public void RegisterLocal_AsLandlord_CreatesUnverifiedLandlordAndBlocksPublishingUntilApproved()
    {
        var landlord = User.RegisterLocal(
            email: "landlord@gmail.com",
            passwordHash: "hashed-password",
            fullName: "Tran Thi Chu Tro",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: FixedNow,
            houseName: "Tro Co Hoa");

        Assert.Multiple(() =>
        {
            Assert.That(landlord.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(landlord.IsVerified, Is.False);
            Assert.That(landlord.PhoneNumber, Is.EqualTo("0987654321"));
            Assert.That(landlord.HouseName, Is.EqualTo("Tro Co Hoa"));
            Assert.That(
                () => landlord.EnsureCanPublishListing(),
                Throws.TypeOf<LandlordNotApprovedException>());
        });

        landlord.ApproveLandlord(FixedNow.AddMinutes(30));

        Assert.That(landlord.IsVerified, Is.True);
        Assert.That(() => landlord.EnsureCanPublishListing(), Throws.Nothing);
    }

    [Test]
    public void RegisterLocal_AsAdmin_ThrowsForbiddenRoleException()
    {
        Assert.That(
            () => User.RegisterLocal(
                email: "admin@ictu.edu.vn",
                passwordHash: "hashed-password",
                fullName: "Unauthorized Admin",
                role: UserRole.Admin,
                phoneNumber: null,
                studentCode: null,
                utcNow: FixedNow),
            Throws.TypeOf<ForbiddenRoleException>());
    }

    [Test]
    public void RegisterLocal_LandlordWithoutPhoneNumber_ThrowsDomainValidationException()
    {
        Assert.That(
            () => User.RegisterLocal(
                email: "landlord@gmail.com",
                passwordHash: "hashed-password",
                fullName: "Landlord Without Phone",
                role: UserRole.Landlord,
                phoneNumber: null,
                studentCode: null,
                utcNow: FixedNow,
                houseName: "Tro Co Hoa"),
            Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void RegisterLocal_AsLandlordWithoutHouseName_ThrowsDomainValidationException()
    {
        Assert.That(
            () => User.RegisterLocal(
                email: "landlord@gmail.com",
                passwordHash: "hashed-password",
                fullName: "Landlord Without House",
                role: UserRole.Landlord,
                phoneNumber: "0987654321",
                studentCode: null,
                utcNow: FixedNow,
                houseName: null),
            Throws.TypeOf<DomainValidationException>());
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("ab")]
    public void RegisterLocal_AsLandlordWithInvalidHouseName_ThrowsDomainValidationException(string? invalidHouseName)
    {
        Assert.That(
            () => User.RegisterLocal(
                email: "landlord@gmail.com",
                passwordHash: "hashed-password",
                fullName: "Landlord Invalid House",
                role: UserRole.Landlord,
                phoneNumber: "0987654321",
                studentCode: null,
                utcNow: FixedNow,
                houseName: invalidHouseName),
            Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void RecordLoginFailure_FiveConsecutiveAttempts_LocksAccountFor15Minutes()
    {
        var user = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "hashed-password",
            fullName: "Student",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: "DTC01",
            utcNow: FixedNow);

        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RecordLoginFailure(FixedNow);
        }

        Assert.Multiple(() =>
        {
            Assert.That(user.FailedLoginCount, Is.EqualTo(5));
            Assert.That(user.IsLockedOut(FixedNow.AddMinutes(10)), Is.True);
            Assert.That(
                () => user.EnsureNotLockedOut(FixedNow.AddMinutes(10)),
                Throws.TypeOf<AccountLockedException>());
            Assert.That(user.IsLockedOut(FixedNow.AddMinutes(16)), Is.False);
        });
    }

    [Test]
    public void RotateRefreshToken_RevokesCurrentTokenAndIssuesReplacement()
    {
        var user = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "hashed-password",
            fullName: "Student",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: "DTC01",
            utcNow: FixedNow);

        user.IssueRefreshToken("token-hash-1", "jti-1", FixedNow, TimeSpan.FromDays(7));
        var nextSession = user.RotateRefreshToken(
            currentTokenHash: "token-hash-1",
            newTokenHash: "token-hash-2",
            newJwtId: "jti-2",
            utcNow: FixedNow.AddHours(1),
            lifetime: TimeSpan.FromDays(7));

        Assert.Multiple(() =>
        {
            Assert.That(nextSession.TokenHash, Is.EqualTo("token-hash-2"));
            var oldToken = user.RefreshTokens.Single(t => t.TokenHash == "token-hash-1");
            Assert.That(oldToken.IsRevoked, Is.True);
            Assert.That(oldToken.ReplacedByTokenHash, Is.EqualTo("token-hash-2"));
            Assert.That(nextSession.IsActive(FixedNow.AddHours(2)), Is.True);
        });
    }

    [Test]
    public void RotateRefreshToken_WhenTokenAlreadyRevoked_RevokesAllActiveTokensAndThrowsInvalidTokenException()
    {
        var user = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "hashed-password",
            fullName: "Student",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: "DTC01",
            utcNow: FixedNow);

        user.IssueRefreshToken("token-hash-1", "jti-1", FixedNow, TimeSpan.FromDays(7));
        user.RotateRefreshToken("token-hash-1", "token-hash-2", "jti-2", FixedNow.AddMinutes(5), TimeSpan.FromDays(7));

        Assert.That(
            () => user.RotateRefreshToken("token-hash-1", "token-hash-3", "jti-3", FixedNow.AddMinutes(10), TimeSpan.FromDays(7)),
            Throws.TypeOf<InvalidTokenException>());

        Assert.That(user.RefreshTokens.All(t => t.IsRevoked), Is.True);
    }

    [Test]
    public void RegisterLocal_WithUndefinedRoleInteger_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "hash",
            fullName: "Test",
            role: (UserRole)999,
            phoneNumber: null,
            studentCode: null,
            utcNow: FixedNow));
    }

    [TestCase("", "valid-jti")]
    [TestCase("   ", "valid-jti")]
    [TestCase(null, "valid-jti")]
    public void RefreshTokenSession_WithInvalidTokenHash_ThrowsDomainValidationException(string? tokenHash, string jwtId)
    {
        Assert.Throws<DomainValidationException>(() => new RefreshTokenSession(
            tokenHash: tokenHash!,
            jwtId: jwtId,
            createdAtUtc: FixedNow,
            expiresAtUtc: FixedNow.AddDays(7)));
    }

    [TestCase("valid-hash", "")]
    [TestCase("valid-hash", "   ")]
    [TestCase("valid-hash", null)]
    public void RefreshTokenSession_WithInvalidJwtId_ThrowsDomainValidationException(string tokenHash, string? jwtId)
    {
        Assert.Throws<DomainValidationException>(() => new RefreshTokenSession(
            tokenHash: tokenHash,
            jwtId: jwtId!,
            createdAtUtc: FixedNow,
            expiresAtUtc: FixedNow.AddDays(7)));
    }

    [Test]
    public void RefreshTokenSession_WithExpirationNotGreaterThanCreation_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new RefreshTokenSession(
            tokenHash: "valid-hash",
            jwtId: "valid-jti",
            createdAtUtc: FixedNow,
            expiresAtUtc: FixedNow));

        Assert.Throws<DomainValidationException>(() => new RefreshTokenSession(
            tokenHash: "valid-hash",
            jwtId: "valid-jti",
            createdAtUtc: FixedNow,
            expiresAtUtc: FixedNow.AddMinutes(-5)));
    }

    [Test]
    public void RegisterGoogle_AsStudent_ValidIctuEmail_CreatesVerifiedStudentAccount()
    {
        var user = User.RegisterGoogle(
            email: "student@ictu.edu.vn",
            fullName: "Google Student",
            googleSubjectId: "google-sub-12345",
            role: UserRole.Student,
            phoneNumber: null,
            utcNow: FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(user.Id, Is.Not.Empty);
            Assert.That(user.Email.Value, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(user.FullName, Is.EqualTo("Google Student"));
            Assert.That(user.Role, Is.EqualTo(UserRole.Student));
            Assert.That(user.AuthProvider, Is.EqualTo(AuthProvider.Google));
            Assert.That(user.SsoSubjectId, Is.EqualTo("google-sub-12345"));
            Assert.That(user.IsVerified, Is.True);
            Assert.That(user.StudentCode, Is.EqualTo("STUDENT"));
        });
    }

    [Test]
    public void RegisterGoogle_AsStudent_NonIctuEmail_ThrowsValidationException()
    {
        Assert.Throws<DomainValidationException>(() => User.RegisterGoogle(
            email: "student@gmail.com",
            fullName: "Student Personal",
            googleSubjectId: "google-sub-123",
            role: UserRole.Student,
            phoneNumber: null,
            utcNow: FixedNow));
    }

    [Test]
    public void RegisterGoogle_AsLandlord_ValidPhone_CreatesUnverifiedLandlord()
    {
        var landlord = User.RegisterGoogle(
            email: "landlord@gmail.com",
            fullName: "Google Landlord",
            googleSubjectId: "google-sub-67890",
            role: UserRole.Landlord,
            phoneNumber: "0912345678",
            utcNow: FixedNow,
            houseName: "Tro Co Hoa");

        Assert.Multiple(() =>
        {
            Assert.That(landlord.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(landlord.AuthProvider, Is.EqualTo(AuthProvider.Google));
            Assert.That(landlord.SsoSubjectId, Is.EqualTo("google-sub-67890"));
            Assert.That(landlord.PhoneNumber, Is.EqualTo("0912345678"));
            Assert.That(landlord.HouseName, Is.EqualTo("Tro Co Hoa"));
            Assert.That(landlord.IsVerified, Is.False);
        });
    }

    [Test]
    public void RegisterGoogle_AsLandlord_MissingPhone_ThrowsValidationException()
    {
        Assert.Throws<DomainValidationException>(() => User.RegisterGoogle(
            email: "landlord@gmail.com",
            fullName: "Google Landlord",
            googleSubjectId: "google-sub-67890",
            role: UserRole.Landlord,
            phoneNumber: null,
            utcNow: FixedNow,
            houseName: "Tro Co Hoa"));
    }

    [Test]
    public void RegisterGoogle_AsLandlord_MissingHouseName_ThrowsValidationException()
    {
        Assert.Throws<HouseNameRequiredException>(() => User.RegisterGoogle(
            email: "landlord@gmail.com",
            fullName: "Google Landlord",
            googleSubjectId: "google-sub-67890",
            role: UserRole.Landlord,
            phoneNumber: "0912345678",
            utcNow: FixedNow,
            houseName: null));
    }

    [Test]
    public void LinkGoogle_ExistingUser_UpdatesSsoSubjectIdAndAuthProvider()
    {
        var user = User.RegisterLocal(
            email: "landlord@gmail.com",
            passwordHash: "hash",
            fullName: "Local Landlord",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: FixedNow,
            houseName: "Tro Co Hoa");

        user.LinkGoogle("google-sub-999", null, FixedNow.AddMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(user.AuthProvider, Is.EqualTo(AuthProvider.Google));
            Assert.That(user.SsoSubjectId, Is.EqualTo("google-sub-999"));
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(FixedNow.AddMinutes(5)));
        });
    }
}

