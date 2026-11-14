using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>ADR-012, system-architecture section 7.2: domain exceptions carry stable error codes for the API error contract.</summary>
[TestFixture]
public class DomainExceptionsTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void EntityNotFoundException_NullOrWhitespaceCode_ThrowsArgumentException(string? code)
    {
        Assert.That(
            () => new EntityNotFoundException(code!),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void EntityNotFoundException_ExposesErrorCodeAndDefaultMessage()
    {
        var withDefault = new EntityNotFoundException("ROOM_NOT_FOUND");
        var withCustom = new EntityNotFoundException("ROOM_NOT_FOUND", "Room 'r1' does not exist.");

        Assert.Multiple(() =>
        {
            Assert.That(withDefault.ErrorCode, Is.EqualTo("ROOM_NOT_FOUND"));
            Assert.That(withDefault.Message, Is.EqualTo("The requested resource was not found."));
            Assert.That(withCustom.Message, Is.EqualTo("Room 'r1' does not exist."));
        });
    }

    [Test]
    public void LimitExceededException_DefaultErrorCodeIsNull()
    {
        var withoutCode = new LimitExceededException("x");
        var withCode = new LimitExceededException("x", "FOO");

        Assert.Multiple(() =>
        {
            Assert.That(withoutCode.ErrorCode, Is.Null);
            Assert.That(withoutCode.Message, Is.EqualTo("x"));
            Assert.That(withCode.ErrorCode, Is.EqualTo("FOO"));
        });
    }

    [Test]
    public void InvalidStateTransitionException_OptionalErrorCode_RoundTrips()
    {
        var withoutCode = new InvalidStateTransitionException("Cannot approve a rejected listing.");
        var withCode = new InvalidStateTransitionException("Cannot approve a rejected listing.", "LISTING_NOT_AVAILABLE");

        Assert.Multiple(() =>
        {
            Assert.That(withoutCode.ErrorCode, Is.Null);
            Assert.That(withCode.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
        });
    }

    [Test]
    public void DomainExceptions_AllLiveInDomainExceptionsNamespace()
    {
        var exceptionTypes = typeof(DomainValidationException).Assembly
            .GetTypes()
            .Where(t => t.IsPublic && typeof(Exception).IsAssignableFrom(t))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(exceptionTypes, Has.Length.EqualTo(12));
            Assert.That(exceptionTypes.Select(t => t.Namespace), Is.All.EqualTo("RoomToRentRadar.Domain.Exceptions"));
        });
    }

    [Test]
    public void DomainValidationException_ErrorCode_RoundTrips()
    {
        var withCode = new DomainValidationException("Invalid input.", "VALIDATION_FAILED");
        var withoutCode = new DomainValidationException("Invalid input.");

        Assert.Multiple(() =>
        {
            Assert.That(withCode.ErrorCode, Is.EqualTo("VALIDATION_FAILED"));
            Assert.That(withCode.Message, Is.EqualTo("Invalid input."));
            Assert.That(withoutCode.ErrorCode, Is.Null);
        });
    }
}