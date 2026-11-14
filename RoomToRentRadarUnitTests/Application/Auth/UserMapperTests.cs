using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadarUnitTests.Application.Auth;

[TestFixture]
public class UserMapperTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ToProfileDto_MapsCampusAndLanguage()
    {
        var user = User.Reconstitute(
            id: "u-1",
            email: "student@ictu.edu.vn",
            fullName: "Nguyen Van A",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: null,
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: FixedNow,
            updatedAtUtc: FixedNow,
            selectedCampusId: "TNUS",
            preferredLanguage: "vi",
            version: 7);

        var dto = UserMapper.ToProfileDto(user);

        Assert.Multiple(() =>
        {
            Assert.That(dto.SelectedCampusId, Is.EqualTo("TNUS"));
            Assert.That(dto.PreferredLanguage, Is.EqualTo("vi"));
            Assert.That(dto.Id, Is.EqualTo("u-1"));
            Assert.That(dto.Email, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(dto.FullName, Is.EqualTo("Nguyen Van A"));
            Assert.That(dto.Role, Is.EqualTo("Student"));
        });
    }

    [Test]
    public void ToProfileDto_NewUser_ReturnsDefaults()
    {
        var user = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "hash",
            fullName: "Nguyen Van A",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: null,
            utcNow: FixedNow);

        var dto = UserMapper.ToProfileDto(user);

        Assert.Multiple(() =>
        {
            Assert.That(dto.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(dto.PreferredLanguage, Is.EqualTo("en"));
        });
    }

    [Test]
    public void ToProfileDto_NullUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => UserMapper.ToProfileDto(null!));
    }
}