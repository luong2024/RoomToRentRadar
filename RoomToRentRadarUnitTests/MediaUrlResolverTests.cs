using RoomToRentRadar.Helpers;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MediaUrlResolverTests
{
    [TestCase(null, TestName = "Resolve_WhenUrlIsNull_ReturnsNull")]
    [TestCase("", TestName = "Resolve_WhenUrlIsEmpty_ReturnsNull")]
    [TestCase("   ", TestName = "Resolve_WhenUrlIsWhitespace_ReturnsNull")]
    public void Resolve_WhenUrlIsBlank_ReturnsNull(string? url)
    {
        var resolved = MediaUrlResolver.Resolve(url);

        Assert.That(resolved, Is.Null);
    }

    [TestCase("https://cdn.example.com/rooms/room-1.jpg", TestName = "Resolve_WhenUrlIsHttps_ReturnsItUnchanged")]
    [TestCase("http://localhost:9000/rooms/room-1.jpg", TestName = "Resolve_WhenUrlIsAbsoluteHttp_ReturnsItUnchanged")]
    public void Resolve_WhenUrlIsAbsolute_ReturnsItUnchanged(string url)
    {
        var resolved = MediaUrlResolver.Resolve(url);

        Assert.That(resolved, Is.EqualTo(url));
    }

    [Test]
    public void Resolve_WhenPathIsRelative_ResolvesAgainstTheApiBaseAddress()
    {
        var resolved = MediaUrlResolver.Resolve("media/room-1.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo("http://localhost:5080/media/room-1.jpg"));
            Assert.That(Uri.IsWellFormedUriString(resolved, UriKind.Absolute), Is.True);
        });
    }

    [Test]
    public void Resolve_WhenPathStartsWithASlash_ResolvesAgainstTheApiBaseHost()
    {
        var resolved = MediaUrlResolver.Resolve("/media/room-1.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo("http://localhost:5080/media/room-1.jpg"));
            Assert.That(new Uri(resolved!).IsAbsoluteUri, Is.True);
        });
    }

    [Test]
    public void Resolve_LeadingSlashAndPlainRelativePath_ProduceTheSameUrl()
    {
        var withLeadingSlash = MediaUrlResolver.Resolve("/media/room-1.jpg");
        var withoutLeadingSlash = MediaUrlResolver.Resolve("media/room-1.jpg");

        Assert.That(withLeadingSlash, Is.EqualTo(withoutLeadingSlash));
    }
}
