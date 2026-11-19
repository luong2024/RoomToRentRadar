using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>
/// ADR-012 / docs/system-architecture.md §7.2: the four preconditions/media exceptions carry a
/// developer-facing message and an optional inner exception; their HTTP status and errorCode are
/// applied by the API layer (IF_MATCH_REQUIRED, ETAG_MISMATCH, FILE_TOO_LARGE, UNSUPPORTED_MEDIA_TYPE).
/// </summary>
[TestFixture]
public class ListingDomainExceptionsTests
{
    [Test]
    public void IfMatchRequiredException_DefaultAndCustomMessage_RoundTrip()
    {
        var withDefault = new IfMatchRequiredException();
        var inner = new InvalidOperationException("inner");
        var withCustom = new IfMatchRequiredException("If-Match required.", inner);

        Assert.Multiple(() =>
        {
            Assert.That(withDefault.Message, Is.Not.Empty);
            Assert.That(withCustom.Message, Is.EqualTo("If-Match required."));
            Assert.That(withCustom.InnerException, Is.SameAs(inner));
        });
    }

    [Test]
    public void EtagMismatchException_DefaultAndCustomMessage_RoundTrip()
    {
        var withDefault = new EtagMismatchException();
        var inner = new InvalidOperationException("inner");
        var withCustom = new EtagMismatchException("ETag mismatch.", inner);

        Assert.Multiple(() =>
        {
            Assert.That(withDefault.Message, Is.Not.Empty);
            Assert.That(withCustom.Message, Is.EqualTo("ETag mismatch."));
            Assert.That(withCustom.InnerException, Is.SameAs(inner));
        });
    }

    [Test]
    public void FileTooLargeException_DefaultAndCustomMessage_RoundTrip()
    {
        var withDefault = new FileTooLargeException();
        var inner = new InvalidOperationException("inner");
        var withCustom = new FileTooLargeException("File is larger than 10 MB.", inner);

        Assert.Multiple(() =>
        {
            Assert.That(withDefault.Message, Is.Not.Empty);
            Assert.That(withCustom.Message, Is.EqualTo("File is larger than 10 MB."));
            Assert.That(withCustom.InnerException, Is.SameAs(inner));
        });
    }

    [Test]
    public void UnsupportedMediaTypeException_DefaultAndCustomMessage_RoundTrip()
    {
        var withDefault = new UnsupportedMediaTypeException();
        var inner = new InvalidOperationException("inner");
        var withCustom = new UnsupportedMediaTypeException("Only JPEG and PNG are accepted.", inner);

        Assert.Multiple(() =>
        {
            Assert.That(withDefault.Message, Is.Not.Empty);
            Assert.That(withCustom.Message, Is.EqualTo("Only JPEG and PNG are accepted."));
            Assert.That(withCustom.InnerException, Is.SameAs(inner));
        });
    }

    [Test]
    public void ListingDomainExceptions_DeriveDirectlyFromException()
    {
        var types = new[]
        {
            typeof(IfMatchRequiredException),
            typeof(EtagMismatchException),
            typeof(FileTooLargeException),
            typeof(UnsupportedMediaTypeException)
        };

        Assert.Multiple(() =>
        {
            foreach (var type in types)
            {
                Assert.That(type.BaseType, Is.EqualTo(typeof(Exception)), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("RoomToRentRadar.Domain.Exceptions"), type.Name);
            }
        });
    }
}
