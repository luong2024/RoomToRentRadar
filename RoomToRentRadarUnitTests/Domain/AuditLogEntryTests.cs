using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class AuditLogEntryTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 10, 10, 30, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidInputs_InitializesPropertiesAndNormalizes()
    {
        var entry = new AuditLogEntry(
            id: null,
            userId: "  user-123  ",
            userEmail: "  STUDENT@ICTU.EDU.VN  ",
            role: "  Student  ",
            action: "  user_login  ",
            entityType: "  User  ",
            entityId: "  ent-456  ",
            detailsJson: "{\"device\":\"android\"}",
            ipAddress: "  10.0.2.2  ",
            userAgent: "  Mozilla/5.0  ",
            statusCode: 200,
            durationMs: 35,
            isSuccess: true,
            errorMessage: null,
            timestampUtc: FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(Guid.TryParseExact(entry.Id, "N", out _), Is.True, "Id should be a 32-char N-format GUID.");
            Assert.That(entry.UserId, Is.EqualTo("user-123"));
            Assert.That(entry.UserEmail, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(entry.Role, Is.EqualTo("Student"));
            Assert.That(entry.Action, Is.EqualTo("USER_LOGIN"));
            Assert.That(entry.EntityType, Is.EqualTo("User"));
            Assert.That(entry.EntityId, Is.EqualTo("ent-456"));
            Assert.That(entry.IpAddress, Is.EqualTo("10.0.2.2"));
            Assert.That(entry.UserAgent, Is.EqualTo("Mozilla/5.0"));
            Assert.That(entry.StatusCode, Is.EqualTo(200));
            Assert.That(entry.DurationMs, Is.EqualTo(35));
            Assert.That(entry.IsSuccess, Is.True);
            Assert.That(entry.TimestampUtc, Is.EqualTo(FixedNow));
            Assert.That(entry.TimestampUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_EmptyAction_ThrowsDomainValidationException(string? invalidAction)
    {
        Assert.That(
            () => new AuditLogEntry(
                id: null,
                userId: "user-1",
                userEmail: "test@test.com",
                role: "Student",
                action: invalidAction!,
                entityType: "Room"),
            Throws.TypeOf<DomainValidationException>()
                .With.Message.EqualTo("Action cannot be empty."));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_EmptyEntityType_ThrowsDomainValidationException(string? invalidEntityType)
    {
        Assert.That(
            () => new AuditLogEntry(
                id: null,
                userId: "user-1",
                userEmail: "test@test.com",
                role: "Student",
                action: "ROOM_CREATED",
                entityType: invalidEntityType!),
            Throws.TypeOf<DomainValidationException>()
                .With.Message.EqualTo("EntityType cannot be empty."));
    }

    [Test]
    public void Constructor_UnspecifiedTimestamp_NormalizesToUtc()
    {
        var unspecifiedTime = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Unspecified);
        var entry = new AuditLogEntry(
            id: "custom-id",
            userId: null,
            userEmail: null,
            role: null,
            action: "SYSTEM_INIT",
            entityType: "System",
            timestampUtc: unspecifiedTime);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Role, Is.EqualTo("Anonymous"));
            Assert.That(entry.TimestampUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(entry.TimestampUtc, Is.EqualTo(DateTime.SpecifyKind(unspecifiedTime, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void GetEffectivePartitionKey_WhenUserIdPresent_ReturnsUserId()
    {
        var entry = new AuditLogEntry(
            id: null,
            userId: "student-ictu-999",
            userEmail: "student@ictu.edu.vn",
            role: "Student",
            action: "ROOM_SEARCH",
            entityType: "Room");

        Assert.That(entry.GetEffectivePartitionKey(), Is.EqualTo("student-ictu-999"));
    }

    [Test]
    public void GetEffectivePartitionKey_WhenUserIdNullOrEmpty_ReturnsAnonymousDateBucket()
    {
        var entry = new AuditLogEntry(
            id: null,
            userId: null,
            userEmail: null,
            role: "Anonymous",
            action: "LOGIN_FAILED",
            entityType: "User",
            timestampUtc: new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc));

        Assert.That(entry.GetEffectivePartitionKey(), Is.EqualTo("anonymous_202611"));
    }
}

